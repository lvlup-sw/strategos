---
title: "Parallel Execution"
---

# Parallel Execution

When workflow steps are independent, running them sequentially wastes time. Financial analysis can examine fundamentals, technicals, and market position simultaneously. Document processing can extract text, images, and metadata in parallel. This tutorial shows you how to use Fork and Join for concurrent execution.

## What You Will Build

A comprehensive stock analysis workflow that runs three independent analyses in parallel:

1. **Financial Analysis** - Revenue, margins, debt ratios
2. **Technical Analysis** - Price trends, support/resistance levels
3. **Market Analysis** - Sector outlook, competitive position

After all three complete, results are synthesized into a final report.

## Step 1: Define the State

The state includes fields for each parallel analysis result:

```csharp
[WorkflowState]
public record AnalysisState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public Company Company { get; init; } = null!;
    public MarketData? MarketData { get; init; }
    public FinancialAnalysis? FinancialAnalysis { get; init; }
    public TechnicalAnalysis? TechnicalAnalysis { get; init; }
    public MarketAnalysis? MarketAnalysis { get; init; }
    public SynthesizedReport? Report { get; init; }
    public string? FinalReport { get; init; }
}

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

## Step 2: Define the Workflow with Fork/Join

Use `Fork` to start parallel paths and `Join` to synchronize them:

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

All three analysis steps execute concurrently. The `Join<SynthesizeResults>` step waits for all paths to complete, then executes with the merged state containing all analysis results.

## Step 3: Implement the Steps

### GatherData

Fetches the market data needed by all analyses:

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
        var data = await _marketData.GetDataAsync(state.Company.Ticker, ct);

        return state
            .With(s => s.MarketData, data)
            .AsResult();
    }
}
```

### FinancialAnalysisStep

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
        var analysis = await _analyzer.AnalyzeAsync(
            state.Company,
            state.MarketData!,
            ct);

        return state
            .With(s => s.FinancialAnalysis, analysis)
            .AsResult();
    }
}
```

### TechnicalAnalysisStep

```csharp
public class TechnicalAnalysisStep : IWorkflowStep<AnalysisState>
{
    private readonly ITechnicalAnalyzer _analyzer;

    public TechnicalAnalysisStep(ITechnicalAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public async Task<StepResult<AnalysisState>> ExecuteAsync(
        AnalysisState state,
        StepContext context,
        CancellationToken ct)
    {
        var analysis = await _analyzer.AnalyzeAsync(
            state.Company.Ticker,
            state.MarketData!.HistoricalPrices,
            ct);

        return state
            .With(s => s.TechnicalAnalysis, analysis)
            .AsResult();
    }
}
```

### MarketAnalysisStep

```csharp
public class MarketAnalysisStep : IWorkflowStep<AnalysisState>
{
    private readonly IMarketAnalyzer _analyzer;

    public MarketAnalysisStep(IMarketAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public async Task<StepResult<AnalysisState>> ExecuteAsync(
        AnalysisState state,
        StepContext context,
        CancellationToken ct)
    {
        var analysis = await _analyzer.AnalyzeAsync(
            state.Company.Sector,
            state.Company.Ticker,
            ct);

        return state
            .With(s => s.MarketAnalysis, analysis)
            .AsResult();
    }
}
```

### SynthesizeResults

The join step receives state with all analysis results populated:

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
        // All three analyses are available here after Join
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

### GenerateReport

```csharp
public class GenerateReport : IWorkflowStep<AnalysisState>
{
    private readonly IReportGenerator _generator;

    public GenerateReport(IReportGenerator generator)
    {
        _generator = generator;
    }

    public async Task<StepResult<AnalysisState>> ExecuteAsync(
        AnalysisState state,
        StepContext context,
        CancellationToken ct)
    {
        var markdown = await _generator.GenerateMarkdownAsync(
            state.Company,
            state.Report!,
            ct);

        return state
            .With(s => s.FinalReport, markdown)
            .AsResult();
    }
}
```

## Understanding State Merging

When fork paths complete, their states are merged using reducer semantics.

### Default Behavior (Overwrite)

By default, the last value wins for scalar properties:

```csharp
// Each fork path sets a different property - no conflicts
public FinancialAnalysis? FinancialAnalysis { get; init; }
public TechnicalAnalysis? TechnicalAnalysis { get; init; }
public MarketAnalysis? MarketAnalysis { get; init; }
```

Since each path sets a unique property, merging is straightforward.

### Collection Accumulation

Use `[Append]` to accumulate values across paths:

```csharp
[WorkflowState]
public record AnalysisState : IWorkflowState
{
    // Scalar properties use overwrite semantics
    public FinancialAnalysis? FinancialAnalysis { get; init; }

    // Collection properties can accumulate with [Append]
    [Append]
    public ImmutableList<AnalysisWarning> Warnings { get; init; } = [];
}
```

Each fork path can add warnings, and all warnings appear in the merged state.

## Advanced Fork Patterns

### Instance Names for Duplicate Steps

If you need the same step type in multiple fork paths, use instance names:

```csharp
.Fork(
    flow => flow.Then<AnalyzeStep>("Technical"),
    flow => flow.Then<AnalyzeStep>("Fundamental"))
.Join<SynthesizeStep>()
```

This generates distinct phases (`Technical`, `Fundamental`) but shares the step implementation.

### Multi-Step Fork Paths

Each fork path can contain multiple sequential steps:

```csharp
.Fork(
    flow => flow
        .Then<FetchFinancials>()
        .Then<AnalyzeFinancials>(),
    flow => flow
        .Then<FetchTechnicals>()
        .Then<AnalyzeTechnicals>())
.Join<Synthesize>()
```

### Error Handling in Fork Paths

By default, fork paths continue independently even if one fails:

```csharp
// Default: continue-on-error
.Fork(
    flow => flow.Then<Analysis1>(),
    flow => flow.Then<Analysis2>(),
    flow => flow.Then<Analysis3>())
.Join<Synthesize>()
```

Enable fail-fast to stop all paths when one fails:

```csharp
// Fail-fast: cancel other paths on first failure
.Fork(
    options => options.FailFast(),
    flow => flow.Then<Analysis1>(),
    flow => flow.Then<Analysis2>(),
    flow => flow.Then<Analysis3>())
.Join<Synthesize>()
```

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

### Fork Handler

The generated saga cascades to all fork paths:

```csharp
// After GatherData completes, start all fork paths
public async Task<object[]> Handle(
    ExecuteGatherDataCommand command,
    GatherData step,
    IDocumentSession session,
    TimeProvider time,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, ct);
    State = AnalysisStateReducer.Reduce(State, result.StateUpdate);

    // Return commands for all fork paths (executed in parallel)
    return [
        new ExecuteFinancialAnalysisStepCommand(WorkflowId),
        new ExecuteTechnicalAnalysisStepCommand(WorkflowId),
        new ExecuteMarketAnalysisStepCommand(WorkflowId)
    ];
}
```

## Key Points

- **Fork paths execute concurrently** for faster total completion time
- **Join waits for all paths** before continuing to the next step
- **State from all paths is merged** using reducer semantics
- **Use instance names** when the same step type appears in multiple paths
- **Each fork path can have multiple steps** chained sequentially
- **Configure error handling** for fail-fast or continue-on-error behavior

## Next Steps

You have learned how to run steps in parallel. Sometimes workflows need to iterate until a condition is met:

- [Loops](./loops) - Repeat steps until quality thresholds are achieved
- [Approvals](./approvals) - Pause workflows for human review
- [Agent Selection](./agents) - Choose the best agent for each task
