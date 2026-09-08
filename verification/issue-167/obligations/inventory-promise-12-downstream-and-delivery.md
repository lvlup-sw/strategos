# Promise obligation 12 — #169 handoff, downstream adoption, and delivery process

- **Historical Stage 2 status:** unproven
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; no #167 PR/final artifact was bound at that stage.
- **Promise sources:** user scope in `stage0.md`; `IC-169-001`/`002`, `IC-COMMENT-010`, claim seed 12; #153 consumer-adoption rule recorded in Stage 0.

This file preserves the Stage 2 inventory record. Current coordination links are Basileus #493 and
Exarchos #1893. Their implementations, #169 semantics, protected PR evidence, and merge authorization
remain absent.

## Obligation

The occurrence identity and closed proof graph delivered by #167 must be consumable by #169 without
a parallel CLR-type-to-action mapping. Until #169 lands, #167 must reject `Compensate<T>` as AGWF042
rather than pretend rollback refinement was proved. Before #167 is merge-authorized, dedicated
Exarchos and Basileus adoption issues must exist for Contracts 0.11/public API changes; verification
and fixes must precede PR creation, and merge still requires the user's final authorization.

The cheapest sufficient proof for the #169 portion is **R4 in the downstream consumer diff**; no
amount of absence checking in #167 can prove a future consumer will reuse the seam. Adoption issue
creation is external-state evidence, and final process evidence must name the PR/review/check runs.

## Evidence already present

- `WorkflowActionReference` and `StepModel.Action` provide the names-only occurrence mapping that a
  downstream rollback planner can consume.
- `WorkflowBindingProofAnalyzer.cs:201-210` reports the explicit reason
  `rollback proof is deferred to #169`; `WorkflowBindingProofAnalyzerTests.cs:361-380` pins AGWF042.
- `stage0.md:18-21` records the exact authorization boundary: verify #167 first, #169 as the next
  stacked diff, one CodeRabbit round permitted, no merge yet.

## Missing evidence

- #169 has not yet consumed the seam, so “no second mapping” remains unproven by design.
- The complete local evidence portfolio is recorded for current final subject `98fabb4` in
  `../final-evidence.md`, but no protected PR/check set or completed CodeRabbit review exists.
- Dedicated adoption issues now exist as Basileus #493 and Exarchos #1893. Their implementation and
  downstream test results remain absent.

This obligation should remain open; it is not a refutation of the #167 code slice.
