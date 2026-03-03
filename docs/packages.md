# Package Documentation

This document provides detailed information about each NuGet package in the Strategos library.

## Package Overview

| Package | Purpose | Required |
|---------|---------|----------|
| `Strategos` | Core fluent DSL, abstractions, and type definitions | Yes |
| `Strategos.Generators` | Roslyn source generators for compile-time code generation | Yes |
| `Strategos.Infrastructure` | Production implementations (Thompson Sampling, loop detection, budgets) | Recommended |
| `Strategos.Agents` | Microsoft Agent Framework integration for LLM-powered steps | For AI workflows |
| `Strategos.Rag` | Vector store adapters for RAG patterns | For RAG workflows |

---

## Strategos

The core package containing the fluent DSL for defining workflows and all foundational abstractions.

### Key Types

| Type | Purpose |
|------|---------|
| `Workflow<TState>` | Entry point for fluent workflow definitions |
| `IWorkflowStep<TState>` | Interface for implementing workflow steps |
| `IWorkflowDefinition<TState>` | Interface for workflow definition classes |
| `StepResult<TState>` | Result type returned from step execution |
| `StepContext` | Execution context passed to steps (correlation ID, timestamp, metadata) |

### Thompson Sampling Types

| Type | Purpose |
|------|---------|
| `AgentBelief` | Beta(α, β) distribution representing agent performance belief |
| `TaskCategory` | Enumeration of task categories (Analysis, Coding, Research, etc.) |
| `TaskFeatures` | Extracted features from task descriptions |
| `IAgentSelector` | Interface for agent selection strategies |
| `IBeliefStore` | Interface for persisting agent beliefs |

### State Attributes

| Attribute | Purpose |
|-----------|---------|
| `[Append]` | Merge lists by appending new items to existing |
| `[Merge]` | Merge dictionaries, new values overwrite existing keys |
| `[WorkflowState]` | Marks a record as workflow state (enables source generation) |

### Installation

```bash
dotnet add package LevelUp.Strategos
```

---

## Strategos.Generators

Roslyn source generators that transform fluent DSL definitions into type-safe artifacts at compile time.

### Generated Artifacts

| Artifact | Description |
|----------|-------------|
| Phase Enum | Type-safe enumeration of workflow phases |
| Commands | Wolverine message types for step transitions |
| Events | Marten event types for audit trail |
| Saga Class | Complete Wolverine saga with handlers |
| State Reducers | Property merge logic based on `[Append]`/`[Merge]` attributes |
| DI Extensions | Service registration helpers |

### Compiler Diagnostics

The generator reports errors and warnings at compile time:

| Code | Severity | Description |
|------|----------|-------------|
| AGWF001 | Error | Workflow name cannot be empty |
| AGWF002 | Warning | No steps found in workflow |
| AGWF003 | Error | Duplicate step name (use instance names) |
| AGWF009 | Error | Workflow must begin with `StartWith<T>()` |
| AGWF010 | Warning | Workflow should end with `Finally<T>()` |
| AGWF012 | Error | Every `Fork` must be followed by `Join<T>()` |

See [Diagnostics Reference](diagnostics.md) for the complete list.

### Installation

```bash
dotnet add package LevelUp.Strategos.Generators
```

> **Note:** This is a development dependency. It runs at compile time and produces no runtime overhead.

---

## Strategos.Infrastructure

Production-ready implementations of core abstractions including Thompson Sampling, loop detection, and budget enforcement.

### Thompson Sampling

| Type | Purpose |
|------|---------|
| `ContextualAgentSelector` | Selects agents using Thompson Sampling with contextual bandits |
| `InMemoryBeliefStore` | In-memory persistence for agent beliefs (dev/testing) |
| `KeywordTaskFeatureExtractor` | Extracts task features for category classification |

### Loop Detection

Detects stuck workflows using four strategies:

| Detector | Description |
|----------|-------------|
| `ExactRepetitionDetector` | Identical action sequences |
| `SemanticRepetitionDetector` | Similar outputs (cosine similarity > threshold) |
| `OscillationDetector` | A-B-A-B patterns |
| `NoProgressDetector` | Activity without state change |

### Budget Guard

| Type | Purpose |
|------|---------|
| `BudgetGuard` | Enforces resource limits (steps, tokens, wall time) |
| `ScarcityLevel` | Abundant → Normal → Scarce → Critical |
| `BudgetOptions` | Configuration for budget thresholds |

### Installation

```bash
dotnet add package LevelUp.Strategos.Infrastructure
```

### Usage

```csharp
services.AddStrategos()
    .AddThompsonSampling(options => options
        .WithPrior(alpha: 2, beta: 2))
    .AddLoopDetection()
    .AddBudgetGuard(options => options
        .WithMaxSteps(100)
        .WithMaxTokens(50_000));
```

---

## Strategos.Agents

Integration with Microsoft Agent Framework via `Microsoft.Extensions.AI` for LLM-powered workflow steps.

### Key Types

| Type | Purpose |
|------|---------|
| `IAgentStep<TState>` | Base interface for LLM-powered steps |
| `AgentStepContext` | Extended context with conversation thread access |
| `IConversationalState` | Interface for state that includes conversation history |
| `IStreamingCallback` | Callback for real-time token streaming |

### Dependencies

- `Microsoft.Extensions.AI` (10.0.1)
- `Microsoft.Extensions.AI.Abstractions` (10.0.1)

### Installation

```bash
dotnet add package LevelUp.Strategos.Agents
```

### Usage

```csharp
public class AnalyzeDocumentStep : IAgentStep<DocumentState>
{
    private readonly IChatClient _chatClient;

    public AnalyzeDocumentStep(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var response = await _chatClient.GetResponseAsync(
            $"Analyze this document: {state.Content}",
            ct);

        return state
            .With(s => s.Analysis, response)
            .AsResult();
    }
}
```

---

## Strategos.Rag

Vector store adapters for Retrieval-Augmented Generation (RAG) patterns.

### Implemented Adapters

| Adapter | Status | Use Case |
|---------|--------|----------|
| `InMemoryVectorSearchAdapter` | Available | Development and testing |

### Planned Adapters

| Adapter | Status | Use Case |
|---------|--------|----------|
| `PgVectorAdapter` | Planned | PostgreSQL with pgvector extension |
| `AzureAISearchAdapter` | Planned | Azure AI Search |

### Key Interfaces

| Type | Purpose |
|------|---------|
| `IVectorSearchAdapter` | Interface for vector similarity search |
| `SearchResult` | Result containing content, score, and metadata |

### Installation

```bash
dotnet add package LevelUp.Strategos.Rag
```

### Usage

```csharp
public class RetrieveContextStep : IWorkflowStep<QueryState>
{
    private readonly IVectorSearchAdapter _vectorSearch;

    public async Task<StepResult<QueryState>> ExecuteAsync(
        QueryState state,
        StepContext context,
        CancellationToken ct)
    {
        var results = await _vectorSearch.SearchAsync(
            state.Query,
            topK: 10,
            minRelevance: 0.7);

        return state
            .With(s => s.RetrievedContext, results)
            .AsResult();
    }
}
```

---

## Installation Scenarios

### Minimal (Non-AI Workflows)

For workflows that don't involve LLM agents:

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
```

### Standard (LLM-Powered Workflows)

Most common setup for AI agent workflows:

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
dotnet add package LevelUp.Strategos.Agents
dotnet add package LevelUp.Strategos.Infrastructure
```

### Full (With RAG)

For workflows that include retrieval-augmented generation:

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
dotnet add package LevelUp.Strategos.Agents
dotnet add package LevelUp.Strategos.Infrastructure
dotnet add package LevelUp.Strategos.Rag
```

---

## Package Dependencies

```
Strategos (core)
├── No external dependencies

Strategos.Generators
├── Microsoft.CodeAnalysis.CSharp
└── [Compile-time only]

Strategos.Infrastructure
├── Strategos
├── Microsoft.Extensions.Caching.Memory
└── Microsoft.Extensions.DependencyInjection.Abstractions

Strategos.Agents
├── Strategos
├── Microsoft.Extensions.AI
├── Microsoft.Extensions.AI.Abstractions
└── Microsoft.Extensions.DependencyInjection.Abstractions

Strategos.Rag
└── Strategos.Agents
```
