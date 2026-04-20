using BenchmarkDotNet.Running;
using MinHash.Benchmark;

var chars = BenchmarkRunner.Run<MinHashCharBenchmarks>();
var words = BenchmarkRunner.Run<MinHashWordBenchmarks>();
