using System.Linq;
using Xunit;

namespace MinHash.Tests;

public class MinHashIndexTests
{
    [Fact]
    public void Add_IncreasesCount()
    {
        var hasher = new MinHasher();
        var index = new MinHashIndex(hasher);
        
        Assert.Equal(0, index.Count);
        index.Add("doc1", "hello world");
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void Query_ReturnsMatches_SortedDescending()
    {
        var hasher = new MinHasher(signatureSize: 128);
        var index = new MinHashIndex(hasher);
        
        index.Add("doc1", "the quick brown fox jumps over the lazy dog");
        index.Add("doc2", "the fast auburn fox leaps across the sleepy hound");
        index.Add("doc3", "completely unrelated text about cooking pasta and eating it");
        
        var results = index.Query("quick brown fox jumps", threshold: 0.1);
        
        Assert.NotEmpty(results);
        Assert.True(results.Count <= 3);
        
        // Ensure sorted descending
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].Similarity >= results[i + 1].Similarity);
        }
        
        // doc1 should be the most similar
        Assert.Equal("doc1", results.First().Key);
    }

    [Fact]
    public void Query_FiltersByThreshold()
    {
        var hasher = new MinHasher(signatureSize: 128);
        var index = new MinHashIndex(hasher);
        
        index.Add("doc1", "the quick brown fox jumps over the lazy dog");
        index.Add("doc2", "completely unrelated text about cooking pasta");
        
        // With a high threshold, doc2 should be excluded
        var results = index.Query("the quick brown fox jumps over the lazy dog", threshold: 0.8);
        
        Assert.Single(results);
        Assert.Equal("doc1", results[0].Key);
    }

    [Fact]
    public void Query_LargeSignature_UsesHeap()
    {
        // 512 signature size means it will fall back to heap allocation in Query
        var hasher = new MinHasher(signatureSize: 512);
        var index = new MinHashIndex(hasher);
        
        index.Add("doc1", "hello world");
        var results = index.Query("hello world", threshold: 0.5);
        
        Assert.Single(results);
        Assert.Equal("doc1", results[0].Key);
    }
}
