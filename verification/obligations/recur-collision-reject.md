# recur-collision-reject

Open class **R2**. Guard candidate **G-R2**. AGWF037 is an instance guard for `PermitTrigger`. The class is first-wins on any keyed declaration.

## What led here

Four production instances: #156.2 (duplicate `PermitTrigger`, CS0152 only, still open as part of #156 until this diff), #189 (`successorWithinPath[stepName] =` last-writer), #191 (`LastStepName` last-writer), #190 (same step type, two instance names → CS0111). Ontology already rejected first-wins on `ClrType`. CodeRabbit found the operator on #154 and again on #187. Each close minted a new AGWF id (003 → 036 → 037). INV-5 monotonic catalog is the local idiom; it is also evidence that collision-reject is not a standing policy.

Decay rule: another correct reject on the next key is not the fix.

## Surfaces at 324768f

- `WorkflowIncrementalGenerator.cs:930–938` — AGWF037 **does** join `hasErrors` and suppresses `Saga.g.cs`.
- Kill fixtures already on the PermitTrigger key: `DuplicatePermittedForkTriggerTests.CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga` (different evidence fields `stampId` / `otherStampId`); `DiagnosticForkExtractorTests.Extract_DuplicatePermitTrigger_RejectsEdgeAndReportsAgwf037`; `ImportRejectionTests.ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga`; `DiagnosticForkModelTests.Create_WithDuplicateTrigger_ThrowsArgumentException`.
- `ImportRejectionTests.EachRejectedCase_HasItsOwnDistinctDiagnosticId` enumerates AGWF027–034 only — AGWF037 is absent from that uniqueness sweep (existing-proof P14).
- Ontology `OntologyGraphBuilder` first-wins tests remain the precedent for a shared reject.

No policy file lists collision keys. The next `dict[k] =` on IR is uncovered.

## Failure

Two declarations share a key. The generator keeps one payload. The other evidence schema, successor, or terminality disappears. Sometimes the C# compiler later reports CS0152/CS0111 (fail-closed, no catalog id, no suppress-by-id). Sometimes the write compiles and the saga mis-routes (#189). Who observes it: the author if the compiler fires; the operator if it does not.

## Expensive to find again

- First-writer-wins “looks like collision handling” (#189 body). A helper that keeps the first entry will pass a “we handle collisions” review.
- Empty trigger names are skipped today. That skip is a hole adjacent to AGWF037 (survey L1) and must be a dated policy exception, not silence.
- #174 title-only first-match is an intentional operator. Folding it into “ban all first-wins” would be a false obligation.

## Open questions (with stakes)

- Does an unlisted collision key already exist on a path this wave did not change? If yes, G-R2’s first run fails on current HEAD and needs a seed row or an exception. Stakes: the architecture test is only honest if the inventory of IR maps is complete enough to run.

### Investigation Log

#### Does AGWF037 already suppress saga emission (unlike AGWF035)?

- Read: `WorkflowIncrementalGenerator.cs:930–938`.
- Found: `hasDuplicatePermittedForkTrigger` is part of `hasErrors`; tests assert no `Saga.g.cs`.
- Conclusion: the *instance* is gated. The *class* is not. Do not treat hasErrors membership for 037 as closing first-wins.
