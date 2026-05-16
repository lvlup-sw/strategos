---
title: "Fork/Join: Parallel Execution with Synchronization"
---

# Fork/Join: Parallel Execution with Synchronization

## The Problem: Sequential Bottlenecks

Your financial analysis system evaluates companies. Technical analysis takes 2 seconds. Financial analysis takes 3 seconds. Market analysis takes 2.5 seconds. Running them sequentially takes 7.5 seconds.

**The problem**: These analyses are independent—they don't depend on each other. Why wait?

```csharp
// Sequential - slow
var financial = await AnalyzeFinancials(company);   // 3s
var technical = await AnalyzeTechnicals(company);   // 2s
var market = await AnalyzeMarket(company);          // 2.5s
// Total: 7.5 seconds
```

**What you need**: A workflow that:
1. Executes independent operations in parallel
2. Waits for all to complete before continuing
3. Merges results from all paths into unified state
4. Handles failures in any path gracefully

This is the **Fork/Join** pattern—parallel execution with synchronization.

---

## Learning Objectives

After this example, you will understand:

- **Fork** to execute multiple paths concurrently
- **Join** to synchronize and merge results
- **State merging** using reducer semantics
- **Error handling** in parallel paths (fail-fast vs. continue)
- **Instance names** for using the same step type in multiple paths

---

## Conceptual Foundation

### Fork vs. Branch

These patterns look similar but serve different purposes:

| Pattern | Execution | Use Case |
|---------|-----------|----------|
| **Branch** | One path executes (exclusive) | Different logic for different inputs |
| **Fork** | All paths execute (parallel) | Independent operations that can run together |

```text
Branch:     A ──▶ B OR C OR D ──▶ E     (one path)
Fork/Join:  A ──▶ B AND C AND D ──▶ E   (all paths)
```

### The Fork-Join Model

Fork creates multiple concurrent execution paths. Join waits for all paths to complete, then merges their state updates:

```text
                    ┌─── Path 1 ───┐
                    │              │
Start ──▶ Fork ────▶│── Path 2 ───│────▶ Join ──▶ Continue
                    │              │
                    └─── Path 3 ───┘
```

**Key properties**:
- All paths start simultaneously
- Each path runs independently
- Join blocks until ALL paths complete
- State from all paths is merged

### State Merging: How Does It Work?

When fork paths complete, their state updates are merged:

```text
Before Fork:
  State = { MarketData: ✓, Financial: null, Technical: null, Market: null }

Path 1 sets: Financial = { ... }
Path 2 sets: Technical = { ... }
Path 3 sets: Market = { ... }

After Join (merged):
  State = { MarketData: ✓, Financial: ✓, Technical: ✓, Market: ✓ }
```

**Merger rules**:
- **Different properties**: No conflict, all updates applied
- **Same property**: Last writer wins (overwrite)
- **`[Append]` properties**: All values accumulated

### When Paths Fail

What happens if one fork path fails?

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **Continue** (default) | Other paths complete, join receives partial state | Resilient workflows |
| **Fail-fast** | All paths canceled on first failure | All-or-nothing workflows |

```csharp
// Default: continue on error
.Fork(
    flow => flow.Then<Analysis1>(),
    flow => flow.Then<Analysis2>(),
    flow => flow.Then<Analysis3>())
.Join<Synthesize>()

// Fail-fast: stop all on first failure
.Fork(
    options => options.FailFast(),
    flow => flow.Then<Analysis1>(),
    flow => flow.Then<Analysis2>(),
    flow => flow.Then<Analysis3>())
.Join<Synthesize>()
```

---

## Design Decisions

| Decision | Why This Approach | Alternative | Trade-off |
|----------|-------------------|-------------|-----------|
| **Explicit Fork/Join** | Clear parallel boundaries | Implicit parallelism | More verbose, but explicit |
| **Continue on error** | Partial results often useful | Fail-fast | May proceed with incomplete data |
| **Overwrite semantics** | Simple, predictable | Conflict detection | No merge conflicts to handle |
| **All paths must complete** | Deterministic behavior | First-complete wins | Waits for slowest path |

### When to Use This Pattern

**Good fit when**:
- Operations are independent (no dependencies between paths)
- All results are needed for the next step
- Latency matters (parallel is faster)
- Partial results are acceptable (or fail-fast is OK)

**Poor fit when**:
- Paths depend on each other (use sequential)
- Only one path should execute (use Branch)
- Results needed as soon as available (use streaming)
- Complex merge logic required

### Anti-Patterns to Avoid

| Anti-Pattern | Problem | Correct Approach |
|--------------|---------|------------------|
| **Dependent paths** | Race conditions, unpredictable | Use sequential for dependencies |
| **Same property in multiple paths** | Last writer wins (unpredictable) | Use different properties or `[Append]` |
| **Too many paths** | Resource exhaustion | Limit parallelism or batch |
| **No error handling** | Silent partial results | Check for nulls in Join step |
| **Long-running paths** | Blocking forever | Add timeouts |

---

## Building the Workflow

### The Shape First

```text
                              ┌─────────────────────────┐
                              │ FinancialAnalysisStep   │
                         ┌───▶│                         │────┐
                         │    └─────────────────────────┘    │
                         │                                   │
┌────────────┐           │    ┌─────────────────────────┐    │    ┌───────────────────┐    ┌────────────────┐
│ GatherData │──── Fork ─┼───▶│ TechnicalAnalysisStep   │────┼───▶│ SynthesizeResults │───▶│ GenerateReport │
│            │           │    └─────────────────────────┘    │    │                   │    │                │
│ Get market │           │                                   │    │ Merge all three   │    │ Create final   │
│ data       │           │    ┌─────────────────────────┐    │    │ analyses          │    │ report         │
└────────────┘           └───▶│ MarketAnalysisStep      │────┘    └───────────────────┘    └────────────────┘
                              └─────────────────────────┘
                                                              Join
```

### State: What We Track

```csharp
[WorkflowState]
public record AnalysisState : IWorkflowState
{
    // Identity
    public Guid WorkflowId { get; init; }

    // Input
    public Company Company { get; init; } = null!;

    // Shared data (gathered before fork)
    public MarketData? MarketData { get; init; }

    // Fork path outputs (each path sets one)
    public FinancialAnalysis? FinancialAnalysis { get; init; }
    public TechnicalAnalysis? TechnicalAnalysis { get; init; }
    public MarketAnalysis? MarketAnalysis { get; init; }

    // Synthesized result (set after join)
    public SynthesizedReport? Report { get; init; }

    // Final output
    public string? FinalReport { get; init; }
}
```

**Why this design?**

- `MarketData`: Set before fork, available to all paths
- `FinancialAnalysis`, `TechnicalAnalysis`, `MarketAnalysis`: Each fork path sets exactly one—no conflicts
- `Report`: Set by the Join step after all analyses complete

### The Supporting Records

```csharp
public record Company(string Ticker, string Name, string Sector);

public record MarketData(
    decimal CurrentPrice,
    decimal Volume,
    IReadOnlyList<decimal> HistoricalPrices);

public record FinancialAnalysis(
    decimal RevenueGrowth,
    decimal ProfitMargin,
    decimal DebtToEquity,
    string Outlook);

public record TechnicalAnalysis(
    string Trend,
    decimal SupportLevel,
    decimal ResistanceLevel,
    IReadOnlyList<string> Signals);

public record MarketAnalysis(
    string SectorOutlook,
    IReadOnlyList<string> Competitors,
    decimal MarketShare,
    string CompetitivePosition);

public record SynthesizedReport(
    string Recommendation,
    decimal TargetPrice,
    string Rationale,
    IReadOnlyList<string> KeyRisks);
```

### The Workflow Definition

```csharp
var workflow = Workflow<AnalysisState>
    .Create("comprehensive-analysis")
    .StartWith<GatherData>()
    .Fork(
        flow => flow.Then<FinancialAnalysisStep>(),
        flow => flow.Then<TechnicalAnalysisStep>(),
        flow => flow.Then<MarketAnalysisStep>())
    .Join<SynthesizeResults>()
    .Finally<GenerateReport>();
```

**Reading this definition**:
1. Gather market data (shared by all analyses)
2. Fork into three parallel paths
3. Join and synthesize results
4. Generate final report

### The Pre-Fork Step

```csharp
public class GatherData : IWorkflowStep<AnalysisState>
{
    private readonly IMarketDataService _marketData;

    public GatherData(IMarketDataService marketData)
    {
        _marketData = marketData;
    }

    public async Task<StepResult<AnalysisState>> ExecuteAsync(
        AnalysisState state,
        StepContext context,
        CancellationToken ct)
    {
        // Fetch data that all fork paths will use
        var data = await _marketData.GetDataAsync(state.Company.Ticker, ct);

        return state
            .With(s => s.MarketData, data)
            .AsResult();
    }
}
```

**Key insight**: Data gathered before the fork is available to all paths.

### A Fork Path Step

```csharp
public class FinancialAnalysisStep : IWorkflowStep<AnalysisState>
{
    private readonly IFinancialAnalyzer _analyzer;

    public FinancialAnalysisStep(IFinancialAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public async Task<StepResult<AnalysisState>> ExecuteAsync(
        AnalysisState state,
        StepContext context,
        CancellationToken ct)
    {
        // Each path only sets its own property
        var analysis = await _analyzer.AnalyzeAsync(
            state.Company,
            state.MarketData!,  // Shared data from before fork
            ct);

        return state
            .With(s => s.FinancialAnalysis, analysis)
            .AsResult();
    }
}
```

**Important**: This step only sets `FinancialAnalysis`. Other paths set their own properties. No conflicts.

### The Join Step

```csharp
public class SynthesizeResults : IWorkflowStep<AnalysisState>
{
    private readonly IReportSynthesizer _synthesizer;

    public SynthesizeResults(IReportSynthesizer synthesizer)
    {
        _synthesizer = synthesizer;
    }

    public async Task<StepResult<AnalysisState>> ExecuteAsync(
        AnalysisState state,
        StepContext context,
        CancellationToken ct)
    {
        // All three analyses are available here after merge
        var report = await _synthesizer.SynthesizeAsync(
            state.FinancialAnalysis!,
            state.TechnicalAnalysis!,
            state.MarketAnalysis!,
            ct);

        return state
            .With(s => s.Report, report)
            .AsResult();
    }
}
```

**The magic**: By the time this step executes, the state has been merged from all three fork paths. All analyses are available.

---

## Accumulating Results with [Append]

When multiple paths should contribute to the same collection, use `[Append]`:

```csharp
[WorkflowState]
public record AnalysisState : IWorkflowState
{
    // ... other properties ...

    // All paths can add warnings - they accumulate
    [Append]
    public ImmutableList<AnalysisWarning> Warnings { get; init; } = [];
}
```

Each fork path can add warnings:

```csharp
// In FinancialAnalysisStep
return state
    .With(s => s.FinancialAnalysis, analysis)
    .With(s => s.Warnings, [new AnalysisWarning("High debt ratio")])
    .AsResult();

// In TechnicalAnalysisStep
return state
    .With(s => s.TechnicalAnalysis, analysis)
    .With(s => s.Warnings, [new AnalysisWarning("Downtrend detected")])
    .AsResult();
```

After join, `Warnings` contains both: `["High debt ratio", "Downtrend detected"]`.

---

## Instance Names for Duplicate Steps

If you need the same step type in multiple fork paths:

```csharp
.Fork(
    flow => flow.Then<AnalyzeStep>("Historical"),    // Instance name
    flow => flow.Then<AnalyzeStep>("Current"))      // Instance name
.Join<CompareResults>()
```

This generates distinct phases (`Historical`, `Current`) while sharing the step implementation.

---

## Generated Artifacts

### Phase Enum

```csharp
public enum ComprehensiveAnalysisPhase
{
    NotStarted,
    GatherData,
    FinancialAnalysisStep,
    TechnicalAnalysisStep,
    MarketAnalysisStep,
    SynthesizeResults,
    GenerateReport,
    Completed,
    Failed
}
```

### Saga Fork Handler

```csharp
// Generated handler for GatherData - cascades to all fork paths
public async Task<object[]> Handle(
    ExecuteGatherDataCommand command,
    GatherData step,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, context, ct);
    State = AnalysisStateReducer.Reduce(State, result.StateUpdate);

    // Return commands for ALL fork paths (executed in parallel)
    return [
        new ExecuteFinancialAnalysisStepCommand(WorkflowId),
        new ExecuteTechnicalAnalysisStepCommand(WorkflowId),
        new ExecuteMarketAnalysisStepCommand(WorkflowId)
    ];
}
```

**The key**: Returning an array of commands triggers parallel execution.

---

## Error Handling in Fork Paths

### Default: Continue on Error

```csharp
.Fork(
    flow => flow.Then<Analysis1>(),  // Fails
    flow => flow.Then<Analysis2>(),  // Succeeds
    flow => flow.Then<Analysis3>())  // Succeeds
.Join<Synthesize>()  // Receives partial state (Analysis2 + Analysis3)
```

The Join step must handle missing data:

```csharp
public async Task<StepResult<AnalysisState>> ExecuteAsync(...)
{
    // Check for missing analyses
    if (state.FinancialAnalysis is null)
    {
        // Handle missing financial data
        // Option 1: Use defaults
        // Option 2: Return partial report
        // Option 3: Fail the workflow
    }
    // ...
}
```

### Fail-Fast Mode

```csharp
.Fork(
    options => options.FailFast(),
    flow => flow.Then<Analysis1>(),
    flow => flow.Then<Analysis2>(),
    flow => flow.Then<Analysis3>())
.Join<Synthesize>()
```

If any path fails, all others are canceled and the workflow fails.

---

## The "Aha Moment"

> **Parallelism isn't just about speed—it's about independence.**
>
> When you identify that three operations are independent, you've discovered something important about your domain. Fork/Join makes that independence explicit. The workflow definition documents: "These three analyses don't depend on each other."
>
> The state merge after Join proves the independence was real—if paths weren't independent, you'd have conflicts. The clean merge validates your design.

---

## Extension Exercises

### Exercise 1: Add Timeout Handling

Prevent slow analyses from blocking forever:

1. Configure timeout per fork path
2. On timeout, continue with partial results
3. Add `TimedOut` flag to track which analyses failed

### Exercise 2: Add Weighted Synthesis

Not all analyses are equally important:

1. Add `Weight` property to each analysis type
2. In SynthesizeResults, weight the contributions
3. Document which analyses matter most

### Exercise 3: Add Conditional Fork Paths

Only run certain analyses based on company type:

1. Check `Company.Sector` before forking
2. For tech companies, add `TechStackAnalysis`
3. For financial companies, add `RegulatoryAnalysis`

---

## Key Takeaways

1. **Fork executes paths in parallel**—all start simultaneously
2. **Join waits for all paths**—no path is skipped
3. **State is merged after join**—each path contributes its updates
4. **Use different properties per path**—avoids merge conflicts
5. **Use `[Append]` for accumulation**—multiple paths can add to same collection
6. **Handle partial results**—some paths may fail, Join must cope

---

## Related

- [Basic Workflow](/strategos/examples/basic-workflow/) - Sequential steps without parallelism
- [Branching Pattern](/strategos/examples/branching/) - Exclusive routing (one path, not all)
- [Iterative Refinement Pattern](/strategos/examples/iterative-refinement/) - Loops for quality improvement
