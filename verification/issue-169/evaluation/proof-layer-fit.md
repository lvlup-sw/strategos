---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
cost_setting: high
scope_rule: proof-layer-fit evaluation of every active issue-169 ledger obligation in the reverse dependency closure
updated: 2026-09-07
skipped: none within the Proof-Layer Fit lens; the other Stage 3 lenses are separate artifacts
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation intent and acceptance criteria carried through Stage 0
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: action-calculus and operator-ordering context carried through Stage 0
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: preceding typed-workflow proof boundary and recurrence evidence
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate and effective-guarantee proof semantics reused by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream adoption coordination only
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream adoption coordination only
---

# Proof-Layer Fit evaluation — issue #169

## Evaluation boundary

This is a fresh Proof-Layer Fit pass over all 31 active obligations in `ledger.md`. It asks only
whether the assigned rung can establish the claim, whether a cheaper rung can do so, whether **Why
not cheaper** closes every lower rung, whether the proof artifact exists and is reachable, and
whether the recorded state is honest under `evidence-binding.md`.

The immutable product subject was confirmed as commit
`42b4ed741c1f406a2de2fdcdaf602ca53dd91dab`, tree
`2d0f3027308580376819e7b56649592cd0a784bc`, over base
`0ac93e916849cceada616a0e15dd7e6c83b34af1`. This evaluator did not rerun tests. Execution claims
below come only from the revision-bound `mutation-evidence.md` and `final-artifact-checks.md` records.
Repository CI files establish declared PR reachability, not branch-protection membership or a result
for this revision.

## Findings

### PF-1 — R1 construction does not establish independent emission reachability

- **Affected:** `nonproven-inverse-is-never-executable`.
- **Concern:** The R1 artifact soundly constrains `ActionInverseAnalysis` and `ActionRollbackLeaf`,
  but the claim and scope also include the generator emission gate. The emitter derives inverse step
  names from `Step.Compensation` independently, while `WorkflowBindingProofAnalyzer.Analyze` reports
  build diagnostics on a separate generator output path. A private/validated core constructor alone
  cannot prove that rejected configuration is unreachable from compiled generated runtime.
- **Evidence:** `ActionRollbackLeaf` nulls `InverseAction` for non-Proven analyses in
  `src/Strategos.Ontology/Descriptors/ActionRollbackPlan.cs`; generated inverse names are collected
  from compensation configuration in
  `src/Strategos.Generators/Emitters/Saga/SagaCompensationComponentEmitter.cs:51`; the proof analyzer
  is invoked separately at `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:151`. The
  obligation's own consequence is an integration failure: a diagnostic exists while runtime code is
  still dispatched.
- **Suggested action:** Split the R1 result-algebra/plan-construction invariant from an R3
  analyzer-to-emitter closure obligation. Leave packaged analyzer activation to the existing R5
  packed-consumer obligation. If kept combined, R3 is the cheapest sound rung and **Why not cheaper**
  must say why R1 construction does not govern the emitter.

### PF-2 — generated diagnostic parity has an independent hand-authored root

- **Affected:** `generated-diagnostic-authority-is-consistent`.
- **Concern:** R1 is sound for enum, entries, schemas, catalog, and Markdown regenerated from
  TypeSpec. It is not sound for the same claim's live `WorkflowDiagnostics` descriptors, whose title,
  severity, and message are hand-authored. The ledger acknowledges a parity self-test but still
  assigns one R1 rung to the combined claim.
- **Evidence:** `src/Strategos.Generators.Tests/Diagnostics/AgwfCatalogParityTests.cs` explicitly says
  live descriptor metadata is hand-authored and compares it with the generated catalog at runtime.
  `final-artifact-checks.md` binds clean generation and Contracts tests, but not that generator-test
  parity artifact on a protected run.
- **Suggested action:** Split generated representation consistency at R1 from live-descriptor
  agreement at R4, or assign the combined claim R4. Do not treat an R4 backstop as if R1 itself
  controls the hand-written root.

### PF-3 — an API baseline analyzer is structural, and API intent is human

- **Affected:** `public-api-authority-covers-issue-169`.
- **Concern:** The C# type system does not reject removal, widening, or co-drift against an API
  baseline. `Microsoft.CodeAnalysis.PublicApiAnalyzers`, allowlist configuration, and reflection
  inventory are deterministic structural controls, which belong at R3. The phrase “intended
  ... compatibility classification” additionally needs R6 judgment; neither the compiler nor a
  baseline decides intent.
- **Evidence:** `scripts/check-builder-api-stability.sh` runs a baseline analyzer and interprets
  RS0016/RS0017-style diagnostics. `src/Strategos/PublicApi.globalconfig` disables the rules globally
  and relies on an explicit selective re-enable. The obligation itself keeps selective-policy co-drift
  as an open question.
- **Suggested action:** Split intended API/compatibility review at R6 from mechanically enforced API
  drift at R3. Shape properties that the C# compiler genuinely makes unrepresentable may remain a
  narrower R2 obligation.

### PF-4 — a docs build cannot establish semantic honesty

- **Affected:** `docs-and-migration-build-and-state-exact-boundaries`.
- **Concern:** R4 can establish that the Starlight site builds, links resolve, and generated terms are
  present. It cannot establish that prose accurately distinguishes effective guarantees,
  SagaDocument limits, serial folding, retained `OutcomeUnknown`, or at-least-once effects. The
  ledger itself says build success is not prose truth, so the combined R4 assignment is unsound.
- **Evidence:** `.github/workflows/ci.yml` has a documentation-build job, but no semantic claim oracle
  exists. The named “claim audit” is a review activity, not a component test artifact.
- **Suggested action:** Split mechanical documentation integrity at R4 from semantic/trust-boundary
  review at R6. If the claim remains combined, R6 is the cheapest sound rung.

### PF-5 — final-subject binding and merge authority do not share one R5 proof

- **Affected:** `protected-final-subject-and-review-are-bound`.
- **Concern:** SHA/tree/digest/check/review freshness can be checked deterministically as hosted
  evidence binding. Finding disposition and explicit merge authorization are human authority. A
  production-path integration test does not establish both halves merely because the records live on
  a PR.
- **Evidence:** The artifact requires CI results, exactly one CodeRabbit result, dispositions, and
  explicit authorization in one row. `final-artifact-checks.md` correctly leaves all hosted events
  Indeterminate.
- **Suggested action:** Split machine-checkable evidence freshness at R3 from review sufficiency and
  merge authorization at R6. Keep product-path R5 results as inputs to the binding check, not as the
  proof rung for human authority.

### PF-6 — nested-scope selection is deposited at R5 although R4 owns its stated semantics

- **Affected:** `rollback-selects-innermost-concrete-scope`.
- **Concern:** The stated claim is generated saga component semantics: choose the innermost concrete
  scope, roll back its descendants, and preserve the enclosing journal. Those semantics can be
  established by compiled generated-handler execution under controlled effects at R4. The current R4
  fixture already drives the handlers and asserts the outer entry remains `Completed`. Requiring a
  real host for this entire claim duplicates the separate R5 persistence/reload obligations.
- **Evidence:**
  `Execute_GeneratedSaga_InnerBranchFailureDoesNotUnwindOuterScope` in
  `src/Strategos.Generators.Tests/Emitters/DerivedCompensationRuntimeTests.cs` invokes the generated
  handlers, excludes `UndoOuterA`, rolls back only the inner entry, and retains the outer entry. The
  evidence file says the missing R5 artifact is a nested PostgreSQL workflow.
- **Suggested action:** Assign the state-machine selection/preservation claim to R4 and add the
  missing later-enclosing-failure component case. If actual serialization/reload or host concurrency
  is required, split that clause into a narrow R5 obligation rather than lifting all nested-scope
  semantics to R5.

### PF-7 — several artifact fields name prospective proofs, not existing artifacts

- **Affected:** `compensation-topology-covers-every-executable-occurrence`,
  `forward-result-requires-exact-durable-dispatch-authority`,
  `journal-is-versioned-topology-bound-contiguous-and-unique`,
  `rollback-selects-innermost-concrete-scope`,
  `fork-rollback-quiesces-before-inverse-dispatch`,
  `rollback-terminal-lifecycle-is-monotonic`, and
  `proof-authority-is-shared-across-runtime-and-analyzers`.
- **Concern:** Each row's State/open questions honestly disclose the gap, so none is a false
  `Verified`. The **Proof artifact** cell nevertheless reads as a concrete artifact although the
  corresponding machine-readable route inventory, crash-window host test, persisted-corruption
  reload fixture, nested host workflow, concurrent fork host fixture, terminal redelivery host test,
  source-ownership guard, or common inverse corpus does not exist.
- **Evidence:** The per-obligation evidence files repeatedly label these as “Missing R5 artifact” or
  “not found.” Repository search found `TopologyClosureInspector` and extensive example tests, but no
  machine-readable emitter-ingress closure inventory; it found source-linked project items but no
  ownership guard or shared inverse-vector corpus.
- **Suggested action:** Prefix each nonexistent artifact with `Proposed:` or `Missing:` and name the
  existing lower-rung backstop separately. Artifact existence should be readable from the artifact
  cell without reconciling it against the State paragraph.

### PF-8 — “shared vectors” are not yet a shared inverse corpus

- **Affected:** `authored-inverse-is-bidirectionally-equivalent` and, as its broader authority
  dependency, `proof-authority-is-shared-across-runtime-and-analyzers`.
- **Concern:** The R3 assignment for semantic equivalence is sound, but the named backing artifact is
  overstated. Independent runtime, AONT216, and AGWF044 examples exist; one neutral corpus executed
  through all three adapters does not.
- **Evidence:** The authored-equivalence evidence file asks whether parallel tests merely cover
  similar examples. The guard register calls the neutral inverse-vector corpus “the missing explicit
  policy-as-data artifact.” No inverse-vector corpus file exists under `src/`.
- **Suggested action:** Mark the corpus as proposed until it exists. Keep local mutation sensitivity
  as evidence for the runtime adapter only; it cannot stand in for cross-front-end parity.

### PF-9 — three Why-not-cheaper rows do not close every lower rung

- **Affected:** `typed-compensation-is-single-and-mandatory-per-occurrence`,
  `rollback-id-is-stable-distinct-and-injective`, and
  `typed-compensation-wire-roundtrips-and-import-fails-closed`.
- **Concern:** Their assigned R3/R4 rungs are defensible, but the required reasoning is incomplete.
  The first explains construction but not R2. The rollback-ID row addresses R1 and R2 but does not
  say why R3 cannot establish the transform/persistence claim. The wire row addresses generated R1
  output but not R2 or R3 for the independent reader/resolver/projection roots.
- **Suggested action:** Add explicit lower-rung exclusions. In brief: C# types cannot decide imported
  whole-program mode; structural inspection alone cannot establish ID behavior through component
  state; and neither types nor source-shape checks establish malformed-input/round-trip semantics
  across independent implementations.

### PF-10 — the rollback-ID fixture does not establish universal injectivity

- **Affected:** `rollback-id-is-stable-distinct-and-injective`.
- **Concern:** R4 is a suitable home for a behavioral/property proof, but the named existing test is
  a boundary sample, not the “property test” claimed by the ledger. It checks a few values around the
  wrap boundary and therefore does not itself establish injectivity over every nonempty GUID.
- **Evidence:**
  `Execute_GeneratedSaga_RollbackIdentityMappingIsStableDistinctAndInjectiveAtBoundary` constructs
  `beforeMaximum`, `maximum`, and one first-nonempty value. The evidence file expressly leaves the
  all-input proof open. The emitted implementation is a cyclic increment over the nonempty 128-bit
  space, but that mathematical fact is not carried by the named test artifact.
- **Suggested action:** Add a direct construction argument plus a property/metamorphic kill fixture
  for the nonempty-domain permutation, or narrow the claim to the behaviors actually sampled. Keep
  the state Unproven until the chosen artifact is protected and bound.

### PF-11 — downstream adoption's claim, proof, and Indeterminate state describe different things

- **Affected:** `downstream-adoption-is-bound-before-claimed`.
- **Concern:** The Claim is a release-policy implication: Strategos must call adoption complete only
  after evidence is bound. The R5 artifact and Indeterminate reason instead answer a different
  proposition: whether Basileus and Exarchos have actually adopted the package. Missing consumer
  revisions makes actual adoption Indeterminate; it does not make the conditional release-policy
  claim Indeterminate.
- **Evidence:** The row says “adoption is complete only after...” while its evidence file concludes
  “Indeterminate” because no consumer commit/build was found.
- **Suggested action:** Split “consumer adoption is complete for these exact revisions/bytes” at R5,
  presently Indeterminate, from “release claims cannot mark adoption complete without bound
  evidence,” which needs a reachable release-policy guard/review and its own state.

### PF-12 — exact-final package evidence landed after the ledger narrative

- **Affected:** `packed-consumer-loads-and-enforces-typed-compensation-warning-free`; secondarily the
  local portions of `generated-diagnostic-authority-is-consistent` and
  `typed-compensation-wire-roundtrips-and-import-fails-closed`.
- **Concern:** The overall `Unproven` state remains correct because no protected result is bound, but
  the package row says final `42b4ed7` bytes “must be rebound.” They have now been rebound locally.
  The exact-final record also binds clean codegen, 187 Contracts tests, package provenance/digests,
  legal warning-free compilation, exclusive AGWF041/AGWF044 negatives, and exit-3 infrastructure
  classification.
- **Evidence:** `final-artifact-checks.md:21-30` records those exact-tree local results and explicitly
  leaves protected CI/review/publish/merge Indeterminate. `.github/workflows/ci.yml:78-104` declares
  the pack/probe on PRs, but no run result or required-check setting is supplied.
- **Suggested action:** Refresh the State/evidence wording to say “exact-final local pass; protected
  binding absent.” Do not promote any row to `Verified` from this local evidence alone.

## Per-obligation audit

`Proper` below means the state is honest for this dossier; it does not mean the obligation is proved.
“Declared CI” means a repository workflow reaches the project/script on a PR to `main`; it does not
establish that the check is required or that it ran for this SHA.

| # | Obligation | Cheapest sound rung assessment | Why-not-cheaper | Artifact existence and reachability | State assessment |
|---:|---|---|---|---|---|
| 1 | `inverse-contract-is-mechanically-derived` | R1 fits: the inverse contract is constructed from one `ActionContractProof`. | Complete; R1 is cheapest. | Constructor path and focused fixture exist; unit project is in declared CI. | `Unproven` is proper without protected binding. |
| 2 | `authored-inverse-is-bidirectionally-equivalent` | R3 fits semantic implication over source-visible closed contracts. | Complete for R1/R2. | Three implementations/examples exist; the claimed shared corpus does not. | `Unproven` is proper; local runtime mutant kill is narrower than parity. |
| 3 | `inverse-subject-frame-and-authority-are-exact` | R3 fits cross-descriptor and lattice-semantic comparison. | Complete for R1/R2. | Comparisons and runtime/analyzer/workflow fixtures exist; declared CI reaches them. | `Unproven` is proper pending protected parity/sensitivity. |
| 4 | `nonproven-inverse-is-never-executable` | R1 fits only the core result/plan shape; combined emitter reachability needs R3. | Incorrect: “R1 is cheapest” ignores the independent emitter. | Core construction exists; no single construction artifact controls proof-to-emission closure. | `Unproven` is proper, but the rung/artifact require split. |
| 5 | `identity-inverse-is-empty-frame-only` | R4 fits the combined semantic classification and no-dispatch component behavior. | Complete through R3. | Named calculus and generated-runtime fixtures exist; declared CI reaches them. | `Unproven` is proper. |
| 6 | `rollback-plan-is-immutable-and-subject-homogeneous` | R1 fits validated construction and snapshots. | Complete; R1 is cheapest. | Internal construction, `ImmutableArray` snapshots, and fixture exist. | `Unproven` is conservative and proper without protected binding. |
| 7 | `completed-prefix-rolls-back-in-reverse-without-failing-leaf` | R5 fits the claim's real entry point, workers, Marten, PostgreSQL, and step bodies. | Complete through R4. | Real host A/B/C -> UndoB/UndoA fixture exists; declared generic CI appears to include its project. | `Unproven` is proper; the recorded mutant kill is R4, not protected R5. |
| 8 | `noncompensable-leaf-invalidates-whole-scope` | R3 fits whole-graph closure and blocking diagnostic. | Complete for R1/R2. | Recursive plan and AGWF045 fixtures exist. | `Unproven` is proper. |
| 9 | `parallel-rollback-is-noninterfering` | R3 fits deterministic graph/resource conflict analysis. | Complete for R1/R2. | Directional/nested/shared-read fixtures exist; mutation/protected evidence does not. | `Unproven` is proper. |
| 10 | `typed-compensation-is-single-and-mandatory-per-occurrence` | R3 fits the combined fluent/imported whole-program rule. | Incomplete: R2 is not addressed. | Builder/configuration/analyzer fixtures exist. | `Unproven` is proper. |
| 11 | `typed-program-has-one-closed-subject-boundary` | R3 fits compilation-wide catalog ownership and closure. | Complete for R1/R2. | Rooted-catalog and multiple-binding fixtures exist. | `Unproven` is proper. |
| 12 | `compensation-topology-covers-every-executable-occurrence` | R3 is the correct target rung. | Complete for R1/R2. | Topology and closure inspection exist; the exhaustive policy-to-emitter route guard does not. | `Unproven` is proper; artifact must be marked proposed. |
| 13 | `typed-legacy-and-dynamic-programs-never-mix` | R3 fits whole-workflow closed-state classification. | Complete for R1/R2. | Program-kind logic, both-order negatives, dynamic negatives, and legacy positive exist. | `Unproven` is proper. |
| 14 | `typed-compensation-is-saga-document-only` | R3 fits a structural persistence-mode rejection. | Complete for R1/R2. | Analyzer/generated cases exist; exact-tree local mutation was killed. | `Unproven` is proper without protected binding. |
| 15 | `forward-result-requires-exact-durable-dispatch-authority` | R5 fits the transaction visibility clause; R4 owns only exact in-memory authority. | Complete through R4. | R4 cases and one local mutant kill exist; crash/visibility/recovery host artifact does not. | `Unproven` is proper. |
| 16 | `journal-is-versioned-topology-bound-contiguous-and-unique` | R5 fits actual persistence/serialization/reload corruption. | Complete through R4. | Broad in-memory corruption fixtures exist; malformed Marten reload fixture does not. | `Unproven` is proper. |
| 17 | `rollback-selects-innermost-concrete-scope` | R4 is cheapest for the stated generated state-machine semantics; reserve R5 for a split reload/host clause. | Current reason overstates the need for R5. | Compiled/invoked nested R4 fixture exists; nested host/reload artifact does not. | `Unproven` remains proper; rung should move/split. |
| 18 | `fork-rollback-quiesces-before-inverse-dispatch` | R5 fits a claim that includes real concurrency and persistence ordering. | Complete through R4. | Many deterministic permutations exist; concurrent controlled host fixture does not. | `Unproven` is proper. |
| 19 | `inverse-message-role-is-isolated-from-forward-flow` | R4 fits compiled/invoked routing and recursion suppression. | Complete through R3. | Same-CLR-type collision and role fixtures exist; declared CI reaches them. | `Unproven` is proper. |
| 20 | `inverse-state-folds-before-next-inverse` | R5 fits the persisted cross-worker state clause. | Complete through R4. | R4 ordering cases and one linear real-host carrier exist; protected ordering kill does not. | `Unproven` is proper. |
| 21 | `rollback-terminal-lifecycle-is-monotonic` | R5 fits real timeout/redelivery after persistence; R4 owns the generated matrix. | Complete through R4. | Broad R4 cases exist; host failure/timeout redelivery fixture does not. | `Unproven` is proper. |
| 22 | `rollback-id-is-stable-distinct-and-injective` | R4 can own a real property/metamorphic proof; current boundary sample is insufficient for the universal claim. | Incomplete: R3 is not explicitly excluded. | Transform and boundary test exist; claimed property/mutation proof does not. | `Unproven` is proper. |
| 23 | `typed-compensation-wire-roundtrips-and-import-fails-closed` | R4 fits agreement and behavior across independent provider/consumer roots; R1 regeneration is prerequisite. | Incomplete: R2/R3 are not addressed. | Codegen/schema and focused import/round-trip fixtures exist; exact-final local run covers codegen/Contracts, not all generator matrices. | `Unproven` is proper. |
| 24 | `proof-authority-is-shared-across-runtime-and-analyzers` | R1 fits only literal source-link ownership; semantic parity remains R3/R4. | Complete only for the narrow R1 claim. | Source-linked project items exist; ownership guard and neutral inverse corpus do not. | `Unproven` is proper; artifact and claim need narrowing. |
| 25 | `packed-consumer-loads-and-enforces-typed-compensation-warning-free` | R5 fits shipped package/analyzer/generated-consumer composition. | Complete through R4. | Script exists, declared CI invokes it, and exact-final local bytes passed all positive/negative/tri-state checks. | `Unproven` remains proper solely because protected binding is absent; narrative is stale. |
| 26 | `generated-diagnostic-authority-is-consistent` | R1 fits generated outputs; combined live parity needs R4. | “R1 is cheapest” is false for the hand-authored root. | Regeneration and parity fixtures exist; exact-final local record covers generation/Contracts only. | `Unproven` is proper; rung must split/move. |
| 27 | `public-api-authority-covers-issue-169` | R3 owns baseline/allowlist drift; R6 owns intended compatibility. R2 is not sound for the combined claim. | The R1 exclusion is real, but it does not justify R2. | Analyzer, baselines, reflection test, script, and declared CI job exist; removal mutation/protected result does not. | `Unproven` is proper; rung must split/move. |
| 28 | `docs-and-migration-build-and-state-exact-boundaries` | R4 owns build/link; R6 owns semantic truth. Combined claim is R6. | Current reason correctly rejects R1-R3 but incorrectly stops at R4. | Docs and CI build exist; no automatic semantic claim oracle exists. | `Unproven` is proper; rung must split/move. |
| 29 | `downstream-adoption-is-bound-before-claimed` | R5 fits actual consumer adoption; a release-policy implication is a different obligation. | Complete for actual external adoption. | Consumer revisions/suites/digests are absent; issues are coordination only. | `Indeterminate` is proper only for a reworded actual-adoption claim, not the current conditional claim. |
| 30 | `protected-final-subject-and-review-are-bound` | R3 can bind machine records; R6 owns disposition/authorization. R5 inputs do not prove the combined claim. | Current reason shows local checks are insufficient but does not justify R5 over R3/R6. | No PR/check/review/authorization artifact exists. | `Indeterminate` is proper; rung must split. |
| 31 | `executable-inverse-effects-remain-a-consumer-trust-boundary` | R6 fits claim scope, intent, and universal-limit judgment. | Complete through R5. | Docs and discriminating no-op/behavioral fixtures exist; final claim review does not. | `Unproven` is proper. |

## Passes

- No obligation is improperly marked `Verified` or `Violated`. The portfolio preserves the central
  rule that local presence or execution is not protected success.
- `protected-final-subject-and-review-are-bound` correctly remains Indeterminate, and the exact-final
  artifact record separately labels hosted CI/review/publish/merge as Indeterminate.
- The R5 assignments for completed-prefix host behavior, durable commit visibility, persisted journal
  corruption, fork concurrency, cross-worker state folding, terminal redelivery, packaged consumer
  composition, and actual downstream adoption are justified by real boundaries rather than test
  authoring convenience.
- The R3 assignments for predicate equivalence, exact subject/frame/authority, graph-wide
  compensability, interference analysis, closed subject ownership, topology closure, mode separation,
  and SagaDocument exclusion are at the structural layer those claims require.
- The R1 construction assignments for the mechanically derived inverse contract and immutable
  subject-homogeneous rollback plan are appropriately cheap.
- With the three exceptions in PF-9 and the composite-rung findings above, **Why not cheaper** reasons
  discuss soundness rather than ease of authoring.
- Every obligation has a corresponding evidence file. All concrete test names sampled during this
  pass resolve to repository sources. Existing unit/component projects, Contracts tests, package
  probe, docs build, API gate, and codegen guard have declared PR workflow paths.

## Uncertainties

- Repository YAML cannot establish GitHub branch-protection membership, required-check names, a PR
  head, or a result for this revision. Those remain external and Indeterminate.
- `ci.yml` delegates the broad build/test job to an immutable organization workflow revision. Its
  complete internals were not part of the local corpus, so project-pattern inclusion is evidence of
  declared reachability, not proof that category filters, container availability, and indeterminate
  mapping behave as intended.
- No primary Wolverine/Marten transaction/outbox guarantee or crash-window fixture was available for
  the durable claim-before-command clause.
- The R4-versus-R5 release ceiling for nested scope behavior is a product assurance decision. This
  lens's proof-fit conclusion is narrower: the current state-machine claim belongs at R4; any real
  reload/host clause should be made explicit before retaining R5.
- No downstream consumer revision or protected compatibility result was available. This pass cannot
  decide whether Basileus or Exarchos adoption exists.
