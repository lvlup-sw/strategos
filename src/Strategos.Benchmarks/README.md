# Strategos.Benchmarks

Performance benchmarking suite for Strategos, built on [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

### Prerequisites

- .NET 10.0 SDK (or .NET 8.0 for stable builds)
- Release configuration required for accurate measurements

### Run All Benchmarks

```bash
dotnet run -c Release --project src/Strategos.Benchmarks
```

### Run with Interactive Picker

When run without arguments, BenchmarkSwitcher presents an interactive menu:

```bash
dotnet run -c Release --project src/Strategos.Benchmarks
```

### Filter Specific Benchmarks

Use `--filter` to run specific benchmarks by name pattern:

```bash
# Run all ledger benchmarks
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter *Ledger*

# Run all selection/Thompson sampling benchmarks
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter *ThompsonSampling*

# Run specific benchmark class
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter *AgentSelectionBenchmarks*

# Measure typed action-composition proof and projection costs
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter *ActionCompositionProofBenchmarks*

# Run specific benchmark method
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter *AgentSelectionBenchmarks.SelectAgent*

# Multiple patterns (OR logic)
dotnet run -c Release --project src/Strategos.Benchmarks -- --filter *Ledger* *Cache*
```

### Filter Syntax Reference

| Pattern | Matches |
|---------|---------|
| `*` | All benchmarks |
| `*Ledger*` | Any benchmark with "Ledger" in the name |
| `*Benchmarks.Method` | Specific method in any class ending with "Benchmarks" |
| `Namespace.*` | All benchmarks in a namespace |
| `*A* *B*` | Multiple patterns (OR) |

### Export Results

```bash
# Export to JSON (for CI/automation)
dotnet run -c Release --project src/Strategos.Benchmarks -- --exporters json

# Export to Markdown (for documentation)
dotnet run -c Release --project src/Strategos.Benchmarks -- --exporters markdown

# Export to both
dotnet run -c Release --project src/Strategos.Benchmarks -- --exporters json markdown

# Custom artifacts directory
dotnet run -c Release --project src/Strategos.Benchmarks -- --artifacts ./my-results
```

Results are written to `BenchmarkDotNet.Artifacts/` by default.

## Interpreting Results

### Key Metrics

| Metric | Description | Target |
|--------|-------------|--------|
| **Mean** | Average execution time | Primary comparison metric |
| **P95** | 95th percentile latency | Tail latency indicator |
| **Gen0** | Gen0 garbage collections | Lower is better (0 ideal) |
| **Allocated** | Memory allocated per operation | Lower is better (0 B ideal) |

### Example Output

```text
| Method          | CandidateCount |     Mean |     Error |   StdDev |      P95 | Rank |   Gen0 | Allocated |
|---------------- |--------------- |---------:|----------:|---------:|---------:|-----:|-------:|----------:|
| SelectAgent     |              5 | 1.234 us | 0.0123 us | 0.011 us | 1.256 us |    1 | 0.0153 |     128 B |
| SelectAgent     |             25 | 2.456 us | 0.0234 us | 0.021 us | 2.501 us |    2 | 0.0305 |     256 B |
| SelectAgent     |            100 | 5.678 us | 0.0567 us | 0.050 us | 5.789 us |    3 | 0.0610 |     512 B |
```

### Understanding Allocations

- **0 B Allocated**: Zero-allocation hot path (optimal)
- **Gen0 > 0**: Short-lived allocations (minor GC pressure)
- **Gen1/Gen2 > 0**: Long-lived allocations (investigate)

### Regression Detection

CI uses a 20% threshold for regression detection:

- **< 20% slower**: No alert
- **>= 20% slower**: Marked as regression
- Comparison requires baseline from `main` branch

## Adding New Benchmarks

### 1. Create Benchmark Class

```csharp
using BenchmarkDotNet.Attributes;

namespace Strategos.Benchmarks.Subsystems.YourSubsystem;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class YourBenchmarks
{
    // Parameterized input
    [Params(10, 100, 1000)]
    public int InputSize { get; set; }

    private YourService _service = null!;

    [GlobalSetup]
    public void Setup()
    {
        _service = new YourService();
        // Initialize with InputSize
    }

    [Benchmark(Baseline = true)]
    public async Task<Result> CurrentImplementation()
    {
        return await _service.DoWorkAsync();
    }

    [Benchmark]
    public async Task<Result> OptimizedImplementation()
    {
        return await _service.DoWorkOptimizedAsync();
    }
}
```

### 2. Benchmark Patterns

#### Pattern A: Latency at Scale

Test how performance scales with input size:

```csharp
[Params(5, 25, 100)]
public int CandidateCount { get; set; }
```

#### Pattern B: Zero-Allocation Validation

Verify hot paths allocate no memory:

```csharp
[MemoryDiagnoser]
// Expected: Gen0 = 0, Allocated = 0 B
```

#### Pattern C: Comparative Analysis

Compare implementations side-by-side:

```csharp
[Benchmark(Baseline = true)]
public Result CurrentApproach() { }

[Benchmark]
public Result NewApproach() { }
```

### 3. Directory Structure

Place benchmarks in the appropriate subsystem directory:

```text
src/Strategos.Benchmarks/
├── Subsystems/
│   ├── ThompsonSampling/     # Agent selection benchmarks
│   ├── LoopDetection/        # Loop detection benchmarks
│   ├── Ledgers/              # Ledger operation benchmarks
│   ├── Budget/               # Workflow budget benchmarks
│   ├── Ontology/             # Exact action-contract proof benchmarks
│   ├── VectorSearch/         # RAG/search benchmarks
│   └── StepExecution/        # Cache and execution benchmarks
├── Comparative/              # Package comparison benchmarks
├── Integration/              # End-to-end workflow benchmarks
└── Fixtures/                 # Shared test data and utilities
```

### 4. Best Practices

1. **Use `[GlobalSetup]`** for initialization, not in benchmarks
2. **Return results** to prevent dead code elimination
3. **Use `[Params]`** to test at multiple scales
4. **Mark baseline** with `[Benchmark(Baseline = true)]`
5. **Avoid I/O** in benchmark methods when testing pure computation
6. **Warmup** is handled automatically by BenchmarkDotNet

The ontology proof benchmarks are non-gating measurements. They deliberately
contain no wall-clock assertions or pass/fail latency budgets; the exhaustive
oracle and contract tests gate correctness, while benchmark results show how
proof cost and allocation scale as predicates introduce more finite-domain
cells.

## Common Commands

```bash
# Quick run with less iterations (for development)
dotnet run -c Release --project src/Strategos.Benchmarks -- --job short

# Dry run (validate setup without running)
dotnet run -c Release --project src/Strategos.Benchmarks -- --job dry

# List all available benchmarks
dotnet run -c Release --project src/Strategos.Benchmarks -- --list flat

# Memory profiling only
dotnet run -c Release --project src/Strategos.Benchmarks -- --memory

# Run with detailed statistics
dotnet run -c Release --project src/Strategos.Benchmarks -- --statisticalTest 5%
```

## CI Integration

### Regression Workflow

The `benchmark-regression.yml` workflow:

1. Triggers on PRs modifying performance-critical paths
2. Detects which subsystems changed
3. Runs targeted benchmarks with `--filter`
4. Compares against baseline (20% threshold)
5. Comments results on PR

### Full Suite Workflow

The `benchmark-full.yml` workflow:

1. Manually triggered via `workflow_dispatch`
2. Runs complete benchmark suite
3. Exports JSON and Markdown artifacts
4. Optionally compares against a reference commit

## Troubleshooting

### "Benchmark failed: Assembly not found"

Ensure you're using Release configuration:

```bash
dotnet run -c Release ...  # Not Debug
```

### "Results vary significantly between runs"

1. Close other applications
2. Disable CPU throttling
3. Run on consistent hardware
4. Increase iteration count

### "GlobalSetup not called"

Ensure benchmark class has `[Config(typeof(BenchmarkConfig))]` or uses default config.

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/articles/overview.html)
- [Performance Targets](../../docs/benchmarks/PERFORMANCE.md)
- [Design Document](../../docs/designs/2026-01-17-performance-benchmarks-and-optimizations.md)
