# Performance Targets and Baselines

This document defines performance targets for Strategos subsystems, documents current baseline measurements, and specifies optimization trigger conditions.

## Performance Targets

| Benchmark | Baseline Target | Optimized Target | Measurement | Actual | Status |
|-----------|-----------------|------------------|-------------|--------|--------|
| Agent selection (25 candidates) | < 5ms p95 | < 2ms p95 | Latency | 3.2 μs | ✓ |
| Agent selection (100 candidates) | < 5ms p95 | < 2ms p95 | Latency | 12.1 μs | ✓ |
| Loop detection (window=20) | < 1ms p95 | < 0.5ms p95 | Latency | 3.8 μs | ✓ |
| Ledger append (1000 entries) | < 100μs | < 50μs | Latency | 328 ns | ✓ |
| Cache hit | < 1μs | < 0.5μs | Latency | 17 ns | ✓ |
| Vector search (1000 docs, k=5) | < 10ms | < 5ms | Latency | 191 μs | ✓ |
| Concurrent workflows (100) | > 500/sec | > 1000/sec | Throughput | 17.4K/sec | ✓ |
| Cache hit allocation | 0 B | 0 B | Memory | 0 B | ✓ |
| Ledger append allocation | < 1KB | < 200B | Memory | 443 B | ⚠ |

**Legend:** ✓ = Meets target | ⚠ = Needs optimization | ✗ = Below target

## Current Baselines

> **Measured:** 2026-01-17 on Intel i9-13900K, .NET 10.0.1

### Agent Selection (ThompsonSampling)

| Candidates | P95 | Status | Date |
|------------|-----|--------|------|
| 5 | 819 ns | ✓ | 2026-01-17 |
| 25 | 3.2 μs | ✓ | 2026-01-17 |
| 100 | 12.1 μs | ✓ | 2026-01-17 |

### Loop Detection

| Window Size | Scenario | P95 | Status | Date |
|-------------|----------|-----|--------|------|
| 5 | No loop | 416 ns | ✓ | 2026-01-17 |
| 10 | No loop | 607 ns | ✓ | 2026-01-17 |
| 20 | No loop | 1.2 μs | ✓ | 2026-01-17 |
| 5 | Oscillation | 824 ns | ✓ | 2026-01-17 |
| 10 | Oscillation | 1.4 μs | ✓ | 2026-01-17 |
| 20 | Oscillation | 3.8 μs | ✓ | 2026-01-17 |

### Ledger Operations

| Operation | Entries | P95 | Status | Date |
|-----------|---------|-----|--------|------|
| Append | 10 | 32 ns | ✓ | 2026-01-17 |
| Append | 100 | 60 ns | ✓ | 2026-01-17 |
| Append | 1000 | 328 ns | ✓ | 2026-01-17 |
| GetRecent | 10 | 1.2 μs | - | 2026-01-17 |
| GetRecent | 100 | 5.5 μs | - | 2026-01-17 |
| GetRecent | 1000 | 65.5 μs | - | 2026-01-17 |

### Cache Operations

| Operation | P95 | Allocated | Status | Date |
|-----------|-----|-----------|--------|------|
| Cache hit | 17 ns | 0 B | ✓ | 2026-01-17 |
| Cache miss | 123 ns | - | ✓ | 2026-01-17 |

### Vector Search

| Corpus Size | k | P95 | Status | Date |
|-------------|---|-----|--------|------|
| 100 | 5 | 15 μs | ✓ | 2026-01-17 |
| 100 | 20 | 15 μs | ✓ | 2026-01-17 |
| 1000 | 5 | 191 μs | ✓ | 2026-01-17 |
| 1000 | 20 | 202 μs | ✓ | 2026-01-17 |
| 10000 | 5 | 2.3 ms | ✓ | 2026-01-17 |
| 10000 | 20 | 2.8 ms | ✓ | 2026-01-17 |

### Integration Benchmarks

| Scenario | Concurrency | P95 Latency | Throughput | Date |
|----------|-------------|-------------|------------|------|
| Simple 3-step | 1 | 547 ns | - | 2026-01-17 |
| Complex 10-step | 1 | 690 ns | - | 2026-01-17 |
| With budget | 1 | 582 ns | - | 2026-01-17 |
| Concurrent | 10 | 6.3 μs | 1.6M/sec | 2026-01-17 |
| Concurrent | 100 | 57.6 μs | 17.4K/sec | 2026-01-17 |

### Comparative Benchmarks

**Serialization (1000 entries):**

| Serializer | Serialize P95 | Deserialize P95 | Speedup |
|------------|---------------|-----------------|---------|
| System.Text.Json | 284 μs | 397 μs | baseline |
| MemoryPack | 87 μs | 98 μs | 3.3-4.1x |

**Caching:**

| Implementation | GetOrAdd P95 |
|----------------|--------------|
| ConcurrentDictionary | 19 ns |
| ConcurrentLru | 90 ns |
| ConcurrentLfu | 124 ns |

**Memory Pooling:**

| Operation | P95 |
|-----------|-----|
| new Array | 99 ns |
| ArrayPool Rent/Return | 7 ns |
| SpanOwner | 6 ns |

## Optimization Triggers

An optimization is warranted when:

### Latency Triggers

| Condition | Action |
|-----------|--------|
| p95 > 2x baseline target | Investigate hot path |
| Mean > 5ms for cache operations | Add caching layer |
| Mean increases > 20% after change | Regression - revert or fix |

### Memory Triggers

| Condition | Action |
|-----------|--------|
| Gen0 > 0 in hot path | Investigate allocation source |
| Allocated > 1KB per operation | Pool or reuse objects |
| Gen1/Gen2 > 0 | Critical - investigate lifetime |

### Throughput Triggers

| Condition | Action |
|-----------|--------|
| < 500 workflows/sec at 100 concurrency | Profile contention |
| Linear scaling fails before 50 workers | Identify bottleneck |

## Package Integration Decision Criteria

High-performance packages should be adopted when measurements justify:

### BitFaster.Caching

| Integration Point | Trigger Condition | Current | Replace With |
|-------------------|-------------------|---------|--------------|
| StepExecutionLedger | Cache miss > 10% AND memory pressure | ConcurrentDictionary | ConcurrentLru |
| BeliefStore | Belief count > 1000 | ConcurrentDictionary | ConcurrentLfu |
| VectorSearch | Repeated query rate > 30% | None | ConcurrentLru |

**Baseline finding:** ConcurrentDictionary is 5-7x faster for simple operations, but BitFaster provides necessary eviction policies for bounded caches.

### MemoryPack

| Integration Point | Trigger Condition | Current | Replace With |
|-------------------|-------------------|---------|--------------|
| Cache entries | Serialization > 5% of op time | JsonSerializer | MemoryPackSerializer |
| Artifact store | Artifact size > 10KB avg | JsonSerializer | MemoryPackSerializer |
| Ledger hashing | Hash > 1% of append time | SHA256 of JSON | MemoryPack bytes |

**Baseline finding:** MemoryPack is 3-6x faster than System.Text.Json. Recommend adoption for serialization hot paths.

### CommunityToolkit.HighPerformance

| Integration Point | Trigger Condition | Current | Replace With |
|-------------------|-------------------|---------|--------------|
| LoopDetector arrays | Allocation > 1KB per detection | `new string[n]` | `SpanOwner<string>` |
| Key creation | > 100 key creations/sec | String concat | StringPool |
| Parallel loops | > 10 independent items | Manual loops | ParallelHelper |

**Baseline finding:** SpanOwner and ArrayPool show 14-16x improvement over allocation. Recommend adoption for temporary arrays.

## Measurement Methodology

### Environment

- **Primary:** Intel i9-13900K, 24 cores, 32 threads
- **OS:** Linux Pop!_OS 24.04 LTS
- **Runtime:** .NET 10.0.1

### BenchmarkDotNet Configuration

```csharp
AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0));
AddDiagnoser(MemoryDiagnoser.Default);
AddColumn(StatisticColumn.P95);
AddExporter(MarkdownExporter.GitHub);
AddExporter(JsonExporter.Full);
```

### Stability Criteria

- Run each benchmark 3 times minimum
- Verify < 5% variance between runs
- Warmup until steady-state detected
- Results should scale predictably with parameters

## Historical Measurements

| Date | Commit | Summary |
|------|--------|---------|
| 2026-01-17 | 5d7139c | Initial baseline - all targets met |

## References

- [Baseline Details](./BASELINE.md)
- [Benchmark README](../../src/Strategos.Benchmarks/README.md)
- [Design Document](../designs/2026-01-17-performance-benchmarks-and-optimizations.md)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
