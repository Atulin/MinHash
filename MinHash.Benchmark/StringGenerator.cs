namespace MinHash.Benchmark;

public static class StringGenerator
{
	private static readonly int[] Parts =
	[
		..Enumerable.Range(32, 126),
		128,
		..Enumerable.Range(130, 140),
		142,
		..Enumerable.Range(145, 156),
		158,
		159,
		..Enumerable.Range(161, 172),
		..Enumerable.Range(174, 255)
	];

	public static string Generate(int length)
	{
		var random = Random.Shared.GetItems(Parts, length)
			.Select(i => (char)i)
			.ToArray();
		
		return new string(random);
	}
}