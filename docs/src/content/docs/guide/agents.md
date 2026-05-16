---
title: "Agent Selection"
---

# Agent Selection

When multiple AI agents can perform a task, which one should you use? A coding agent might excel at implementation but struggle with analysis. A research agent might be thorough but slow. This tutorial shows you how to use Thompson Sampling for intelligent agent selection that learns from outcomes and improves over time.

## What You Will Learn

Thompson Sampling is a contextual multi-armed bandit algorithm that:

- **Balances exploration vs. exploitation** - Tries different agents while favoring known performers
- **Learns from outcomes** - Updates beliefs after each task execution
- **Adapts to context** - Selects differently based on task category

## How Thompson Sampling Works

Each agent maintains a Beta distribution belief for each task category:

```text
Agent "analyst" for "Analysis" tasks:
  Beta(alpha=15, beta=3)  ->  High success rate, confident

Agent "coder" for "Coding" tasks:
  Beta(alpha=8, beta=2)   ->  Good success rate, less experience
```

When selecting an agent:
1. **Sample** a random value from each agent's Beta distribution for the task category
2. **Select** the agent with the highest sampled value
3. **Execute** the task with the selected agent
4. **Update** the belief based on the outcome (success or failure)

This naturally balances trying new agents (exploration) with using proven agents (exploitation).

## Step 1: Configure Services

Add agent selection to your service registration:

```csharp
services.AddAgentSelection(options => options
    .WithPrior(alpha: 2, beta: 2)  // Uninformative prior
    .WithCategories(
        TaskCategory.Analysis,
        TaskCategory.Coding,
        TaskCategory.Research,
        TaskCategory.Writing));
```

The prior `Beta(2, 2)` represents no initial bias - agents start with equal assumed performance.

## Step 2: Register Agents

Define the available agents:

```csharp
services.AddAgent("analyst", new AgentConfig
{
    Name = "Data Analyst",
    Capabilities = ["data-analysis", "visualization", "statistics"]
});

services.AddAgent("coder", new AgentConfig
{
    Name = "Software Developer",
    Capabilities = ["code-generation", "debugging", "refactoring"]
});

services.AddAgent("researcher", new AgentConfig
{
    Name = "Research Specialist",
    Capabilities = ["literature-review", "synthesis", "citations"]
});
```

## Step 3: Select and Execute

Use the agent selector in your task routing:

```csharp
public class TaskRouter
{
    private readonly IAgentSelector _selector;
    private readonly IAgentRegistry _agents;

    public TaskRouter(IAgentSelector selector, IAgentRegistry agents)
    {
        _selector = selector;
        _agents = agents;
    }

    public async Task<AgentResult> RouteTaskAsync(
        string taskDescription,
        CancellationToken ct)
    {
        // 1. Select agent via Thompson Sampling
        var selection = await _selector.SelectAgentAsync(new AgentSelectionContext
        {
            AvailableAgentIds = ["analyst", "coder", "researcher"],
            TaskDescription = taskDescription
        }, ct);

        // 2. Execute with selected agent
        var agent = _agents.Get(selection.SelectedAgentId);
        var result = await agent.ExecuteAsync(taskDescription, ct);

        // 3. Record outcome for learning
        await _selector.RecordOutcomeAsync(
            selection.SelectedAgentId,
            selection.TaskCategory,
            result.Success
                ? AgentOutcome.Succeeded(result.ConfidenceScore)
                : AgentOutcome.Failed(result.ErrorMessage),
            ct);

        return result;
    }
}
```

## Task Categories

Tasks are classified into categories for context-aware selection. The library includes 7 predefined categories:

| Category | Keywords | Example Tasks |
|----------|----------|---------------|
| Analysis | analyze, examine, evaluate, assess | "Analyze sales trends" |
| Coding | code, implement, debug, refactor | "Implement OAuth flow" |
| Research | research, investigate, explore | "Research competitor pricing" |
| Writing | write, draft, compose, document | "Write API documentation" |
| Data | data, transform, migrate, etl | "Transform CSV to JSON" |
| Integration | integrate, connect, api, webhook | "Connect Stripe API" |
| General | (fallback) | "Help with this task" |

### Custom Category Classification

Override default classification for domain-specific needs:

```csharp
public class DomainFeatureExtractor : ITaskFeatureExtractor
{
    public TaskFeatures Extract(string taskDescription)
    {
        var lower = taskDescription.ToLowerInvariant();

        // Domain-specific classification
        if (lower.Contains("compliance") || lower.Contains("regulation"))
        {
            return new TaskFeatures(
                Category: TaskCategory.Analysis,
                Complexity: TaskComplexity.High,
                MatchedKeywords: ["compliance", "regulation"]);
        }

        if (lower.Contains("migration") || lower.Contains("upgrade"))
        {
            return new TaskFeatures(
                Category: TaskCategory.Data,
                Complexity: TaskComplexity.High,
                MatchedKeywords: ["migration"]);
        }

        // Fall back to default extraction
        return DefaultFeatureExtractor.Extract(taskDescription);
    }
}

// Register custom extractor
services.AddSingleton<ITaskFeatureExtractor, DomainFeatureExtractor>();
```

## Belief Persistence

### Development (In-Memory)

```csharp
services.AddSingleton<IBeliefStore, InMemoryBeliefStore>();
```

Beliefs reset when the application restarts.

### Production (PostgreSQL)

```csharp
services.AddSingleton<IBeliefStore, PostgresBeliefStore>(sp =>
    new PostgresBeliefStore(sp.GetRequiredService<IDocumentSession>()));
```

Beliefs persist across restarts.

### Belief Structure

```csharp
public record AgentBelief(
    string AgentId,
    TaskCategory Category,
    int Alpha,      // Success count + prior
    int Beta,       // Failure count + prior
    int TotalTrials,
    DateTimeOffset LastUpdated);

// Example beliefs after training:
// Agent: analyst
//   Analysis: Beta(45, 5)   = 90% success, 50 trials
//   Coding:   Beta(3, 7)    = 30% success, 10 trials
//   Research: Beta(20, 4)   = 83% success, 24 trials
```

## Recording Outcomes

After task execution, record the outcome to update beliefs:

```csharp
// Success with confidence score
await selector.RecordOutcomeAsync(
    agentId: "analyst",
    category: TaskCategory.Analysis,
    outcome: AgentOutcome.Succeeded(confidenceScore: 0.92),
    ct);

// Failure with reason
await selector.RecordOutcomeAsync(
    agentId: "coder",
    category: TaskCategory.Coding,
    outcome: AgentOutcome.Failed("Syntax errors in generated code"),
    ct);

// Partial success
await selector.RecordOutcomeAsync(
    agentId: "researcher",
    category: TaskCategory.Research,
    outcome: AgentOutcome.Partial(completionRate: 0.7),
    ct);
```

## Integration with Workflows

Use agent selection within workflow steps:

```csharp
public class DelegateToAgent : IWorkflowStep<TaskState>
{
    private readonly IAgentSelector _selector;
    private readonly IAgentRegistry _agents;

    public DelegateToAgent(
        IAgentSelector selector,
        IAgentRegistry agents)
    {
        _selector = selector;
        _agents = agents;
    }

    public async Task<StepResult<TaskState>> ExecuteAsync(
        TaskState state,
        StepContext context,
        CancellationToken ct)
    {
        // Select best agent for the task
        var selection = await _selector.SelectAgentAsync(new AgentSelectionContext
        {
            AvailableAgentIds = state.AvailableAgents,
            TaskDescription = state.CurrentTask.Description
        }, ct);

        // Execute task
        var agent = _agents.Get(selection.SelectedAgentId);
        var result = await agent.ExecuteAsync(state.CurrentTask, ct);

        // Record outcome for learning
        await _selector.RecordOutcomeAsync(
            selection.SelectedAgentId,
            selection.TaskCategory,
            result.ToOutcome(),
            ct);

        return state
            .With(s => s.SelectedAgent, selection.SelectedAgentId)
            .With(s => s.TaskResult, result)
            .AsResult();
    }
}
```

## Monitoring Performance

Query agent performance across categories:

```csharp
var performance = await beliefStore.GetPerformanceReportAsync(ct);

// Output:
// Agent: analyst
//   Analysis: 90.0% (45/50 trials)
//   Research: 83.3% (20/24 trials)
//   Coding:   30.0% (3/10 trials)
//
// Agent: coder
//   Coding:   88.0% (44/50 trials)
//   Analysis: 45.0% (9/20 trials)
```

Use this data to:
- Identify agent strengths and weaknesses
- Decide when to add new agents
- Monitor for performance degradation

## The Selection Algorithm

For reference, here is how Thompson Sampling selects agents:

```csharp
public class ThompsonSamplingSelector : IAgentSelector
{
    public async Task<AgentSelection> SelectAgentAsync(
        AgentSelectionContext context,
        CancellationToken ct)
    {
        // 1. Classify task
        var features = _featureExtractor.Extract(context.TaskDescription);

        // 2. Get beliefs for all available agents
        var beliefs = await _beliefStore.GetBeliefsAsync(
            context.AvailableAgentIds,
            features.Category,
            ct);

        // 3. Sample from each agent's Beta distribution
        var samples = beliefs.Select(b => new
        {
            b.AgentId,
            Sample = SampleBeta(b.Alpha, b.Beta)
        });

        // 4. Select agent with highest sample
        var selected = samples.MaxBy(s => s.Sample)!;

        return new AgentSelection(
            SelectedAgentId: selected.AgentId,
            TaskCategory: features.Category,
            SampledValue: selected.Sample,
            Features: features);
    }

    private double SampleBeta(int alpha, int beta)
    {
        return BetaDistribution.Sample(_random, alpha, beta);
    }
}
```

The random sampling from Beta distributions means:
- **Well-performing agents** (high alpha, low beta) sample high values often
- **Uncertain agents** (low trials) have high variance and get explored
- **Poor performers** (low alpha, high beta) sample low values but occasionally get a chance

## Key Points

- **Thompson Sampling automatically balances** exploration and exploitation
- **Beliefs update after each task** - the system learns continuously
- **Prior `Beta(2, 2)` provides an uninformative start** - no initial bias
- **Task classification happens via keyword extraction** or custom extractors
- **Beliefs persist across restarts** with PostgreSQL storage
- **Performance improves** as more tasks are executed
- **Works with any number of agents** and task categories

## Next Steps

You have completed the Guide tutorials. You now know how to:

- Install and configure Strategos
- Build sequential, branching, parallel, and iterative workflows
- Incorporate human approvals
- Use intelligent agent selection

For deeper understanding, explore:

- [Core Concepts](/learn/core-concepts) - Theoretical foundations
- [API Reference](/reference/api/workflow) - Complete API documentation
- [Examples](/examples/) - Full application examples
