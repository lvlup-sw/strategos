# claim-aont205-ingested-only — AONT205 retargets to mechanical ingestion

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 33, 34, 57, 77, 129, 130, 131, 132, 133
Confidence: high for the runtime invariant retarget; low that TypeSpec/JSON ingest assigns `HandAuthoredContract`

## Ledger

| | |
|---|---|
| **Claim** | AONT205 rejects intent-only fields only when `DescriptorSource.Ingested`. `HandAuthored` and `HandAuthoredContract` pass through. A contract-authored action survives graph merge; mechanically ingested intent on Actions/Events/Lifecycle/InterfaceActionMappings/ExternalLinkExtensionPoints still fails AONT205. |
| **Scope** | `IngestedIntentInvariant`; `OntologyGraphBuilder` freeze and merge; runtime `OntologyCompositionException` id AONT205. |
| **Consequence** | Contract-authored actions fail the build after merge, or ingested intent-only collections silently survive. Who fails the build changes (stage 0 S4). |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `ApplyDelta` / `Build` / merge tests that ingested+intent throws AONT205 and `HandAuthoredContract`+Actions does not. A producer-path test that TypeSpec/JSON ingest tags `HandAuthoredContract` is missing — that is the open file `claim-handauthoredcontract-ingest-assignment`. |
| **Why not cheaper** | Enum member `= 2` (rung 2) does not retarget the check. Structural analysis can find `Source != Ingested` and cannot decide the field list or merge interaction. |
| **Failure signal** | Runtime `OntologyCompositionException` containing `AONT205`. The Roslyn descriptor has no `ReportDiagnostic` site (survey L4 / existing-proof P38), so compile-time AONT205 is not a signal. |
| **Rollback** | Revert the invariant predicate. Enum value `2` if published is a compatibility event (`claim-handauthoredcontract-additive`). |
| **Lenses** | 6 Claim Derivation (claims 33 / 57 / 129 / 132). Survey lenses 1, 4, 5. |

**Open questions:**

- Does merge intend to collapse `HandAuthoredContract` → `HandAuthored`? P37 asserts `Source == HandAuthored` after merge. If CHANGELOG/docs claim provenance is preserved, that half is false (`claim-handauthoredcontract-ingest-assignment`).
- Is compile-time AONT205 supposed to fire, or is the runtime invariant the only enforcement? No report site found.

## Evidence

Highest-stakes CHANGELOG (`CHANGELOG.md:193–194`, claim 57) and plan T5 (claims 33–34). Descriptor comments (claims 129–132). Merge test summary (claim 133). Commit `662f0d1` (claim 77).

Existing-proof P35–P37 bind `ApplyDelta` and graph-freeze of this revision. P36 descriptors are constructed in the test — not TypeSpec/JSON ingest. P37 surviving-action claim is tested; provenance "stays HandAuthoredContract" is not what line 87 asserts. P38: AONT205 Roslyn descriptor exists, no report site.

The "so TypeSpec / JSON contract-authored actions survive graph merge" clause of claim 57 is not validated here. It is the open question `claim-handauthoredcontract-ingest-assignment`.
