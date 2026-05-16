---
title: "Iterative Refinement: Quality-Driven Loops"
---

# Iterative Refinement: Quality-Driven Loops

## The Problem: AI Gets It Wrong on the First Try

You've asked an LLM to generate a report, article, or code. The output is... okay. Not great. It misses nuances, uses awkward phrasing, or contains subtle errors.

**The naive approach**: Prompt again with "please improve it." Problems:
- The AI doesn't know WHAT needs improvement (just that something does)
- Each attempt is independent—no learning from previous feedback
- You waste tokens on directionless iteration
- Eventually you give up or run out of budget

**What you need**: A structured refinement loop that:
1. Objectively measures quality (not just "try again")
2. Identifies specific weaknesses to address
3. Accumulates history so you can debug what was tried
4. Has a hard limit to prevent infinite loops
5. Exits automatically when quality is achieved

This is the **RepeatUntil** pattern—condition-based loops with quality-driven termination.

---

## Learning Objectives

After this example, you will understand:

- **RepeatUntil loops** with condition-based termination
- **Quality thresholds** as exit criteria
- **The `[Append]` attribute** for accumulating history across iterations
- **Why loops need bounds** (maxIterations as a circuit breaker)
- **State design** that supports iterative refinement

---

## Conceptual Foundation

### Iterative Refinement vs. Random Retry

The difference between effective refinement and pointless retry:

| Approach | How It Works | Result |
|----------|--------------|--------|
| **Random Retry** | "Try again" with no context | Same mistakes repeated |
| **Iterative Refinement** | Feed failure details into next attempt | Each attempt is informed by previous weaknesses |

The key is **what you do with feedback**:

```text
Random Retry:
  Attempt 1 → "Not good" → "Try again" → Attempt 2 → Same problems

Iterative Refinement:
  Attempt 1 → "Weak intro, unclear conclusion" → "Fix: strengthen intro, clarify conclusion" → Attempt 2 → Better
```

### Why Loops Need Bounds

An unbounded refinement loop is a bug waiting to happen:

```text
Loop forever until quality >= 0.9:
  - What if the topic is inherently difficult?
  - What if the quality metric is flawed?
  - What if the AI is stuck in a local minimum?
  - What if you run out of tokens/budget?
```

**The solution**: `maxIterations` as a circuit breaker. After N attempts, accept the best result or escalate to a human rather than burning resources indefinitely.

### The [Append] Attribute: Why Accumulate History?

Consider two approaches to tracking refinement attempts:

**Replace (default)**:
```csharp
public CritiqueResult? LatestCritique { get; init; }  // Only keeps last
```

**Append**:
```csharp
[Append]
public ImmutableList<CritiqueResult> CritiqueHistory { get; init; } = [];  // Keeps all
```

Why append?

1. **Debugging**: See what was tried before the final version
2. **Audit trail**: Compliance may require knowing the refinement journey
3. **Feedback loop**: Next attempt can see what improvements were already attempted
4. **Learning**: Patterns emerge across many workflows

### Quality Metrics: What Makes a Good Exit Condition?

Your exit condition determines when refinement stops. Design it carefully:

| Condition Type | Example | Strength | Weakness |
|----------------|---------|----------|----------|
| **Threshold** | `QualityScore >= 0.9` | Clear target | May be unreachable |
| **Convergence** | `ImprovementDelta < 0.01` | Detects stagnation | Might exit too early |
| **Error count** | `Errors.Count == 0` | Objective | May be impossible |
| **Combined** | `Score >= 0.85 \|\| Iterations >= 3` | Practical | More complex |

---

## Design Decisions

| Decision | Why This Approach | Alternative | Trade-off |
|----------|-------------------|-------------|-----------|
| **RepeatUntil** | Condition-based exit | Fixed iteration count | More flexible but needs good condition |
| **maxIterations: 5** | Typical improvement plateaus | 3, 10, unlimited | More attempts = more cost, diminishing returns |
| **[Append] on history** | Need full audit trail | Replace with latest | Memory usage, but essential for debugging |
| **Separate Critique and Refine** | Single responsibility | Combined step | More steps, but clearer logic |

### When to Use This Pattern

**Good fit when**:
- Output quality can be objectively measured (scores, error counts, tests)
- Feedback provides actionable information (not just "wrong")
- Multiple attempts typically improve results
- You have budget for iteration

**Poor fit when**:
- No objective quality measure exists
- Feedback doesn't inform improvements
- First attempt is typically good enough
- Budget is extremely limited

### Anti-Patterns to Avoid

| Anti-Pattern | Problem | Correct Approach |
|--------------|---------|------------------|
| **No maxIterations** | Infinite loops burn budget | Always set `maxIterations` |
| **Vague condition** | Loop never exits | Use measurable threshold |
| **Replace instead of Append** | Lose audit trail | Use `[Append]` for history |
| **Critique without suggestions** | No actionable feedback | Critique must include improvement directions |
| **Too high threshold** | May be unreachable | Set realistic quality targets |

---

## Building the Workflow

### The Shape First

```text
┌───────────────┐    ┌────────────────────────────────────────────────┐    ┌─────────┐
│ GenerateDraft │───▶│            Refinement Loop                     │───▶│ Publish │
│               │    │  ┌──────────┐    ┌────────┐                    │    │         │
│ Initial       │    │  │ Critique │───▶│ Refine │                    │    │ Final   │
│ content       │    │  └──────────┘    └────────┘                    │    │ output  │
│               │    │        ▲              │                        │    │         │
│               │    │        └──────────────┘                        │    │         │
│               │    │   (until QualityScore >= 0.9 OR 5 iterations)  │    │         │
└───────────────┘    └────────────────────────────────────────────────┘    └─────────┘
```

### State: What We Track

```csharp
[WorkflowState]
public record RefinementState : IWorkflowState
{
    // Identity
    public Guid WorkflowId { get; init; }

    // Input
    public string Topic { get; init; } = string.Empty;

    // Current content (replaced each iteration)
    public string? CurrentDraft { get; init; }

    // Quality measurement (replaced each iteration)
    public decimal QualityScore { get; init; }

    // Iteration counter
    public int IterationCount { get; init; }

    // Full critique history (accumulates via [Append])
    [Append]
    public ImmutableList<CritiqueResult> CritiqueHistory { get; init; } = [];

    // Full refinement history (accumulates via [Append])
    [Append]
    public ImmutableList<RefinementAttempt> RefinementHistory { get; init; } = [];

    // Final output
    public string? FinalContent { get; init; }
}
```

**Why this design?**

- `CurrentDraft`: Replaced each iteration—we only need the latest version for refinement
- `QualityScore`: Replaced each iteration—used for exit condition
- `CritiqueHistory`: Accumulates via `[Append]`—we need the full journey
- `RefinementHistory`: Accumulates via `[Append]`—audit trail of all changes

### The Supporting Records

```csharp
public record CritiqueResult(
    decimal Score,                      // Quality measurement (0.0-1.0)
    IReadOnlyList<string> Strengths,    // What's working
    IReadOnlyList<string> Weaknesses,   // What needs improvement
    IReadOnlyList<string> Suggestions); // Actionable fixes

public record RefinementAttempt(
    int Iteration,                      // Which attempt
    string BeforeContent,               // Input to this iteration
    string AfterContent,                // Output of this iteration
    IReadOnlyList<string> ChangesApplied); // What was changed
```

### The Workflow Definition

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

**Reading this definition**:
1. Generate an initial draft from the topic
2. Loop: critique → refine (until quality >= 0.9 OR 5 iterations)
3. Publish the final version

### Loop Patterns

Different termination strategies for different use cases:

**Quality Threshold**:
```csharp
.RepeatUntil(
    condition: state => state.QualityScore >= 0.9m,
    maxIterations: 10,
    body: flow => flow
        .Then<Evaluate>()
        .Then<Improve>())
```

**Convergence Detection** (stop when improvement stalls):
```csharp
.RepeatUntil(
    condition: state => state.ImprovementDelta < 0.01m,
    maxIterations: 20,
    body: flow => flow
        .Then<Iterate>()
        .Then<MeasureDelta>())
```

**Error Correction**:
```csharp
.RepeatUntil(
    condition: state => state.Errors.Count == 0,
    maxIterations: 3,
    body: flow => flow
        .Then<Validate>()
        .Then<FixErrors>())
```

### The Key Step: Critique

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
        // Get objective quality assessment with actionable suggestions
        var critique = await _critic.CritiqueAsync(state.CurrentDraft!, ct);

        // [Append] attribute means this adds to existing list
        return state
            .With(s => s.QualityScore, critique.Score)
            .With(s => s.CritiqueHistory, state.CritiqueHistory.Add(critique))
            .AsResult();
    }
}
```

**The feedback loop**: The critique provides ACTIONABLE suggestions. This is what makes it iterative refinement rather than random retry.

### The Refine Step

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
        // Use the latest critique's suggestions to guide refinement
        var latestCritique = state.CritiqueHistory[^1];

        var refinedContent = await _refiner.RefineAsync(
            state.CurrentDraft!,
            latestCritique.Suggestions,  // Feed suggestions forward
            ct);

        // Record what was done for audit trail
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

---

## Loop Control Flow

The generated saga handles the loop logic automatically:

```csharp
// After Refine step completes
public async Task<object> Handle(
    ExecuteRefinement_RefineCommand command,
    Refine step,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, ct);
    State = RefinementStateReducer.Reduce(State, result.StateUpdate);

    // Check loop condition
    if (State.QualityScore >= 0.9m)
    {
        // Exit loop - proceed to Publish
        return new ExecutePublishCommand(WorkflowId);
    }

    if (State.IterationCount >= 5)
    {
        // Max iterations reached - accept current quality
        return new ExecutePublishCommand(WorkflowId);
    }

    // Continue loop - back to Critique
    return new ExecuteRefinement_CritiqueCommand(WorkflowId);
}
```

---

## The "Aha Moment"

> **The difference between "retry on failure" and "iterative refinement" is what you do with the failure information.**
>
> A retry says "try again." Iterative refinement says "try again, and here's exactly what went wrong last time."
>
> The `[Append]` attribute on history collections isn't just for logging—it's the memory that enables learning across iterations. Each attempt can see the full history of what's been tried, preventing the same mistakes from being repeated.

---

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

---

## Extension Exercises

### Exercise 1: Add Convergence Detection

Modify the loop to exit when improvement stalls:

1. Add `PreviousQualityScore` to state
2. Calculate improvement delta after each critique
3. Add condition: `ImprovementDelta < 0.01m`
4. Combine with threshold: exit if converged OR quality met

### Exercise 2: Track Best Version

Sometimes iteration makes things worse. Keep the best version:

1. Add `BestDraft` and `BestScore` to state
2. After each critique, compare to best
3. Update best if current is better
4. In Publish, use best version (not latest)

### Exercise 3: Escalate on Max Iterations

When quality target isn't met after max iterations:

1. Add `RequiresHumanReview` flag to state
2. After loop, check if `QualityScore < 0.9`
3. If so, route to human review step instead of publish
4. Record why escalation happened

---

## Key Takeaways

1. **RepeatUntil continues until condition is true OR max iterations reached**
2. **[Append] reducers accumulate history** for debugging and audit
3. **Critique must provide actionable suggestions**—generic feedback doesn't help
4. **maxIterations is a circuit breaker**—prevents infinite loops and budget overruns
5. **Each iteration should use feedback from the previous**—this is what makes it "iterative"
6. **Phase names include loop prefix** for uniqueness in nested scenarios

---

## Related

- [AgenticCoder Sample](../../samples/AgenticCoder/) - Working implementation of iterative refinement
- [Approval Flow Pattern](/strategos/examples/approval-flow/) - Human checkpoints within loops
- [Basic Workflow](/strategos/examples/basic-workflow/) - Sequential steps without loops
