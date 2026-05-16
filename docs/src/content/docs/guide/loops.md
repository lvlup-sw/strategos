---
title: "Iterative Refinement"
---

# Iterative Refinement

AI-generated content rarely achieves production quality on the first attempt. Code needs debugging. Articles need editing. Designs need revision. This tutorial shows you how to use `RepeatUntil` for quality-driven iteration, where workflows repeat until a threshold is met or a maximum iteration count is reached.

## What You Will Build

A content refinement workflow that:

1. **Generates** an initial draft on a topic
2. **Critiques** the draft and assigns a quality score
3. **Refines** the content based on critique feedback
4. **Repeats** steps 2-3 until quality reaches 90% or 5 iterations complete
5. **Publishes** the final content

## Step 1: Define the State

The state tracks the current draft, quality score, iteration count, and history:

```csharp
[WorkflowState]
public record RefinementState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string? CurrentDraft { get; init; }
    public decimal QualityScore { get; init; }
    public int IterationCount { get; init; }

    [Append]
    public ImmutableList<CritiqueResult> CritiqueHistory { get; init; } = [];

    [Append]
    public ImmutableList<RefinementAttempt> RefinementHistory { get; init; } = [];

    public string? FinalContent { get; init; }
}

public record CritiqueResult(
    decimal Score,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Suggestions);

public record RefinementAttempt(
    int Iteration,
    string BeforeContent,
    string AfterContent,
    IReadOnlyList<string> ChangesApplied);
```

The `[Append]` attribute on the history collections ensures each iteration's results accumulate rather than overwrite.

## Step 2: Define the Workflow with RepeatUntil

The `RepeatUntil` method takes a condition, maximum iterations, and a loop body:

```csharp
var workflow = Workflow<RefinementState>
    .Create("iterative-refinement")
    .StartWith<GenerateDraft>()
    .RepeatUntil(
        condition: state => state.QualityScore >= 0.9m,
        maxIterations: 5,
        body: flow => flow
            .Then<Critique>()
            .Then<Refine>())
    .Finally<Publish>();
```

The loop continues until either:
1. `QualityScore >= 0.9` (quality threshold met)
2. 5 iterations have been completed (maximum reached)

After exiting the loop, execution continues to `Publish`.

## Loop Patterns

### Quality Threshold

Stop when output quality is sufficient:

```csharp
.RepeatUntil(
    condition: state => state.QualityScore >= 0.9m,
    maxIterations: 10,
    body: flow => flow
        .Then<Evaluate>()
        .Then<Improve>())
```

### Convergence Detection

Stop when improvement plateaus:

```csharp
.RepeatUntil(
    condition: state => state.ImprovementDelta < 0.01m,
    maxIterations: 20,
    body: flow => flow
        .Then<Iterate>()
        .Then<MeasureDelta>())
```

### Error Correction

Stop when all errors are fixed:

```csharp
.RepeatUntil(
    condition: state => state.Errors.Count == 0,
    maxIterations: 3,
    body: flow => flow
        .Then<Validate>()
        .Then<FixErrors>())
```

### Test-Driven Development

Stop when tests pass:

```csharp
.RepeatUntil(
    condition: state => state.AllTestsPassing,
    maxIterations: 5,
    body: flow => flow
        .Then<GenerateCode>()
        .Then<RunTests>()
        .Then<AnalyzeFailures>()
        .Then<RefineCode>())
```

## Step 3: Implement the Steps

### GenerateDraft

Creates the initial content:

```csharp
public class GenerateDraft : IWorkflowStep<RefinementState>
{
    private readonly IContentGenerator _generator;

    public GenerateDraft(IContentGenerator generator)
    {
        _generator = generator;
    }

    public async Task<StepResult<RefinementState>> ExecuteAsync(
        RefinementState state,
        StepContext context,
        CancellationToken ct)
    {
        var draft = await _generator.GenerateAsync(state.Topic, ct);

        return state
            .With(s => s.CurrentDraft, draft)
            .With(s => s.IterationCount, 0)
            .AsResult();
    }
}
```

### Critique

Evaluates the current draft and assigns a score:

```csharp
public class Critique : IWorkflowStep<RefinementState>
{
    private readonly IContentCritic _critic;

    public Critique(IContentCritic critic)
    {
        _critic = critic;
    }

    public async Task<StepResult<RefinementState>> ExecuteAsync(
        RefinementState state,
        StepContext context,
        CancellationToken ct)
    {
        var critique = await _critic.CritiqueAsync(state.CurrentDraft!, ct);

        return state
            .With(s => s.QualityScore, critique.Score)
            .With(s => s.CritiqueHistory, state.CritiqueHistory.Add(critique))
            .AsResult();
    }
}
```

### Refine

Improves the draft based on critique suggestions:

```csharp
public class Refine : IWorkflowStep<RefinementState>
{
    private readonly IContentRefiner _refiner;

    public Refine(IContentRefiner refiner)
    {
        _refiner = refiner;
    }

    public async Task<StepResult<RefinementState>> ExecuteAsync(
        RefinementState state,
        StepContext context,
        CancellationToken ct)
    {
        var latestCritique = state.CritiqueHistory[^1];

        var refinedContent = await _refiner.RefineAsync(
            state.CurrentDraft!,
            latestCritique.Suggestions,
            ct);

        var attempt = new RefinementAttempt(
            Iteration: state.IterationCount + 1,
            BeforeContent: state.CurrentDraft!,
            AfterContent: refinedContent,
            ChangesApplied: latestCritique.Suggestions);

        return state
            .With(s => s.CurrentDraft, refinedContent)
            .With(s => s.IterationCount, state.IterationCount + 1)
            .With(s => s.RefinementHistory, state.RefinementHistory.Add(attempt))
            .AsResult();
    }
}
```

### Publish

Outputs the final content:

```csharp
public class Publish : IWorkflowStep<RefinementState>
{
    private readonly IPublisher _publisher;

    public Publish(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task<StepResult<RefinementState>> ExecuteAsync(
        RefinementState state,
        StepContext context,
        CancellationToken ct)
    {
        await _publisher.PublishAsync(state.CurrentDraft!, ct);

        return state
            .With(s => s.FinalContent, state.CurrentDraft)
            .AsResult();
    }
}
```

## Understanding Generated Artifacts

### Phase Enum

Loop steps are prefixed with a loop identifier for uniqueness:

```csharp
public enum IterativeRefinementPhase
{
    NotStarted,
    GenerateDraft,
    Refinement_Critique,      // Loop "Refinement" contains "Critique"
    Refinement_Refine,        // Loop "Refinement" contains "Refine"
    Publish,
    Completed,
    Failed
}
```

### Loop Control Flow

The generated saga handles loop logic automatically:

```csharp
// After Refine step completes
public async Task<object> Handle(
    ExecuteRefinement_RefineCommand command,
    Refine step,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, ct);
    State = RefinementStateReducer.Reduce(State, result.StateUpdate);

    // Check loop exit condition
    if (State.QualityScore >= 0.9m)
    {
        // Exit loop - proceed to Publish
        return new ExecutePublishCommand(WorkflowId);
    }

    if (State.IterationCount >= 5)
    {
        // Max iterations reached - proceed to Publish
        return new ExecutePublishCommand(WorkflowId);
    }

    // Continue loop - back to Critique
    return new ExecuteRefinement_CritiqueCommand(WorkflowId);
}
```

## Tracking Progress with Append Reducers

The `[Append]` attribute accumulates history across iterations:

```csharp
// After 3 iterations, CritiqueHistory contains:
// [Critique1, Critique2, Critique3]

// RefinementHistory contains:
// [Attempt1, Attempt2, Attempt3]
```

This provides a complete audit trail of how content evolved through refinement.

## Nested Loops

Loops can be nested for complex refinement patterns:

```csharp
.RepeatUntil(
    condition: state => state.ChapterCount >= 5,
    maxIterations: 10,
    body: flow => flow
        .Then<GenerateChapter>()
        .RepeatUntil(
            condition: state => state.ChapterQuality >= 0.8m,
            maxIterations: 3,
            body: inner => inner
                .Then<CritiqueChapter>()
                .Then<RefineChapter>()))
```

Phase names preserve the hierarchy:

```csharp
public enum BookWritingPhase
{
    Chapters_GenerateChapter,
    Chapters_ChapterRefinement_CritiqueChapter,
    Chapters_ChapterRefinement_RefineChapter,
    // ...
}
```

## Recovering Mid-Loop

Because each iteration is persisted, workflows can recover mid-loop:

1. **Iteration 1** - Critique and Refine complete, persisted
2. **Iteration 2** - Critique completes, process crashes
3. **On restart** - Workflow resumes at Refine in iteration 2

No work is lost, and the iteration counter accurately reflects progress.

## Key Points

- **`RepeatUntil` continues** until the condition is true OR maximum iterations are reached
- **Loop body can contain multiple steps** executed sequentially each iteration
- **Use `[Append]` reducers** to accumulate history across iterations
- **Phase names include loop prefix** to ensure uniqueness
- **Built-in infinite loop protection** via `maxIterations`
- **Loops can be nested** for complex multi-level refinement
- **Each iteration is persisted**, enabling recovery mid-loop

## Next Steps

You have learned how to iterate until quality thresholds are met. Some workflows need human input:

- [Approvals](./approvals) - Pause workflows for human review and sign-off
- [Agent Selection](./agents) - Choose the best agent for each task dynamically
