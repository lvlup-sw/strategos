# claim-mcp-resulttype-complete — Every constructed CallToolResult sets 2026-07-28 resultType

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 27, 28, 29, 53, 55, 66, 126, 127, 145, 147, 149, 150, 151
Confidence: high for the protocol claim; medium that every sibling mapper is covered

## Ledger

| | |
|---|---|
| **Claim** | Hosting pins MCP SDK 2.2.0 so every constructed `CallToolResult` (`MapTraversalResult`, `ErrorResult`, and sibling mappers) sets `resultType` with 2026-07-28 semantics. Strategos always emits `"complete"` on constructed results (no MRTR `input_required`). INV-3 denies the pre-2026-07-28 absent-field shape. The wire form, not only the in-memory default, carries `"complete"`. |
| **Scope** | `OntologyServerToolFactory` constructions; Hosting `VersionOverride`; INV-3 / deterministic-checks 3.3–3.4; MCP client wire. |
| **Consequence** | A 2026-07-28 client that requires `resultType` treats an omitted field as the pre-2026-07-28 shape. CPM 1.3.0 has no `ResultType`; dropping the Hosting pin makes the assignment uncompilable or silently omitted. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | SDK client/server round-trip that serializes `CallToolResult` for every factory mapper (traverse, query, action, explore, abstain, error) and asserts the JSON contains `"resultType":"complete"`. A structural or pack test that Hosting's production csproj pin is 2.2.0 (tests' own override is the wrong subject). |
| **Why not cheaper** | Rung 2 can require the property to exist under 2.2.0 and cannot require every construction to assign it or the serializer to emit it. Rung 3 greps (deterministic-checks 3.4) are satisfied by a comment or a sibling identifier in the same file. Rung 4 in-memory `ResultType` can be an SDK default while the wire omits the field. |
| **Failure signal** | Protocol clients. INV-3 is a human checklist unless someone runs it. |
| **Rollback** | Revert constructions and downgrade the Hosting pin. Clients that already expect `resultType` then see the old omission. |
| **Lenses** | 6 Claim Derivation (claims 53 / 27 / 126 / 151). Survey lenses 1, 4, 5. |

**Open questions:**

- Is `ErrorResult` with `resultType: complete` protocol-legal? Survey L7 / run-wide question. The factory comment (claim 126) says always `complete` including `isError: true`.
- Are action / explore / abstain paths covered for *wire* `resultType`, or only traverse (P26) plus query in-memory (P27)?
- Hosting packaging tests do not assert the 2.2.0 pin (P30). CPM remains 1.3.0.

## Evidence

Highest-stakes CHANGELOG (`CHANGELOG.md:187–189`) and factory comment (`OntologyServerToolFactory.cs:51–55`, claim 126). Plan T4 (claims 27–29). INV-3 ACs (claims 145, 147, 149) and deterministic-checks 3.3–3.4 (claims 150–151). Claim 55 is the INV-3 swap: deny the pre-2026-07-28 response shape instead of flagging the icon gap. Claim 28 is the "confirm the property on the installed SDK" process lead — the obligation is that the installed pin exposes `ResultType`, not that someone remembered to look.

Existing-proof P26 is the one serialized round-trip (traverse). P27 is query in-memory only. P32 INV-3 greps are not CI. P33 is two object initializers at `OntologyServerToolFactory.cs:384–386` and `:410–412`.

Claim 66's "and optional Icons" half lives on `claim-icons-null-when-unset`.
