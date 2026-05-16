---
title: Graph Versioning
sidebar:
  order: 7
---

`OntologyGraph.Version` is a deterministic SHA-256 hash over the structural fields of a frozen graph. The hash is computed by `OntologyGraphHasher` at the end of `OntologyGraphBuilder.Build()` and exposed as a 64-character lowercase hex string (no algorithm prefix). Identical DSL input produces an identical hash across processes and machines. MCP responses surface it as `_meta.ontologyVersion` (with a `"sha256:"` prefix added at the wire boundary) so consumers can invalidate cached schema views when the graph changes shape.

Namespace: `Strategos.Ontology`. Source: `src/Strategos.Ontology/Internal/OntologyGraphHasher.cs`, `src/Strategos.Ontology/OntologyGraph.cs`.

Implements issue #44.

## Property

| Member | Signature | Notes |
|---|---|---|
| `Version` | `string Version { get; }` | 64-char lowercase hex SHA-256 over the structural canonicalisation. Computed once at `OntologyGraph` construction. |

## What is hashed

The hasher canonicalises the graph into a stable byte stream (length-prefixed UTF-8 strings, sorted under `StringComparer.Ordinal` at every level) and hashes that stream. Included fields:

- **Domains.** All `Domains[*].DomainName`, sorted.
- **Object types** (sorted by `DomainName`, then `Name`). Per type:
  - `Name`, `DomainName`, `ParentTypeName`, `Kind` (`ObjectKind` enum), `KeyProperty.Name`.
  - `Properties[*]` (sorted by `Name`): `Name`, `Kind`, `PropertyType.FullName`, `IsRequired`, `VectorDimensions`.
  - `Actions[*]` (sorted by `Name`): `Name`, `AcceptsType.FullName`, `ReturnsType.FullName`, `BindingType`, `BoundWorkflowName`, `BoundToolName`, `BoundToolMethod`, `Preconditions[*]` (`Description`, `Expression`, `Kind`, `LinkName`, `Strength`), `Postconditions[*]` (`Kind`, `PropertyName`, `LinkName`, `EventTypeName`, `TargetTypeName`).
  - `Links[*]` (sorted by `Name`): `Name`, `TargetTypeName`, `Cardinality`, `EdgeProperties[*]` (`Name`, `Kind`).
  - `Events[*]` (sorted by `EventType.FullName`): `EventType.FullName`, `Severity`, `MaterializedLinks`, `UpdatedProperties`.
  - `Lifecycle` (when present): `PropertyName`, `StateEnumTypeName`, `States[*]` (`Name`, `IsInitial`, `IsTerminal`), `Transitions[*]` (`FromState`, `ToState`, `TriggerActionName`, `TriggerEventTypeName`).
  - `ImplementedInterfaces[*].Name`.
  - `InterfacePropertyMappings[*]` (sorted by `InterfaceName`, `TargetPropertyName`, `SourcePropertyName`): `InterfaceName`, `TargetPropertyName`, `SourcePropertyName`.
  - `InterfaceActionMappings[*]` (sorted by `InterfaceActionName`, `ConcreteActionName`): `InterfaceActionName`, `ConcreteActionName`.
- **Interfaces** (sorted by `Name`): `Name`, `Properties` (`Name`, `Kind`, `PropertyType.FullName`), `Actions` (`Name`, `AcceptsTypeName`, `ReturnsTypeName`).
- **Cross-domain links** (sorted by `SourceDomain`, `SourceObjectType.Name`, `Name`, `TargetDomain`, `TargetObjectType.Name`, `Cardinality`): `SourceDomain`, `SourceObjectType.Name`, `Name`, `TargetDomain`, `TargetObjectType.Name`, `Cardinality`, `EdgeProperties[*]` (`Name`, `Kind`).
- **Workflow chains** (sorted by `WorkflowName`, then consumed/produced type identity): `WorkflowName`, `ConsumedType` (FullName / SymbolKey / Name fallback), `ProducedType` (same fallback chain).

`ActionPrecondition.Description` is included because it is the precondition's identity / sort key — distinct from per-action free-form documentation prose.

## What is deliberately NOT hashed

Excluded so the hash remains a structural-only fingerprint and documentation churn does not bust caches that downstream consumers maintain to track schema shape:

| Excluded | Rationale |
|---|---|
| `Description` text on actions, links, properties, lifecycle states/transitions, events, cross-domain links, interface actions | Documentation prose changes do not affect dispatch behaviour or the surface agents reason about. |
| `OntologyGraph.Warnings` | Advisory, non-structural diagnostic strings. |
| `OntologyGraph.ObjectTypeNamesByType` | Derived index from `ObjectTypes`; mutation without mutating the underlying list is impossible. |
| `PropertyDescriptor.IsComputed` / `DerivedFrom` / `TransitiveDerivedFrom` | Captured implicitly via `PropertyKind == Computed`; the derivation chain is reconstructable from `Properties`. |
| `ExternalLinkExtensionPoints.MatchedLinkNames` | Derived during build, not user-authored input. |

## When the hash changes

Any structural mutation of the included fields produces a new hash. Examples:

- Adding, removing, or renaming an object type, property, link, action, event, lifecycle state, or transition.
- Changing a property's `PropertyType`, `IsRequired`, `VectorDimensions`, or `Kind`.
- Rebinding an action's `BoundWorkflowName`, `BoundToolName`, or `BoundToolMethod` (a dispatch-routing change).
- Adding, removing, or modifying a precondition or postcondition.
- Changing a link's `Cardinality` or `EdgeProperties`.
- Changing the `(consumed, produced)` shape of a workflow chain.

Examples that do *not* change the hash:

- Editing a `Description` string anywhere it appears.
- Reordering registration calls (canonicalisation sorts every collection).
- Adding entries to `OntologyGraph.Warnings`.

## How consumers should react

Treat `Version` as an opaque cache key. When a downstream cache (a planner's tool list, an agent's action-availability snapshot, a UI's type browser) is keyed by the hash and the current `OntologyGraph.Version` does not match the cached value, invalidate and rebuild. Two graphs with the same hash are guaranteed to share every hashed structural field; two graphs with different hashes differ in at least one such field.

The MCP surface emits `_meta.ontologyVersion` on every tool response — MCP clients should compare against their last-seen value and invalidate cached descriptors on mismatch. See [MCP integration guide](/guide/ontology/mcp-integration/) for the `_meta` envelope shape.

## Determinism guarantees

Three properties make the hash stable across runs:

1. **Sorted collections at every level.** Domains, object types, properties, actions, links, events, interfaces, cross-domain links, and workflow chains are all sorted under `StringComparer.Ordinal`. Registration order does not influence the hash.
2. **Length-prefixed UTF-8 framing.** Every string written into the byte stream is prefixed with its UTF-8 byte length, preventing adjacent-field concatenation aliasing.
3. **Tie-breakers cover every structurally-significant field.** `WorkflowChain` and `ResolvedCrossDomainLink` carry full tie-breaker chains so collections with identical primary keys but differing secondary fields sort identically across builders.
