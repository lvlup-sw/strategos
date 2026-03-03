# Strategos.Infrastructure

Infrastructure implementations for Strategos including Thompson Sampling agent selection, loop detection, and budget enforcement.

## Installation

```bash
dotnet add package LevelUp.Strategos.Infrastructure
```

## Features

### Thompson Sampling Agent Selection

Contextual bandit algorithm for intelligent agent selection based on historical performance.

```csharp
services.AddStrategosInfrastructure(options =>
{
    options.ThompsonSampling.ExplorationWeight = 0.3;
    options.ThompsonSampling.MinSamples = 10;
});

// In your step
var agent = await agentSelector.SelectAgentAsync(
    availableAgents,
    taskContext,
    cancellationToken);
```

### Loop Detection

Detects stuck workflows using multiple strategies:

- **Exact Repetition**: Identical action sequences in sliding window
- **Semantic Similarity**: Similar outputs (cosine similarity > 0.85)
- **Oscillation**: A-B-A-B patterns
- **No Progress**: Activity without observable state change

```csharp
var loopResult = await loopDetector.DetectLoopAsync(
    workflowHistory,
    cancellationToken);

if (loopResult.IsLooping)
{
    // Apply recovery strategy
    var recovery = loopResult.SuggestedRecovery;
}
```

### Budget Guard

Enforces resource limits with scarcity-aware scoring:

```csharp
var budget = new WorkflowBudget
{
    MaxSteps = 50,
    MaxTokens = 100_000,
    MaxWallTime = TimeSpan.FromMinutes(30)
};

var canProceed = await budgetGuard.CheckBudgetAsync(
    currentUsage,
    budget,
    cancellationToken);
```

**Scarcity Levels:**
- **Abundant** (>70%): Normal operation
- **Normal** (30-70%): Slight penalty on expensive actions
- **Scarce** (10-30%): Prioritize efficient actions
- **Critical** (<=10%): Essential operations only

## Configuration

```csharp
services.AddStrategosInfrastructure(options =>
{
    // Thompson Sampling
    options.ThompsonSampling.ExplorationWeight = 0.3;
    options.ThompsonSampling.SuccessWeight = 2.0;

    // Loop Detection
    options.LoopDetection.WindowSize = 10;
    options.LoopDetection.SemanticThreshold = 0.85;

    // Budget
    options.Budget.DefaultScarcityLevel = ScarcityLevel.Normal;
});
```

## Documentation

- **[Infrastructure API Reference](https://lvlup-sw.github.io/strategos/reference/api/infrastructure)** - Complete API documentation
- **[Configuration Guide](https://lvlup-sw.github.io/strategos/reference/configuration)** - Configuration options

## License

MIT
