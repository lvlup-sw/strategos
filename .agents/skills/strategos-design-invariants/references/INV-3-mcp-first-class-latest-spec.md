# INV-3: MCP is first-class and tracks the latest protocol spec (2026-07-28)

Strategos's MCP layer (`Strategos.Ontology.MCP`) targets the *current* MCP protocol spec and leverages its modern features: structured tool descriptors with `OutputSchema`, `_meta` envelopes on every response, `ToolAnnotations`, capability hints. It does not write to a lowest-common-denominator subset that older clients would also accept.

When the MCP spec advances, the project upgrades in full rather than maintaining a compatibility shim for the older spec.

## Acceptance questions

- Does every new MCP response record carry `[JsonPropertyName("_meta")] ResponseMeta Meta { get; init; }`?
- Does every new tool descriptor expose `OutputSchema`?
- Does the proposal introduce a code path that conditionally omits `_meta` or `OutputSchema` "for older clients"?
- If a new feature lands in the MCP spec, is the design upgrading to use it, or working around it?
- Does the proposal introduce a magic string `"2024-11-05"` (or any pre-2026-07-28 revision)?

## Repo-grounded checks

- `src/Strategos.Ontology.MCP/ToolAnnotations.cs:4` — version marker comment "MCP 2026-07-28 tool annotations"
- `src/Strategos.Agents.Mcp/README.md:9` — package README pin; this file ships inside the nupkg, so a stale revision here is the most visible of the three
- `src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs:7,24` — `OutputSchema { get; init; }` on every descriptor
- `src/Strategos.Ontology.MCP/{ActionToolResult,QueryResult,ExploreResult,ValidateResult}.cs` — every result record carries `[JsonPropertyName("_meta")] ResponseMeta Meta`
- `src/Strategos.Ontology.MCP/OntologyServerCapabilitiesProvider.cs:6,21` — populates `capabilities._meta.ontologyVersion`
- `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs:54,74,93,110` — generates `OutputSchema` per tool via `JsonSchemaHelper`

## Cross-cutting overlap

**axiom_overlap:** `DIM-3` (primary, Contracts — protocol shape) · `DIM-6` (Architecture — MCP as peer transport, not subordinate adapter)

## External grounding

- [MCP specification](https://modelcontextprotocol.io/specification) — current revision 2026-07-28
- Structured `_meta` envelopes, typed `OutputSchema` on tool descriptors, and richer capability advertisement arrived in the 2025-11-25 revision and carry forward unchanged into 2026-07-28; INV-3 mandates Strategos use all of these features rather than the implicit superset of older clients.
- `ToolAnnotations` is the same shape in 2025-11-25 and 2026-07-28 (`title`, `readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`), so moving the pin between those two revisions is a docstring change and not a code change. Two genuine gaps against the newer revision remain open and are tracked separately.

## Severity guide

- **HIGH:** A code path downgrades the response to a pre-2026-07-28 shape "for compatibility" — ossifies the LCD subset and erodes future protocol fidelity.
- **MEDIUM:** A new response record omits `_meta` or `OutputSchema`; modern clients lose information silently.
- **LOW:** A comment or doc refers to MCP in the abstract without naming the spec revision.

## Worked example

**Violation:**

```csharp
public sealed record QueryResult(IReadOnlyList<ObjectSummary> Items);
// No _meta envelope. Modern clients expect ResponseMeta on every result, and capability hints
// flow through it. Consumers can't read pagination cursors or trace IDs.
```

**Fix:**

```csharp
public sealed record QueryResult(IReadOnlyList<ObjectSummary> Items)
{
    [JsonPropertyName("_meta")]
    public ResponseMeta Meta { get; init; } = new();
}
```

If a genuine spec-version negotiation is needed in the future, add a single `McpProtocolVersion = "2026-07-28"` constant and gate the downgrade path explicitly — never via implicit omission.
