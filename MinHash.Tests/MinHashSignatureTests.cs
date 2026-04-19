using System;
using Xunit;

namespace MinHash.Tests;

public class MinHashSignatureTests
{
    [Fact]
    public void Constructor_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MinHashSignature(null!));
    }

    [Fact]
    public void Properties_MatchUnderlyingArray()
    {
        var data = new uint[] { 1, 2, 3 };
        var sig = new MinHashSignature(data);
        
        Assert.Equal(3, sig.Length);
        Assert.Equal(1u, sig.Span[0]);
        Assert.Equal(2u, sig.Span[1]);
        Assert.Equal(3u, sig.Span[2]);
    }

    [Fact]
    public void Jaccard_DelegatesCorrectly()
    {
        var hasher = new MinHasher(signatureSize: 128);
        var textA = "the quick brown fox";
        var textB = "the fast brown fox";
        
        var sigA = new MinHashSignature(hasher.ComputeSignature(textA));
        var sigB = new MinHashSignature(hasher.ComputeSignature(textB));
        
        var sigJaccard = sigA.Jaccard(sigB);
        var staticJaccard = MinHasher.EstimateJaccard(sigA.Span, sigB.Span);
        
        Assert.Equal(staticJaccard, sigJaccard);
    }
}
