# Performance Baseline - January 2026

This document captures performance measurements for the Strategos library, including Phase 1 baseline and Phase 2 optimization results.

## Test Environment

- **OS:** Linux Pop!_OS 24.04 LTS
- **CPU:** 13th Gen Intel Core i9-13900K @ 0.80GHz (24 physical cores, 32 logical)
- **Runtime:** .NET 10.0.1 (10.0.125.57005), X64 RyuJIT x86-64-v3
- **BenchmarkDotNet:** v0.15.8
- **Phase 1 Date:** 2026-01-17
- **Phase 2 Date:** 2026-01-18

## Summary

| Subsystem | Operation | P95 Latency | Target | Status |
|-----------|-----------|-------------|--------|--------|
| Thompson Sampling | Agent selection (5 candidates) | 819 ns | < 5ms | ✓ |
| Thompson Sampling | Agent selection (25 candidates) | 3.2 μs | < 5ms | ✓ |
| Thompson Sampling | Agent selection (100 candidates) | 12.6 μs | < 5ms | ✓ |
| Loop Detection | Detection (no loop) | 416 ns | < 1ms | ✓ |
| Loop Detection | Detection (repetition) | 640 ns | < 1ms | ✓ |
| Loop Detection | Detection (oscillation) | 824 ns | < 1ms | ✓ |
| Ledgers | Progress append (10 entries) | 32 ns | < 100μs | ✓ |
| Ledgers | Progress append (100 entries) | 60 ns | < 100μs | ✓ |
| Ledgers | Progress append (1000 entries) | 328 ns | < 100μs | ✓ |
| Cache | Cache hit | 17 ns | < 1μs | ✓ |
| Cache | Cache miss | 123 ns | < 1μs | ✓ |
| Vector Search | Search (100 docs, k=5) | 15 μs | < 10ms | ✓ |
| Vector Search | Search (1000 docs, k=5) | 191 μs | < 10ms | ✓ |
| Vector Search | Search (10000 docs, k=5) | 2.3 ms | < 10ms | ✓ |
| Workflow | Simple 3-step | 547 ns | - | - |
| Workflow | Complex 10-step | 690 ns | - | - |
| Workflow | Concurrent (10) | 6.3 μs | - | - |
| Workflow | Concurrent (100) | 57.6 μs | - | - |

**Legend:** ✓ = Meets target | ⚠ = Needs optimization | ✗ = Below target

---

## Phase 2 Optimization Results

Phase 2 implemented high-performance packages and eliminated allocation hot spots.

### Key Improvements

| Optimization | Before | After | Improvement |
|--------------|--------|-------|-------------|
| SpanOwner vs new Array | 99 ns | 6 ns | **16x faster** |
| MemoryPack vs STJ (10 entries) | 2.4 μs | 442 ns | **5.4x faster** |
| MemoryPack vs STJ (1000 entries) | 284 μs | 87 μs | **3.3x faster** |
| Loop oscillation (window=20) | 3.8 μs | 698 ns | **5.4x faster** |
| Memory allocations (SpanOwner) | 4,120 B | 0 B | **100% reduction** |

### Large-Scale Benchmarks (New in Phase 2)

| Scenario | P95 Latency | Allocations | Target | Status |
|----------|-------------|-------------|--------|--------|
| Agent selection (100 candidates) | 12.6 μs | 34 KB | < 5ms | ✓ |
| Agent selection (1,000 candidates) | 123 μs | 329 KB | < 5ms | ✓ |
| Agent selection (10,000 candidates) | 2.6 ms | 3.3 MB | < 5ms | ✓ |
| Document search (10,000 docs, k=5) | 2.3 ms | - | < 10ms | ✓ |

### Packages Integrated

| Package | Version | Usage |
|---------|---------|-------|
| MemoryPack | 1.21.3 | Ledger hashing, cache serialization |
| CommunityToolkit.HighPerformance | 8.4.0 | SpanOwner for temp arrays |
| BitFaster.Caching | 2.5.2 | Optional LRU cache backend |

---

## Detailed Results

### Thompson Sampling

Agent selection latency scales with candidate count.

| Candidates | P95 Latency | Memory |
|------------|-------------|--------|
| 5 | 819 ns | - |
| 25 | 3.2 μs | - |
| 100 | 12.6 μs | - |

**Belief Store Operations:**

| Operation | P95 Latency |
|-----------|-------------|
| GetBeliefAsync (cache hit) | 34 ns |
| GetBeliefAsync (allocation tracking) | 4.9 μs |
| GetBeliefsForAgentAsync | 357 ns |
| GetBeliefsForCategoryAsync | 774 ns |

**Note:** Agent selection at 100 candidates meets the 5ms target. BitFaster.Caching is now available as an optional cache backend (see Phase 2 integration).

---

### Loop Detection

Detection latency across different loop types.

| Scenario | Window Size | P95 Latency |
|----------|-------------|-------------|
| No loop detected | 5 | 416 ns |
| | 10 | 607 ns |
| | 20 | 1.2 μs |
| Repetition detected | 5 | 640 ns |
| | 10 | 996 ns |
| | 20 | 2.7 μs |
| Oscillation detected | 5 | 824 ns |
| | 10 | 1.4 μs |
| | 20 | 3.8 μs |

**Oscillation Pattern Analysis:**

| Pattern | P95 Latency |
|---------|-------------|
| Period 2 | 997 ns |
| Period 3 | 1.1 μs |
| No period | 1.4 μs |

**Semantic Similarity:**

| Scenario | P95 Latency |
|----------|-------------|
| High confidence (skip similarity) | 3.0 μs |
| Low confidence (call similarity) | 6.2 μs |

---

### Ledgers

Append and retrieval performance.

**Progress Ledger:**

| Operation | Entry Count | P95 Latency |
|-----------|-------------|-------------|
| WithEntry (append) | 10 | 32 ns |
| | 100 | 60 ns |
| | 1000 | 328 ns |
| GetRecentEntries | 10 | 1.2 μs |
| | 100 | 5.5 μs |
| | 1000 | 65.5 μs |
| GetMetrics | - | 70 ns |

**Task Ledger:**

| Operation | Task Count | P95 Latency |
|-----------|------------|-------------|
| WithTask (hash) | 10 | 34 ns |
| | 100 | 67 ns |
| | 1000 | 335 ns |
| VerifyIntegrity | 10 | 93 ns |
| | 100 | 89 ns |
| | 1000 | 87 ns |
| ComputeContentHash | 10 | 286 ns |
| | 100 | 1.7 μs |
| | 1000 | 18.2 μs |

**Allocation Benchmarks:**

| Operation | Allocations |
|-----------|-------------|
| ProgressLedger.WithEntry | 443 B |
| TaskLedger.WithTask | 0 B (cached) / 70 B |

---

### Budget & Execution

**Workflow Budget:**

| Operation | P95 Latency |
|-----------|-------------|
| OverallScarcity (first access) | 443 ns |
| OverallScarcity (cached) | 0 ns |
| WithConsumption | 70 ns |

**Step Execution Cache:**

| Operation | P95 Latency |
|-----------|-------------|
| Cache hit | 17 ns |
| Cache miss | 123 ns |

**Cache Allocation:** Zero-allocation confirmed for cache hit path.

---

### Vector Search

Search latency scales with corpus size.

| Document Count | Top-K | P95 Latency |
|----------------|-------|-------------|
| 100 | 5 | 15 μs |
| 100 | 20 | 15 μs |
| 1000 | 5 | 191 μs |
| 1000 | 20 | 202 μs |
| 10000 | 5 | 2.3 ms |
| 10000 | 20 | 2.8 ms |

**Batch vs Sequential (10 queries):**

| Mode | P95 Latency |
|------|-------------|
| Sequential | 1.6 ms |
| Parallel | 1.6 ms |

**Filter Index:**

| Filter | P95 Latency |
|--------|-------------|
| No filter | 188 μs |
| With filter | Not implemented |

---

### Comparative Benchmarks

**Serialization (JSON vs MemoryPack):**

| Entry Count | STJ Serialize | MemoryPack Serialize | Speedup |
|-------------|---------------|----------------------|---------|
| 10 | 2.4 μs | 442 ns | 5.4x |
| 100 | 23 μs | 5.3 μs | 4.3x |
| 1000 | 284 μs | 87 μs | 3.3x |

| Entry Count | STJ Deserialize | MemoryPack Deserialize | Speedup |
|-------------|-----------------|------------------------|---------|
| 10 | 3.9 μs | 667 ns | 5.8x |
| 100 | 39 μs | 7.9 μs | 4.9x |
| 1000 | 397 μs | 98 μs | 4.1x |

**Caching (ConcurrentDictionary vs BitFaster):**

| Operation | ConcurrentDict | ConcurrentLru | ConcurrentLfu |
|-----------|----------------|---------------|---------------|
| GetOrAdd | 19 ns | 90 ns | 124 ns |
| TryGetValue | 16 ns | - | - |

**Note:** ConcurrentDictionary is faster for simple operations, but BitFaster provides eviction policies needed for bounded caches.

**Memory Pooling:**

| Operation | P95 Latency |
|-----------|-------------|
| new Array | 99 ns |
| ArrayPool Rent/Return | 7 ns |
| SpanOwner Rent/Return | 6 ns |
| StringPool GetOrAdd | 35 ns |

---

### Integration Benchmarks

**Workflow Execution:**

| Scenario | P95 Latency |
|----------|-------------|
| Simple 3-step | 547 ns |
| Complex 10-step | 690 ns |
| With budget tracking | 582 ns |

**Concurrent Workflows:**

| Concurrency | P95 Latency |
|-------------|-------------|
| 10 workflows | 6.3 μs |
| 100 workflows | 57.6 μs |

---

## Optimization Status

### Completed in Phase 2

| Recommendation | Status | Result |
|----------------|--------|--------|
| MemoryPack for serialization hot paths | ✓ Complete | 3-6x speedup |
| SpanOwner for loop detection temp arrays | ✓ Complete | 5.4x speedup |
| ValueTask for sync async paths | ✓ Complete | Reduced allocations |
| BitFaster cache option | ✓ Complete | Optional LRU backend |
| Pre-sized lists in TaskLedger | ✓ Complete | Reduced allocations |
| HashSet indices in BeliefStore | ✓ Complete | Cleaner API |

### Future Considerations

1. **Filter index for vector search** - Not yet implemented
2. **Parallel sampling** - Not needed at current scale
3. **Result caching for vector search** - Optional, gated by configuration

---

## Benchmark Artifacts

Full benchmark results stored in:
- `BenchmarkDotNet.Artifacts/results/*.json` - Raw JSON data
- `BenchmarkDotNet.Artifacts/results/*.md` - GitHub-flavored markdown

To reproduce:
```bash
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter '*'
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| Phase 1 | 2026-01-17 | Initial baseline measurements |
| Phase 2 | 2026-01-18 | MemoryPack, SpanOwner, ValueTask, BitFaster integration |
