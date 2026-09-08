# Promise obligation 05 — fork noninterference and joint guarantees

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** claim seed 5; `IC-MIGRATION-003`, `IC-CALCULUS-004`, `IC-CHANGELOG-003`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

For each accepted fork, the proof footprint must include every occurrence reachable on each path
before the join, including transitive low-confidence diversions and the root failure diversion that
a path worker can publish while sibling workers remain active. Distinct paths must not overlap on
write/write or write/predicate-read resources. The join precondition must be implied by the
conjunction of every path tail's effective guarantee, not by any one tail alone.

The cheapest sufficient proof is **R4 plus a mutation backstop** because presence-only tests cannot
show that the path walker actually contributes handler footprints or that join logic uses all
tails.

## Delivery evidence

- `WorkflowBindingProofAnalyzer.cs:982-1054` deterministically compares all fork path pairs for the
  three prohibited intersections.
- `:1056-1104` builds each footprint from a queue traversal that follows every step's low-confidence
  handler chain, adds root failure diversions, and unions proven frame writes and state reads.
- `:1296-1357` removes ordinary fork-tail edges from one-at-a-time seam checking, then constructs
  `LogicFormula.All` over all tail effective guarantees for the join obligation.
- `WorkflowBindingProofAnalyzerTests.cs:145-219` covers configured joins, external/event conflicts,
  low-confidence handler write/write and write/read conflicts, and disjoint handlers.
- `WorkflowBindingTopologySemanticsTests.cs:264+` requires a fork whose join succeeds only from the
  joint guarantee and includes a root-failure-handler/sibling write-conflict kill.
- During the prior verification pass, deleting the confidence-handler traversal caused exactly the
  two handler-interference fixtures to fail; restoration returned them green. This mutation result
  must be repeated or bound to the final committed subject in Stage 3.
