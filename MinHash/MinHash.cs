using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace MinHash;

/// <summary>
/// Thread-safe, allocation-free MinHash engine for string Jaccard estimation.
/// </summary>
public sealed class MinHasher
{
	// 2^31 − 1: Mersenne prime — enables a fast bitwise mod.
	private const uint Mp = 2_147_483_647u;

	private readonly uint[] _hashMultipliers;
	private readonly uint[] _hashOffsets;
	private readonly int _shingleSize;
	private readonly ShingleMode _shingleMode;
	
	public int SignatureSize { get; }

	/// <param name="signatureSize">
	///   Number of hash functions (signature length). Must be a multiple of 8
	///   for the best SIMD throughput; 128–256 is typical.
	/// </param>
	/// <param name="shingleSize">
	/// n-gram length (k).
	/// - If <paramref name="shingleMode"/> is <see cref="ShingleMode.Chars"/>, denotes the number of chars per shingle.
	/// - If <paramref name="shingleMode"/> is <see cref="ShingleMode.Words"/>, denotes the number of words per shingle.
	/// </param>
	/// <param name="shingleMode">What shingling should be done on</param>
	/// <param name="seed">Seed for deterministic parameter generation.</param>
	public MinHasher(
		int signatureSize = 128,
		int shingleSize = 3,
		ShingleMode shingleMode = ShingleMode.Chars,
		int seed = unchecked((int)0xDEADBEEF))
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(signatureSize, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(shingleSize, 1);

		SignatureSize = signatureSize;
		_shingleSize = shingleSize;
		_shingleMode = shingleMode;
		_hashMultipliers = new uint[signatureSize];
		_hashOffsets = new uint[signatureSize];

		var rng = new Random(seed);
		for (var i = 0; i < signatureSize; i++)
		{
			_hashMultipliers[i] = (uint)rng.Next(1, (int)Mp) | 1u; // keep odd for better mixing
			_hashOffsets[i] = (uint)rng.Next(0, (int)Mp);
		}
	}

	/// <summary>
	/// Compute a MinHash signature, returning a newly allocated array.
	/// For zero-allocation usage call <see cref="ComputeSignatureInto"/> instead.
	/// </summary>
	public uint[] ComputeSignature(ReadOnlySpan<char> text)
	{
		var sig = new uint[SignatureSize];
		ComputeSignatureInto(text, sig);
		return sig;
	}

	
	/// <summary>
	/// Zero-allocation overload — writes the signature into the caller-supplied
	/// span (must be ≥ <see cref="SignatureSize"/>).
	/// </summary>
	public void ComputeSignatureInto(ReadOnlySpan<char> text, Span<uint> signature)
	{
		
		if (signature.Length < SignatureSize)
		{
			throw new ArgumentException("Signature buffer is smaller than SignatureSize.");
		}

		signature[..SignatureSize].Fill(uint.MaxValue);
		
		switch (_shingleMode)
		{
			case ShingleMode.Chars:
				ComputeCharSignatureInto(text, signature);
				break;
			case ShingleMode.Words:
				ComputeWordSignatureInto(text, signature);
				break;
			default:
				throw new EnumOutOfRangeException<ShingleMode>(_shingleMode);
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void ComputeCharSignatureInto(ReadOnlySpan<char> text, Span<uint> signature)
	{
		var limit = text.Length - _shingleSize + 1;
		if (limit <= 0)
		{
			return;
		}

		ReadOnlySpan<uint> a = _hashMultipliers;
		ReadOnlySpan<uint> b = _hashOffsets;

		for (var s = 0; s < limit; s++)
		{
			var sh = HashShingle(text.Slice(s, _shingleSize));
			UpdateSignature(signature, a, b, sh, SignatureSize);
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	private void ComputeWordSignatureInto(ReadOnlySpan<char> text, Span<uint> signature)
	{
		var words = new List<Range>();
		TokenizeWords(text, words);

		var limit = words.Count - _shingleSize + 1;
		if (limit <= 0)
		{
			return;
		}

		ReadOnlySpan<uint> a = _hashMultipliers;
		ReadOnlySpan<uint> b = _hashOffsets;

		var wordsSpan = CollectionsMarshal.AsSpan(words);
		for (var s = 0; s < limit; s++)
		{
			var sh = HashWordShingle(text, wordsSpan.Slice(s, _shingleSize));
			UpdateSignature(signature, a, b, sh, SignatureSize);
		}
	}

	private static void TokenizeWords(ReadOnlySpan<char> text, List<Range> words)
	{
		var i = 0;
		var n = text.Length;

		while (i < n)
		{
			while (i < n && char.IsWhiteSpace(text[i]))
			{
				i++;
			}
			
			if (i >= n)
			{
				break;
			}
			
			var start = i;

			while (i < n && !char.IsWhiteSpace(text[i]))
			{
				i++;
			}
			
			words.Add(start..i);
		}
	}

	/// <summary>
	/// Hash a word-level shingle (a window of consecutive word spans) to a
	/// 32-bit value using xxHash32.  Words are separated by a null byte so
	/// that {"foo bar", "baz"} ≠ {"foo", "barbaz"}.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint HashWordShingle(ReadOnlySpan<char> text, ReadOnlySpan<Range> wordRanges)
	{
		// Build a temporary char buffer on the stack for small shingles,
		// falling back to the heap for large ones.
		// Maximum reasonable prose word length ~ 30 chars; with a separator
		// that's roughly (30 + 1) * shingleSize chars at most.
		// We cap the stack buffer at 256 chars (~512 bytes).
		const int stackLimit = 256;

		// Calculate total required length first
		var totalLen = 0;
		foreach (var r in wordRanges)
		{
			totalLen += r.End.Value - r.Start.Value;
		}
		
		// Add separators between words (shingleSize - 1 separators)
		totalLen += wordRanges.Length - 1;

		char[]? rented = null;
		var buf = totalLen <= stackLimit
			? stackalloc char[totalLen]
			: (rented = System.Buffers.ArrayPool<char>.Shared.Rent(totalLen)).AsSpan(0, totalLen);

		try
		{
			var pos = 0;
			for (var i = 0; i < wordRanges.Length; i++)
			{
				if (i > 0)
				{
					buf[pos++] = '\0';
				}
				var r = wordRanges[i];
				text[r].CopyTo(buf[pos..]);
				pos += r.End.Value - r.Start.Value;
			}

			return XxHash32.HashToUInt32(MemoryMarshal.AsBytes(buf[..pos]));
		}
		finally
		{
			if (rented is not null)
			{
				System.Buffers.ArrayPool<char>.Shared.Return(rented);
			}
		}
	}

	/// <summary>
	/// Estimate Jaccard similarity ∈ [0,1] between two pre-computed signatures.
	/// Zero allocation, fully SIMD-accelerated.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static double EstimateJaccard(ReadOnlySpan<uint> sigA, ReadOnlySpan<uint> sigB)
	{
		if (sigA.Length != sigB.Length)
		{
			throw new ArgumentException("Both signatures must have the same length.");
		}
		var matches = CountMatchesSIMD(sigA, sigB);
		return (double)matches / sigA.Length;
	}

	/// <summary>
	/// Hash a k-char shingle to a 32-bit value using xxHash32 over the raw
	/// UTF-16 bytes — very fast, excellent avalanche.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint HashShingle(ReadOnlySpan<char> shingle)
	{
		var bytes = MemoryMarshal.AsBytes(shingle);
		return XxHash32.HashToUInt32(bytes);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static void UpdateSignature(
		Span<uint> sig,
		ReadOnlySpan<uint> a,
		ReadOnlySpan<uint> b,
		uint x,
		int n)
	{
		ref var sigR = ref MemoryMarshal.GetReference(sig);
		ref var aR = ref MemoryMarshal.GetReference(a);
		ref var bR = ref MemoryMarshal.GetReference(b);

		// AVX2 / Vector256 path (8 lanes × uint32)
		if (Vector256.IsHardwareAccelerated && n >= 8)
		{
			var vx = Vector256.Create((ulong)x);
			var prime = Vector256.Create((ulong)Mp);

			var limit = n & ~7; // round down to multiple of 8
			for (var i = 0; i < limit; i += 8)
			{
				ref var aSlot = ref Unsafe.Add(ref aR, i);
				ref var bSlot = ref Unsafe.Add(ref bR, i);
				ref var sigSlot = ref Unsafe.Add(ref sigR, i);

				var (aLo, aHi) = Vector256.Widen(Vector256.LoadUnsafe(ref aSlot));
				var (bLo, bHi) = Vector256.Widen(Vector256.LoadUnsafe(ref bSlot));

				// h = (a·x + b) mod Mp
				var h = Vector256.Narrow(
					MersMod256(aLo * vx + bLo, prime),
					MersMod256(aHi * vx + bHi, prime));

				// sig[i..i+8] = min(sig[i..i+8], h)
				var cur = Vector256.LoadUnsafe(ref sigSlot);
				Vector256.Min(h, cur).StoreUnsafe(ref sigSlot);
			}
		}

		// SSE2 / Vector128 path (4 lanes × uint32)
		else if (Vector128.IsHardwareAccelerated && n >= 4)
		{
			var vx = Vector128.Create((ulong)x);
			var prime = Vector128.Create((ulong)Mp);

			var limit = n & ~3;
			for (var i = 0; i < limit; i += 4)
			{
				ref var aSlot = ref Unsafe.Add(ref aR, i);
				ref var bSlot = ref Unsafe.Add(ref bR, i);
				ref var sigSlot = ref Unsafe.Add(ref sigR, i);

				var (aLo, aHi) = Vector128.Widen(Vector128.LoadUnsafe(ref aSlot));
				var (bLo, bHi) = Vector128.Widen(Vector128.LoadUnsafe(ref bSlot));

				var h = Vector128.Narrow(
					MersMod128(aLo * vx + bLo, prime),
					MersMod128(aHi * vx + bHi, prime));

				var cur = Vector128.LoadUnsafe(ref sigSlot);
				Vector128.Min(h, cur).StoreUnsafe(ref sigSlot);
			}
		}

		// Scalar tail
		for (var i = 0; i < n; i++)
		{
			var h = MersHash(Unsafe.Add(ref aR, i), Unsafe.Add(ref bR, i), x);
			ref var slot = ref Unsafe.Add(ref sigR, i);
			if (h < slot) slot = h;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static int CountMatchesSIMD(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
	{
		int n = a.Length, i = 0, matches = 0;
		ref var aR = ref MemoryMarshal.GetReference(a);
		ref var bR = ref MemoryMarshal.GetReference(b);

		if (Vector256.IsHardwareAccelerated && n >= 8)
		{
			// Each equal lane → 0xFFFFFFFF; subtract from zero accumulator = +1 per match
			var acc = Vector256<uint>.Zero;
			var limit = n & ~7;
			for (; i < limit; i += 8)
			{
				var va = Vector256.LoadUnsafe(ref Unsafe.Add(ref aR, i));
				var vb = Vector256.LoadUnsafe(ref Unsafe.Add(ref bR, i));
				// Equals returns ~0 per matching lane → subtracting ~0 adds 1
				acc -= Vector256.Equals(va, vb).AsUInt32();
			}
			matches += (int)Vector256.Sum(acc);
		}
		else if (Vector128.IsHardwareAccelerated && n >= 4)
		{
			var acc = Vector128<uint>.Zero;
			var limit = n & ~3;
			for (; i < limit; i += 4)
			{
				var va = Vector128.LoadUnsafe(ref Unsafe.Add(ref aR, i));
				var vb = Vector128.LoadUnsafe(ref Unsafe.Add(ref bR, i));
				acc -= Vector128.Equals(va, vb).AsUInt32();
			}
			matches += (int)Vector128.Sum(acc);
		}

		for (; i < n; i++)
		{
			if (Unsafe.Add(ref aR, i) == Unsafe.Add(ref bR, i))
			{
				matches++;
			}
		}

		return matches;
	}

	/// <summary>Scalar: (a·x + b) mod (2³¹−1)</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint MersHash(uint a, uint b, uint x)
	{
		var v = (ulong)a * x + b;

		// Fast Mersenne mod: fold the high bits down
		v = (v >> 31) + (v & Mp);
		if (v >= Mp)
		{
			v -= Mp;
		}
		return (uint)v;
	}

	/// <summary>Vectorised Mersenne mod for Vector256&lt;ulong&gt;.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector256<ulong> MersMod256(Vector256<ulong> v, Vector256<ulong> prime)
	{
		v = (v >> 31) + (v & prime);
		v -= Vector256.GreaterThanOrEqual(v, prime) & prime;
		return v;
	}

	/// <summary>Vectorised Mersenne mod for Vector128&lt;ulong&gt;.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Vector128<ulong> MersMod128(Vector128<ulong> v, Vector128<ulong> prime)
	{
		v = (v >> 31) + (v & prime);
		v -= Vector128.GreaterThanOrEqual(v, prime) & prime;
		return v;
	}
}