using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MinHash.Benchmark;

[MemoryDiagnoser]
public class MinHashBenchmarks
{
    private MinHasher _charHasher = null!;
    private MinHasher _wordHasher = null!;
    private string _text1 = null!;
    private string _text2 = null!;
    private uint[] _sig1 = null!;
    private uint[] _sig2 = null!;

    [Params(128, 256)]
    public int SignatureSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _charHasher = new MinHasher(SignatureSize, 3, ShingleMode.Chars);
        _wordHasher = new MinHasher(SignatureSize, 3, ShingleMode.Words);
        
        _text1 = "Sphinx of black quartz, judge my vow. Sphinx of black quartz, judge my vow. Sphinx of black quartz, judge my vow. Sphinx of black quartz, judge my vow.";
        _text2 = "Sphynx of black quartz, judge my vow. Sphinx of black quartz, judge my vow. Sphynx of black quartz, judge my vow. Sphinx of black quartz, judge my vow.";

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
