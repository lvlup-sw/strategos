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
    .AddMcpServer()
    .AddOntologyTools(ontologyGraph);
```

`AddOntologyTools` discovers the four ontology tools (`ontology_explore`, `ontology_query`, `ontology_action`, `ontology_validate`) and registers them as callable MCP server tools.

To obtain the adapted tools directly:

```csharp
IEnumerable<McpServerTool> tools = OntologyServerToolFactory.CreateServerTools(ontologyGraph);
```

Each tool preserves the originating descriptor's `OutputSchema`, annotations, and action constraint summaries (carried in the tool's `_meta`).

## License

MIT
