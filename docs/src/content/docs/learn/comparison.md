---
title: "Framework Comparison"
outline: deep
---

# Framework Comparison

How does Strategos compare to other solutions? This page provides an accurate, detailed comparison to help you choose the right tool.

## Quick Comparison

| Capability | Strategos | LangGraph | MAF Workflows | Temporal |
|------------|:----------------:|:---------:|:-------------:|:--------:|
| .NET native | :white_check_mark: | | :white_check_mark: | :white_check_mark: |
| Python native | | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Durable execution | event-sourced | checkpoints | checkpoints (BSP) | event history |
| Full audit trail | :white_check_mark: | | | :white_check_mark: |
| Compensation handlers | :white_check_mark: DSL | | | :white_check_mark: Saga |
| Thompson Sampling | :white_check_mark: | | | |
| Compile-time validation | :white_check_mark: | | | |
| Human-in-the-loop | :white_check_mark: | :white_check_mark: | :white_check_mark: | :white_check_mark: |
| Visual dashboard | | | :white_check_mark: DTS | :white_check_mark: |
| Cloud-agnostic | :white_check_mark: | :white_check_mark: | | :white_check_mark: |
| Production status | 1.0 | stable | GA | stable |

## Detailed Comparison

### LangGraph

[LangGraph](https://www.langchain.com/langgraph) is part of the LangChain ecosystem, providing a graph-based approach to building agent workflows in Python.

**Strengths:**
- Rich ecosystem of LangChain integrations
- Active community and comprehensive documentation
- Flexible graph-based composition
- Three checkpointing modes for different durability needs

**Technical Details:**

LangGraph offers three checkpointing modes:

| Mode | Behavior | Trade-off |
|------|----------|-----------|
| `exit` | Persist only on completion/error/interrupt | Best performance, no mid-execution recovery |
| `async` | Persist asynchronously while next step runs | Good balance, small crash-during-write risk |
| `sync` | Persist synchronously before each step | Highest durability, performance overhead |

**Limitations:**
- **Python-only** — No native .NET support
- **No compensation/rollback** — Must implement manually in exception handlers
- **No time-travel debugging** — Checkpoints capture state, not decision context
- **Idempotency required** — Workflows must be deterministic for safe replay; side effects must be wrapped in "tasks"
- **No intelligent agent selection** — Manual routing or static selection only

**Definition Style:**
```python
# LangGraph: Graph-based with explicit state schema
from langgraph.graph import StateGraph

builder = StateGraph(AgentState)
builder.add_node("agent", call_model)
builder.add_conditional_edges("agent", should_continue, {
    "continue": "agent",
    "end": END
})
graph = builder.compile(checkpointer=PostgresSaver(...))
```

**Choose LangGraph when:**
- Your team is Python-native
- You need deep LangChain ecosystem integration
- Rapid prototyping is more important than audit compliance

**Choose Strategos when:**
- You're building in .NET
- You need complete audit trails (what did the agent see?)
- Compile-time safety is a priority
- You want intelligent agent selection (Thompson Sampling)
- You need compensation handlers for rollback

---

### Microsoft Agent Framework Workflows

[Microsoft Agent Framework Workflows](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/overview) is Microsoft's graph-based orchestration system for AI agent applications, running on Azure Functions with the Durable Task Scheduler.

**Strengths:**
- **5 built-in multi-agent patterns**: Sequential, Concurrent, Group Chat, Handoff, Magentic
- **DTS Dashboard**: Visual debugging UI showing agent interactions, conversation history, tool calls
- **Serverless economics**: $0 cost during human-in-the-loop waits (Azure Functions scale-to-zero)
- **BSP execution model**: Deterministic, reproducible execution with superstep barriers
- Backed by Microsoft with Azure ecosystem integration

**Technical Details:**

MAF uses Bulk Synchronous Parallel (BSP) execution:
```
Superstep N: All executors run in parallel → BARRIER → Checkpoint → Superstep N+1
```

This provides determinism but means the slowest parallel branch blocks all others.

**Limitations:**
- **Azure-only** — Requires Azure Functions + Durable Task Scheduler (no cloud portability)
- **No compensation handlers** — Must implement rollback manually via conditional edges
- **Checkpoint-based** — Snapshots at superstep boundaries, not full event sourcing
- **No intelligent agent selection** — Group Chat manager or declarative edges only
- **No compile-time validation** — Workflow errors surface at runtime

**Definition Style:**
```csharp
// MAF: Graph-based with explicit nodes and edges
var builder = new WorkflowBuilder();
builder.AddNode<TriageExecutor>("triage");
builder.AddNode<BillingExecutor>("billing");
builder.AddConditionalEdge("triage", "billing", msg => msg.Type == "billing");
var workflow = builder.Build();
```

**Choose MAF Workflows when:**
- You're already invested in Azure
- Need serverless scale-to-zero economics
- Want pre-built multi-agent patterns (Group Chat, Handoff)
- Visual debugging dashboard is important
- Human approvals may take hours/days

**Choose Strategos when:**
- You need cloud portability (any .NET host)
- Complete audit trails are required ("what did the agent see?")
- You want learning-based agent selection (Thompson Sampling)
- Compile-time workflow validation is valuable
- You need DSL-based compensation handlers

::: tip Deep Dive
For a comprehensive 400+ line comparison, see [MAF Workflows Deep Dive](/learn/maf-deep-dive).
:::

---

### Temporal

[Temporal](https://temporal.io/) is a battle-tested workflow orchestration platform used by Coinbase, Netflix, and other major companies for mission-critical workflows.

**Strengths:**
- **Extremely mature** — Battle-tested at massive scale
- **First-class saga compensation** — `addCompensation()` with automatic reverse-order execution
- **Excellent durability** — Event history with replay, similar to event sourcing
- **Strong .NET SDK** — Well-documented, production-ready
- Visual dashboard for workflow inspection

**Technical Details:**

Temporal's saga pattern provides true compensation:

```java
// Temporal (Java): Saga with compensation
Saga saga = new Saga(options);
saga.addCompensation(activities::cancelHotel, clientId);
activities.bookHotel(info);
// If later steps fail, cancelHotel runs automatically
```

```go
// Temporal (Go): Compensation tracking
compensations.AddCompensation(CancelHotel)
err = workflow.ExecuteActivity(ctx, BookHotel, info).Get(ctx, nil)
// On failure, compensations execute in LIFO order
```

**Limitations:**
- **No AI-specific abstractions** — Generic workflow primitives require glue code for:
  - Confidence-based routing
  - Context assembly from workflow state
  - Agent selection strategies
- **No intelligent agent selection** — No concept of agents or learning-based routing
- **Requires idempotent activities** — Side effects must be idempotent for safe retry

**Definition Style:**
```csharp
// Temporal: Imperative workflow code
public class OrderWorkflow : IOrderWorkflow
{
    public async Task<OrderResult> ProcessOrder(OrderInput input)
    {
        await activities.ValidateOrder(input);
        await activities.ProcessPayment(input);
        return await activities.FulfillOrder(input);
    }
}
```

**Choose Temporal when:**
- You have existing Temporal infrastructure
- Your workflows aren't primarily AI-driven
- You need Temporal's specific scale characteristics
- Saga compensation is critical and you want battle-tested maturity

**Choose Strategos when:**
- Your workflows center around AI agent decisions
- You want confidence routing out of the box
- Thompson Sampling for agent selection is valuable
- You prefer AI-specific abstractions over generic primitives
- You want DSL-based workflow definition with compile-time validation

---

## Compensation Comparison

One area that deserves special attention is how each framework handles compensation (rollback) when workflows fail:

| Framework | Compensation Support | How It Works |
|-----------|---------------------|--------------|
| **Strategos** | `.Compensate<T>()` DSL | Automatic reverse-order execution, compile-time wiring |
| **Temporal** | `saga.addCompensation()` | Runtime registration, LIFO execution |
| **LangGraph** | None | Manual rollback in exception handlers |
| **MAF Workflows** | None | Manual rollback via conditional edges |

**Strategos compensation:**
```csharp
Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ChargePayment>()
        .Compensate<RefundPayment>()     // Automatic rollback
    .Then<ReserveInventory>()
        .Compensate<ReleaseInventory>()  // Runs in reverse order
    .Then<ShipOrder>()
    .Finally<Confirm>();
```

On failure, compensations run automatically in reverse order: `ReleaseInventory` → `RefundPayment`.

---

## Unique Features Explained

### Event-Sourced Audit Trail

Unlike checkpoint-based systems, Strategos captures every decision as an immutable event:

```csharp
// What gets captured for every agent decision:
AgentDecisionEvent {
    WorkflowId = "order-123",
    StepName = "AssessClaim",
    InputContext = "Full prompt sent to model...",
    OutputDecision = "Claim approved with conditions...",
    ModelVersion = "gpt-4o-2024-05-13",
    Confidence = 0.87m,
    TokensUsed = 1247,
    Timestamp = "2024-01-15T10:30:00Z"
}
```

**What this enables:**
- **Debugging**: "What prompt was sent when this decision was made?"
- **Compliance**: "What information did the agent have access to?"
- **Reproducibility**: "What would a different model decide given the same context?"

With checkpoints, you only get "workflow is at step 5 with this state"—no record of *why*.

### Thompson Sampling

[Thompson Sampling](https://en.wikipedia.org/wiki/Thompson_sampling) is a contextual multi-armed bandit algorithm for intelligent agent selection. **No other framework in this comparison offers this capability.**

When multiple agents can handle a task, Thompson Sampling balances:
- **Exploitation** — Using agents that have performed well
- **Exploration** — Trying potentially better agents

```csharp
// Agent selection that learns over time
var selector = services.GetRequiredService<IAgentSelector>();
var selection = await selector.SelectAgentAsync(new AgentSelectionContext
{
    AvailableAgentIds = ["analyst-a", "analyst-b", "analyst-c"],
    TaskDescription = "Analyze the quarterly sales data"
});

// Record outcome for learning
await selector.RecordOutcomeAsync(
    selection.SelectedAgentId,
    selection.TaskCategory,
    AgentOutcome.Succeeded(confidenceScore: 0.92));
```

The system learns which agents perform best for which task categories—no manual tuning required.

### Compile-Time Validation

Roslyn source generators analyze workflow definitions at compile time:

| Diagnostic | Description |
|------------|-------------|
| AGWF001 | Empty workflow name |
| AGWF003 | Duplicate step name (use instance names) |
| AGWF009 | Missing `StartWith<T>()` |
| AGWF010 | Missing `Finally<T>()` |
| AGWF012 | `Fork` without matching `Join<T>()` |
| AGWF014 | `RepeatUntil` loop without body |

If your workflow has structural problems, you see them as build errors—not runtime exceptions.

### Confidence-Based Routing

AI agents don't always produce high-confidence outputs. Route decisions based on confidence:

```csharp
.Then<ClassifyDocument>()
    .OnConfidence(c => c
        .When(conf => conf >= 0.9m, b => b.Then<AutoProcess>())
        .When(conf => conf >= 0.5m, b => b.Then<HumanReview>())
        .Otherwise(b => b.Then<ManualClassification>()))
```

Low-confidence decisions automatically route to human review without custom conditional logic.

---

## Decision Guide

**You need Strategos if:**
- :white_check_mark: Building AI agent workflows in .NET
- :white_check_mark: Audit compliance requires full decision history
- :white_check_mark: You want compile-time safety for workflow definitions
- :white_check_mark: Intelligent agent selection would improve outcomes
- :white_check_mark: Confidence-based routing is important
- :white_check_mark: You need cloud portability (not Azure-only)

**Consider alternatives if:**
- Your team is Python-native → **LangGraph**
- You're not building AI-driven workflows → **Temporal**
- You need Azure ecosystem + visual dashboard → **MAF Workflows**
- You have existing Temporal infrastructure → **Temporal**
- You're prototyping and don't need durability yet

---

## What's Next

Ready to get started? Head to the [installation guide](/guide/installation) to add Strategos to your project, or see [complete examples](/examples/) of workflows in action.
