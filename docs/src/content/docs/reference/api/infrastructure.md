---
title: "Infrastructure Types"
---

# Infrastructure Types

The `Strategos.Infrastructure` package provides production-ready implementations of core abstractions.

## Thompson Sampling

Adaptive agent selection using contextual multi-armed bandits.

### ContextualAgentSelector

Selects the best agent for a task using Thompson Sampling with learned beliefs.

#### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `SelectAgentAsync` | `string taskDescription`, `CancellationToken ct` | `Task<AgentId>` | Selects best agent for task |
| `RecordOutcomeAsync` | `AgentId agent`, `TaskCategory category`, `bool success`, `CancellationToken ct` | `Task` | Updates beliefs based on outcome |

#### Example

```csharp
public class AssignAgentStep : IWorkflowStep<TaskState>
{
    private readonly IAgentSelector _selector;

    public async Task<StepResult<TaskState>> ExecuteAsync(
        TaskState state,
        StepContext context,
        CancellationToken ct)
    {
        var agent = await _selector.SelectAgentAsync(state.TaskDescription, ct);
        return state.With(s => s.AssignedAgent, agent).AsResult();
    }
}
```

---

### AgentBelief

Represents performance belief as a Beta distribution.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Alpha` | `double` | Success count + prior |
| `Beta` | `double` | Failure count + prior |
| `Mean` | `double` | Expected success rate (alpha / (alpha + beta)) |
| `Variance` | `double` | Uncertainty in estimate |

#### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `Sample` | `Random random` | `double` | Draws sample from distribution |
| `Update` | `bool success` | `AgentBelief` | Returns updated belief |

#### Example

```csharp
// Initial belief with uniform prior
var belief = new AgentBelief(alpha: 1, beta: 1);

// After 7 successes and 3 failures
belief = belief.Update(success: true);  // Repeat for each outcome
// belief.Mean ~= 0.727
```

---

### TaskCategory

Enumeration of task categories for contextual selection.

| Value | Description |
|-------|-------------|
| `Analysis` | Data analysis and interpretation |
| `Coding` | Code generation and modification |
| `Research` | Information gathering and synthesis |
| `Writing` | Content creation and editing |
| `Planning` | Strategy and roadmap development |
| `Review` | Evaluation and feedback |
| `Unknown` | Unclassified task type |

---

### IBeliefStore

Interface for persisting agent performance beliefs.

#### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `GetBeliefAsync` | `AgentId agent`, `TaskCategory category` | `Task<AgentBelief>` | Gets belief for agent/category |
| `SetBeliefAsync` | `AgentId agent`, `TaskCategory category`, `AgentBelief belief` | `Task` | Persists updated belief |

#### Implementations

| Type | Description |
|------|-------------|
| `InMemoryBeliefStore` | In-memory storage (dev/testing) |

---

## Loop Detection

Detects stuck workflows using multiple strategies.

### ExactRepetitionDetector

Detects identical action sequences.

#### Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `WindowSize` | `int` | 10 | Number of recent actions to analyze |
| `Threshold` | `int` | 3 | Repetitions to trigger detection |

#### Example

```csharp
services.AddLoopDetection(options => options
    .AddExactRepetition(config => config
        .WithWindowSize(10)
        .WithThreshold(3)));
```

---

### SemanticRepetitionDetector

Detects similar outputs using cosine similarity.

#### Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SimilarityThreshold` | `double` | 0.95 | Minimum similarity to consider repetition |
| `WindowSize` | `int` | 5 | Number of recent outputs to compare |

#### Example

```csharp
services.AddLoopDetection(options => options
    .AddSemanticRepetition(config => config
        .WithSimilarityThreshold(0.95)
        .WithWindowSize(5)));
```

---

### OscillationDetector

Detects A-B-A-B patterns.

#### Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PatternLength` | `int` | 2 | Length of oscillation pattern |
| `Repetitions` | `int` | 2 | Times pattern must repeat |

#### Example

```csharp
services.AddLoopDetection(options => options
    .AddOscillation(config => config
        .WithPatternLength(2)
        .WithRepetitions(2)));
```

---

### NoProgressDetector

Detects activity without meaningful state change.

#### Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `StepThreshold` | `int` | 5 | Steps without progress to trigger |
| `ProgressEvaluator` | `Func<TState, TState, bool>` | - | Custom progress evaluation |

#### Example

```csharp
services.AddLoopDetection(options => options
    .AddNoProgress(config => config
        .WithStepThreshold(5)
        .WithProgressEvaluator((prev, curr) =>
            prev.ProcessedCount != curr.ProcessedCount)));
```

---

## Budget Guard

Enforces resource limits on workflow execution.

### BudgetGuard

Main type for budget enforcement.

#### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CheckBudgetAsync` | `StepContext context` | `Task<BudgetStatus>` | Checks current budget status |
| `RecordUsageAsync` | `ResourceUsage usage` | `Task` | Records resource consumption |

---

### BudgetOptions

Configuration for budget thresholds.

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxSteps` | `int` | 100 | Maximum step executions |
| `MaxTokens` | `int` | 50,000 | Maximum LLM tokens |
| `MaxWallTime` | `TimeSpan` | 30 minutes | Maximum elapsed time |
| `ScarceThreshold` | `double` | 0.75 | Percentage to enter Scarce state |
| `CriticalThreshold` | `double` | 0.90 | Percentage to enter Critical state |

#### Example

```csharp
services.AddBudgetGuard(options => options
    .WithMaxSteps(100)
    .WithMaxTokens(50_000)
    .WithMaxWallTime(TimeSpan.FromMinutes(30))
    .WithScarceThreshold(0.75)
    .WithCriticalThreshold(0.90));
```

---

### ScarcityLevel

Budget consumption state enumeration.

| Value | Description | Typical Response |
|-------|-------------|------------------|
| `Abundant` | < 50% consumed | Normal operation |
| `Normal` | 50-75% consumed | Monitor usage |
| `Scarce` | 75-90% consumed | Reduce exploration |
| `Critical` | > 90% consumed | Minimal operations only |

---

### BudgetStatus

Current budget state returned from checks.

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Level` | `ScarcityLevel` | Current scarcity level |
| `StepsRemaining` | `int` | Steps until limit |
| `TokensRemaining` | `int` | Tokens until limit |
| `TimeRemaining` | `TimeSpan` | Time until limit |
| `IsExhausted` | `bool` | Whether any budget is exhausted |

---

## Registration

Complete infrastructure setup:

```csharp
services.AddStrategos()
    .AddThompsonSampling(options => options
        .WithPrior(alpha: 2, beta: 2)
        .WithBeliefStore<InMemoryBeliefStore>())
    .AddLoopDetection(options => options
        .AddExactRepetition()
        .AddSemanticRepetition()
        .AddOscillation()
        .AddNoProgress())
    .AddBudgetGuard(options => options
        .WithMaxSteps(100)
        .WithMaxTokens(50_000)
        .WithMaxWallTime(TimeSpan.FromMinutes(30)));
```
