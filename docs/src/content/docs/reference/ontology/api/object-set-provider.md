---
title: IObjectSetProvider & expressions
sidebar:
  order: 3
---

An object set is a queryable, typed collection of instances of an ontology Object Type. `IObjectSetProvider` is the read path and `IObjectSetWriter` is the write path; both are backed by an `ObjectSetExpression` tree built from a fluent root (`IOntologyQuery.GetObjectSet<T>(name)`) and then executed against a backend implementation (in-memory for tests, pgvector for production).

Namespace: `Strategos.Ontology.ObjectSets`. Source: `src/Strategos.Ontology/ObjectSets/`.

## IObjectSetProvider

| Member | Signature |
|---|---|
| `ExecuteAsync<T>` | `Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default) where T : class` |
| `StreamAsync<T>` | `IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default) where T : class` |
| `ExecuteSimilarityAsync<T>` | `Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(SimilarityExpression expression, CancellationToken ct = default) where T : class` |

`ExecuteAsync` materializes results into an `ObjectSetResult<T>`; `StreamAsync` returns the same data as an async sequence without buffering; `ExecuteSimilarityAsync` consumes a `SimilarityExpression` and returns scored results sorted by similarity descending.

## IObjectSetWriter

| Member | Signature |
|---|---|
| `StoreAsync<T>` (default-named) | `Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class` |
| `StoreBatchAsync<T>` (default-named) | `Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class` |
| `StoreAsync<T>` (explicit-name) | `Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class` |
| `StoreBatchAsync<T>` (explicit-name) | `Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class` |

The default-named overloads target the descriptor resolved by convention for `T` (typically `typeof(T).Name`). When the same CLR type is registered against multiple descriptor names (Strategos 2.4.1 multi-registration), use the explicit-name overloads — the default overloads throw when resolution is ambiguous.

## Expression hierarchy

`ObjectSetExpression` is the abstract base of the expression tree. Every node carries the CLR type it produces (`ObjectType`) and the descriptor name the query targets (`RootObjectTypeName`, derived by walking back to the root).

```
ObjectSetExpression (abstract)
├── RootExpression
├── FilterExpression
├── InterfaceNarrowExpression
├── RawFilterExpression
├── IncludeExpression
├── SimilarityExpression
└── TraverseLinkExpression
```

| Node | Purpose | Key fields |
|---|---|---|
| `RootExpression` | Starting point. Built by `IOntologyQuery.GetObjectSet<T>(name)`. | `ObjectTypeName` (descriptor the root was dispatched against) |
| `FilterExpression` | Represents a typed `Where(...)` predicate. | `Source`, `Predicate` (LambdaExpression) |
| `InterfaceNarrowExpression` | Narrows to objects implementing a specific interface. | `Source`, `InterfaceType` |
| `RawFilterExpression` | Unprocessed string filter (e.g. from MCP tool input). | `Source`, `FilterText` |
| `IncludeExpression` | Declares which data facets to include in results. | `Source`, `Inclusion` (`ObjectSetInclusion`) |
| `SimilarityExpression` | Vector/semantic similarity search. Provider embeds the text. | `QueryText`, `TopK`, `MinRelevance`, `Metric` (`DistanceMetric`), `EmbeddingPropertyName`, `QueryVector`, `Filters` |
| `TraverseLinkExpression` | Follows a declared link to a related type. | `Source`, `LinkName` — `ObjectType` reflects the linked type |

### `RootObjectTypeName` and walk-to-root

Every node except `TraverseLinkExpression` exposes `RootObjectTypeName` via the virtual base, which walks the `Source` chain back to the root and returns its `ObjectTypeName`. `TraverseLinkExpression` overrides the walk because traversal breaks the root chain — after a link traversal the query targets the linked type's descriptor, not the source root's. Under Option X (multi-registered types cannot be link targets, enforced by `AONT041`), this is always unambiguous.

### SimilarityExpression validation

The constructor enforces the following invariants:

- `source`, `queryText` must be non-null (throws `ArgumentNullException`).
- `topK >= 1` (throws `ArgumentOutOfRangeException`).
- `0.0 <= minRelevance <= 1.0` (throws `ArgumentOutOfRangeException`).

The `Metric` field is a `DistanceMetric` (`Cosine`, `L2`, `InnerProduct`). `EmbeddingPropertyName` selects the embedding property when an object has multiple; `QueryVector` lets callers supply a precomputed embedding instead of having the provider embed `QueryText`; `Filters` is an optional structured filter dictionary applied alongside the similarity search.

## Working with the expression tree

Most callers do not construct expression nodes directly — `ObjectSet<T>` and `SimilarObjectSet<T>` provide fluent setters that produce the right nodes. Direct construction is exposed for backends that translate the tree (`PgVectorObjectSetProvider`'s `ExpressionTranslator`) and for test doubles that inspect nodes by type.
