---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
evidence_binding: current final subject is product commit 98fabb410e4432cc39fd71ec92651e6ceecfcc7c; all recorded local proofs, including the complete solution, Contracts/codegen, schema, docs, standalone gates, fresh packages and consumer, hardened pack script, and Basileus smoke, are exact-current; protected PR CI and review remain unbound
cost_setting: high
scope_rule: synthesis of proof-layer-fit, scope coverage, per-obligation refutation, and wildcard evaluation
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: acceptance and enforcement boundary
  - path: https://github.com/lvlup-sw/strategos/issues/153
    why: downstream adoption rule
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: next dependent semantic slice
  - path: https://github.com/lvlup-sw/strategos/issues/204
    why: explicit portable-catalog deferral
---

# Stage 3 evaluation synthesis — issue #167

## Outcome

The obligation portfolio now covers the full recorded reverse-dependency closure and assigns each
claim to a sound proof layer. Adversarial passes found and repaired six concrete defect classes: a
top-level/fork-path projection echo could hide different occurrence configuration; parenthesized
fluent receivers could truncate extraction; underscore-based loop membership could omit a
back-edge; name-based loop ancestry could omit sibling ingress seams in two shapes; a root failure
handler could be missing from a concurrent fork footprint; and an ambiguous self-reference could be
classified as recursion before identity ambiguity. The evaluation also refuted one conditional
obligation because no supported public front end can produce an empty workflow model.

A later concurrent inspection found an additional event-consumer identity mismatch: fork paths that
reuse one CLR step type emit qualified completed-event stems, while the `NotFound` emitter still used
the suppressed unqualified type event. Commit `595a949` made both emitters share
`ForkPathCompletedNaming` and added fork-only and fork-plus-linear kills. The complete exact-current
local portfolio exercised that repair; it was discovered after the original six evaluation repairs
but is part of the current product subject.

This is **not a verified release verdict**. Current final subject `98fabb4` (tree `f7271c9`) contains
the closed raw import/approval harness routes, valid-rooted-descriptor and exact emitter-boundary
kills, shared-type fork `NotFound` repair, serialized local-feed pack invocation, and the repaired
workflow-name nonblank contract. The exact-current local portfolio binds to it: the serial Release
build and 5,517-success/5,533-total solution run, Contracts 185/185 and stable regeneration, live
schema and documentation gates, standalone policy gates, fresh packages and consumer, hardened pack
script, and Basileus smoke all passed. Protected PR execution and completed-diff review remain
pending. The authoritative ledger therefore has 21 `Indeterminate`, zero `Violated`, two `Unproven`,
and zero `Verified` active obligations. No proof may be promoted before its required protected path
runs on the final subject.

## Categorized findings and actions

| Finding | Category | Action taken or required | Evidence |
|---|---|---|---|
| The former delivery obligation combined external adoption and #169 semantics at incompatible rungs. | Refinement | Split into `downstream-contract-adoption` (R5) and `compensation-handoff-to-169` (R4). | `proof-layer-fit.md`; `../ledger.md` |
| Diagnostic representation equality was assigned alongside behavioral component proof. | Refinement | Isolated `generated-diagnostic-authority` at R1; emission behavior remains in workflow obligations. | `proof-layer-fit.md`; `../ledger.md` |
| The canonical wire workflow name accepted whitespace although every runtime/import lookup rejected it. | Gap | Current commit `98fabb4` adds the exact nonwhitespace TypeSpec pattern, regenerates standalone/bundled schemas and C#, and adds read/write semantic tests; Contracts 185/185 and stable codegen passed. The mismatch is no longer a current finding. | `../survey/authority-topology.md`; `../final-evidence.md` |
| Duplicate stable wire IDs could hide conflicting action/configuration/runtime semantics. | Gap | Repaired production comparison, added `wire-echo-semantic-equivalence`, collision regressions, and exhaustive DTO-property fingerprint guard. | `refutation-occurrence-wire.md`; `../obligations/wire-echo-semantic-equivalence.md` |
| Parenthesized fluent receivers could terminate invocation walking and hide an occurrence or reject an otherwise closed workflow. | Gap | Centralized transparent-receiver stripping and added helper, entry-occurrence, and branch-semantic regressions. | `refutation.md`; `../obligations/configured-occurrence-extraction-complete.md` |
| Flattened underscore-delimited loop names were treated as membership/ancestry, omitting an underscore-named back-edge and sibling seams for `A`/`A_B` and nested `A_B`/`C`. | Gap | Replaced name parsing with immutable outer-to-inner `RepeatUntil.ArgumentList.SpanStart` paths and added three kill regressions. | `refutation.md`; `../obligations/configured-occurrence-extraction-complete.md` |
| A root failure handler was absent from fork footprints although it can execute while sibling workers remain active. | Gap | Added root failure diversions to footprint construction and a write-conflict kill. | `refutation.md`; `../obligations/fork-handler-footprint-complete.md` |
| Recursive dependency discovery could outrank ambiguous occurrence identity. | Gap | Traverse only uniquely resolved occurrences and pin ambiguous self-reference to one AGWF040 rather than AGWF042. | `refutation.md`; `../ledger.md` |
| Distinct ordinal workflow IDs can collide after generated-name normalization. | Gap | Added AGWF043 to resolution, diagnostic authority, contracts, docs, and collision fixtures. | `coverage.md`; `../ledger.md` |
| Shared-type fork `NotFound` handlers could name a suppressed unqualified completed event and omit the qualified emitted events. | Gap | Commit `595a949` derives handler stems from `ForkPathCompletedNaming` and adds fork-only plus fork-and-linear reuse kills; the complete exact-current local portfolio passed. Keep the identity guard `Indeterminate` pending protected execution and review. | `../guards.md`; `../final-evidence.md` |
| Historical raw import and approval parser routes could bypass the shared subject-validating harness. | Gap | Commit `595a949` migrates those routes to validated shared helpers, and the complete exact-current local portfolio passed. Keep `proof-fixture-subject-binding` `Indeterminate` pending protected execution and review. | `coverage.md`; `refutation.md`; `../obligations/binding-proof-fixtures-compile-intended-subject.md` |
| Earlier dirty-tree results were discussed beside a stationary base SHA. | Gap | Replaced them with immutable, explicitly scoped evidence at `595a949`, `6851fe7`, and current subject `98fabb4`. Protected evidence remains unbound. | `wildcard.md`; `../ledger.md`; `../final-evidence.md` |
| Final-subject protected CI and future Contracts-tag results do not exist. | Gap | Keep affected obligations `Indeterminate`; bind final runs to commit, policy SHA, environment/tool versions, and nupkg hashes. The future tag must create fresh release evidence. | `coverage.md`; `../ledger.md` |
| Basileus/Exarchos coordination exists, but adoption implementation does not. | Gap | Basileus #493 and Exarchos #1893 are linked; keep downstream adoption `Unproven` until consumer upgrades and tests run. | `coverage.md`; `wildcard.md`; `../basileus-adoption.md`; `../exarchos-adoption.md` |
| An empty workflow might need an entry-to-guarantee semantic proof. | Refuted | Removed from the active ledger: JSON rejects zero steps, and compiler-valid public C# authoring cannot return a definition without `StartWith` plus a terminal operation. | `refutation.md`; `../obligations/empty-workflow-entry-proof-defined.md` |
| Duplicate JSON member handling might reopen the action-echo bug. | Refuted as a #167 obligation | Retained as a general wire-policy residual; it is not the separately represented stable-ID echo mechanism and no action-specific bypass was found. | `wildcard.md` |
| Missing combined instance-name/configure overloads might be unsound. | Refuted as a correctness gap | The analyzer rejects ambiguous phase identity and docs disclose the expressiveness boundary; record only as possible API ergonomics work. | `wildcard.md`; `refutation-occurrence-wire.md` |

No unresolved **bias** finding remains. The portfolio originally favored in-repository component
proof, but the package/release/downstream deficit is now explicit as active obligations rather than
being silently judged acceptable.

## Guard consequences

The recurrence classes and their ratchets are recorded in `../guards.md`. The highest-value guards
cover accepted-surface closure, structural topology ownership, identity transport, wire-echo
equality, and future DTO-field additions. The current committed subject closes the identified
proof-subject routes through validated shared helpers and aligns workflow-name validation; the guards
remain evidence-indeterminate until protected execution and completed-diff review are recorded.

## Required finalization sequence

1. Run the protected PR checks on exact revision `98fabb4` and record their check URLs and immutable
   reusable-workflow policy SHA.
2. Complete the required completed-diff review on that exact subject.
3. Let a future Contracts tag regenerate, test, compare, pack, and digest-bind its own release bytes;
   do not inherit the PR or parent package evidence.
4. Retain the existing Basileus #493 and Exarchos #1893 links and track their implementation/test
   results separately; issue creation alone is not adoption proof.
5. Update `ledger.md` one obligation at a time. Promote only proofs that ran on the final protected
   subject; leave #169's future half `Unproven` until that slice exists.

## Dependency check after refutation

No active obligation depended on empty-workflow support. Removing it does not weaken topology
closure, because rejection before model construction remains covered. The wire gap added one active
obligation. The parenthesized-receiver, structural-loop, root-failure-footprint, and diagnostic-
precedence findings refine existing topology, fork, and occurrence obligations rather than creating
parallel claims; their proof-layer, coverage, refutation, wildcard, and guard consequences were all
re-evaluated here.
