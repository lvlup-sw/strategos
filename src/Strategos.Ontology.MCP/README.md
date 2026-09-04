# Strategos.Ontology.MCP

MCP tool surface for Strategos ontology. Exposes ontology exploration, querying, and action dispatch as MCP tools for AI agent integration.

## Installation

```bash
dotnet add package LevelUp.Strategos.Ontology.MCP
```

Requires `Strategos.Ontology` (included as a dependency).

## MCP Tools

Three tools are auto-generated from your ontology definitions:

| Tool | Purpose |
|------|---------|
| `ontology_explore` | Browse domains, object types, actions, links, events, interfaces |
| `ontology_query` | Query object sets with filters, link traversal, interface narrowing |
| `ontology_action` | Dispatch actions (single or batch) through `IActionDispatcher` |

## Tool Discovery

`OntologyToolDiscovery` generates tool descriptors enriched with constraint summaries:

```csharp
var discovery = new OntologyToolDiscovery(ontologyGraph);
IReadOnlyList<OntologyToolDescriptor> tools = discovery.Discover();
```

Each action tool descriptor includes `ActionConstraintSummary` records with hard/soft constraint counts, enabling agents to assess action availability directly from tool discovery.

## Features

- **Schema Exploration**: 7 scopes (domains, objectTypes, actions, links, events, interfaces, workflowChains) with BFS link traversal
- **Object Queries**: Composable filter, link traversal, interface narrowing, and include expressions
- **Action Dispatch**: Single-object or batch execution routed through `IActionDispatcher`
- **Constraint Summaries**: Hard/soft constraint counts embedded in tool descriptions for zero-shot agent reasoning
- **Python Stubs**: `OntologyStubGenerator` produces `.pyi`-style type stubs for agent tooling

Direct action-tool callers must bind the authenticated caller explicitly:

```csharp
var principal = new ActionPrincipal("User", "user-42");
var result = await actionTool.ExecuteAsync(
    principal,
    objectType: "Order",
    action: "submit",
    request: request,
    objectId: "order-7");
```

Passing no principal is refused before the dispatcher is called. The MCP hosting package resolves this value from the authenticated request automatically.

## License

MIT
