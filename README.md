# Strategos

[![NuGet](https://img.shields.io/nuget/v/LevelUp.Strategos.svg)](https://www.nuget.org/packages/LevelUp.Strategos)
[![Build Status](https://img.shields.io/github/actions/workflow/status/lvlup-sw/strategos/ci.yml?branch=main)](https://github.com/lvlup-sw/strategos/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> Deterministic orchestration for AI-powered workflows

## Documentation

**[View the full documentation](https://lvlup-sw.github.io/strategos/)**

- [Learn](https://lvlup-sw.github.io/strategos/learn/) - Core concepts and value proposition
- [Guide](https://lvlup-sw.github.io/strategos/guide/) - Step-by-step tutorials
- [Reference](https://lvlup-sw.github.io/strategos/reference/) - API documentation
- [Examples](https://lvlup-sw.github.io/strategos/examples/) - Real-world workflows

## Why Strategos?

Building AI-powered automation? You need more than just "call the LLM":

- **Content pipelines** need human approval gates and rollback
- **Multi-model systems** need intelligent routing that learns
- **Agentic coding** needs iteration loops with guardrails

Strategos provides these patterns out of the box, with complete audit trails.

### Try the Samples

```bash
# Content publishing with approval workflow
dotnet run --project samples/ContentPipeline

# Intelligent model selection with Thompson Sampling
dotnet run --project samples/MultiModelRouter

# Iterative code generation with human checkpoints
dotnet run --project samples/AgenticCoder
```

## The Problem

AI agents are inherently probabilistic—given the same input, an LLM may produce different outputs. Current solutions force an unsatisfying choice:

- **Agent frameworks** ([LangGraph](https://www.langchain.com/langgraph), [MS Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)) offer great developer experience but rely on checkpoint-based persistence—they can resume workflows, but can't answer "what did the agent see when it made that decision?"

- **Workflow engines** ([Temporal](https://temporal.io/)) provide battle-tested durability but have no awareness of agent-specific patterns: confidence handling, context assembly, AI-aware compensation.

## The Solution

Strategos bridges these domains with a key insight: while agent *outputs* are probabilistic, the *workflow itself* can be deterministic if we treat each agent decision as an immutable event in an event-sourced system.

```csharp
var workflow = Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Then<FulfillOrder>()
    .Finally<SendConfirmation>();
```

## How It Works

The library builds on proven .NET infrastructure rather than reinventing durability:

**[Wolverine](https://wolverine.netlify.app/)** provides saga orchestration—each workflow becomes a saga with automatic message routing, transactional outbox (state + messages commit atomically), and retry policies.

**[Marten](https://martendb.io/)** provides event sourcing—every step completion, branch decision, and approval is captured as an immutable event in PostgreSQL. This enables time-travel debugging ("what was the state when this decision was made?") and complete audit trails.

**Roslyn Source Generators** transform fluent DSL definitions into type-safe artifacts at compile time: phase enums, commands, events, saga handlers, and state reducers. Invalid workflows fail at build time with clear diagnostics, not at runtime with cryptic exceptions.

## Packages

| Package | Purpose |
|---------|---------|
| `Strategos` | Core fluent DSL and abstractions |
| `Strategos.Generators` | Compile-time source generation (sagas, events, phase enums) |
| `Strategos.Infrastructure` | Production implementations (Thompson Sampling, loop detection, budgets) |
| `Strategos.Agents` | Microsoft Agent Framework integration for LLM-powered steps |
| `Strategos.Rag` | Vector store adapters for RAG patterns |

**Minimal setup** (workflows without LLM agents):
```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
```

**With LLM integration** (most common):
```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
dotnet add package LevelUp.Strategos.Agents
dotnet add package LevelUp.Strategos.Infrastructure
```

See [Package Documentation](docs/packages.md) for detailed guidance.

## How It Compares

| Capability | Strategos | [LangGraph](https://www.langchain.com/langgraph) | [MAF Workflows](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/overview) | [Temporal](https://temporal.io/) |
|------------|:----------------:|:---------:|:-------------:|:--------:|
| .NET native | ✓ | | ✓ | ✓ |
| Durable execution | event-sourced | checkpoints | checkpoints (BSP) | event history |
| Compensation/rollback | ✓ DSL | | | ✓ Saga |
| Human-in-the-loop | ✓ | ✓ | ✓ | ✓ |
| Decision explainability | ✓ | | | |
| Confidence routing | ✓ | | | |
| Budget governance | ✓ | | | |
| Loop detection | ✓ | | | |
| Intelligent agent selection | ✓ | | | |
| Visual dashboard | | | ✓ DTS | ✓ |
| Cloud-agnostic | ✓ | ✓ | | ✓ |

## Key Features

- **Fluent DSL** — Workflow definitions that read like natural language
- **Decision Explainability** — Full audit trail: what the agent saw, what it decided, which model produced the output
- **Budget Governance** — Enforce per-workflow resource limits; prevent runaway costs
- **Confidence Routing** — Low-confidence decisions automatically escalate to human review
- **Intelligent Agent Selection** — Learning-based routing that improves over time (Thompson Sampling)
- **Loop Detection** — Catch stuck agents before they burn through your budget
- **Compensation Handlers** — DSL-based rollback when workflows fail
- **Compile-Time Validation** — Invalid workflows fail at build time, not runtime
- **Durable by Default** — Automatic persistence via Wolverine sagas and Marten event sourcing

## Quick Start

```csharp
// Register workflows at startup
services.AddStrategos()
    .AddWorkflow<ProcessOrderWorkflow>();

// Define a workflow
public class ProcessOrderWorkflow : IWorkflowDefinition<OrderState>
{
    public IWorkflow<OrderState> Define() =>
        Workflow<OrderState>
            .Create("process-order")
            .StartWith<ValidateOrder>()
            .Then<ProcessPayment>()
            .Finally<SendConfirmation>();
}
```

## Requirements

- .NET 10 or later
- PostgreSQL (for Wolverine/Marten persistence)

## License

MIT — see [LICENSE](LICENSE) for details.
