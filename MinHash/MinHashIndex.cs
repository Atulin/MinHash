using System.Runtime.CompilerServices;

namespace MinHash;

/// <summary>
/// Stores pre-computed signatures and provides fast similarity search.
/// Not thread-safe for writes; reads are safe to parallelise.
/// </summary>
public sealed class MinHashIndex(MinHasher hasher)
{
	private readonly List<(string Key, uint[] Sig)> _entries = [];

	public int Count => _entries.Count;

	/// <summary>Index a string with an associated key.</summary>
	public void Add(string key, ReadOnlySpan<char> text)
	{
		var sig = hasher.ComputeSignature(text);
		_entries.Add((key, sig));
	}

	/// <summary>
	/// Return all indexed entries whose Jaccard similarity with
	/// <paramref name="query"/> is ≥ <paramref name="threshold"/>.
	/// Results are sorted descending by similarity.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	public List<(string Key, double Similarity)> Query(ReadOnlySpan<char> query, double threshold = 0.5)
	{
		Span<uint> qSig = stackalloc uint[hasher.SignatureSize <= 256 ? hasher.SignatureSize : 0];

		if (qSig.IsEmpty)
		{
			var qSigHeap = new uint[hasher.SignatureSize];
			qSig = qSigHeap;
		}

		hasher.ComputeSignatureInto(query, qSig);

		var results = new List<(string, double)>();
		foreach (var (key, sig) in _entries)
		{
			var sim = MinHasher.EstimateJaccard(qSig, sig);
			if (sim >= threshold)
			{
				results.Add((key, sim));
			}
		}

		results.Sort(static (x, y) => y.Item2.CompareTo(x.Item2));
		return results;
	}
}