---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: static inventory of all existing checks that can support or refute issue-169 obligations, from schema generation through real-host behavior and packed consumption
updated: 2026-09-07
skipped: test execution, mutation execution, protected-CI inspection, review inspection, external-consumer execution, and release verification
lens: existing-proof-inventory
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: acceptance criteria against which proof mechanisms are inventoried
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream proof remains outside this repository inventory
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream proof remains outside this repository inventory
---

# Stage 1 survey — existing proof inventory

## Reading rule

This lens inspected what checks are present and what their assertions could establish. It did not run
them. Test names, XML comments, generated artifacts, workflow names, and prior commentary are not
accepted as evidence of success. A later exact-current execution may bind results to this inventory;
until then every issue-169 obligation remains unverified.

## Proof-ladder map

| Rung | Existing mechanism | What a future green result can establish | What it cannot establish |
|---|---|---|---|
| R1 schema/generation | TypeSpec codegen, generated-artifact diff, schema/catalog tests | deterministic 0.12 wire and closed diagnostic artifacts from checked-in sources | runtime import, analyzer loading, external adoption |
| R2 construction/type invariants | immutable model tests, API analyzer/baselines, builder tests | closed result shapes, snapshots, public signatures, duplicate declaration behavior | semantic solver correctness or generated execution |
| R3 compile/analyzer | AONT216, AGWF044/045, topology, import, generated-compilation suites | closed source cases accepted/rejected with diagnostics and compile-safe generated code | database durability or arbitrary inverse implementation truth |
| R4 component/adversarial runtime | in-memory compiled saga execution and message/state assertions | concrete redelivery, scope, authority, timeout, corruption, and ordering behavior in generated code | real transport/session transaction behavior |
| R5 integration | Wolverine + Marten + PostgreSQL behavioral fixture | one real A/B/C completed-prefix rollback through supported host/persistence | all schedules, external service idempotency, other consumer repos |
| R5 package/consumer | isolated local-feed nupkg probe | public package completeness, analyzer loading, legal typed consumer, cause-specific negatives | release publication or downstream application adoption |
| Delivery | protected CI, CodeRabbit/human review, release and consumer checks | repository policy/review and actual published/adopted bytes when exact-bound | semantic proof beyond the checks run |

No wall-clock benchmark is a correctness gate for this feature. Mutation adequacy is a later
verification dimension, not an existing Stage 1 result.

## EP-1 — runtime inverse calculus and plan algebra

`src/Strategos.Ontology.Tests/Descriptors/ActionInverseTests.cs` provides a concentrated public API
suite. Its named cases cover:

- validated ontology identity and immutable analysis/result shapes;
- derivation from effective guarantee and hard requirement;
- semantic rather than syntactic predicate equivalence;
- both implication directions for authored requirement and guarantee;
- preserved untouched requirements;
- exact subject/frame and canonical frame order/duplicate elimination;
- semantic authority aliases plus stronger/weaker refutations;
- invalid, opaque, missing, broken explicit, and empty-frame identity outcomes;
- completed-prefix reversal and empty subject-typed identity;
- parallel structure, frame union, nested scopes, write/write and both write/read interference,
  nested read footprints, shared reads, and within-branch repeats;
- nested sequence flattening, upward noncompensability, no executable code for a refuted leaf,
  snapshots, subject mixing, and cancellation.

`ActionCalculusTests` additionally exercises graph-freeze AONT216 behavior, including the malformed-
authority diagnostic ownership boundary. `InvariantGuardTests` inventories the closed/sealed public
inverse and rollback surface. `PublicAPI.Unshipped.txt` and the assembly API analyzer guard the exact
ontology API shape.

**Potential proof strength:** R2/R3 for public algebra and finite-solver cases. **Blind spot:** these
tests call the runtime orchestration only; they do not by themselves establish analyzer parity or
that a caller's “completed prefix” enumerable contains only completed actions.

## EP-2 — AONT216 analyzer

`src/Strategos.Ontology.Generators.Tests/Analyzers/AONT216CompensationTests.cs` has ten named methods
covering different/same frame, wrong guarantee, wrong requirement, exact semantic inverse,
equivalent/different authority, preserved requirements/effective guarantee, unreadable selectors,
and a decoy `Define` overload whose source order must not hide disagreement.

The tests run Roslyn over real source snippets and observe compiler diagnostics. The shared action
catalog fail-closed tests in the workflow-generator project additionally protect rooted descriptor
ownership inherited from #167.

**Potential proof strength:** R3 for visible source forms. **Blind spot:** AONT216 is an independently
implemented orchestration; its examples are not visibly generated from the runtime/workflow vector
set. Unreadable syntax intentionally avoids a false positive and relies on graph freeze, which needs
its own exact case.

## EP-3 — workflow inverse and compensability diagnostics

`WorkflowBindingProofAnalyzerTests` contains focused #169 cases for:

- exact typed inverse, preserved requirement, and semantic authority aliases;
- typed EventSourced rejection despite a no-op fold;
- uncompensated written sibling and absent bound workflow contract;
- mixed typed/legacy declarations in both collapsed orders;
- multiple same-subject bindings, with AGWF044 and AGWF045 deduplication;
- opaque and cross-subject secondary bindings;
- legacy standalone runtime-only behavior and legacy use in a bound workflow;
- dynamic inverse identity and contradictory authored contract; and
- a rollback-safe bound action whose written leaf has no inverse.

The file retains the #167 entry/seam/frame/authority/noninterference suite that forms the forward proof
precondition. `WorkflowBindingTopologySemanticsTests` adds typed compensation cases for confidence
handlers outside runtime topology, identity leaves in unsupported paths, approval-path ownership,
ambiguous and unique terminal approval anchors, shared branch phases, fork-owned terminal approval,
and other topology-closure cases. `TopologyClosureProofTests`, imported-workflow proof tests, and
catalog fail-closed tests cover the underlying front ends and ownership model.

**Potential proof strength:** R3 build-time acceptance/refutation and diagnostic precedence.
**Blind spots:** most inverse vectors are authored independently from the runtime/AONT216 suite;
diagnostic absence assertions must reject all unexpected compilation errors, not merely filter for
AGWF044/045; and same-compilation catalog visibility remains an explicit supported boundary.

## EP-4 — generated saga structure and adversarial execution

`src/Strategos.Generators.Tests/Emitters/DerivedCompensationRuntimeTests.cs` is the dominant #169
component suite. Its cases can be grouped by the obligation they attack.

### Plan and state-machine shape

- typed linear journal and reverse completed prefix;
- failed occurrence not journaled or compensated;
- malformed inverse routed only to rollback failure;
- inverse type also used as `OnFailure` does not recurse;
- failure and unknown outcome retain the saga;
- fork quiescence and serialized lane heads;
- approval/confidence lane terminality before successors;
- empty-frame identity; nested loop descendant handling; deep loop names; and full fork compilation;
- generated typed and legacy programs compile, with typed output explicitly rejecting CS86xx nullable
  warnings at the final revision.

### Executed ordering and scope

- C failure after A/B executes the reversed A/B prefix;
- inner branch failure does not unwind outer scope;
- branch approval rejection preserves enclosing scope;
- forward reducer failure acquires authority and rolls back before starting the failure handler;
- fork-dispatch, fork-path, and loop reducer failures produce only authenticated rollback;
- terminal approval and diagnostic-fork ingress carry complete scope metadata.

### Message authority and corruption

- trigger and journal topology contradiction;
- inverse terminal monotonicity and unmatched completion;
- noncanonical/overflowed scope counters;
- forward timeout versus late completion and stale start during rollback;
- delayed fork start, two failed lanes, stale loop iteration;
- forged post-completion trigger, altered failure kind, corrupt failure ledgers;
- duplicate fork-lane failure, diagnostic fork during rollback/forward dispatch;
- stale approval and join controls;
- never-dispatched occurrence trigger;
- exact nonempty forward execution ID and wrong stable occurrence;
- stable/distinct/injective rollback mapping; and
- null/corrupt dispatch claims.

Most `Execute_GeneratedSaga_*` cases compile the emitted code and invoke handlers, which is stronger
than string inspection. Some `Emit_*` cases assert generated substrings only; those are structural
guards and can false-green if equivalent unsafe code is emitted elsewhere. The final full-compilation
test calls the compiler diagnostics helper directly and rejects all CS86xx warnings, closing the
specific package-probe warning that preceded commit `40edb5f`.

**Potential proof strength:** R3 for emitted shape/compilation and R4 for adversarial handler
execution. **Blind spot:** in-memory invocation does not reproduce Wolverine scheduling, Marten
transactions, or process crash windows.

## EP-5 — failure-route and topology support suites

The diff also changes or adds:

- `FailureHandlerRoleIdentityTests` for role-aware handler identities;
- `BranchHandlerEmitterTests`, `StepCompletedHandlerEmitterTests`,
  `WorkerHandlerEmitterUnitTests`, and `ConfidenceLoweringTests` for changed ingress and successor
  routing;
- `StepExtractorResilienceTests`, topology-semantics tests, and terminal reachability guards for
  accepted syntax and closed topology; and
- `WireStepFingerprintCoverageTests` and round-trip IR tests so compensation fields participate in
  structural fingerprints and survive front-end conversion.

These suites are critical because a correct central compensation component cannot recover an omitted
failure producer or a weakened occurrence identity in an outer emitter. A future inventory/mutation
must ensure every changed route is necessary to at least one failing case.

## EP-6 — real Wolverine/Marten/PostgreSQL behavior

`src/Strategos.Generators.Behavioral.Tests/Workflows/DerivedCompensationWorkflow.cs` defines a concrete
typed workflow and inverse ontology contracts. `CompensationHostFixture` registers its steps and host
dependencies. `CompensationBehaviorTests.Saga_TypedCompensation_DerivesAndExecutesReversedCompletedPrefix`
drives A and B to completion, makes C fail, and expects the trace A, B, C, UndoB, UndoA with no UndoC.

**Potential proof strength:** R5 for one end-to-end supported SagaDocument host path, including
generated code, Wolverine dispatch, Marten/PostgreSQL persistence, real step bodies, and reverse
order. **Blind spots:** the fixture may be unavailable or skipped without PostgreSQL; one linear flow
does not cover crash windows, nested scopes, forks, timeouts, corrupt storage, or external-effect
idempotency. Its exact environment outcome must be recorded rather than inferred.

## EP-7 — fluent API and model checks

`StepConfigurationBuilderTests` now checks typed inverse storage, null rejection, and duplicate
compensation rejection. `CompensationConfigurationTests` checks type capture, inverse structural
identity, null guards, default required flag and timeout, immutability, preservation on `WithTimeout`,
and structural equality. `BuilderApiBaselineTests`, `.editorconfig`, `PublicApi.globalconfig`, and both
public API ledgers cover the newly exposed builder/configuration surface.

**Potential proof strength:** R2 for API shape and local construction. **Blind spot:** a reflection
baseline and analyzer allowlist can drift together; Stage 3 should mutate a tracked #169 member or
compare the intended exported types with the gate scope.

## EP-8 — TypeSpec, wire import, and generated contract

The existing mechanisms include:

- TypeSpec source plus `scripts/contracts-codegen.sh` and generated-artifact drift checks;
- generated C# and standalone/bundled schema for `CompensationConfiguration`;
- `ApprovalFailureConfigTests` schema-definition checks;
- import robustness cases for null/scalar/array/incomplete/blank inverse identity and missing/blank/
  non-string compensation step type;
- resolvable/unresolvable compensation moniker cases;
- projection and round-trip IR fidelity tests;
- AGWF enum, constant, catalog, schema, and Markdown tests updated for 044/045; and
- Contracts packaging tests and package version checks.

**Potential proof strength:** R1-R3 for generated and hand-imported wire shape. **Resolved discovery:**
the first survey pass found that TypeSpec/schema admitted whitespace-only `compensationStepType`
values while `MinimalJsonReader` rejected them. Commit `42b4ed7` adds `@minLength(1)` and
`@pattern(".*\\S.*")` to `StepConfiguration.tsp`; both generated schema forms carry those exact
constraints; generated `CompensationConfiguration.g.cs` calls `RequireNonWhitespace` during both
serialization and deserialization; and `ApprovalFailureConfigTests` directly asserts `minLength`,
`pattern`, and empty/whitespace read/write rejection. The current source mechanisms are aligned, but
this lens has not executed the tests or regeneration guard. **Remaining blind spot:** the closed AGWF
enum and per-entry TypeSpec declarations remain two authored roots unless the updated tests compare
their sets.

## EP-9 — isolated packed consumer

`scripts/verify-generator-consumer-build.sh` now contains all three required consumer cases:

1. legal typed binding plus typed inverse;
2. an illegal seam expected to fail exclusively with AGWF041; and
3. a contradictory inverse expected to fail exclusively with AGWF044 and its intended explanation.

The script inspects package identity/contents, uses a local feed and isolated cache, separates restore
from `--no-restore` build, treats nullable warnings as errors, and distinguishes exit 3 infrastructure
indeterminacy from exit 2 product failure. The second negative prevents a false green where the
analyzer package is present but #169 enforcement is not loaded.

**Potential proof strength:** R5 consumer/package boundary. **Blind spots:** the positive probe's step
implementations need not semantically realize the ontology transitions; the probe proves package API
and analyzer metadata enforcement, not arbitrary executable inversion. Its dependency restore still
uses external package infrastructure and can be indeterminate.

## EP-10 — documentation, CI, review, and adoption

Docs source now covers inverse laws, effective guarantees, typed authoring, diagnostics,
completed-prefix/nested/fork behavior, stable rollback ID, at-least-once idempotency responsibility,
failure retention, SagaDocument-only support, legacy separation, migration/drain requirements, and
Contracts 0.12. The docs build can prove the site compiles and links resolve locally; it cannot prove
the semantics described.

Repository workflows, full solution tests, codegen guards, public API checks, and the package probe
are candidate CI mechanisms. No protected check URL or review result is bound by this Stage 1 lens.
Likewise, Basileus #495 and Exarchos #1895 are coordination records only; no downstream checkout or
released 0.12 package result is existing proof here.

## False-green audit

1. **Filtered diagnostics:** a test that asks only whether AGWF044 exists can ignore unrelated compile
   errors. The shared valid-input harness and exclusive-code package negative are the relevant guard;
   their exact use must be inspected in Stage 3.
2. **String-only generated assertions:** substring presence does not show the guard dominates every
   ingress. Prefer compiled/invoked cases and mutation of the guard.
3. **Wrong subject:** an in-repo project-reference build can succeed while the packed analyzer is
   missing or stale. The isolated nupkg probe is designed to kill this.
4. **Warm dependency cache:** restore can make package provenance ambiguous. The isolated cache and
   byte/digest checks mitigate local Strategos substitution, while external restore remains an
   explicit precondition.
5. **Behavioral environment skip:** unavailable PostgreSQL is not a pass. Record it as indeterminate
   and run the fixture in protected CI with the expected service.
6. **Declared-versus-executed contract:** analyzer success does not prove the inverse step body or
   external effect. Preserve the trust boundary and retain at least one behavioral fixture.
7. **Parallel examples drift:** three inverse semantic implementations can each have green local
   tests while disagreeing on an unshared edge vector. A common corpus or direct parity oracle is
   needed.
8. **Docs/API/catalog co-edit:** multiple hand-maintained copies changed in one diff can agree by
   review but lack a kill. Mutation must demonstrate the executable authority.

## Stage 2 obligation seeds

1. Promote each issue/changelog/documentation promise to a separate obligation with the cheapest
   adequate rung; do not bundle static inverse equivalence with durable runtime behavior.
2. Require shared runtime/AONT216/AGWF044 inverse vectors, including both implication directions,
   authority aliases, effective guarantees, empty identity, missing, opaque, invalid, and precedence.
3. Require structural and semantic coverage of every compensation topology kind and failure ingress,
   with mutation kills for omitted claim creation/validation.
4. Require adversarial execution for forged/stale/duplicate/cross-scope/corrupt messages, prefix
   selection, nested isolation, fork quiescence, reducer ordering, failure-handler ordering, and
   monotonic reconciliation.
5. Require the real PostgreSQL host result and keep the transactional boundary explicit.
6. Require TypeSpec/codegen/schema/parser/catalog/API drift gates and a clean strict packed consumer
   with both AGWF041 and AGWF044 negatives.
7. Record restore/environment inability as Indeterminate, never Verified or Refuted.
8. Leave publication, protected CI/review, and both external adoptions unverified until exact artifacts
   are available.
