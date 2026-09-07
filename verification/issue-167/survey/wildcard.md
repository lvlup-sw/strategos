---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
implementation_fingerprint_command: git rev-parse 98fabb410e4432cc39fd71ec92651e6ceecfcc7c^{tree}
cost_setting: high
scope_rule: independent wildcard refutation of issue 167's closed-topology and fork-noninterference guarantee after deduplicating all six directed survey lenses
updated: 2026-09-06
skipped: broader speculative wildcard hypotheses were not promoted without an independently reproduced issue-167 contract violation
lens: wildcard
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative workflow-refinement and fork-noninterference acceptance boundary
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: frame/read semantics consumed by the workflow proof
---

# Stage 1 survey — wildcard

This is a **historical discovery record**. The false-green reproduction below describes the state in
which it was found. The low-confidence traversal and root-failure diversion are present and locally
supported by the complete exact-current `98fabb4` local portfolio; protected execution and review
remain pending.

## Scope boundary

I read the complete `verify-code` skill, its scoping/workspace/survey instructions, Stage 0, and every
other issue-167 survey artifact before searching for an independent failure. Those artifacts were used
only to navigate and deduplicate. This lens did not repeat their ownership of catalog reachability,
front-end identity, proof-harness adequacy, public API authority, consumer packaging, or historical
topology-extraction recurrence.

The explicit issue scope requires build-time refinement across the executable workflow graph and
fork-path noninterference. The additional closure rule used here is inferred but necessary for that
claim: every action reachable as a diversion of a fork path while sibling paths remain concurrent is
part of that path's read/write footprint. Issue #167 does not separately spell out that attribution
rule, but excluding such a reachable action makes the stated noninterference result unsound.

## Finding

### W1 — Critical false proof: a fork path's low-confidence handler was absent from its concurrent footprint

The runtime treats a low-confidence handler on the last step of a fork path as executable before that
path is marked successful. `ForkJoinHandlerEmitter` evaluates the confidence gate, changes phase,
yields the handler command, and exits before recording path success or checking the join
(`src/Strategos.Generators/Emitters/Saga/ForkJoinHandlerEmitter.cs:112-123,160-214`). The sibling fork
path can therefore still be running when the handler executes.

At discovery time, `BuildForkFootprint` collected reads and writes only from the direct
`ForkPathModel.Steps`. It did not traverse each step's
`Confidence.OnLowConfidenceHandlerChain.Steps`. This was narrower than the rest of the analyzer:
`BuildOccurrenceMap` already admitted those handler occurrences to contract resolution, and the phase
graph already emitted the confidence edge. A handler could consequently conflict with another fork
path while AGWF041 reported no interference.

I reproduced the false acceptance through the real `WorkflowIncrementalGenerator` using a closed,
compiler-visible workflow:

1. both fork-path leaf actions had empty frames;
2. the left leaf diverted on low confidence to an action that modified `Order.Stage`;
3. the right path executed an action that also modified `Order.Stage`;
4. every occurrence had a resolved typed action identity and every action contract was closed.

The generator returned zero AGWF039-AGWF042 diagnostics. The allowed execution contains a
write/write race and should deterministically report AGWF041. This is not merely a missing test: it
was a concrete false proof at the compiler enforcement boundary.

## Disposition at the recorded fingerprint

The working tree was repaired concurrently after the refutation. `BuildForkFootprint` now traverses
the transitive low-confidence handler chains with `EnumerateForkPathOccurrences` before taking frame
and read unions
(`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:982-1104`). Focused regression cases
now require:

- a stable AGWF041 for handler write/write conflict;
- AGWF041 for handler write/read conflict across paths; and
- no diagnostic for disjoint handler frames

(`src/Strategos.Generators.Tests/Proof/WorkflowBindingProofAnalyzerTests.cs:175-218,788-879`). This
survey lens did not execute those concurrently added tests, so their result belongs to the later
verification stage rather than this Stage 1 record.

## Stage 2 obligation seed

For each fork path, define its footprint over all executable occurrences reachable before that path
reaches the join, including confidence diversions. Prove write/write and both write/read directions
against every sibling path, with deterministic path attribution. Mutation evidence should make the
new traversal necessary: reverting the handler-chain traversal must turn at least the write/write and
write/read cases from red to green. Cheapest sound evidence is rung 4 through the real generator,
supplemented by the emitted runtime-routing assertion that the handler runs before path success.
