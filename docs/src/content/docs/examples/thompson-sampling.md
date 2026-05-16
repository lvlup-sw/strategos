---
title: "Thompson Sampling: Intelligent Agent Selection"
---

# Thompson Sampling: Intelligent Agent Selection

## The Problem: How Do You Pick the Right Agent?

You have multiple AI agents with different strengths. A data analyst agent, a coder agent, a research agent. When a task arrives, which one should handle it?

**Naive approaches fail**:
- **Random selection**: Wastes good agents on bad-fit tasks
- **Hard-coded rules**: Can't adapt when performance changes
- **Always use the best**: Never discovers if other agents improved

**What you need**: A system that:
1. Learns which agent performs best for each task type
2. Tries uncertain agents occasionally (they might be great)
3. Exploits proven performers most of the time
4. Adapts automatically as performance changes

This is the **multi-armed bandit problem**, and Thompson Sampling is the solution.

---

## Learning Objectives

After this example, you will understand:

- **The exploration-exploitation dilemma** and why it matters
- **Beta distributions** as representations of uncertainty
- **Thompson Sampling** as "optimistic under uncertainty"
- **Belief updating** based on outcomes
- **Task categorization** for specialized routing

---

## Conceptual Foundation

### The Multi-Armed Bandit Problem

Imagine slot machines ("one-armed bandits") with unknown payout rates. You want to maximize winnings with limited pulls.

| Strategy | Approach | Problem |
|----------|----------|---------|
| **Explore only** | Try each equally | Waste pulls on bad machines |
| **Exploit only** | Always use current best | Miss better machines |
| **Balanced** | Explore uncertain, exploit proven | Thompson Sampling! |

Agent selection is identical:
- **Machines** = AI agents
- **Pulls** = Task assignments
- **Payouts** = Successful completions

### Why Thompson Sampling Beats Alternatives

| Algorithm | Mechanism | Weakness |
|-----------|-----------|----------|
| **Epsilon-Greedy** | Random 10% of the time | Explores bad options unnecessarily |
| **UCB** | Upper confidence bound | Deterministic, exploitable |
| **Thompson Sampling** | Sample from belief distributions | ✓ Naturally balanced |

**Thompson Sampling wins because**:
- Uncertain agents get more chances (higher variance in samples)
- Proven agents dominate (tight distributions around true rate)
- Exploration decreases automatically as beliefs converge
- No tuning parameters required

### Beta Distributions: Representing Uncertainty

A Beta distribution represents belief about a probability:

```text
Beta(α, β) where:
  α = successes + prior
  β = failures + prior
  Mean = α / (α + β)
  Variance decreases as α + β increases
```

**Visual intuition**:

```text
Beta(2, 2)  - Flat, uncertain     ████████████████
Beta(5, 5)  - Moderate certainty      ████████
Beta(20, 5) - High confidence           ██████ (peaked around 0.8)
```

When you **sample** from Beta(5, 5), you might get anything from 0.2 to 0.8.
When you **sample** from Beta(50, 10), you'll almost always get ~0.83.

This is the magic: **uncertain agents occasionally win the lottery**.

### The Selection Algorithm

```text
For each task:
1. Classify task → category (Coding, Analysis, Research, etc.)
2. For each available agent:
   a. Get belief Beta(α, β) for (agent, category)
   b. Sample θ ~ Beta(α, β)
3. Select agent with highest sampled θ
4. Execute task, observe outcome
5. Update belief: success → α += 1, failure → β += 1
```

---

## Design Decisions

| Decision | Why | Alternative | Trade-off |
|----------|-----|-------------|-----------|
| **Beta distribution** | Conjugate prior for binary outcomes | Gaussian | Beta is mathematically elegant for success/failure |
| **Per-category beliefs** | Agents specialize | Single belief per agent | More parameters, but better routing |
| **Prior α=2, β=2** | Uninformative, slight regularization | α=1, β=1 (uniform) | Prevents extreme early beliefs |
| **Keyword classification** | Simple, interpretable | LLM-based | Fast, no external calls |

### When to Use This Pattern

**Good fit**:
- Multiple agents with overlapping capabilities
- Performance varies by task type
- Feedback is available (success/failure)
- Task volume is sufficient for learning (100+ per category)

**Poor fit**:
- Single agent (nothing to select)
- No feedback mechanism
- Highly variable task types (no stable categories)
- Need deterministic selection (audit requirements)

### Anti-Patterns

| Anti-Pattern | Problem | Fix |
|--------------|---------|-----|
| **Ignoring categories** | Agents conflated across task types | Use per-category beliefs |
| **No outcome recording** | Beliefs never improve | Always call `RecordOutcomeAsync` |
| **Cold start panic** | Expecting good results immediately | Set realistic prior, allow learning period |
| **Too many categories** | Sparse data per category | Consolidate similar categories |

---

## Implementation

### Setup

```csharp
// Configure agent selection
services.AddAgentSelection(options => options
    .WithPrior(alpha: 2, beta: 2)  // Uninformative prior
    .WithCategories(
        TaskCategory.Analysis,
        TaskCategory.Coding,
        TaskCategory.Research,
        TaskCategory.Writing));

// Register agents
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
```

### Selection Flow

```csharp
public class TaskRouter
{
    private readonly IAgentSelector _selector;
    private readonly IAgentRegistry _agents;

    public async Task<AgentResult> RouteTaskAsync(
        string taskDescription,
        CancellationToken ct)
    {
        // 1. Select agent via Thompson Sampling
        var selection = await _selector.SelectAgentAsync(
            new AgentSelectionContext
            {
                AvailableAgentIds = ["analyst", "coder", "researcher"],
                TaskDescription = taskDescription
            }, ct);

        // 2. Execute with selected agent
        var agent = _agents.Get(selection.SelectedAgentId);
        var result = await agent.ExecuteAsync(taskDescription, ct);

        // 3. Record outcome for learning (CRITICAL!)
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

### Task Categories

Built-in categories with keyword detection:

| Category | Keywords | Example |
|----------|----------|---------|
| Analysis | analyze, examine, evaluate | "Analyze sales trends" |
| Coding | code, implement, debug | "Implement OAuth flow" |
| Research | research, investigate | "Research competitor pricing" |
| Writing | write, draft, compose | "Write API documentation" |
| Data | data, transform, migrate | "Transform CSV to JSON" |
| Integration | integrate, connect, api | "Connect Stripe API" |
| General | (fallback) | "Help with this task" |

### Custom Classification

```csharp
public class DomainFeatureExtractor : ITaskFeatureExtractor
{
    public TaskFeatures Extract(string taskDescription)
    {
        var lower = taskDescription.ToLowerInvariant();

        // Domain-specific rules
        if (lower.Contains("compliance") || lower.Contains("regulation"))
        {
            return new TaskFeatures(
                Category: TaskCategory.Analysis,
                Complexity: TaskComplexity.High,
                MatchedKeywords: ["compliance"]);
        }

        // Fall back to default
        return DefaultFeatureExtractor.Extract(taskDescription);
    }
}

services.AddSingleton<ITaskFeatureExtractor, DomainFeatureExtractor>();
```

### Belief Persistence

```csharp
// Development: In-memory (resets on restart)
services.AddSingleton<IBeliefStore, InMemoryBeliefStore>();

// Production: PostgreSQL (persists across restarts)
services.AddSingleton<IBeliefStore, PostgresBeliefStore>();
```

### The Core Algorithm

```csharp
public class ThompsonSamplingSelector : IAgentSelector
{
    public async Task<AgentSelection> SelectAgentAsync(
        AgentSelectionContext context,
        CancellationToken ct)
    {
        // 1. Classify task
        var features = _featureExtractor.Extract(context.TaskDescription);

        // 2. Get beliefs for all agents
        var beliefs = await _beliefStore.GetBeliefsAsync(
            context.AvailableAgentIds,
            features.Category,
            ct);

        // 3. Sample from each agent's Beta distribution
        var samples = beliefs.Select(b => new
        {
            b.AgentId,
            Sample = SampleBeta(b.Alpha, b.Beta)  // THE KEY STEP
        });

        // 4. Select highest sample (not highest mean!)
        var selected = samples.MaxBy(s => s.Sample)!;

        return new AgentSelection(
            SelectedAgentId: selected.AgentId,
            TaskCategory: features.Category,
            SampledValue: selected.Sample);
    }

    private double SampleBeta(int alpha, int beta)
    {
        return BetaDistribution.Sample(_random, alpha, beta);
    }
}
```

### Recording Outcomes

```csharp
// Success
await selector.RecordOutcomeAsync(
    agentId: "analyst",
    category: TaskCategory.Analysis,
    outcome: AgentOutcome.Succeeded(confidenceScore: 0.92),
    ct);

// Failure
await selector.RecordOutcomeAsync(
    agentId: "coder",
    category: TaskCategory.Coding,
    outcome: AgentOutcome.Failed("Syntax errors in generated code"),
    ct);

// Partial success (weighted)
await selector.RecordOutcomeAsync(
    agentId: "researcher",
    category: TaskCategory.Research,
    outcome: AgentOutcome.Partial(completionRate: 0.7),
    ct);
```

### Workflow Integration

```csharp
public class DelegateToAgent : IWorkflowStep<TaskState>
{
    public async Task<StepResult<TaskState>> ExecuteAsync(
        TaskState state,
        StepContext context,
        CancellationToken ct)
    {
        // Select best agent
        var selection = await _selector.SelectAgentAsync(
            new AgentSelectionContext
            {
                AvailableAgentIds = state.AvailableAgents,
                TaskDescription = state.CurrentTask.Description
            }, ct);

        // Execute
        var agent = _agents.Get(selection.SelectedAgentId);
        var result = await agent.ExecuteAsync(state.CurrentTask, ct);

        // Learn from outcome
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

---

## The "Aha Moment"

> **Uncertainty is opportunity, not risk.**
>
> An agent with 2 successes and 0 failures has a 100% success rate—but only 2 data points. An agent with 80 successes and 20 failures has an 80% rate with 100 data points.
>
> Which is actually better? We don't know! Thompson Sampling acknowledges this by letting the uncertain agent occasionally win the selection lottery. If it performs well, we learn something valuable. If it fails, we've only lost one task.

---

## Monitoring

```csharp
// Get performance report
var report = await beliefStore.GetPerformanceReportAsync(ct);

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

Watch for:
- **Low trial counts**: Beliefs haven't converged yet
- **Cross-category performance**: Agents may surprise you
- **Belief drift**: Performance changes over time

---

## Key Takeaways

1. **Thompson Sampling balances exploration and exploitation automatically**
2. **Beta distributions represent uncertainty**—wider = less certain
3. **Sampling, not averaging**—uncertain agents get lucky sometimes
4. **Per-category beliefs** enable specialization
5. **Outcome recording is essential**—no feedback = no learning
6. **Performance improves over time**—patience during cold start

---

## Related

- [MultiModelRouter Sample](../../samples/MultiModelRouter/) - Working implementation
- [Multi-Armed Bandits (Wikipedia)](https://en.wikipedia.org/wiki/Multi-armed_bandit) - Mathematical background
- [Thompson Sampling Tutorial](https://web.stanford.edu/~bvr/pubs/TS_Tutorial.pdf) - Academic deep dive
