---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
evidence_binding: static Stage 3 coverage evaluation of the immutable product diff, Stage 0 scope set, Stage 1 survey, Stage 2 ledger/evidence, guard register, mutation record, and exact-final local artifact record; no protected, published, or downstream result is asserted
cost_setting: high
scope_rule: compare every member of the recorded reverse-dependency closure and the actual changed-file/deletion/proof inventory with an active obligation, including compatibility in both directions, configuration, persistence, trust, operational, package, generated-artifact, and external-consumer boundaries
updated: 2026-09-07
skipped: none
lens: coverage-against-scope-set
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation intent and acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: v2.13 action-calculus operator program and failure-mode context
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: preceding typed-workflow-binding scope and proof boundary
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate, effective-guarantee, and finite-proof semantics reused by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: named downstream contract consumer and adoption coordination boundary
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: named downstream API/schema mirror and adoption coordination boundary
---

# Coverage against the scope set

## Evaluation boundary

This lens asks whether the ledger is the right set for the recorded target. It does not decide
whether an obligation is real, whether its rung is cheapest, or whether local evidence upgrades its
state. The subject is the high-tier diff
`0ac93e916849cceada616a0e15dd7e6c83b34af1..42b4ed741c1f406a2de2fdcdaf602ca53dd91dab`.
The changed-file inventory contains 118 paths, 16,505 insertions, 837 deletions, no deleted file, and
one 88%-similarity relocation of `OntologyActionCatalog.cs` into the shared analyzer source boundary.

I read the eight-part reverse-dependency closure in `stage0.md:72-100`, the six consolidated scope
groups in `survey.md:66-85`, all seven detailed survey records, all 31 active ledger entries, the
guard register, the four mutation probes, the exact-final artifact record, both downstream-adoption
records, and obligation evidence where a broad row might or might not cover a seam. An item counts as
covered only when an active obligation's claim and scope encompass it. A survey seed, guard entry,
test name, open question, rollback column, or rejected-candidate note does not substitute for an
active obligation.

## Scope-to-ledger map

| Recorded closure member | Active coverage | Coverage concern |
|---|---|---|
| Public inverse calculus and rollback algebra | `inverse-contract-is-mechanically-derived`, `authored-inverse-is-bidirectionally-equivalent`, `inverse-subject-frame-and-authority-are-exact`, `nonproven-inverse-is-never-executable`, `identity-inverse-is-empty-frame-only`, `rollback-plan-is-immutable-and-subject-homogeneous`, `completed-prefix-rolls-back-in-reverse-without-failing-leaf`, `noncompensable-leaf-invalidates-whole-scope`, `parallel-rollback-is-noninterfering`, `public-api-authority-covers-issue-169` | COV-01 and COV-02 |
| Ontology graph freeze, AONT216, shared catalog/parser/solver | the inverse-contract rows plus `proof-authority-is-shared-across-runtime-and-analyzers` | COV-02 and COV-05 |
| Typed C#/JSON authoring, immutable configuration, extraction, IR, occurrence identity | `typed-compensation-is-single-and-mandatory-per-occurrence`, `typed-program-has-one-closed-subject-boundary`, `typed-legacy-and-dynamic-programs-never-mix`, `typed-compensation-wire-roundtrips-and-import-fails-closed`, `public-api-authority-covers-issue-169` | COV-06 |
| AGWF044/045 workflow proof, closed scope, multiple bindings, invalid/dynamic precedence, persistence mode | equivalence, exact-boundary, whole-scope, topology, mixed-mode, and SagaDocument obligations | COV-02 |
| Topology ownership and public/generated plan lowering | topology, completed-prefix, nested-scope, fork-quiescence, noninterference, inverse-role, and reducer-order obligations | COV-03 and COV-04 |
| Durable generated protocol, journal, messages, reducers, timeout/redelivery, terminal retention | forward authority, journal integrity, nested scope, fork quiescence, inverse roles, reducer ordering, terminal monotonicity, and rollback-ID obligations | COV-03, COV-04, and COV-06 |
| TypeSpec/generated/hand wire forms, diagnostic catalog, public API, package composition | wire round-trip, generated AGWF authority, public API, and packed-consumer obligations | COV-05 |
| Documentation, migration, delivery, trust boundary, external consumers | docs/migration, downstream adoption, protected-final-subject, and executable-effects trust-boundary obligations | COV-07 |

## Findings

### COV-01 — cooperative cancellation was surveyed and tested but has no active claim

**Affected:** ledger-wide; nearest entries are `inverse-contract-is-mechanically-derived` and
`rollback-plan-is-immutable-and-subject-homogeneous`.

**Concern:** The consolidated public-calculus scope explicitly includes cancellation
(`survey.md:68-70`), and the claim-derived seed requires public inverse/rollback results to be
"cancellation-aware" (`survey.md:211-218`). No active ledger claim states when inverse analysis must
observe cancellation or that cancellation must never be converted into `Invalid`, `Opaque`, or a
partial result. The only active references to cancellation are outside the ledger, even though the
diff adds `ActionInverseTests.InverseAnalysisHonorsCancellation`
(`src/Strategos.Ontology.Tests/Descriptors/ActionInverseTests.cs:642-652`). That new test therefore
has no obligation whose correctness claim it proves.

**Scope:** Public `ActionCalculus.AnalyzeInverse` overloads, propagation into
`ActionContractProofEngine`/`FiniteDomainSolver`, and the closed result algebra.

**Suggested action:** Add a narrow active obligation stating that a cancelled inverse analysis throws
`OperationCanceledException` before returning any semantic verdict and propagates cancellation
through every solver call. Bind the existing test and a mid-analysis cancellation fixture to it. If
only inverse analysis, rather than rollback factory construction, is cancellable by design, state
that exact boundary.

### COV-02 — diagnostic/status precedence and stable witnesses fell out between semantic and metadata obligations

**Affected:** `authored-inverse-is-bidirectionally-equivalent`,
`inverse-subject-frame-and-authority-are-exact`, `noncompensable-leaf-invalidates-whole-scope`,
`generated-diagnostic-authority-is-consistent`, and `proof-authority-is-shared-across-runtime-and-analyzers`.

**Concern:** Stage 0 places AGWF044/AGWF045 precedence in scope (`stage0.md:85-87`). The mechanism
survey then asks for identical front-end precedence/stable counterexamples and separately says
AGWF044 must own inverse disagreement, AGWF045 must own incomplete or unrealizable scope, and earlier
root causes must not produce duplicate compensation diagnostics (`survey/mechanism.md:245-256`). The
ledger proves predicate relations and exact subject/frame/authority, and its AGWF generation row
proves enum/catalog/schema/live-descriptor metadata. It never claims the semantic selection rule:
which `ActionInverseStatus` wins when several conditions apply, which diagnostic owns a mixed
failure, whether an earlier AGWF/AONT root cause suppresses follow-on noise, or whether normalized
reason/counterexample output is stable across the runtime, AONT216, and AGWF044 front ends. The
common-vector question remains only an open question at `ledger.md:454`.

**Scope:** Runtime status/obligation selection, AONT216 graph/source diagnostics, AGWF044 versus
AGWF045 and earlier workflow diagnostics, deterministic failure ordering, and canonical witnesses.

**Suggested action:** Promote the surveyed decision table to an active obligation. Its proof should
run shared multi-fault vectors through all three front ends and compare status, obligation set,
diagnostic ownership/count, and normalized witness. Keep generated metadata parity as the separate
representation claim it already is.

### COV-03 — the durable forward-completion/post-completion-failure transition has no complete obligation

**Affected:** `forward-result-requires-exact-durable-dispatch-authority`,
`compensation-topology-covers-every-executable-occurrence`, and
`journal-is-versioned-topology-bound-contiguous-and-unique`.

**Concern:** The recorded runtime closure includes both forward-dispatch and failure-trigger claims,
reducer ordering, handler ordering, and every failure ingress (`stage0.md:91-94`). The production
trace is more precise: a valid completion must apply the forward reducer, convert the dispatch claim
to a completion journal entry, and only then route a successor; reducer or routing failure must mint
a distinct durable post-completion failure capability tied to that entry
(`survey/production-path.md:105-125`). The active forward-authority claim covers only completion and
**pre-completion** failure consuming `ForwardDispatchClaim` (`ledger.md:286-301`). The topology row
only establishes structural occurrence/route closure (`ledger.md:235-250`), while the journal row
requires noncontradictory claims without specifying how a post-completion capability is created or
consumed (`ledger.md:303-318`). These rows do not collectively state that every approval,
confidence, branch, loop, fork, diagnostic, reducer, and successor-routing failure either consumes
the correct persisted authority exactly once or cannot start rollback.

**Scope:** Forward reducer ordering, `FailureTriggerClaim`, completion-to-journal transition,
post-completion failure ingress, exact failure kind/scope/high-water identity, and successor/failure
handler ordering.

**Suggested action:** Add one active transition obligation over
`dispatch claim -> reduced state + journal entry -> optional post-completion failure claim -> consumed
rollback trigger`. Require a machine-readable ingress matrix and adversarial cases for each
post-completion producer, altered failure kind, duplicate consumption, and crash/reload boundaries.
This is distinct from topology membership and from pre-dispatch transaction visibility.

### COV-04 — the public-parallel/generated-serial refinement is recorded only as prose

**Affected:** `parallel-rollback-is-noninterfering`,
`fork-rollback-quiesces-before-inverse-dispatch`, and
`inverse-state-folds-before-next-inverse`.

**Concern:** Stage 1 explicitly requires the structural parallel law and generated runtime refinement
to remain distinct claims (`survey.md:148-154`). The public-plan obligation covers noninterference and
preservation of parallel branch structure (`ledger.md:184-199`). Fork quiescence covers the point at
which rollback may begin, and inverse-state folding covers reducer-before-next-inverse ordering, but
neither claim says that a quiesced fork is lowered to **serial reverse completion-sequence** execution
or that this lowering remains deterministic while the public plan remains parallel. The fact appears
only in survey prose and in the non-active rejected-candidate note at `ledger.md:575-583`.

**Scope:** Fork completion journal order, lowering of a parallel `ActionRollbackPlan`, serial inverse
dispatch for generic `TState`, preservation of noninterference assumptions, and the boundary to
consumer-owned external-effect commutativity.

**Suggested action:** Add a generated-runtime refinement obligation. It should require two or more
completed lanes, quiescence, exact descending completion-sequence dispatch, reducer visibility
between lane inverses, and no mutation of the public parallel plan. Retain
`executable-inverse-effects-remain-a-consumer-trust-boundary` for effects outside the generic state
fold.

### COV-05 — exact-byte package coverage omits the AONT216 analyzer product

**Affected:** `packed-consumer-loads-and-enforces-typed-compensation-warning-free`,
`authored-inverse-is-bidirectionally-equivalent`, and
`proof-authority-is-shared-across-runtime-and-analyzers`.

**Concern:** The closure includes ontology source diagnostics and analyzer packaging
(`stage0.md:78-81`, `stage0.md:95-97`). `LevelUp.Strategos.Ontology.Generators` is a separately packed
Roslyn product; its project packs its DLL under `analyzers/dotnet/cs`
(`src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj:3-28`) and now source-links
the shared catalog for AONT216 (`:40-47`). The exact-byte consumer script selects and hashes core,
Agents, Contracts, workflow Generators, Identity, and Ontology packages, but does not select,
reference, restore-verify, or exercise `LevelUp.Strategos.Ontology.Generators`
(`scripts/verify-generator-consumer-build.sh:73-119`, `:145-162`). Its negatives establish AGWF041
and AGWF044 only (`:378-415`). The final artifact record proves the ontology-analyzer nupkg was
created and its nuspec names the target commit, but not that Roslyn can load its exact analyzer bytes
or that those bytes emit enabled-by-default Error AONT216 (`final-artifact-checks.md:150-164`,
`:175-193`). None of the active package claims names this second analyzer product.

**Scope:** `LevelUp.Strategos.Ontology.Generators` nupkg contents/dependencies, analyzer load, AONT216
registration/severity, exact-byte provenance, and compatibility impact on older ontology source that
the stronger diagnostic can now reject.

**Suggested action:** Add a shipped-ontology-analyzer obligation and an isolated consumer that
explicitly references the packed ontology-generator package. Verify the analyzer DLL/dependencies,
compile a legal `CompensatedBy` pair, and require a deliberately contradictory or missing named
inverse to fail exclusively with AONT216 and the intended reason. Preserve restore inability as
Indeterminate, as the workflow package probe already does.

### COV-06 — configured inverse timeout is not covered end to end

**Affected:** `typed-compensation-is-single-and-mandatory-per-occurrence`,
`typed-compensation-wire-roundtrips-and-import-fails-closed`,
`inverse-message-role-is-isolated-from-forward-flow`, `journal-is-versioned-topology-bound-contiguous-and-unique`,
and `rollback-terminal-lifecycle-is-monotonic`.

**Concern:** Typed authoring/configuration and runtime timeout behavior are both in the scope set
(`stage0.md:82-84`, `stage0.md:91-94`). The diff adds `Timeout` to the generator's compensation model
(`src/Strategos.Generators/Models/ResilienceModels.cs:55-71`), records either the configured ticks or
a default in each journal entry
(`src/Strategos.Generators/Emitters/Saga/CompensationJournalEmitter.cs:121-141`), validates the exact
ticks against compiled topology
(`src/Strategos.Generators/Emitters/Saga/SagaCompensationComponentEmitter.cs:430-464`), and schedules
the rollback timeout with that value (`:1800-1816`). Existing obligations cover the declaration
count/required flag, inverse identity round-trip, distinct timeout message role, and terminal outcome.
No active claim connects a C# or imported JSON compensation timeout through extraction, IR,
topology, journal, scheduled message, and exact timeout validation; it also does not state the
default when none is supplied.

**Scope:** Fluent and JSON compensation timeout, defaulting/validation, source/import IR parity,
`InverseTimeoutTicks`, `CompensationRollbackTimeout`, and stale/forged timeout rejection.

**Suggested action:** Add an end-to-end configuration obligation with source and imported fixtures
for default and custom timeouts. Assert exact persisted/scheduled ticks and prove that zero,
negative, altered, or stale timeout values cannot transition rollback.

### COV-07 — release/publication is in scope but stops at local pack and PR merge

**Affected:** `packed-consumer-loads-and-enforces-typed-compensation-warning-free`,
`protected-final-subject-and-review-are-bound`, and `downstream-adoption-is-bound-before-claimed`.

**Concern:** Stage 0 explicitly includes release as well as PR, protected CI, and merge boundaries
(`stage0.md:98-100`). The packed-consumer obligation covers locally built candidate bytes. The
protected-final-subject claim covers CI, one CodeRabbit round, merge authorization, PR/merge subject,
and candidate package digests (`ledger.md:541-556`), but does not claim that a `v*` and the independent
`contracts-v0.12.0` tag build the intended commit, that trusted publication succeeds, or that the
downloaded NuGet bytes/provenance match the approved artifacts. Downstream adoption requires a release
disposition but cannot serve as the producer-side publication obligation. The exact-final artifact
record itself states that a future tag rebuild and immutable NuGet publication remain Indeterminate
(`final-artifact-checks.md:216-226`).

**Scope:** Release tags, independent Contracts version/tag, trusted publish job, published package
identity/digests/provenance, post-publish retrieval, and release rollback/deprecation disposition.

**Suggested action:** Add an active publication obligation separate from PR merge and downstream
adoption. Bind the product and Contracts tags to the approved SHA/tree, require all intended package
IDs/versions, capture NuGet-side provenance and downloaded digests, and distinguish a failed publish
from an indeterminate registry/network result. State that deprecation/yanking does not reverse bytes
already restored by consumers.

## Passes

- **The portfolio is not concentrated at expensive proof layers.** Its 31 entries distribute as five
  R1, one R2, nine R3, five R4, ten R5, and one R6. R1-R3 coverage is substantial for generated
  contracts, result construction, public API, semantic proof, topology, program-kind, and
  persistence-mode restrictions. The R5 concentration corresponds to process/persistence/package/
  delivery boundaries rather than a low-risk surface.
- **No low-risk surface is over-invested relative to the declared high-tier target.** Documentation,
  public API, generated diagnostic representation, external adoption, and the consumer trust boundary
  each have one focused entry. The largest clusters follow the high-consequence durable protocol and
  published calculus. This lens found no obligation-count quota behavior and no low-risk cluster that
  displaced the risky runtime or package surfaces.
- **Reverse compatibility is represented.** `typed-compensation-wire-roundtrips-and-import-fails-closed`
  requires new code to read legacy omission while rejecting present malformed typed data;
  `typed-legacy-and-dynamic-programs-never-mix` preserves a pure-legacy positive;
  `typed-compensation-is-saga-document-only` explicitly keeps legacy EventSourced behavior; and
  `public-api-authority-covers-issue-169` carries the source/API compatibility classification.
  The closed-enum reverse direction is assigned to the generated-diagnostic and downstream-adoption
  entries rather than incorrectly called additive for old consumers.
- **The persistence and trust boundaries are not collapsed into component correctness.** Dispatch
  visibility, real-host completed-prefix behavior, corrupt persisted reload, nested/fork schedules,
  reducer state, redelivery, and external effects are separate active claims. The packed no-op probe
  is explicitly prevented from impersonating an executable restoration proof by
  `executable-inverse-effects-remain-a-consumer-trust-boundary`.
- **Operational reversal is represented up to publication.** The journal/version obligation rejects
  unfamiliar/corrupt state, the docs/migration obligation requires drain-or-version guidance, and
  rollback columns consistently retain saga evidence rather than synthesize success. There is no
  claimed automatic backfill or safe downgrade. COV-07 is the remaining delivery event, not a missing
  in-place downgrade mechanism.
- **The known external closure is represented without inventing success.** Basileus #495 and Exarchos
  #1895 share one explicit evidence-binding obligation and each has a separate adoption record naming
  its API/schema/diagnostic compatibility fixtures. Their issue links remain coordination only.
- **Most new proof code has a corresponding obligation and an anti-vacuity boundary.** The new
  calculus, AONT216, workflow-proof, topology, runtime, behavioral, builder, import, Contracts,
  public-API, and package tests map to active entries. The 3,693-line generated-runtime suite is
  justified by distributed ingress and durable-state risk, while the topology guard calls out
  substring-only false greens. The package probe's no-op executable body maps to the explicit trust
  boundary. The four recorded mutants map to bidirectional equivalence, reverse prefix order, exact
  occurrence authority, and EventSourced exclusion. COV-01 identifies the one clear new proof with
  no active claim.
- **Deletion review found no deleted product or test file.** The shared catalog is a relocation, not
  loss. The removed name/list rollback helper and its old test are explicitly recorded as an
  unshipped public-API replacement (`survey/history-and-recurrence.md:180-184`); structured-plan
  replacement behavior and compatibility classification are covered by the rollback-plan and public-
  API obligations. The old frame-only AONT216 check is replaced by the compilation-wide semantic
  analyzer and maps to the semantic/parity entries. No otherwise-unrepresented removed check was
  found.
- **There is no changed runtime feature flag or deployment-time configuration switch.** Activation is
  controlled by closed program kind, persistence mode, compensation metadata, and analyzer
  diagnostics; the first three are represented. COV-06 isolates the one configuration value whose
  propagation is missing rather than inventing a feature-flag obligation.

## Uncertainties

- The recorded external reverse closure names Basileus and Exarchos. No authoritative consumer
  registry or dependency graph was available in the dossier, so this evaluation cannot establish
  that no third repository consumes the Contracts closed enum, JSON schema, builder mirror, or
  analyzer packages. I did not expand beyond the recorded closure.
- The `public-api-authority-covers-issue-169` wording may be intended to include every deliberate
  removal as part of "compatibility classification." I treated the two unshipped name/list helpers as
  covered because Stage 1 names them and the replacement is explicit. If the portfolio requires one
  claim per deletion rather than one public-delta claim, that entry should name the two removed
  signatures directly.
- COV-03 could be expressed as a new obligation or by widening the forward-authority claim. The
  coverage requirement is the complete persisted transition and every post-completion producer; this
  lens does not prescribe ledger granularity.
- COV-04 does not assert that serial lowering is wrong. It says the declared refinement is not an
  active correctness claim. Whether the intended public contract promises a particular order for
  independent fork lanes is a product-policy decision, but current docs and generated behavior do
  make the reverse-completion rule observable.
- COV-06 concerns the newly introduced generator/runtime propagation of an existing public
  compensation timeout. If maintainers intentionally exclude exact timeout configuration from issue
  #169, the recorded Stage 0 runtime/configuration closure should be narrowed explicitly; silence
  currently reads as coverage.
- This lens did not re-run tests or assess assertion logic beyond obvious subject mismatch/vacuity.
  `mutation-evidence.md` and `final-artifact-checks.md` were used only to determine whether proof
  families and artifact boundaries have matching obligations. Protected CI, review, publication, and
  downstream execution remain separate evidence events.
