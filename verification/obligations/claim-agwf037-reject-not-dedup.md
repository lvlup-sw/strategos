# claim-agwf037-reject-not-dedup — Duplicate PermitTrigger is rejected, not first-wins-deduped

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 22, 23, 46, 47, 48, 49, 67, 68, 70, 115, 116, 117, 118, 119, 120
Confidence: high

## Ledger

| | |
|---|---|
| **Claim** | Two `PermitTrigger` declarations on one diagnostic-fork edge that name the same closed trigger are rejected as AGWF037 on both the C# extractor and the JSON-import bridge. The edge is not first-wins-deduped. Distinct triggers stay clean and still lower a saga. C# twins also emit no saga. |
| **Scope** | `DiagnosticForkExtractor`, `DiagnosticForkModel.Create`, JSON import (`WireToModelBridge` / `EmitWorkflowSources`), `WorkflowDiagnostics` AGWF037, catalog entry. |
| **Consequence** | First-wins silently drops one evidence schema. Two same-trigger declarations can carry different schemas (claims 22, 48, 68, 117). JSON import has no CS0152 to fail closed. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | Generator-driven C# twin and JSON-import twin that assert AGWF037 *and* that both evidence schemas remain visible as a reject (not a post-dedup diagnostic). Distinct-trigger negatives. IR `Create` throw is a floor, not the diagnostic. |
| **Why not cheaper** | CS0152 is a different C# mechanism and does not exist on JSON import. The compiler cannot see two fluent `PermitTrigger` calls as one closed-trigger domain. Structural analysis can prove a uniqueness check exists and cannot prove it rejects rather than dedups. |
| **Failure signal** | Compile-time AGWF037. A silent first-wins has no signal. |
| **Rollback** | Revert AGWF037 wiring. A published Contracts 0.7.0 catalog that already names AGWF037 does not reverse for upgraded Exarchos consumers. |
| **Lenses** | 6 Claim Derivation (claims 22 / 47 / 49 / 116). Survey lenses 1, 3, 4: new reject, not a deleted first-wins helper. |

**Open questions:**

- Do the C#/import paths convert `Create`'s `ArgumentException` into AGWF037, or can they CS8785 instead? Existing-proof P13 flags that class.
- Does any path first-wins-dedup *before* `Create` and still report AGWF037? P9/P14 do not re-read retained evidence fields.
- Empty trigger names are skipped (survey backbone §5). Is that a hole in "each trigger at most once"?
- Dual uniqueness authorities (runtime `HashSet<ForkTrigger>` vs generator string set) — do they disagree on closed-trigger identity?

## Evidence

Highest-stakes plan T2 / CHANGELOG (`CHANGELOG.md:179–182`). Claim 6 is the wave-inclusion flag for #156.2; it is not a second invariant. Claim 118 is the C# twin + no-saga AC; claim 119 is the distinct-trigger AC; claim 120 is the catalog remediation.

Existing-proof P9–P14: C# generator twin, extractor unit, IR floor, JSON import. Subject binding is yes for this revision's generator on C# and JSON-import compositions (P9, P14). P11 is syntax-only and does not bind the generator. `EachRejectedCase_HasItsOwnDistinctDiagnosticId` enumerates AGWF027–034 only — AGWF037 is absent from that uniqueness sweep.

Survey: AGWF037 extractor returns false; `Create` throws; AGWF037 *does* join `hasErrors` and suppress saga emission (unlike AGWF035).

Line anchors at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `DiagnosticForkModel.cs:129`, `:143–144`; `AgwfCatalog.tsp:364`; `CHANGELOG.md:179–182`.
