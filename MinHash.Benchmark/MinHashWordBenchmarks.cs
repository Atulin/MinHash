using BenchmarkDotNet.Attributes;
using LoremNET;

namespace MinHash.Benchmark;

[MemoryDiagnoser]
public class MinHashWordBenchmarks
{
    private MinHasher _wordHasher = null!;
    private string _text1 = null!;
    private string _text2 = null!;
    private uint[] _sig1 = null!;
    private uint[] _sig2 = null!;

    [Params(128, 256)]
    public int SignatureSize { get; set; }

    [Params(100, 1000, 10000)]
    public int WordCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _wordHasher = new MinHasher(SignatureSize, 3, ShingleMode.Words);
        
        _text1 = Lorem.Words(WordCount);
        _text2 = _text1.Replace('a', 'b').Replace('A', 'z').Replace("lorem", "merol");

        _sig1 = _wordHasher.ComputeSignature(_text1);
        _sig2 = _wordHasher.ComputeSignature(_text2);
    }

    [Benchmark]
    public uint[] ComputeWordSignature()
    {
        return _wordHasher.ComputeSignature(_text1);
    }

    [Benchmark]
    public void ComputeWordSignatureInto()
    {
        _wordHasher.ComputeSignatureInto(_text1, _sig1);
    }

    [Benchmark]
    public double EstimateJaccard()
    {
        return MinHasher.EstimateJaccard(_sig1, _sig2);
    }
}
