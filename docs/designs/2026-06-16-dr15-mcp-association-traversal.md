# DR-15 (#125) — MCP association + instance-level traversal surface

Status: in progress (branch `feat/dr14-provider-bound-dispatch`, continues DR-14)

## Framing

Expose the live ontology edge layer (associations + instance-anchored traversal,
DR-3/DR-4/DR-10) through the MCP tool surface so an agent can: distinguish
association objects/edges from plain objects in results, list the link/association
metadata it needs to drive a traversal, walk from a specific instance across an
association to a far endpoint (with edge-attribute filtering), and relate/unrelate
through the existing action gate.

Provider-agnostic: every dispatch goes through the public
`IObjectSetProvider.ExecuteAsync<object>(ObjectSetExpression)` and `OntologyGraph`
metadata. No `Npgsql` internals; no `InMemoryObjectSetProvider.GetRelations`
(internal). The traversal depth bound is the MCP layer's own constant
`OntologyTraversalLimits.MaxDepth = 3` (matching the documented join-chain budget),
NOT a reference to DR-12's `JoinChainDepthBudget`.

## INV-3 (MCP 2025-11-25) hard constraints

- Every result record carries `[JsonPropertyName("_meta")] ResponseMeta Meta` and
  every tool descriptor carries `OutputSchema`. New result shapes extend the
  existing `_meta` envelope, never omit it.
- Traversal-tool provenance keys go under `_meta` with the prefix
  `sw.lvlup.strategos/` (never `mcp/` or any modelcontextprotocol-reserved key).
- Malformed tool args → `isError: true` CallToolResult (SEP-1303), NOT a thrown
  JSON-RPC protocol error.
- Large subgraphs → a `resource_link` content block + opaque cursor (cursor has its
  own schema; it is not a far-endpoint row).
- INV-8: outputs name targets by descriptor name; a SymbolKey-only target MUST NOT
  leak a CLR type name.

## Tasks

### T16 — Association/endpoint result shape
Add an association/edge result branch to `QueryResultUnion` (new `resultKind`
discriminator value) and an association indicator on explore items, DISTINCT from a
plain object row. Preserve `_meta` + `OutputSchema`.

### T17 — Descriptor/link/association LISTING tool
A read-only metadata tool listing object types, links, and associations (the DR-10
metadata an agent needs before traversing). SymbolKey-only targets surface their
descriptor name / SymbolKey, never a CLR name.

### T18 — Instance-anchored traversal tool
Closed-vocabulary inputs (link name ∈ graph, integer depth ≤ `MaxDepth`, direction
enum). Builds `Root → RawFilter(anchor id) → TraverseLink(edge) → [RawFilter(edge
attr)] → TraverseLink(role)` and executes via `ExecuteAsync<object>`. Opaque cursor;
self-imposed row budget → `resource_link`; malformed args → `isError:true`;
`_meta` provenance under `sw.lvlup.strategos/`. Edge-attribute filter parity with the
in-process `ObjectSet` path (`ObjectSetTraversalTests` is the reference).

### T19 — Action-gated write path
relate/unrelate dispatch stays through `OntologyActionTool` (the `IActionDispatcher`
pre-wired in DR-14), never a direct `IObjectSetWriter` call from a read tool.

## Reference

In-process traversal contract:
`src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTraversalTests.cs`
(`InstanceAnchoredTraversalTests`). Expression model:
`src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs`. Relate-store read:
`InMemoryObjectSetProvider.GetRelations` (internal — not used by MCP).
