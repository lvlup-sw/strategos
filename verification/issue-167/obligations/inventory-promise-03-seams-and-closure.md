# Promise obligation 03 — internal seams and fail-closed topology

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** `IC-168-001`/`002`, `IC-MIGRATION-004`/`005`, `IC-CALCULUS-004`/`005`, `IC-COMMENT-004`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

Every non-failure transition represented in an accepted closed workflow must satisfy
`effectiveGuarantee(upstream) => requirement(downstream)`. Branch/loop/fork/approval/confidence/
failure callback forms which cannot be represented without executing user code must fail AGWF042,
not be silently dropped so a smaller graph can pass. Recursion, unsupported compensation, invalid
contracts, and opaque predicates also fail closed.

Transparent parentheses around a fluent receiver do not terminate chain extraction. Loop ownership
is structural, not derived from flattened underscore-delimited phase names. A
top-level loop `A_B` is a sibling of `A` even though its display prefix starts with `A_`; if that
sibling begins in nested loop `C`, the additional display-name depth still does not make it a child
of `A`. An underscore-bearing step name likewise cannot change direct membership or erase a
back-edge.

The cheapest sufficient proof is **R4, component topology tests**. A compiler-shape check cannot
show that the graph used for proof includes branch joins, loop back-edges, approval/failure ingress,
or all successful exits.

## Delivery evidence

- `TopologyClosureInspector.cs` records analyzer-visible closure failures without inventing facts.
  `WorkflowBindingProofAnalyzer.cs:190-210` rejects those failures and defers compensation to #169.
- `PhaseGraph` is the shared transition representation consumed by the binding proof. Seam checking
  at `WorkflowBindingProofAnalyzer.cs:1241-1357` ignores failure edges in the ordinary seam loop,
  proves every transparent successor, proves successful completions, and conjuncts all fork-tail
  guarantees before a join.
- Failure and approval ingress are proved at `:672-892`; recursion is detected at `:81-181`.
- `TopologyClosureProofTests.cs` contains 24 explicit dynamic/unsupported forms, including dynamic
  branch cases, loops, fork collections, callbacks, duplicate approval handlers, nonterminal and
  fork-path failure handlers, and nested approvals; each expects AGWF042.
- `StepExtractor` and `LoopExtractor` carry immutable outer-to-inner `RepeatUntil` identities using
  each invocation's unique `ArgumentList.SpanStart`; direct membership and ancestry compare those
  paths rather than phase text.
- `InvocationChainWalker` and the extractors strip transparent parenthesized receivers; helper,
  entry-occurrence, and branch-path regressions cover the end-to-end shape.
- `WorkflowBindingTopologySemanticsTests.cs` includes dedicated incompatible-ingress kills for
  an underscore-bearing body step, sibling `A`/`A_B`, and prefixed sibling `A_B` beginning in nested
  `C`, in addition to the legal and refuted branch, loop, approval, failure, confidence, and fork
  cases.

## Scope reconciliation

The current action-calculus reference now says “all reachable occurrences represented by the
closed ... graph” and immediately lists conservative exclusions. The migration guide similarly
states that unbound workflows receive no binding enforcement while topology-lowering fixes still
apply generally. This removes the earlier overbroad no-runtime-change implication.
