# Strategos

[![NuGet](https://img.shields.io/nuget/v/LevelUp.Strategos.svg)](https://www.nuget.org/packages/LevelUp.Strategos)
[![Build Status](https://img.shields.io/github/actions/workflow/status/lvlup-sw/strategos/ci.yml?branch=main)](https://github.com/lvlup-sw/strategos/actions)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

> A compiled, durable-execution runtime for agentic workflows.

Strategos is a .NET library for building AI workflows that survive restarts, roll
back on failure, and answer "what did the agent see when it made that decision?"
Agent outputs are stochastic. The workflow around them is deterministic.

**Declare.** Write the workflow in a fluent C# DSL: steps, branches, approvals,
and compensation.

**Compile.** A Roslyn source generator lowers the definition to a typed saga at
build time. Invalid workflows fail compilation instead of failing in production.

**Run.** The saga executes durably on Wolverine and Marten, with rollback, budget
ceilings, confidence-based escalation, and an event log you can audit or replay.

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

AI agents are inherently stochastic. Given the same input, an LLM may produce different outputs. Current solutions force an unsatisfying choice:

- **Agent frameworks** ([LangGraph](https://www.langchain.com/langgraph), [MS Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)) offer great developer experience but rely on checkpoint-based persistence—they can resume workflows, but can't answer "what did the agent see when it made that decision?"

- **Workflow engines** ([Temporal](https://temporal.io/)) provide battle-tested durability but have no awareness of agent-specific patterns: confidence handling, context assembly, AI-aware compensation.

## The Solution

Strategos bridges these domains with a key insight: while agent *outputs* are stochastic, the *workflow itself* can be deterministic if we treat each agent decision as an immutable event in an event-sourced system.

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

## How It Compares

Strategos is not an agent framework, and it is not a general workflow engine. It sits between the two: the durability and audit of a workflow engine, with execution primitives that understand agents.

| | Strategos | [LangGraph](https://www.langchain.com/langgraph) | [MAF Workflows](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/overview) | [Temporal](https://temporal.io/) |
|---|---|---|---|---|
| Platform | .NET | Python, JS | .NET, Python | polyglot |
| Durability | event-sourced on Postgres | checkpoints | checkpoints (BSP) | event history |
| Authoring | compiled DSL, source-generated saga | imperative graph | fluent graph | SDK code |
| Compensation | built into the DSL | your code | your code | saga pattern |
| Agent-native controls | confidence routing, budgets, loop detection, agent selection | not built in | not built in | not agent-aware |
| Decision audit | replay the exact state behind any step | checkpoint state | checkpoint state | event history |

Where the others lead: LangGraph has the largest ecosystem, MAF has a visual designer and the rest of the Microsoft stack behind it, and Temporal has years of production hardening and a mature operations dashboard. Strategos trades that breadth for determinism, event-sourced audit, and agent-aware execution on .NET.

## Packages

Everything ships on NuGet under the `LevelUp.` prefix. Add what a given project needs.

| Package | Purpose |
|---------|---------|
| `LevelUp.Strategos` | Fluent workflow DSL and core abstractions: steps, branches, compensation, fork/join |
| `LevelUp.Strategos.Generators` | Roslyn source generators that emit the saga, phase enum, commands, events, and DI wiring |
| `LevelUp.Strategos.Infrastructure` | Runtime implementations: Thompson Sampling selection, loop detection, budget enforcement |
| `LevelUp.Strategos.Identity.Abstractions` | Identity seam: workflow and agent identity records, ports, and header constants |
| `LevelUp.Strategos.Contracts` | TypeSpec-canonical event, workflow-IR, and invariant contracts as C# records plus JSON Schema |
| `LevelUp.Strategos.Agents` | Microsoft Agent Framework integration for LLM-powered steps |
| `LevelUp.Strategos.Agents.Mcp` | Model Context Protocol tool source for agent steps |
| `LevelUp.Strategos.Ontology` | Type-safe domain ontology DSL: object types, links, actions, and an ingestion pipeline |
| `LevelUp.Strategos.Ontology.Generators` | Compile-time ontology analyzers (AONT diagnostics) |
| `LevelUp.Strategos.Ontology.Embeddings` | OpenAI-compatible embedding provider for vector search |
| `LevelUp.Strategos.Ontology.Npgsql` | PostgreSQL pgvector object-set provider |
| `LevelUp.Strategos.Ontology.MCP` | Ontology exposed as MCP tools for exploration, querying, and action dispatch |
| `LevelUp.Strategos.Ontology.MCP.Hosting` | Hosting bridge that registers ontology tools on an MCP server |

`LevelUp.Strategos.Rag` is deprecated; use `LevelUp.Strategos.Ontology` instead.

## Requirements

- .NET 10 or later
- PostgreSQL (for Wolverine/Marten persistence)

## License

Apache-2.0 — see [LICENSE](LICENSE) for details.
