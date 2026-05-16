---
title: "Why Strategos"
outline: deep
---

# Why Strategos

Strategos provides deterministic orchestration for probabilistic AI agents. It bridges the gap between flexible agent frameworks and battle-tested workflow engines, giving you the best of both worlds.

## The Problem

AI agents are inherently probabilistic. Given the same input, an LLM may produce different outputs. This fundamental characteristic creates a challenge: how do you build reliable, auditable systems on top of non-deterministic components?

Current solutions force you to make an unsatisfying choice:

### Agent Frameworks

Solutions like [LangGraph](https://www.langchain.com/langgraph) and [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/) offer excellent developer experience for building AI agents. However, they rely on checkpoint-based persistence.

Checkpoints let you resume workflows after failures, but they can't answer the critical question: **"What did the agent see when it made that decision?"**

When something goes wrong in production, you need to understand the full context: the input data, the model version, the temperature setting, and the exact prompt that led to an unexpected output. Checkpoint-based systems don't capture this.

### Workflow Engines

Tools like [Temporal](https://temporal.io/) provide battle-tested durability patterns with excellent compensation support (saga pattern with `addCompensation()`). However, they have no awareness of agent-specific patterns:

- **Confidence handling** — What happens when an agent is uncertain?
- **Context assembly** — How do you build prompts from workflow state?
- **Intelligent agent selection** — How do you route tasks to the best-performing agent?

You end up writing significant glue code to adapt generic workflow primitives to AI-specific needs.

## The Solution

Strategos bridges these domains with a key insight:

> While agent *outputs* are probabilistic, the *workflow itself* can be deterministic if we treat each agent decision as an immutable event in an event-sourced system.

This means every decision, every context, and every output is captured as a permanent record. You get:

- **Reproducibility** - Replay any workflow to see exactly what happened
- **Auditability** - Complete trail from input to output for compliance
- **Debuggability** - Time-travel to any point in workflow execution

```csharp
var workflow = Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Then<FulfillOrder>()
    .Finally<SendConfirmation>();
```

The workflow definition is simple and declarative. Behind the scenes, the library captures every state transition, every agent response, and every routing decision as immutable events.

## Built on Proven Infrastructure

Rather than reinventing durability primitives, Strategos builds on proven .NET infrastructure:

**[Wolverine](https://wolverine.netlify.app/)** provides saga orchestration. Each workflow becomes a saga with automatic message routing, transactional outbox (state and messages commit atomically), and configurable retry policies.

**[Marten](https://martendb.io/)** provides event sourcing. Every step completion, branch decision, and approval is captured as an immutable event in PostgreSQL. This enables time-travel debugging and complete audit trails.

**[Roslyn Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)** transform fluent DSL definitions into type-safe artifacts at compile time: phase enums, commands, events, saga handlers, and state reducers. Invalid workflows fail at build time with clear diagnostics, not at runtime with cryptic exceptions.

## Key Features

- **Fluent DSL** - Intuitive workflow definitions that read like natural language
- **Compile-time Validation** - Invalid workflows fail at build time, not runtime
- **Thompson Sampling** - Intelligent agent selection using contextual multi-armed bandits
- **Confidence Routing** - Automatic escalation to human review for low-confidence decisions
- **Event-Sourced Audit Trail** - Complete decision history for debugging and compliance
- **Human-in-the-Loop** - Built-in approval workflows with timeout escalation
- **Compensation Handlers** - Explicit rollback strategies for AI decisions when workflows fail

## Try the Samples

::: tip Ready to see it in action?
Run any of our complete sample applications:

```bash
# Content publishing with approval workflow
dotnet run --project samples/ContentPipeline

# Intelligent model selection with Thompson Sampling
dotnet run --project samples/MultiModelRouter

# Iterative code generation with human checkpoints
dotnet run --project samples/AgenticCoder
```

See the [Sample Applications](/examples/#sample-applications) for details.
:::

## What's Next

Now that you understand why Strategos exists, learn about the [Core Concepts](/learn/core-concepts) that power it, or see how it [compares to alternatives](/learn/comparison).
