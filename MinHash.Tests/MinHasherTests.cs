using System;
using Xunit;

namespace MinHash.Tests;

public class MinHasherTests
{
    [Fact]
    public void Constructor_InvalidParameters_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MinHasher(signatureSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MinHasher(shingleSize: 0));
    }

    [Fact]
    public void Constructor_ValidParameters_SetsProperties()
    {
        var hasher = new MinHasher(signatureSize: 256);
        Assert.Equal(256, hasher.SignatureSize);
    }

    [Fact]
    public void ComputeSignature_Chars_Deterministic()
    {
        var hasher1 = new MinHasher(seed: 42);
        var hasher2 = new MinHasher(seed: 42);
        
        var text = "hello world";
        var sig1 = hasher1.ComputeSignature(text);
        var sig2 = hasher2.ComputeSignature(text);

        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_Chars_ShortString()
    {
        var hasher = new MinHasher(shingleSize: 5);
        var text = "hey"; // Shorter than shingle size
        var sig = hasher.ComputeSignature(text);
        
        // When limit <= 0, it should return max value signature
        foreach (var val in sig)
        {
            Assert.Equal(uint.MaxValue, val);
        }
    }

    [Fact]
    public void ComputeSignature_Words_Deterministic()
    {
        var hasher = new MinHasher(shingleMode: ShingleMode.Words, seed: 42);
        var text = "hello  world   test";
        var sig1 = hasher.ComputeSignature(text);
        var sig2 = hasher.ComputeSignature("hello world test");
        
        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void ComputeSignature_Words_ShortString()
    {
        var hasher = new MinHasher(shingleSize: 3, shingleMode: ShingleMode.Words);
        var text = "two words"; // Fewer words than shingle size
        var sig = hasher.ComputeSignature(text);
        
        foreach (var val in sig)
        {
            Assert.Equal(uint.MaxValue, val);
        }
    }

    [Fact]
    public void ComputeSignatureInto_BufferTooSmall_Throws()
    {
        var hasher = new MinHasher(signatureSize: 128);
        var buffer = new uint[127];
        
        Assert.Throws<ArgumentException>(() => hasher.ComputeSignatureInto("test", buffer));
    }

    [Fact]
    public void EstimateJaccard_DifferentLengths_Throws()
    {
        var sig1 = new uint[128];
        var sig2 = new uint[256];
        
        Assert.Throws<ArgumentException>(() => MinHasher.EstimateJaccard(sig1, sig2));
    }

    [Fact]
    public void EstimateJaccard_Identical()
    {
        var hasher = new MinHasher();
        var sig1 = hasher.ComputeSignature("the quick brown fox jumps over the lazy dog");
        var sig2 = hasher.ComputeSignature("the quick brown fox jumps over the lazy dog");
        
        var jaccard = MinHasher.EstimateJaccard(sig1, sig2);
        Assert.Equal(1.0, jaccard, 5);
    }

    [Fact]
    public void EstimateJaccard_CompletelyDifferent()
    {
        var hasher = new MinHasher();
        var sig1 = hasher.ComputeSignature("the quick brown fox jumps over the lazy dog");
        var sig2 = hasher.ComputeSignature("completely different string that shares no words or chars");
        
        var jaccard = MinHasher.EstimateJaccard(sig1, sig2);
        Assert.InRange(jaccard, 0.0, 0.15); // Should be very low
    }

    [Fact]
    public void InvalidShingleMode_Throws()
    {
        var hasher = new MinHasher(shingleMode: (ShingleMode)999);
        var buffer = new uint[hasher.SignatureSize];
        
        var ex = Assert.Throws<EnumOutOfRangeException<ShingleMode>>(() => hasher.ComputeSignatureInto("test", buffer));
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public void ComputeSignature_ScalarTail_Covered()
    {
        // 13 is not a multiple of 8 or 4, so it covers the scalar tail in UpdateSignature and CountMatchesSIMD
        var hasher = new MinHasher(signatureSize: 13, shingleSize: 3);
        var sig1 = hasher.ComputeSignature("hello world");
        var sig2 = hasher.ComputeSignature("hello world");
        
        var jaccard = MinHasher.EstimateJaccard(sig1, sig2);
        Assert.Equal(1.0, jaccard);
    }

    [Fact]
    public void TokenizeWords_EdgeWhitespaces_Covered()
    {
        var hasher = new MinHasher(shingleMode: ShingleMode.Words, shingleSize: 1);
        var sig = hasher.ComputeSignature(" \t \n hello \r\n world  \t ");
        // Should find exactly two words.
        Assert.DoesNotContain(uint.MaxValue, sig);
    }

    [Fact]
    public void HashWordShingle_HeapFallback_Covered()
    {
        var hasher = new MinHasher(shingleMode: ShingleMode.Words, shingleSize: 2);
        // Create a word > 256 chars
        var longWord = new string('a', 300);
        var text = $"{longWord} {longWord}";
        var sig = hasher.ComputeSignature(text);
        
        Assert.DoesNotContain(uint.MaxValue, sig);
    }
}
