---
title: Ontology API
sidebar:
  order: 1
---

`Strategos.Ontology` is a type-safe semantic graph for domain modelling: Object Types, Properties, Links, Actions, Events, Lifecycles, and Interfaces declared in C# and validated at compile time by Roslyn source generators. Agents and dispatchers query the composed graph at runtime through `IOntologyQuery`. This section documents the public API surface for each package — for task-oriented walkthroughs see the [Ontology guide](/guide/ontology/).

## Package map

| Package | Contains | Use when |
|---|---|---|
| `LevelUp.Strategos.Ontology` | `DomainOntology`, fluent builders (`IOntologyBuilder`, `IObjectTypeBuilder<T>`, ...), descriptor records, `IOntologyQuery`, `IObjectSetProvider`, `IObjectSetWriter`, expression nodes, `IActionDispatcher`, `IEmbeddingProvider`, `IOntologySource`, `OntologyGraph` | You define domain ontologies or write a runtime consumer (e.g. an MCP tool, a dispatcher decorator). |
| `LevelUp.Strategos.Ontology.Generators` | Roslyn incremental generator emitting validation diagnostics (`AONT001`–`AONT208`) and the source-generated `IOntologyQuery` implementation. | Always — referenced as an `Analyzer` by the core package's consumers. |
| `LevelUp.Strategos.Ontology.Npgsql` | `PgVectorObjectSetProvider`, `PgVectorOptions`, schema-creation helpers. Implements `IObjectSetProvider` + `IObjectSetWriter` against PostgreSQL with the `pgvector` extension. | You run similarity search against a PostgreSQL instance with `pgvector`. |
| `LevelUp.Strategos.Ontology.Embeddings` | OpenAI-compatible `IEmbeddingProvider` implementation. | You need a production embedding provider without writing your own. |
| `LevelUp.Strategos.Ontology.MCP` | Progressive disclosure MCP integration (`OntologyToolDescriptor`, `_meta` envelope). | You expose ontology actions over MCP. |

## Reference pages

The pages below are sourced directly from the `.cs` files under `src/Strategos.Ontology/` and `src/Strategos.Ontology.Npgsql/`. Each page lists members with signatures, return types, and the XML-doc summary attached to the member.

- [`IOntologyQuery`](/reference/ontology/api/ontology-query/) — runtime query surface (types, actions, links, lifecycle, blast radius, pattern violations).
- [`IObjectSetProvider` & expressions](/reference/ontology/api/object-set-provider/) — read/write paths against an object backend and the expression tree they consume.
- [`IEmbeddingProvider`](/reference/ontology/api/embedding-provider/) — embedding generation contract.
- [Action Dispatcher](/reference/ontology/api/dispatcher/) — `IActionDispatcher`, `DispatchReadOnlyAsync`, `ActionResult`, `IActionDispatchObserver`.
- [`IOntologySource` & Builder](/reference/ontology/api/source/) — external-source extension contract and the `IOntologyBuilder` DSL.
- [Graph Versioning](/reference/ontology/graph-versioning/) — `OntologyGraph.Version` hash and cache invalidation.
- [`Strategos.Ontology.Npgsql`](/reference/ontology/npgsql/) — pgvector-backed provider, `PgVectorOptions`, `EnsureSchemaAsync`.

For the broader architectural picture (Palantir concept mapping, source-generator pipeline, agent query interface), see [Platform Architecture §4.14](/reference/platform-architecture/).

## Where to go from here

- **Building your first ontology:** start with [Getting Started with Ontology](/guide/ontology/).
- **Similarity search setup:** [Similarity search guide](/guide/ontology/similarity-search/) walks through `IEmbeddingProvider` + `pgvector` end to end.
- **Diagnostic code lookup:** [Diagnostics index](/reference/diagnostics/) lists every `AONT` code with severity and fix.
