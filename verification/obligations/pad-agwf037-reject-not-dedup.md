# pad-agwf037-reject-not-dedup

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 6, 22, 23, 46–49, 67, 68, 70, 115–120

| | |
|---|---|
| **Claim** | Two `PermitTrigger` declarations of the same closed trigger on one edge are rejected (AGWF037), not first-wins-deduped, on C# extract and JSON import, and no saga is emitted. |
| **Scope** | `DiagnosticForkExtractor.TryParseDiagnosticFork`, `DiagnosticForkModel.Create` / `FindDuplicateTriggerNames`, `WireToModelBridge` rejection scan, `hasErrors` in `WorkflowIncrementalGenerator`. |
| **Consequence** | First-wins would drop one evidence schema. A reject that still emits a saga would leave CS0152 as the closed-fail. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `Extract_DuplicatePermitTrigger_RejectsEdgeAndReportsAgwf037`; `DuplicatePermittedForkTriggerTests`; `ImportRejectionTests` JSON twin; runtime `AllowDiagnosticFork_DuplicateTrigger_Throws`. |
| **Why not cheaper** | Uniqueness of trigger names on one edge is representable as a set, but the reject-vs-dedup *policy* (report AGWF037, drop the model, join `hasErrors`) is a composition property. Types cannot force the generator to refuse emission. |
| **Failure signal** | AGWF037 at compile / import. Separates fail from skip: a missing report plus a generated saga is the violation. |
| **Rollback** | Revert AGWF037. Runtime builder still throws on the C# fluent path. JSON import would again reach `Create` (throw) or CS0152. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High for non-empty trigger names. |

**Open questions:**

- Empty / null trigger names on JSON import are skipped by `FindDuplicateTriggerNames` (`DiagnosticForkModel.cs:158-161`) after `Trigger ?? string.Empty` (`WireToModelBridge.cs:466`). Two empty names are not reported as AGWF037. Stakes: if the wire can carry empty trigger names past other rejections, the pair is still first-wins-adjacent. C# extract never inserts an empty name (`TryGetForkTriggerName` returns false).

## Discriminating detail

Extractor (`DiagnosticForkExtractor.cs:142-180`): on a second same `TriggerName`, reports `DuplicatePermittedForkTrigger` and sets `hasDuplicateTrigger`. Then `return false` — no `Create`, no model. Comment at `:174-176` states the reject policy.

Generator (`WorkflowIncrementalGenerator.cs:930-941`): `hasDuplicatePermittedForkTrigger` joins `hasErrors`; model is null; no `EmitWorkflowSources`.

JSON (`WireToModelBridge.cs:459-492`): same `FindDuplicateTriggerNames`, same diagnostic id, before mapping.

Runtime builder already throws (`DiagnosticForkBuilderTests.cs:198-210`). AGWF037 is not a deleted first-wins-dedup; it is a new generator/import reject in front of CS0152.

## Disposition

- Inventory 6, 22, 23, 46–49, 67, 68, 70, 115–120: **supported** for named triggers on C# and JSON.
- Empty-name hole is a residual, not a contradiction of the named-trigger claim.
