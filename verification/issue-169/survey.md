---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
evidence_binding: Stage 1 is a static discovery and proof-inventory record bound to the immutable product tree; no local test, CI, review, package execution, downstream adoption, release, or merge result is asserted
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, persistence, wire, packaging, and documentation surface against base 0ac93e916849cceada616a0e15dd7e6c83b34af1
updated: 2026-09-07
skipped: none of the seven survey lenses; execution and verdict assignment are intentionally deferred to later verification stages
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: v2.13 action-calculus operator program
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: preceding typed-workflow proof and exact scope handoff
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate/effective-guarantee solver semantics reused by inverse proof
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream adoption coordination only
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream adoption coordination only
---

# Stage 1 survey synthesis — issue #169

## Subject and reading discipline

All seven lenses inspected the immutable product subject
`42b4ed741c1f406a2de2fdcdaf602ca53dd91dab`, tree
`2d0f3027308580376819e7b56649592cd0a784bc`, over base
`0ac93e916849cceada616a0e15dd7e6c83b34af1`. The product history is:

1. `7082184` core inverse calculus and rollback algebra;
2. `a73fbcc` typed workflow inverse proof and Contracts 0.12 surface;
3. `494471f` durable generated completed-prefix rollback;
4. `464439c` documentation and migration contract;
5. `8f8dd34` proof, topology, identity, import, and runtime-integrity hardening;
6. `a37470e` isolated packed typed-compensation consumer;
7. `40edb5f` warning-free generated guards plus package `FAIL`/`INDETERMINATE` separation; and
8. `42b4ed7` nonblank compensation-step monikers across TypeSpec, generated contracts, schemas, and
   tests.

The detailed records are in `verification/issue-169/survey/`. This synthesis deduplicates their scope,
findings, claim seeds, and recurrence seeds. It does not execute the inventoried checks and does not
assign a `Verified` verdict merely because the final commits contain fixes or tests.

## Exact requested scope

The prior exact scope answer was:

> Implement the next slice #167 and #169. Use [$verify-code](/home/reedsalus/.agents/skills/verify-code/SKILL.md) to review your changes. After applying any fixes, open PRs. You may do a single round of review with coderabbitai before pressing for my final authorization to merge.

The current continuation request was:

> Please complete the remaining work for 169 as planned.

This survey therefore treats #169 as the remaining half of the planned #167 -> #169 program and uses
the issue-167 dossier as the identity/proof baseline. It does not reinterpret downstream issue
creation or a future PR as evidence of adoption or authorization.

## Scope set

1. **Public calculus:** inverse status/obligation/contract/failure/result types, exact semantic
   derivation, identity rule, rollback syntax tree, completed-prefix reversal, scoped composition,
   parallel noninterference, cancellation, immutability, and public API.
2. **Ontology enforcement:** source-visible AONT216 analysis, shared rooted action catalog and proof
   kernel, runtime graph-freeze resolution, authority-lattice ownership, and diagnostic parity.
3. **Typed workflow contract:** typed/legacy compensation overloads, one-declaration rule,
   `CompensationConfiguration`, `RequiredOnFailure`, occurrence forward/inverse identities, source and
   import extraction, and IR fidelity.
4. **Workflow proof:** AGWF044 authored-inverse equivalence, AGWF045 propagation/closed scope,
   same-subject/boundary rules, multiple bindings, dynamic/opaque/invalid precedence, EventSourced
   exclusion, and compilation-local catalog limits.
5. **Topology/runtime:** root, loop, branch, fork, approval, confidence, failure, terminal, and
   diagnostic-fork occurrences; durable claims/journal; completed-prefix selection; nested isolation;
   fork quiescence; distinct inverse messages; reducer and failure-handler ordering; timeout,
   redelivery, corruption, and reconciliation.
6. **Contracts and delivery:** TypeSpec, generated C#/schemas/catalog/docs, hand DTO/parser/bridge,
   package 0.12 contents, public API gates, docs/migration/changelog, strict isolated package consumer,
   CI/review/release, and Basileus/Exarchos adoption coordination.

## Deduplicated findings

### F-1 — three inverse-proof orchestration authorities require explicit parity

The runtime `ActionCalculus`, ontology AONT216 analyzer, and workflow AGWF044 analyzer implement the
same high-level inverse obligations independently. They share the normalized formula/finite solver,
Roslyn predicate parser, and rooted catalog where possible, but do not share status precedence,
effective-guarantee construction, subject/frame/authority comparison, both implication calls, and
reason assembly as one orchestration implementation. Graph freeze calls the runtime calculus and is
not a fourth authority.

No disagreement is established by Stage 1. The structural risk is nevertheless high because each
front end can remain locally green while accepting a contract another rejects. Stage 2 must require a
single vector corpus interpreted by all three, or a neutral shared inverse kernel, including invalid,
opaque, missing, empty identity, semantic authority aliases, preserved requirements, both implication
directions, and deterministic counterexamples.

### F-2 — static proof establishes declared contract identity, not arbitrary inverse code

The analyzer binds a CLR compensation step type to an ontology action identity and proves the
**descriptor** equivalent to the derived inverse. It does not inspect arbitrary `ExecuteAsync` bodies
or external effects. The packed legal consumer makes this boundary concrete: ontology descriptors
declare `Order.Stage` transitions, while all executable probe steps operate on a `FlowState` with no
stage and return it unchanged. A legal compile is still the correct package/analyzer outcome.

This is not classified as a product defect. It is a proof-classification boundary: package and
analyzer success cannot substantiate executable restoration. The real Wolverine/Marten fixture, in
which forward and inverse steps actually check/fold stages, supplies one stronger behavioral rung.
Universal consumer implementation correctness remains an explicit assumption, mitigated by stable
rollback IDs and documented idempotency responsibility.

### F-3 — “persisted before dispatch” crosses Wolverine/Marten semantics

Generated handlers create a dispatch claim before yielding the worker command, and the state is a
Marten saga document. The source ordering is necessary, but atomic durability relative to command
visibility depends on supported Wolverine/Marten session/outbox semantics and crash behavior. The
large generated-runtime suite can prove handler logic; only a real host/database rung can evidence the
combined boundary. Reports must preserve this assumption instead of treating emitted statement order
as a transaction proof.

### F-4 — topology is centralized, while ingress ownership remains distributed

`CompensationTopology` is one closed static authority for occurrence/scope/lane/ordinal/action
identity and is shared by proof and emission. Many distinct emitters still own start, completion,
approval, confidence, branch, loop, fork, diagnostic, timeout, reducer-failure, and failure-handler
routes. Repository history repeatedly shows accepted callback shapes omitted or under-scoped in
downstream lowering. The broad adversarial suite is appropriate, but Stage 3 must inventory and
mutation-kill every ingress rather than infer completeness from the central component.

### F-5 — durable authority is exact and ambitious; failure triage must remain monotonic

The generated protocol links forward dispatch claims, completion journal entries or failure claims,
the selected concrete scope, and stable rollback IDs. It validates schema version, high-water and
continuity, canonical scope history, uniqueness, topology, role, execution identity, and redelivery.
The first rollback terminal outcome is intended to be monotonic; failure, timeout, unknown outcome,
or corrupt state retains the saga rather than recursively compensating or assuming success.

The docs sometimes summarize all such outcomes as remaining in `Failed`, while generated state also
uses a distinct `OutcomeUnknown`. Later obligations must verify that both are retained non-success
states and preserve their operational distinction.

### F-6 — parallel algebra is preserved structurally but refined to serial state folds

The public rollback plan retains independent parallel branches and rejects write/write and both
write/read conflicts. Generated inverse workers are serialized in reverse completion order because
arbitrary `TState` has no sound merge operation. This is documented and conservative for shared state,
but it is not literal execution of `(A || B)^-1` in parallel and does not prove arbitrary external
effects commute. Stage 2 must keep the structural law and runtime refinement as distinct claims.

### F-7 — wire and diagnostic representations have executable guards but more than one root

The inverse action triple crosses public types, TypeSpec, generated C#, two schema forms, hand DTO,
reader, bridge, generator IR, topology, and journal. AGWF044/045 cross a closed TypeSpec enum, separate
entry models, generated artifacts, live descriptors, tests, and prose. Codegen/schema/catalog/API
guards cover much of this topology; later stages must inspect and mutation-test their exact binding.

**Resolved discovery at the bound revision.** The survey found that `compensationStepType` was only a
required TypeSpec `string` while the hand importer rejected empty and whitespace-only monikers. Commit
`42b4ed7` aligned the authorities: `StepConfiguration.tsp` now applies `@minLength(1)` and
`@pattern(".*\\S.*")`; the standalone `CompensationConfiguration.json` and bundled workflow schema
carry `minLength: 1` plus the same pattern; generated `CompensationConfiguration.g.cs` implements
`IJsonOnDeserialized` and `IJsonOnSerializing` and calls `RequireNonWhitespace(..., required: true)`;
and `ApprovalFailureConfigTests` asserts the schema constraints and supplies both `""` and `"   "`
to read/write rejection cases. The exact three-name `inverseAction` continues to inherit the same
nonblank class from `ActionReferenceV1`. The mismatch is no longer present in tree `2d0f3027...`;
execution and mutation of these guards remain later-stage evidence obligations.

### F-8 — the package harness gap was found and hardened inside the final subject

The pre-final packed probe exercised #167 but not typed compensation. `a37470e` added a legal typed
program and a contradictory inverse that must fail exclusively with AGWF044. A fresh run then exposed
three generated CS8604 warnings; `40edb5f` narrowed the journal before dereference, added a CS86xx
compilation assertion, and made nullable warnings fatal in the consumer. The same commit split restore
infrastructure failure (`INDETERMINATE`, exit 3) from product compilation failure (`FAIL`, exit 2).

These are product and harness mechanisms now, not Stage 1 pass claims. Exact-current execution and
mutation must show the positives and negatives are necessary and that CI preserves the three-way
result.

### F-9 — downstream issues are coordination, not adoption evidence

Basileus #495 and Exarchos #1895 explicitly request Contracts 0.12, `inverseAction`, exact identity,
legacy omission, AGWF044/045, and retained failed/unknown reconciliation. No downstream commit,
package lock, build, fixture, or release is bound to this tree. Their implementation remains
Unproven in this dossier.

## Initial-question disposition after survey

| Stage 0 question | Stage 1 disposition |
|---|---|
| Three proof paths implement one inverse contract | shared lower kernel found; three orchestration authorities remain an explicit parity obligation |
| Every typed occurrence enters one closed topology | shared topology and fail-closed diagnostics found; distributed ingress completeness still requires inventory/mutation |
| Forged/stale/reordered messages cannot acquire authority | exact capability-chain mechanisms and broad adversarial tests found; no result claimed |
| Completed prefix/scope/fork semantics are exact | source and targeted unit/behavioral mechanisms found; no result claimed |
| Failure/unknown/timeout/corruption retain monotonically | mechanisms found; `Failed` versus `OutcomeUnknown` prose/state distinction must be kept exact |
| All public/wire/diagnostic representations agree | the discovered compensation-step blank-schema mismatch is resolved by `42b4ed7`; multi-root diagnostic topology still requires guard validation |
| Packed analyzer enforces #169 and triages infrastructure | strict positive/AGWF044 negative/warnings/exit split found at final revision; execution remains pending |
| “Proved inverse” is not overstated | docs mostly say contract and disclose external idempotency; wildcard requires evidence reports to preserve the boundary |

## Claim-derived seed set

Every seed must become an obligation, an evidence-backed refutation, or an explicit open assumption in
Stage 2.

1. For a valid closed forward action, inverse requirement equals its effective guarantee, inverse
   effective guarantee equals its hard requirement, and subject/frame/semantic authority are equal.
2. Authored inverse equivalence is bidirectional; weaker/stronger acceptance, weaker/stronger
   restoration, wrong subject/frame/authority/name, invalidity, and opacity cannot become `Proven`.
3. Non-empty frames require executable authored inverse code. Empty frames use identity only when no
   explicit inverse declaration is broken.
4. Public inverse/rollback result types are immutable, structurally valid, cancellation-aware, and
   deterministic in failures/counterexamples.
5. Sequential rollback reverses exactly the completed prefix; empty input is a subject identity;
   scopes remain nested; one noncompensable leaf propagates through every composite kind.
6. Parallel rollback rejects overlapping writes and both write/read directions while allowing shared
   reads and preserving independent branch structure.
7. Runtime calculus, AONT216, and AGWF044 interpret one semantic vector corpus identically or expose a
   deliberately documented distinction.
8. Typed authoring preserves exact forward/inverse identities and executable CLR type through C# and
   JSON; a duplicate declaration throws; legacy omission remains valid; mixed/dynamic programs fail
   closed; typed `RequiredOnFailure=false` is rejected.
9. AGWF045 propagates rollback safety across every executable leaf of the concrete closed scope,
   including nested/approval/confidence/fork/failure routes and every same-subject workflow binding.
10. Typed EventSourced programs are rejected because rollback fold cannot be proved; legacy
    event-sourced compensation is not broken.
11. Every forward dispatch establishes one exact persisted capability before its result can be
    accepted; claims/journal identities and rollback IDs are nonempty, stable, distinct, and injective.
12. Every failure ingress carries exact persisted authority; forged, stale, duplicate, altered-kind,
    cross-occurrence, cross-scope, corrupt, or incomplete messages cannot begin or advance rollback.
13. The selected plan includes exactly completed entries in the innermost failed scope, excludes the
    failed leaf, waits for fork quiescence, and preserves enclosing history for later failure.
14. Inverse state is reduced before the next inverse; identity skips code; the failure handler starts
    only after successful rollback; failure/timeout/unknown/corruption are monotonic retained states.
15. Contracts 0.12 TypeSpec, generated C#/schemas, hand parser/bridge, AGWF catalogs, package contents,
    public API baselines, docs, and migration guidance are bound by effective drift guards.
16. The isolated packed consumer uses exact local bytes, rejects nullable warnings, proves legal
    authoring, fails exclusively for AGWF041/AGWF044 negatives, and distinguishes infrastructure
    indeterminacy from product failure.
17. A real supported Wolverine/Marten/PostgreSQL path demonstrates A/B completion, C failure, and
    UndoB/UndoA without UndoC; the external transaction assumption remains explicit.
18. Arbitrary inverse code and external effects satisfying the declared contract/idempotency rule are
    consumer obligations, not universal facts proved by Strategos analyzers.
19. Basileus and Exarchos adoption remains unverified until exact consumer revisions and package
    results are bound.
20. Protected CI, exactly one permitted CodeRabbit review round, human review state, release, and merge
    are delivery evidence separate from local correctness.

## Recurrence-to-guard seed set

| Recurrent class | Prior/current evidence | Present exposure | Candidate earliest guard |
|---|---|---|---|
| Accepted fluent path omitted downstream | #135/#140, #143/#144, #187/#196, #167 confidence footprint | all compensation/failure ingress and nested scope ownership | occurrence/ingress inventory plus real-generator kill tests |
| Semantic identity weakened | repeated phase/CLR/role fixes before #169 | occurrence, scope, lane, action, execution, rollback identity | validated exact values and wrong-field adversarial cases |
| Harness observes incomplete subject | earlier parity fixes, #167 rooted catalog, initial #169 pack gap, final CS8604 discovery | analyzer/package/generator consumer | isolated exact-byte positive and cause-specific negatives with warnings fatal |
| JSON/schema/import divergence | prior fail-closed import hardening | inverse object and CLR compensation moniker | schema/DTO/reader/bridge malformed matrix and round trip |
| Diagnostic metadata drift | earlier AGWF/AONT parity repairs | AONT216 and AGWF044/045 | generation/catalog/live-descriptor equality with mutation |
| Runtime/analyzer proof drift | #168/#167 shared solver but repeated orchestration | three inverse front ends | common vectors or neutral inverse kernel |
| At-least-once redelivery reopens state | prior retry/timeout/compensation ordering fixes | claims, journal, inverse outcomes, terminal state | capability invariants plus duplicate/stale/timeout integration cases |
| Generated fixture compiles but consumer warns/fails | past consumer compile regressions and final CS8604 reproduction | emitted nullability and package dependencies | full compilation diagnostics and strict packed consumer |
| Schema/data change lacks safe reversal | prior saga migration history | compensation schema version 1/in-flight sagas | version guard, drain/version docs, retain-on-corruption tests |

## Evidence boundaries entering Stage 2

- No command result is recorded as current proof by these Stage 0–1 artifacts. The presence of tests
  and the parent agent's prior execution notes are inputs for later exact-current validation, not
  verdicts here.
- The finite solver and action-contract proof were delivered by #168, and the occurrence proof by
  #167, but #169's new inverse orchestration and runtime use must be independently bound to this tree.
- The packed consumer can prove package/analyzer behavior only. The real host fixture is needed for one
  executable/persistence path; neither proves arbitrary external compensation correctness.
- Protected CI, CodeRabbit/human review, package publication, and merge have not been inspected here.
- The future Contracts tag must create fresh release evidence. Checked-in version metadata and local
  nupkgs cannot establish that event.
- Basileus #495 and Exarchos #1895 establish coordination only. Their implementation and tests remain
  outside the current evidence set.
