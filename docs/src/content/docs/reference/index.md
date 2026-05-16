---
title: "Reference"
---

# Reference

Complete API reference and technical documentation for Strategos.

## What's in this Section

The Reference section provides detailed, factual documentation for looking up specific information. Use this section when you:

- Need to check a type signature or method parameters
- Want to understand what a diagnostic code means
- Need configuration options for integrations
- Want to see all available packages and their purposes

## Quick Links

### Core Documentation

| Topic | Description |
|-------|-------------|
| [Packages](/reference/packages) | NuGet package overview, dependencies, and installation scenarios |
| [Diagnostics](/reference/diagnostics) | Compiler diagnostic codes (AGWF/AGSR) and resolutions |
| [Configuration](/reference/configuration) | Wolverine, Marten, and PostgreSQL integration setup |

### API Reference

| Package | Contents |
|---------|----------|
| [Workflow API](/reference/api/workflow) | `Workflow<TState>`, `IWorkflowStep<TState>`, `StepResult<TState>`, state attributes |
| [Generators](/reference/api/generators) | Source generator outputs: enums, commands, events, sagas |
| [Infrastructure](/reference/api/infrastructure) | Thompson Sampling, loop detection, budget guard |
| [Agents](/reference/api/agents) | `IAgentStep<TState>`, `AgentStepContext`, IChatClient integration |
| [RAG](/reference/api/rag) | `IVectorSearchAdapter`, `SearchResult`, vector store adapters |

## Reference vs Guide

| Use Reference When | Use Guide When |
|--------------------|----------------|
| Looking up a specific type or method | Learning how to build something |
| Checking diagnostic error codes | Following a step-by-step tutorial |
| Configuring an integration | Understanding patterns and best practices |
| Comparing package options | Getting started with the library |

The [Guide](/guide/) provides tutorials and explanations; the Reference provides facts and specifications.
