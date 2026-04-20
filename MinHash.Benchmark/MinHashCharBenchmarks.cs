using BenchmarkDotNet.Attributes;

namespace MinHash.Benchmark;

[MemoryDiagnoser]
public class MinHashCharBenchmarks
{
    private MinHasher _charHasher = null!;
    private string _text1 = null!;
    private string _text2 = null!;
    private uint[] _sig1 = null!;
    private uint[] _sig2 = null!;

    [Params(128, 256)]
    public int SignatureSize { get; set; }

    [Params(100, 1000, 10000)]
    public int StringLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _charHasher = new MinHasher(SignatureSize);
        
        _text1 = StringGenerator.Generate(StringLength);
        _text2 = _text1.Replace('a', 'b').Replace('A', 'z').Replace('6', '7');

        _sig1 = _charHasher.ComputeSignature(_text1);
        _sig2 = _charHasher.ComputeSignature(_text2);
    }

    [Benchmark]
    public uint[] ComputeCharSignature()
    {
        return _charHasher.ComputeSignature(_text1);
    }

    [Benchmark]
    public void ComputeCharSignatureInto()
    {
        _charHasher.ComputeSignatureInto(_text1, _sig1);
    }

    [Benchmark]
    public double EstimateJaccard()
    {
        return MinHasher.EstimateJaccard(_sig1, _sig2);
    }
}
