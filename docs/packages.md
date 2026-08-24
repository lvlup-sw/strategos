# Package Documentation

This document is the plain-markdown mirror of the published [Package Ecosystem](src/content/docs/reference/packages.md) reference. It is kept here for links from the design and plan records; the site builds from the published copy.

Strategos is distributed as fourteen NuGet packages, allowing you to include only what you need. Every package listed here ships to nuget.org under the `LevelUp.` prefix.

## Workflow packages

| Package | Purpose | Install |
|---------|---------|---------|
| `Strategos` | Core fluent DSL, abstractions, and type definitions | Yes |
| `Strategos.Generators` | Roslyn source generators for compile-time code generation | Yes |
| `Strategos.Identity.Abstractions` | Workflow and agent identity records, header constants, and the saga ports | Arrives with `Strategos` |
| `Strategos.Contracts` | TypeSpec-canonical event, workflow-IR, and invariant contracts | Arrives with `Strategos` |
| `Strategos.Infrastructure` | Production implementations (Thompson Sampling, loop detection, budgets) | Recommended |
| `Strategos.Agents` | Microsoft.Extensions.AI integration for LLM-powered steps | For AI workflows |
| `Strategos.Agents.Mcp` | Consume external MCP servers as agent tool sources | For MCP tool sources |

## Ontology packages

| Package | Purpose | Install |
|---------|---------|---------|
| `Strategos.Ontology` | Type-safe ontology definition layer: descriptors, builders, object sets | For domain modeling |
| `Strategos.Ontology.Generators` | Roslyn analyzers that validate ontology declarations at compile time | With `Strategos.Ontology` |
| `Strategos.Ontology.Npgsql` | PostgreSQL pgvector-backed object set provider | For a durable object set |
| `Strategos.Ontology.Embeddings` | OpenAI-compatible embedding provider for vector search | For vector search |
| `Strategos.Ontology.MCP` | Ontology exploration, query, and action dispatch as MCP tools | For agent access |
| `Strategos.Ontology.MCP.Hosting` | Registers those tools on an MCP server builder | To host the tools |

## Deprecated

| Package | Purpose | Install |
|---------|---------|---------|
| `Strategos.Rag` | Vector store adapters, superseded by `Strategos.Ontology` | No |

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
| `AgentBelief` | Beta(alpha, beta) distribution representing agent performance belief |
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

The generator reports errors and warnings at compile time. See [Diagnostics Reference](diagnostics.md) for the complete list.

| Code | Severity | Description |
|------|----------|-------------|
| AGWF001 | Error | Workflow name cannot be empty |
| AGWF002 | Warning | No steps found in workflow |
| AGWF003 | Error | Duplicate step name (use instance names) |
| AGWF009 | Error | Workflow must begin with `StartWith<T>()` |
| AGWF010 | Warning | Workflow should end with `Finally<T>()` |
| AGWF012 | Error | Every `Fork` must be followed by `Join<T>()` |

### Installation

```bash
dotnet add package LevelUp.Strategos.Generators
```

> **Development dependency.** This is a compile-time dependency. It runs during build and produces no runtime overhead.

---

## Strategos.Identity.Abstractions

The identity seam: workflow and agent identity value records, header constants, and the ports that Wolverine envelope-header propagation consumes.

### Key Types

| Type | Purpose |
|------|---------|
| `IAgentIdentityProvider` | Supplies the agent identity for an outgoing call |
| `IAgentIdentityAccessor` | Reads the ambient agent identity inside a step |
| `IPhaseAwareSaga` | Port a generated saga implements so a host can read its current phase |

This package has no dependencies of its own and arrives transitively with `LevelUp.Strategos`. Install it directly only when a project needs the ports without the DSL.

### Installation

```bash
dotnet add package LevelUp.Strategos.Identity.Abstractions
```

---

## Strategos.Contracts

The cross-product schema substrate. TypeSpec is canonical; the contracts are emitted both as C# records (this assembly) and as JSON Schema shipped as package content for non-.NET consumers.

The package covers SDLC event contracts, the workflow wire-IR that `WorkflowDefinitionProjection.ToContract()` exports, and the invariant and diagnostic catalogs. It versions independently of the workflow packages.

It arrives transitively with `LevelUp.Strategos`; install it directly when a project consumes the schemas without the DSL.

### Installation

```bash
dotnet add package LevelUp.Strategos.Contracts
```

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
| `ScarcityLevel` | Abundant, Normal, Scarce, Critical |
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

Integration with Microsoft.Extensions.AI for LLM-powered workflow steps.

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

## Strategos.Agents.Mcp

The Model Context Protocol adapter for `Strategos.Agents`. It supplies `McpToolSource`, an `IToolSource` implementation wrapping the ModelContextProtocol C# SDK, so a Strategos agent can consume an external MCP server as a skill provider.

It ships separately from `Strategos.Agents` on purpose: the core agents package stays free of MCP dependencies, which keeps the port and the adapter apart.

### Installation

```bash
dotnet add package LevelUp.Strategos.Agents.Mcp
```

---

## Strategos.Ontology

The type-safe ontology definition layer for domain modeling. It provides descriptors, builders, and a fluent DSL for defining object types, links, actions, events, and interfaces across domains.

### Key Types

| Type | Purpose |
|------|---------|
| `IObjectSetProvider` | Port a backing store implements to serve object sets |
| `InMemoryObjectSetProvider` | In-memory provider for development and testing |
| `IEmbeddingProvider` | Port for producing embedding vectors |
| `ITextChunker` | Chunking strategies: sentence, paragraph, and fixed-size |
| `IngestionPipeline` | Chunk, embed, and load source text into an object set |

See the [Ontology Reference](src/content/docs/reference/ontology/index.md) for the modeling guide.

### Installation

```bash
dotnet add package LevelUp.Strategos.Ontology
```

---

## Strategos.Ontology.Generators

Roslyn diagnostic analyzers for ontology definitions. They validate domain ontology declarations at compile time across the core, precondition, lifecycle, derivation, interface-action, and extension-point rules.

The analyzers report the `AONT` diagnostic series; see [AONT001-AONT099](src/content/docs/reference/diagnostics/aont-001-aont-099.md), [AONT100-AONT199](src/content/docs/reference/diagnostics/aont-100-aont-199.md), and the [AONT200 series](src/content/docs/reference/diagnostics/aont-200-series.md).

### Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.Generators
```

> **Development dependency.** Analyzers run during build and produce no runtime overhead.

---

## Strategos.Ontology.Npgsql

A PostgreSQL pgvector-backed `IObjectSetProvider`, for ontologies that need a durable object set with vector search rather than the in-memory provider.

### Key Types

| Type | Purpose |
|------|---------|
| `PgVectorObjectSetProvider` | The pgvector-backed object set provider |
| `PgVectorOptions` | Connection, schema, and search configuration |
| `IterativeScanOptions` | Iterative-scan tuning for filtered vector search |

See the [Npgsql provider reference](src/content/docs/reference/ontology/npgsql.md) for schema and index setup.

### Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.Npgsql
```

---

## Strategos.Ontology.Embeddings

An OpenAI-compatible embedding provider for ontology vector search. It targets any endpoint that speaks the OpenAI embeddings API shape.

### Key Types

| Type | Purpose |
|------|---------|
| `OpenAiCompatibleEmbeddingProvider` | `IEmbeddingProvider` over an OpenAI-compatible endpoint |
| `OpenAiEmbeddingOptions` | Endpoint, model, and dimension configuration |

### Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.Embeddings
```

---

## Strategos.Ontology.MCP

The MCP tool surface for the ontology. It exposes exploration, querying, and action dispatch as MCP tools so external agents can read and act on the domain model.

### Key Types

| Type | Purpose |
|------|---------|
| `OntologyExploreTool` | Walks object types, links, and actions |
| `OntologyQueryTool` | Runs object-set queries, including hybrid retrieval |
| `OntologyActionTool` | Dispatches an ontology action |
| `OntologyToolDescriptor` | Tool descriptor carrying title, output schema, and annotations |

This package carries the tool definitions only. Registering them on a server is `Strategos.Ontology.MCP.Hosting`.

### Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.MCP
```

---

## Strategos.Ontology.MCP.Hosting

The hosting bridge. It adapts ontology tool descriptors into ModelContextProtocol server tools and registers them on an MCP server builder.

### Key Types

| Type | Purpose |
|------|---------|
| `OntologyMcpServerBuilderExtensions` | Registration surface on the MCP server builder |
| `OntologyServerToolFactory` | Builds server tools from ontology descriptors |
| `LoggingOntologyAuditSink` | Audit sink that writes tool invocations to the logger |

### Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.MCP.Hosting
```

---

## Strategos.Rag

> **Deprecated.** `Strategos.Rag` is deprecated. Use `Strategos.Ontology` with an `IObjectSetProvider` instead — it covers the same retrieval need with a typed object model, and it is where the pgvector and embedding work landed.

Vector store adapters for Retrieval-Augmented Generation patterns.

### Implemented Adapters

| Adapter | Status | Use Case |
|---------|--------|----------|
| `InMemoryVectorSearchAdapter` | Available | Development and testing |

### Key Interfaces

| Type | Purpose |
|------|---------|
| `IVectorSearchAdapter` | Interface for vector similarity search |
| `SearchResult` | Result containing content, score, and metadata |

### Installation

```bash
dotnet add package LevelUp.Strategos.Rag
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

### Ontology (Domain Modeling)

For a typed domain model with compile-time validation:

```bash
dotnet add package LevelUp.Strategos.Ontology
dotnet add package LevelUp.Strategos.Ontology.Generators
```

### Ontology with Durable Retrieval

Adds a pgvector-backed object set and an embedding provider:

```bash
dotnet add package LevelUp.Strategos.Ontology
dotnet add package LevelUp.Strategos.Ontology.Generators
dotnet add package LevelUp.Strategos.Ontology.Npgsql
dotnet add package LevelUp.Strategos.Ontology.Embeddings
```

### Ontology over MCP

Exposes the ontology to external agents:

```bash
dotnet add package LevelUp.Strategos.Ontology
dotnet add package LevelUp.Strategos.Ontology.MCP
dotnet add package LevelUp.Strategos.Ontology.MCP.Hosting
```

---

## Package Dependencies

```plaintext
Strategos (core)
+-- Strategos.Identity.Abstractions
+-- Strategos.Contracts
+-- MemoryPack

Strategos.Identity.Abstractions
+-- No dependencies

Strategos.Contracts
+-- No dependencies

Strategos.Generators
+-- Microsoft.CodeAnalysis.CSharp
+-- [Compile-time only; the identity abstractions ship inside the analyzer folder]

Strategos.Infrastructure
+-- Strategos
+-- BitFaster.Caching
+-- CommunityToolkit.HighPerformance
+-- MemoryPack
+-- Microsoft.Extensions.Caching.Memory
+-- Microsoft.Extensions.DependencyInjection.Abstractions
+-- Microsoft.Extensions.Logging.Abstractions

Strategos.Agents
+-- Strategos
+-- Microsoft.Extensions.AI
+-- Microsoft.Extensions.AI.Abstractions
+-- Microsoft.Extensions.DependencyInjection.Abstractions

Strategos.Agents.Mcp
+-- Strategos.Agents
+-- Microsoft.Extensions.AI
+-- Microsoft.Extensions.AI.Abstractions
+-- ModelContextProtocol

Strategos.Ontology
+-- Microsoft.Extensions.DependencyInjection.Abstractions
+-- Microsoft.Extensions.Logging.Abstractions

Strategos.Ontology.Generators
+-- Microsoft.CodeAnalysis.CSharp
+-- [Compile-time only]

Strategos.Ontology.Npgsql
+-- Strategos.Ontology
+-- Npgsql
+-- Pgvector
+-- Microsoft.Extensions.Logging.Abstractions
+-- Microsoft.Extensions.Options

Strategos.Ontology.Embeddings
+-- Strategos.Ontology
+-- Microsoft.Extensions.Http
+-- Microsoft.Extensions.Logging.Abstractions
+-- Microsoft.Extensions.Options

Strategos.Ontology.MCP
+-- Strategos.Ontology
+-- Microsoft.Extensions.Logging.Abstractions

Strategos.Ontology.MCP.Hosting
+-- Strategos.Ontology.MCP
+-- ModelContextProtocol

Strategos.Rag  [deprecated]
+-- Strategos.Agents
+-- Microsoft.Extensions.DependencyInjection.Abstractions
```
