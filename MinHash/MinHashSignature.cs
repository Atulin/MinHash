using System.Runtime.CompilerServices;

namespace MinHash;

/// <summary>
/// Immutable wrapper around a MinHash signature array.
/// Supports direct similarity queries without additional allocations.
/// </summary>
public readonly struct MinHashSignature(uint[] data)
{
	private readonly uint[] _data = data ?? throw new ArgumentNullException(nameof(data));

	public ReadOnlySpan<uint> Span => _data;
	public int Length => _data.Length;

	/// <summary>Jaccard similarity ∈ [0,1] against another signature.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public double Jaccard(in MinHashSignature other) =>
		MinHasher.EstimateJaccard(_data, other._data);
}