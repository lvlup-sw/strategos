# Strategos.Ontology.MCP.Hosting

MCP server hosting bridge for Strategos ontology. Adapts ontology tool descriptors into [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) server tools and registers them on an MCP server builder.

## Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.MCP.Hosting
```

Brings in `LevelUp.Strategos.Ontology.MCP` (the SDK-free core) and the `ModelContextProtocol` server SDK.

## Why a separate package

The core `Strategos.Ontology.MCP` package stays free of any `ModelContextProtocol` dependency so it can be referenced by AOT-published and SDK-agnostic consumers. This companion package carries the server-SDK bridge.

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.MCP.Hosting;

builder.Services
    .AddOntology(o => o.AddDomain<MyDomain>())   // registers OntologyGraph + IObjectSetProvider
    .AddMcpServer()
    .AddOntologyTools();                          // resolves the graph + providers from DI
```

`AddOntologyTools` discovers the four ontology tools (`ontology_explore`, `ontology_query`, `ontology_action`, `ontology_validate`) and registers them as **provider-bound** MCP server tools: each tool dispatches against the host's DI-resolved `IObjectSetProvider` (and, where applicable, `IActionDispatcher`, `IEventStreamProvider`, `IOntologyQuery`), resolved per call from the request's `IServiceProvider`. So `ontology_query` executes against the configured provider and returns real rows.

The no-argument overload resolves the `OntologyGraph` from the service collection. Pass a graph explicitly when it is not registered there:

```csharp
builder.Services
    .AddMcpServer()
    .AddOntologyTools(ontologyGraph);
```

To obtain the adapted tools directly:

```csharp
IEnumerable<McpServerTool> tools = OntologyServerToolFactory.CreateServerTools(ontologyGraph);
```

Each tool preserves the originating descriptor's `OutputSchema`, annotations, and action constraint summaries (carried in the tool's `_meta`). The query path requires an `IObjectSetProvider` in the host's service provider; register one (e.g. via `AddOntology`) before serving traffic.

## License

MIT
