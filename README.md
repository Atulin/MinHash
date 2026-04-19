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

## Installation

```shell
dotnet add package Atulin.MinHash
````

Requires **.NET 10.0** or later.

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
Console.WriteLine($"Similarity: {similarity:P1}");
```

## Usage

### `MinHasher` — Core Engine

```csharp
// Default: 128 hash functions, 3-char shingles, fixed seed
var hasher = new MinHasher();

// Custom configuration
var hasher = new MinHasher(
    signatureSize: 256,
    shingleSize:   3,
    seed:          42);
```

#### `ComputeSignature` — allocating overload

```csharp
uint[] signature = hasher.ComputeSignature("hello world");
```

#### `ComputeSignatureTo` — zero-allocation overload

```csharp
uint[] buffer = new uint[hasher.SignatureSize];
hasher.ComputeSignatureTo("hello world", buffer);
```

#### `EstimateJaccard` — static, SIMD-accelerated

```csharp
double j = MinHasher.EstimateJaccard(sigA, sigB); // 0.0 – 1.0
```

### `MinHashSignature` — Immutable Wrapper

```csharp
var hasher = new MinHasher(signatureSize: 128);

MinHashSignature sigA = new(hasher.ComputeSignature("document one"));
MinHashSignature sigB = new(hasher.ComputeSignature("document two"));

double j = sigA.Jaccard(sigB);

Console.WriteLine(sigA.Length); // 128
ReadOnlySpan<uint> raw = sigA.Span;
```

### `MinHashIndex` — Similarity Search

```csharp
var hasher = new MinHasher(signatureSize: 128);
var index  = new MinHashIndex(hasher);

index.Add("doc-1", "the quick brown fox jumps over the lazy dog");
index.Add("doc-2", "a fast auburn fox leaps across a sleepy hound");
index.Add("doc-3", "completely unrelated text about cooking pasta");

var results = index.Query("quick fox jumps over dog", threshold: 0.3);

foreach (var (key, similarity) in results)
    Console.WriteLine($"{key}: {similarity:P1}");
```

## Algorithm

1. Decompose text into overlapping **$k$-character shingles** (character n-grams).
2. Hash each shingle with **xxHash32** over its raw UTF-16 bytes.
3. Apply **$n$ universal hash functions**: $h_i(x) = (a_i x + b_i) \bmod (2^{31} - 1)$
4. Compute signature: $\text{Signature}[i] = \min_{s \in S} h_i(s)$
5. Estimate similarity: $J(A, B) \approx \frac{|{i : \text{sig}_A[i] = \text{sig}_B[i]}|}{n}$

The Mersenne-prime modulo $2^{31} - 1$ is computed with a fast bitwise fold instead of integer division.

## Benchmarks

Measured on **AMD Ryzen 9 9900X**, .NET 10.0.6, x64 RyuJIT (AVX512/AVX2 available), BenchmarkDotNet v0.15.8.

### Benchmarks on 151-character text

| Method                     | Signature Size |      Mean | Allocated |
| -------------------------- | -------------: | --------: | --------: |
| `ComputeCharSignature`     |            128 |  16.72 µs |     536 B |
| `ComputeCharSignatureInto` |            128 |  51.53 µs |         - |
| `ComputeWordSignature`     |            128 |   3.37 µs |    1144 B |
| `ComputeWordSignatureInto` |            128 |   9.70 µs |     608 B |
| `EstimateJaccard`          |            128 |   5.43 ns |         - |
| `ComputeCharSignature`     |            256 |  32.45 µs |    1048 B |
| `ComputeCharSignatureInto` |            256 | 101.96 µs |         - |
| `ComputeWordSignature`     |            256 |   6.07 µs |    1656 B |
| `ComputeWordSignatureInto` |            256 |  18.06 µs |     608 B |
| `EstimateJaccard`          |            256 |   9.67 ns |         - |

## Configuration Guide

| Parameter       | Default      | Recommendation                                    |
| --------------- | ------------ | ------------------------------------------------- |
| `signatureSize` | 128          | 128 for ~97% accuracy; 256 for ~99% accuracy      |
| `shingleSize`   | 3            | 3–4 for short texts; 5 for longer documents       |
| `seed`          | `0xDEADBEEF` | Change only if you need independent hash families |

**Accuracy vs. size trade-off:** Jaccard estimation error is approximately $\frac{1}{\sqrt{n}}$

* At $n = 128$: error $\approx 8.8%$
* At $n = 256$: error $\approx 6.25%$

## Thread Safety

| Type               | Read   | Write                           |
| ------------------ | ------ | ------------------------------- |
| `MinHasher`        | ✅ Safe | ✅ Safe                          |
| `MinHashSignature` | ✅ Safe | N/A                             |
| `MinHashIndex`     | ✅ Safe | ❌ Not safe for concurrent `Add` |

## License

MIT — see [LICENSE](LICENSE).
