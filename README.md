# Atulin.MinHash

[![NuGet](https://img.shields.io/nuget/v/Atulin.MinHash.svg)](https://www.nuget.org/packages/Atulin.MinHash)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![codecov](https://codecov.io/gh/Atulin/MinHash/graph/badge.svg?token=DBMYHAO7BI)](https://codecov.io/gh/Atulin/MinHash)

Maximum-performance **MinHash** library for .NET 10, designed for fast approximate **Jaccard similarity** estimation over text.

## Features

- **Zero-allocation hot paths** — `Span<T>`, `stackalloc`, and ref locals throughout
- **SIMD acceleration** — Vector256 (AVX2) and Vector128 (SSE2) paths auto-selected at runtime
- **xxHash32 shingle hashing** via `System.IO.Hashing` — excellent avalanche, minimal collisions
- **Mersenne-prime universal hashing** with vectorised modulo reduction
- **Thread-safe reads** — `MinHasher` and `MinHashSignature` are safe to share across threads
- **`MinHashIndex`** — in-memory similarity-search index with threshold filtering and sorted results

---

## Installation

```shell
dotnet add package Atulin.MinHash
```

Requires **.NET 10.0** or later.

---

## Quick Start

```csharp
using MinHash;

// 1. Create a hasher (shared, thread-safe)
var hasher = new MinHasher(signatureSize: 128, shingleSize: 3);

// 2. Compute signatures
uint[] sigA = hasher.ComputeSignature("the quick brown fox jumps over the lazy dog");
uint[] sigB = hasher.ComputeSignature("the quick brown fox leaps over the sleepy cat");

// 3. Estimate Jaccard similarity
double similarity = MinHasher.EstimateJaccard(sigA, sigB);
Console.WriteLine($"Similarity: {similarity:P1}"); // e.g. "Similarity: 62.5%"
```

---

## Usage

### `MinHasher` — Core Engine

```csharp
// Default: 128 hash functions, 3-char shingles, fixed seed
var hasher = new MinHasher();

// Custom configuration
var hasher = new MinHasher(
    signatureSize: 256,   // More functions → higher accuracy, more memory
    shingleSize:   3,     // Character n-gram length; 3–5 works well for most text
    seed:          42);   // Deterministic parameter generation
```

#### `ComputeSignature` — allocating overload

```csharp
uint[] signature = hasher.ComputeSignature("hello world");
```

#### `ComputeSignatureTo` — zero-allocation overload

```csharp
// Reuse a pre-allocated buffer — no heap allocation in the hot path
uint[] buffer = new uint[hasher.SignatureSize];
hasher.ComputeSignatureTo("hello world", buffer);
```

#### `EstimateJaccard` — static, SIMD-accelerated

```csharp
double j = MinHasher.EstimateJaccard(sigA, sigB); // 0.0 – 1.0
```

Both spans must have the same length; an `ArgumentException` is thrown otherwise.

---

### `MinHashSignature` — Immutable Wrapper

Wraps a raw `uint[]` signature to provide an ergonomic, value-type API:

```csharp
var hasher = new MinHasher(signatureSize: 128);

MinHashSignature sigA = new(hasher.ComputeSignature("document one"));
MinHashSignature sigB = new(hasher.ComputeSignature("document two"));

double j = sigA.Jaccard(sigB);

Console.WriteLine(sigA.Length); // 128
ReadOnlySpan<uint> raw = sigA.Span;
```

---

### `MinHashIndex` — Similarity Search

Index a collection of documents and query by approximate Jaccard similarity:

```csharp
var hasher = new MinHasher(signatureSize: 128);
var index  = new MinHashIndex(hasher);

// Add documents
index.Add("doc-1", "the quick brown fox jumps over the lazy dog");
index.Add("doc-2", "a fast auburn fox leaps across a sleepy hound");
index.Add("doc-3", "completely unrelated text about cooking pasta");

// Query: returns all entries with similarity ≥ threshold, sorted descending
var results = index.Query("quick fox jumps over dog", threshold: 0.3);

foreach (var (key, similarity) in results)
    Console.WriteLine($"{key}: {similarity:P1}");

// Example output:
// doc-1: 68.8%
// doc-2: 35.9%
```

> [!NOTE]
> `MinHashIndex` is not thread-safe for concurrent writes. Reads (`Query`) may be parallelised safely once the index is fully populated.

---

## Algorithm

1. Decompose text into overlapping **k-character shingles** (character n-grams).
2. Hash each shingle with **xxHash32** over its raw UTF-16 bytes.
3. Apply **`numHashFunctions` universal hashes**: `h_i(x) = (aᵢ·x + bᵢ) mod (2³¹−1)`.
4. `Signature[i]` = **minimum** over all shingles of `hᵢ(shingle)`.
5. **Jaccard(A, B) ≈ |{i : sigA[i] == sigB[i]}| / numHashFunctions**.

The Mersenne-prime modulo `(2³¹−1)` is computed with a fast bitwise fold instead of integer division.

---

## Benchmarks

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)    
AMD Ryzen 9 9900X 4.40 GHz, 1 CPU, 24 logical and 12 physical cores    
.NET SDK 10.0.202

> [!TIP]
> 1 ms = 1 000 000 ns 

| Method                   | SignatureSize | StringLength | Mean             | Error          | StdDev         | Gen0   | Allocated |
|------------------------- |-------------- |------------- |-----------------:|---------------:|---------------:|-------:|----------:|
| ComputeCharSignature     | 128           | 100          |    10,354.366 ns |     91.5582 ns |     76.4552 ns | 0.0305 |     536 B |
| ComputeCharSignatureInto | 128           | 100          |    32,480.913 ns |     53.7245 ns |     41.9445 ns |      - |         - |
| EstimateJaccard          | 128           | 100          |         5.002 ns |      0.0766 ns |      0.0679 ns |      - |         - |
| ComputeCharSignature     | 128           | 1000         |   363,994.238 ns |  5,511.1743 ns |  5,155.1559 ns |      - |     536 B |
| ComputeCharSignatureInto | 128           | 1000         |   348,559.600 ns |  6,804.6557 ns |  9,084.0207 ns |      - |         - |
| EstimateJaccard          | 128           | 1000         |         5.523 ns |      0.1280 ns |      0.2467 ns |      - |         - |
| ComputeCharSignature     | 128           | 10000        | 3,723,055.273 ns | 45,820.0107 ns | 38,261.7841 ns |      - |     536 B |
| ComputeCharSignatureInto | 128           | 10000        | 3,423,947.377 ns | 29,347.3944 ns | 26,015.7007 ns |      - |         - |
| EstimateJaccard          | 128           | 10000        |         5.267 ns |      0.0175 ns |      0.0155 ns |      - |         - |
| ComputeCharSignature     | 256           | 100          |    23,017.684 ns |    316.6331 ns |    280.6870 ns | 0.0610 |    1048 B |
| ComputeCharSignatureInto | 256           | 100          |    62,919.740 ns |    489.8068 ns |    434.2009 ns |      - |         - |
| EstimateJaccard          | 256           | 100          |         9.709 ns |      0.0918 ns |      0.0813 ns |      - |         - |
| ComputeCharSignature     | 256           | 1000         |   729,181.595 ns |  3,801.7468 ns |  3,556.1563 ns |      - |    1048 B |
| ComputeCharSignatureInto | 256           | 1000         |   652,213.525 ns |  2,518.8727 ns |  2,232.9150 ns |      - |         - |
| EstimateJaccard          | 256           | 1000         |         9.128 ns |      0.0546 ns |      0.0484 ns |      - |         - |
| ComputeCharSignature     | 256           | 10000        | 7,597,260.365 ns | 70,214.9017 ns | 65,679.0627 ns |      - |    1048 B |
| ComputeCharSignatureInto | 256           | 10000        | 6,776,450.060 ns | 66,512.5836 ns | 55,541.0197 ns |      - |         - |
| EstimateJaccard          | 256           | 10000        |         9.594 ns |      0.1088 ns |      0.0908 ns |      - |         - |

| Method                   | SignatureSize | WordCount | Mean           | Error         | StdDev        | Gen0   | Allocated |
|------------------------- |-------------- |---------- |---------------:|--------------:|--------------:|-------:|----------:|
| ComputeWordSignature     | 128           | 100       |  12,969.747 ns |   228.0700 ns |   262.6458 ns | 0.1526 |    2728 B |
| ComputeWordSignatureInto | 128           | 100       |  37,677.623 ns |   209.5786 ns |   175.0076 ns | 0.1221 |    2192 B |
| EstimateJaccard          | 128           | 100       |       5.185 ns |     0.0237 ns |     0.0210 ns |      - |         - |
| ComputeWordSignature     | 128           | 1000      |  33,682.019 ns |   510.5536 ns |   398.6066 ns | 0.2441 |    4800 B |
| ComputeWordSignatureInto | 128           | 1000      |  94,254.856 ns | 1,865.2124 ns | 2,358.8967 ns | 0.2441 |    4264 B |
| EstimateJaccard          | 128           | 1000      |       5.442 ns |     0.1289 ns |     0.1143 ns |      - |         - |
| ComputeWordSignature     | 128           | 10000     |  35,363.723 ns |   700.3849 ns | 1,150.7525 ns | 0.2441 |    4800 B |
| ComputeWordSignatureInto | 128           | 10000     |  90,928.486 ns |   592.2256 ns |   524.9925 ns | 0.2441 |    4264 B |
| EstimateJaccard          | 128           | 10000     |       5.378 ns |     0.0397 ns |     0.0352 ns |      - |         - |
| ComputeWordSignature     | 256           | 100       |  24,609.826 ns |   231.8569 ns |   205.5351 ns | 0.1831 |    3240 B |
| ComputeWordSignatureInto | 256           | 100       |  69,685.128 ns | 1,057.9764 ns |   937.8686 ns | 0.1221 |    2192 B |
| EstimateJaccard          | 256           | 100       |       9.560 ns |     0.0768 ns |     0.0641 ns |      - |         - |
| ComputeWordSignature     | 256           | 1000      | 142,392.912 ns | 2,790.1497 ns | 2,609.9077 ns | 0.2441 |    5312 B |
| ComputeWordSignatureInto | 256           | 1000      | 170,191.572 ns |   856.1088 ns |   714.8896 ns | 0.2441 |    4264 B |
| EstimateJaccard          | 256           | 1000      |       9.072 ns |     0.0485 ns |     0.0430 ns |      - |         - |
| ComputeWordSignature     | 256           | 10000     | 142,703.622 ns | 2,524.0600 ns | 2,237.5134 ns | 0.2441 |    5312 B |
| ComputeWordSignatureInto | 256           | 10000     | 175,198.652 ns | 1,072.4958 ns |   950.7396 ns | 0.2441 |    4264 B |
| EstimateJaccard          | 256           | 10000     |       9.446 ns |     0.0554 ns |     0.0518 ns |      - |         - |

---

## Configuration Guide

| Parameter       | Default | Recommendation                                         |
|-----------------|---------|--------------------------------------------------------|
| `signatureSize` | 128     | 128 for ~97% accuracy; 256 for ~99% accuracy           |
| `shingleSize`   | 3       | 3–4 for short texts; 5 for paragraphs/documents        |
| `seed`          | 0xDEADBEEF | Change only if you need independent hash families  |

**Accuracy vs. size trade-off:** Jaccard estimation error is approximately `1/√(signatureSize)`. At 128 functions the expected error is ≈ 8.8%; at 256 it drops to ≈ 6.25%.

---

## Thread Safety

| Type              | Read  | Write |
|-------------------|-------|-------|
| `MinHasher`       | ✅ Safe | ✅ Safe (stateless after construction) |
| `MinHashSignature`| ✅ Safe | N/A (immutable) |
| `MinHashIndex`    | ✅ Safe | ❌ Not safe for concurrent `Add` calls |

---

## License

MIT — see [LICENSE](LICENSE).
