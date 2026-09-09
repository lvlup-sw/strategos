---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: three independent refutation attempts for every active issue-169 obligation in the reverse dependency closure
updated: 2026-09-07
skipped: none within the Refutation lens; execution results are consumed only from revision-bound evidence records
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation intent and acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: action-calculus and operator-ordering context
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: preceding typed-workflow proof boundary
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate and effective-guarantee proof semantics reused by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream adoption coordination only
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream adoption coordination only
---

# Refutation evaluation — issue #169

## Boundary and method

This is a fresh Refutation-lens pass over the 31 active obligations in `ledger.md`. For every
obligation I independently tested (1) whether the asserted failure has a reachable product or
delivery path and whether its premise was validated, (2) whether another mechanism already makes
the failure impossible or the obligation merely duplicates another row, and (3) whether the named
proof actually establishes the claim. I applied the required default-to-refuted rule: uncertainty
about the reality of an obligation counts against it. A weak proof assignment does not by itself
kill a real obligation; that disagreement is recorded as dissent.

The inspected product subject is commit
`42b4ed741c1f406a2de2fdcdaf602ca53dd91dab`, tree
`2d0f3027308580376819e7b56649592cd0a784bc`, over base
`0ac93e916849cceada616a0e15dd7e6c83b34af1`. Local execution claims below come only from
`mutation-evidence.md` and `final-artifact-checks.md`, which bind their results to that subject.
Issue #169 directly requires computed rollback of the completed prefix, whole-composite refusal for
a noncompensable leaf, rejection of a contradicting authored inverse, nested-scope isolation, and a
machine-checked generator. The downstream issues remain open coordination records and name no
consumer implementation revision.

| Set | Survived | Refuted |
|---|---:|---:|
| Active obligations | 31 | 0 |
| Candidate claims rejected before promotion | 0 | 5 |

## Active-obligation verdicts

### 1. `inverse-contract-is-mechanically-derived` — SURVIVED

- **Reachability/premise attempt:** `ActionCalculus.AnalyzeInverse` is public and reaches the proof
  engine and derived-contract construction at
  `src/Strategos.Ontology/Descriptors/ActionCalculus.cs:11-89`; graph freeze also invokes the
  calculus from `src/Strategos.Ontology/OntologyGraphBuilder.cs:701-760`. A wrong swap therefore
  reaches runtime and analyzer consumers. Issue #169 explicitly makes the inverse a derivation from
  the forward action, so the premise is not inherited from an unvalidated comment.
- **Independent-guarantee/duplication attempt:** Formula equivalence of an authored descriptor does
  not derive the expected descriptor, and exact frame/subject checks do not determine which
  requirement and guarantee to compare. This row supplies the expected contract consumed by rows 2
  and 3 rather than duplicating them.
- **Named-proof attempt:** The construction assigns effective guarantee to inverse requirement and
  hard requirement to inverse guarantee at `ActionCalculus.cs:68-89`; the exact-tree mutation record
  also binds the swap backstop. Dissent: the protected execution required by the ledger is absent,
  and the proof object does not independently attest the descriptor/lattice inputs. Those are proof
  limits, not evidence that the obligation is unreal.
- **Verdict:** **SURVIVED.** All three attacks leave a distinct, reachable derivation obligation.

### 2. `authored-inverse-is-bidirectionally-equivalent` — SURVIVED

- **Reachability/premise attempt:** Authors can supply a weaker or stronger inverse through three
  reachable proof front ends. `ActionCalculus.cs:841-868` and
  `src/Strategos.Ontology.Generators/Analyzers/OntologyInverseContractAnalyzer.cs:184-207` each test
  both implication directions; the workflow analyzer does likewise at
  `src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:797-813,1063-1086`. The issue's
  contradicting-authored-compensation acceptance criterion validates the premise.
- **Independent-guarantee/duplication attempt:** Exact identity, subject, frame, and authority can all
  match while formulas differ semantically. Conversely, the derived swap alone says nothing about
  an independently authored descriptor. Rows 1 and 3 cannot guarantee this claim.
- **Named-proof attempt:** The two directional calls are discriminating, and
  `mutation-evidence.md` records that deleting authored-to-derived implication killed the exact-tree
  suite. Dissent: the ledger names shared positive/negative vectors, but no single shared corpus
  proves parity across all three front ends, and the mutation was local rather than protected.
- **Verdict:** **SURVIVED.** The counterexample class is reachable and not covered by another row.

### 3. `inverse-subject-frame-and-authority-are-exact` — SURVIVED

- **Reachability/premise attempt:** A descriptor can name the right formulas while targeting another
  ontology subject, touching a different resource set, or requesting excess authority. Runtime
  validation at `ActionCalculus.cs:159-221`, ontology analysis at
  `OntologyInverseContractAnalyzer.cs:114-181`, and workflow analysis at
  `WorkflowBindingProofAnalyzer.cs:952-1042` all contain separate comparisons, confirming that these
  states are representable inputs rather than hypothetical impossible states.
- **Independent-guarantee/duplication attempt:** Bidirectional predicate equivalence does not imply
  subject, canonical frame, or authority equality. The subject-wide workflow boundary in row 11
  also cannot establish the per-forward/per-inverse equality here.
- **Named-proof attempt:** The implementations compare exact subject and canonical frame and use
  lattice implication in both directions for semantic authority. Dissent: the ledger's claimed
  runtime/analyzer/workflow vector parity is not embodied in one revision-bound cross-front-end
  artifact, and protected exact-subject execution is missing.
- **Verdict:** **SURVIVED.** The obligation owns independent rollback authority boundaries.

### 4. `nonproven-inverse-is-never-executable` — SURVIVED

- **Reachability/premise attempt:** `ActionInverseAnalysis` accepts the closed non-Proven states, while
  public rollback factories are callable independently. `ActionRollbackLeaf` erases executable
  inverse identity unless the analysis is Proven at
  `src/Strategos.Ontology/Descriptors/ActionRollbackPlan.cs:27-45`. Generated sources are emitted on
  a separate generator output path from proof diagnostics at
  `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:122-152`, so proof-to-emission closure is
  a real seam.
- **Independent-guarantee/duplication attempt:** AGWF044/045 are enabled-by-default Error descriptors
  at `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:689-706`, which prevents a rejected
  compilation from becoming an executable assembly. That mechanism narrows, but does not erase, the
  public core-plan path or the need to keep diagnostic and emission activation together. No other
  obligation covers both.
- **Named-proof attempt:** Closed construction proves the core result algebra and leaf behavior, but
  the named R1 artifact alone does not prove the separate generator emission gate. This is explicit
  dissent about the proof's reach, not a refutation of the fail-closed obligation.
- **Verdict:** **SURVIVED.** One attack found a second safety mechanism, but the other two leave
  reachable independent surfaces; the majority does not refute.

### 5. `identity-inverse-is-empty-frame-only` — SURVIVED

- **Reachability/premise attempt:** Empty-frame actions and missing explicit inverses are accepted
  authoring shapes. `ActionCalculus.cs:117-151` distinguishes a legitimate empty-frame identity from
  a broken explicit name and from a non-empty missing inverse, so silently skipping a mutating action
  is a concrete branch in the implementation.
- **Independent-guarantee/duplication attempt:** General non-Proven nonexecution does not cover the
  distinct Proven identity state, and whole-scope compensability trusts this classification. Neither
  the type system nor formula equivalence implies that the canonical frame is empty.
- **Named-proof attempt:** The classification is directly visible in construction and is backed by
  calculus and generated identity cases named in the evidence. Dissent: component examples do not
  by themselves prove every generated dispatch route omits identity workers, and no protected result
  is bound.
- **Verdict:** **SURVIVED.** The identity exception is reachable, semantically distinct, and capable
  of hiding a real effect if misclassified.

### 6. `rollback-plan-is-immutable-and-subject-homogeneous` — SURVIVED

- **Reachability/premise attempt:** The rollback factories are public at `ActionCalculus.cs:627-769`
  and accept caller-owned child collections. Without snapshots and validation, callers could mutate
  a returned plan or compose actions across subjects. The private constructor at
  `ActionRollbackPlan.cs:130-200` performs the relevant copying and closed-shape checks, which shows
  those inputs reach the boundary.
- **Independent-guarantee/duplication attempt:** Predicate proof, leaf compensability, and workflow
  topology do not govern a caller using the public ontology plan API. Immutable arrays prevent later
  mutation only after correct construction; they do not make mixed-subject input impossible before
  validation.
- **Named-proof attempt:** Private validated construction, get-only properties, immutable-array
  snapshots, and normalized factories are a direct construction proof. Dissent: the ledger calls
  this R1 although several properties are compiler/type-system invariants; the protected API check is
  not bound. That does not make the contract redundant.
- **Verdict:** **SURVIVED.** This is the sole obligation closing malformed public rollback plans.

### 7. `completed-prefix-rolls-back-in-reverse-without-failing-leaf` — SURVIVED

- **Reachability/premise attempt:** The generated runtime journals only successful completion and
  selects completed entries in descending order at
  `src/Strategos.Generators/Emitters/Saga/SagaCompensationComponentEmitter.cs:268-289,1712-1716`.
  A/B-complete, C-fails is both a normal runtime path and an explicit issue #169 acceptance case.
- **Independent-guarantee/duplication attempt:** Reverse order in the ontology plan
  (`ActionCalculus.cs:668-674`) cannot guarantee Wolverine dispatch, persistence, worker execution,
  or exclusion of C. Journal integrity and state-fold ordering are necessary but do not imply the
  selected prefix.
- **Named-proof attempt:** `mutation-evidence.md` records eight exact-tree failures after reversing
  the runtime ordering, including the UndoB/UndoA discriminator, and the named host fixture crosses
  real bodies. Dissent: no protected exact-subject host result is bound, and PostgreSQL absence must
  remain Indeterminate.
- **Verdict:** **SURVIVED.** This is a primary, reachable end-to-end acceptance obligation.

### 8. `noncompensable-leaf-invalidates-whole-scope` — SURVIVED

- **Reachability/premise attempt:** A reachable non-empty-frame action can omit compensation. The
  recursive plan factories propagate noncompensability, and the workflow analyzer rejects any such
  occurrence at `WorkflowBindingProofAnalyzer.cs:824-905`. Issue #169 expressly says a composite
  containing a non-compensable leaf cannot be rollback-safe.
- **Independent-guarantee/duplication attempt:** Row 4 prevents dispatch from one non-Proven leaf; it
  does not prevent a surrounding sequence, parallel group, or workflow from advertising and partly
  executing rollback. Topology coverage also says where leaves are, not whether every one is
  compensable.
- **Named-proof attempt:** Recursive plan state plus AGWF045 whole-program traversal discriminate the
  failure. Dissent: analyzer vectors establish enumerated graph shapes, while their exhaustiveness
  still depends on the separate topology-closure obligation and lacks protected binding.
- **Verdict:** **SURVIVED.** The upward propagation claim is distinct and directly required.

### 9. `parallel-rollback-is-noninterfering` — SURVIVED

- **Reachability/premise attempt:** Public parallel-plan construction accepts arbitrary nested plans.
  `ActionCalculus.cs:690-758` explicitly checks write/write and both write/read directions, proving
  that shared-resource combinations reach the decision point. A shared-read-only combination remains
  an intended positive path.
- **Independent-guarantee/duplication attempt:** Runtime inverse workers are serialized, but serial
  execution does not make a structurally parallel plan semantically safe: one inverse can invalidate
  the next inverse's read requirement. Subject homogeneity and exact frames provide inputs, not the
  interference decision.
- **Named-proof attempt:** Pairwise resource analysis covers both directions and nested read
  footprints in source. Dissent: the named protected vectors and both-direction mutation kills are
  not bound, so the evidence does not establish regression sensitivity on the final protected path.
- **Verdict:** **SURVIVED.** Serialization is not a discriminating refutation of semantic
  interference.

### 10. `typed-compensation-is-single-and-mandatory-per-occurrence` — SURVIVED

- **Reachability/premise attempt:** Fluent configuration can call `Compensate` twice and imported
  configuration can set typed `RequiredOnFailure=false`. The builder rejects duplicates at
  `src/Strategos/Builders/StepConfigurationBuilder.cs:84-115`; the workflow proof independently
  rejects typed opt-out at `WorkflowBindingProofAnalyzer.cs:729-747`.
- **Independent-guarantee/duplication attempt:** A builder guard cannot protect imported or collapsed
  generator representations, while program-kind mixing operates across occurrences rather than
  within one occurrence. Exact inverse proof also cannot decide which of two declarations owns the
  code identity.
- **Named-proof attempt:** The guard and analyzer checks establish the two principal source paths.
  Dissent: the combined proof is distributed across front ends, and the evidence does not bind one
  protected matrix covering fluent, import, alias collapse, and both source orders.
- **Verdict:** **SURVIVED.** Last-write-wins and typed opt-out are independently reachable invalid
  states.

### 11. `typed-program-has-one-closed-subject-boundary` — SURVIVED

- **Reachability/premise attempt:** Workflows can bind multiple definitions and actions can resolve
  to different subjects or ambiguous catalogs. `WorkflowBindingProofAnalyzer.cs:105-190` builds the
  rooted catalog and explicitly rejects missing, multiple, and cross-subject workflow bindings.
- **Independent-guarantee/duplication attempt:** Per-inverse subject equality in row 3 still permits
  two individually consistent subject families in one generated workflow. CLR type identity and
  string names cannot independently establish ontology ownership.
- **Named-proof attempt:** SymbolKey-rooted resolution and the workflow-group subject check are
  discriminating structural analysis. Dissent: no one artifact proves catalog-root parity across all
  front ends, and protected multiple-binding/cross-subject cases remain absent.
- **Verdict:** **SURVIVED.** This is a compilation-wide boundary not implied by leaf validity.

### 12. `compensation-topology-covers-every-executable-occurrence` — SURVIVED

- **Reachability/premise attempt:** `BuildOccurrenceMap` deliberately enumerates main, loop, fork,
  failure, confidence, and approval steps at
  `WorkflowBindingProofAnalyzer.cs:1118-1182`, while `CompensationTopology.Build` independently adds
  fork, branch, loop, and root occurrences at
  `src/Strategos.Generators/Models/CompensationTopology.cs:209-276`. The analyzer rejects an accepted
  action absent from that topology at `WorkflowBindingProofAnalyzer.cs:845-863`; tests at
  `WorkflowBindingTopologySemanticsTests.cs:403-531` demonstrate reachable confidence and approval
  omissions.
- **Independent-guarantee/duplication attempt:** Central topology construction cannot force every
  independent start/completion/failure emitter to use it. Whole-scope compensability trusts the
  topology it receives and therefore cannot detect an occurrence never inventoried. No other row
  owns route-to-topology closure.
- **Named-proof attempt:** Exact keys and the current unjournaled-phase check catch known omissions.
  Dissent: `TopologyClosureInspector` uses a hand-maintained method-name/ownership policy and is not
  the named machine-readable inventory of every emitted handler family; a new emitter and the policy
  can drift together.
- **Verdict:** **SURVIVED.** The recurrence evidence and executable negative fixtures defeat the
  misreading/unreachability attacks.

### 13. `typed-legacy-and-dynamic-programs-never-mix` — SURVIVED

- **Reachability/premise attempt:** Compensation declarations have Missing, Resolved, and
  DynamicOrInvalid states; `CompensationProgramKind` classifies their whole-program combinations at
  `CompensationTopology.cs:31-44,161-202`. Mixed programs deliberately enter the derived runtime so
  whole-scope preflight can fail closed rather than silently selecting a legacy path.
- **Independent-guarantee/duplication attempt:** The single-declaration guard does not prevent one
  valid typed occurrence beside a valid legacy occurrence. Per-inverse proof examines declarations
  individually and therefore cannot replace a whole-program compatibility decision.
- **Named-proof attempt:** Closed classification and AGWF044/045 cases are appropriate structural
  evidence. Dissent: source/import and both-order matrices are not protected-bound, and generated
  sources may still be produced even though Error diagnostics block assembly execution.
- **Verdict:** **SURVIVED.** Mixed-mode authoring is representable and semantically distinct.

### 14. `typed-compensation-is-saga-document-only` — SURVIVED

- **Reachability/premise attempt:** EventSourced is a supported persistence mode, and consumer
  `ApplyEvent` code is opaque to static contract proof. The workflow analyzer rejects a typed
  EventSourced program before acceptance at `WorkflowBindingProofAnalyzer.cs:147-151`; the docs state
  the same boundary. `mutation-evidence.md` records a discriminating failure when this rejection was
  removed.
- **Independent-guarantee/duplication attempt:** Compiling an event fold, exact wire identity, and
  inverse contract equivalence do not prove that replay and the live inverse state produce the same
  state. The general consumer trust-boundary row is broader; this row chooses a specific fail-closed
  persistence policy.
- **Named-proof attempt:** The persistence predicate plus typed-negative and legacy-positive cases is
  the right structural proof. Dissent: only the negative-predicate mutation is exact-tree local;
  protected source/import positives and negatives are not bound.
- **Verdict:** **SURVIVED.** A compiling no-op fold is a concrete false green, not a refutation.

### 15. `forward-result-requires-exact-durable-dispatch-authority` — SURVIVED

- **Reachability/premise attempt:** Worker completion and pre-completion failure messages are external
  ingress. The emitter records claims only after validating exact occurrence, action, execution,
  scope, and routing identity at
  `SagaCompensationComponentEmitter.cs:193-289,1003-1113`. A forged or stale result can therefore
  reach these handlers and would invent rollback authority if matching were weakened.
- **Independent-guarantee/duplication attempt:** Journal uniqueness is checked after a completion is
  admitted; it cannot prove that the completion had authority or that the claim committed before the
  worker command became visible. Source statement order and Wolverine/Marten transaction visibility
  are not equivalent guarantees.
- **Named-proof attempt:** Exact-match R4 cases and the stable-occurrence mutant establish identity
  sensitivity. Dissent: the named R5 fault-injected host proof of claim-commit/outbox visibility is
  absent, and repository evidence does not identify the Wolverine/Marten transactional mode that
  would close the crash window.
- **Verdict:** **SURVIVED.** The external ingress and durability seam remain reachable and unique.

### 16. `journal-is-versioned-topology-bound-contiguous-and-unique` — SURVIVED

- **Reachability/premise attempt:** Saga documents persist across versions and can reload old or
  malformed state. Generated validators check version/topology, contiguous sequence/high-water,
  unique execution/rollback/scope-occurrence identity, and canonical scope history at
  `SagaCompensationComponentEmitter.cs:427-468,1333-1399` before selection.
- **Independent-guarantee/duplication attempt:** Exact dispatch authority governs new entries, not
  previously persisted or externally corrupted documents. Stable rollback IDs do not imply sequence
  continuity or topology compatibility. Thus rows 15 and 22 cannot guarantee this history claim.
- **Named-proof attempt:** In-memory corruption cases exercise generated validators, but the named
  proof is persist/reload fault injection. Dissent: no revision-bound real-storage malformed-document
  fixture exists, so serialization, database hydration, and validator ordering remain unproved.
- **Verdict:** **SURVIVED.** Persisted untrusted history is a distinct reachable input boundary.

### 17. `rollback-selects-innermost-concrete-scope` — SURVIVED

- **Reachability/premise attempt:** The topology has Root, LoopIteration, BranchPath, and Fork scope
  kinds (`CompensationTopology.cs:15-28`), and rollback selection filters journal entries by the
  exact expected concrete scope at `SagaCompensationComponentEmitter.cs:1634-1775`. Nested failures
  are explicit in issue #169, which says an inner-scope failure must not unwind the enclosing scope.
- **Independent-guarantee/duplication attempt:** Topology closure proves occurrence ownership but not
  which persisted concrete instance a failure selects. Completed-prefix reversal can be correct
  inside the wrong scope. Neither row duplicates nested-scope isolation.
- **Named-proof attempt:** Generated-saga dynamic cases exercise scope filtering and preserved outer
  history. Dissent: the named R5 nested SagaDocument host fixture is not bound, and the ledger itself
  leaves a human question about whether R4 was intended as the release ceiling.
- **Verdict:** **SURVIVED.** The precise failure boundary is reachable and primary acceptance scope.

### 18. `fork-rollback-quiesces-before-inverse-dispatch` — SURVIVED

- **Reachability/premise attempt:** Fork lanes can complete or fail in any delivery order. The
  generated selector checks complete lane histories and rejects active forward claims before inverse
  dispatch at `SagaCompensationComponentEmitter.cs:1656-1707`, with a separate fork-quiescence
  predicate beginning at line 1425. Delayed starts and late completions therefore reach a genuine
  concurrency boundary.
- **Independent-guarantee/duplication attempt:** General terminal-state guards do not prove all lanes
  are terminal before the first inverse, and journal continuity can hold for an incomplete prefix.
  Structural noninterference concerns resources, not forward-worker quiescence.
- **Named-proof attempt:** R4 message-order permutations are discriminating for the state machine.
  Dissent: serial invocation cannot establish production persistence races; the named controlled-host
  concurrent fixture is unidentified and no protected result is bound.
- **Verdict:** **SURVIVED.** No existing invariant makes partially live fork rollback unreachable.

### 19. `inverse-message-role-is-isolated-from-forward-flow` — SURVIVED

- **Reachability/premise attempt:** Authors may reuse the same CLR state/result types for forward,
  inverse, and failure steps. The emitter creates distinct inverse start/completion/failure/timeout
  messages and inverse-specific handlers; inverse failure paths retain failure rather than recursively
  invoking the forward failure trigger at `SagaCompensationComponentEmitter.cs:1826-2085`.
- **Independent-guarantee/duplication attempt:** CLR generic type distinction alone is insufficient
  when payload types collide; handler role/registration and generated message identity are the
  discriminators. Terminal monotonicity begins after routing and therefore cannot prevent a
  misrouted inverse result from advancing forward flow.
- **Named-proof attempt:** Compiled role-collision execution and registration assertions directly test
  the seam. Dissent: only component-level cases are recorded; no protected packaged/host invocation
  binds role registration on the final subject.
- **Verdict:** **SURVIVED.** Same-type role reuse is reachable and no other row owns routing identity.

### 20. `inverse-state-folds-before-next-inverse` — SURVIVED

- **Reachability/premise attempt:** An inverse may return `UpdatedState` needed by the next inverse or
  failure handler. The completion emitter applies the state through `StateApplicationHelper` before
  setting RolledBack and selecting the next inverse at
  `SagaCompensationComponentEmitter.cs:1895-1912`, demonstrating an observable ordering seam.
- **Independent-guarantee/duplication attempt:** Serial worker dispatch prevents simultaneous folds
  but does not determine fold-before-dispatch order. Reverse journal selection and terminal
  monotonicity likewise do not guarantee that UndoA sees UndoB's state.
- **Named-proof attempt:** The named UndoB-output-required-by-UndoA carrier is discriminating, and R4
  source/order cases back it. Dissent: no protected mutation or real-host persisted-state result is
  bound; source order alone cannot prove transaction visibility across workers.
- **Verdict:** **SURVIVED.** The reducer result has a reachable downstream observer and a distinct
  ordering contract.

### 21. `rollback-terminal-lifecycle-is-monotonic` — SURVIVED

- **Reachability/premise attempt:** At-least-once delivery makes stale completion, failure, timeout,
  and trigger messages normal reachable ingress after RolledBack, Failed, OutcomeUnknown, or finished.
  The emitter repeats terminal and active-entry checks across those handlers at
  `SagaCompensationComponentEmitter.cs:1826-2085`; failure and timeout retain distinct states.
- **Independent-guarantee/duplication attempt:** Stable IDs correlate a retry but do not forbid state
  resurrection. Exact journal validation describes history and does not guarantee every later handler
  refuses it. No single other obligation covers all terminal ingress.
- **Named-proof attempt:** The generated monotonicity matrix covers many redelivery permutations.
  Dissent: repeated per-handler guards are not a centralized construction proof, and no R5 delayed
  transport/redelivery fixture is identified for failure or timeout.
- **Verdict:** **SURVIVED.** Redelivery makes the failure reachable; correlation does not guarantee
  monotonicity.

### 22. `rollback-id-is-stable-distinct-and-injective` — SURVIVED

- **Reachability/premise attempt:** Every forward execution ID is persisted and transformed into the
  inverse retry/deduplication identity. The generated transform at
  `SagaCompensationComponentEmitter.cs:303-325` increments the 128-bit value and maps the all-ones
  boundary to one; thus boundary and collision behavior directly affect consumer retries.
- **Independent-guarantee/duplication attempt:** GUID nonemptiness and journal uniqueness validate
  observed values, but neither derives deterministic retry identity nor prevents two forward IDs
  mapping to one rollback ID. Consumer idempotency assumes rather than guarantees this mapping.
- **Named-proof attempt:** Direct inspection supplies a universal argument: on the nonzero GUID domain
  the transform is one cycle, so it is deterministic, nonzero, distinct, and injective. Dissent: the
  ledger names a property/boundary test, while recorded tests are samples and no mutation is bound.
  The source proof is stronger than that sampled artifact, but should be named honestly.
- **Verdict:** **SURVIVED.** The claim is real and presently derivable; proof success does not make the
  consumer correlation contract redundant.

### 23. `typed-compensation-wire-roundtrips-and-import-fails-closed` — SURVIVED

- **Reachability/premise attempt:** TypeSpec/generated DTO, JSON reader, resolver bridge, projection,
  and generator IR are independent representations. The schema requires a nonblank CLR moniker at
  `src/Strategos.Contracts/Workflow/StepConfiguration.tsp:38-52`; the hand reader rejects malformed
  typed objects at `src/Strategos.Generators/Import/MinimalJsonReader.cs:737-778`, and the bridge
  resolves CLR identity fail closed at `src/Strategos.Generators/Import/WireToModelBridge.cs:1058-1100`.
- **Independent-guarantee/duplication attempt:** Generated-schema correctness cannot govern the hand
  parser or symbol resolver, and analyzer proof cannot recover an inverse identity silently dropped
  during import. Legacy omission is a separate valid state, so treating malformed presence as
  omission would evade mixed-mode checks.
- **Named-proof attempt:** `final-artifact-checks.md` binds clean codegen and 187/187 Contracts tests
  locally. Dissent: no single protected matrix binds projection, serialization, malformed presence,
  symbol resolution, IR, and fingerprint round trip against the exact packages.
- **Verdict:** **SURVIVED.** Multiple semantic roots make silent typed-to-legacy downgrade reachable.

### 24. `proof-authority-is-shared-across-runtime-and-analyzers` — SURVIVED

- **Reachability/premise attempt:** Runtime, ontology analyzer, and workflow analyzer all normalize
  predicates and decide implication. Project source links share the neutral normalizer/finite solver,
  while each front end still assembles status, frame, authority, catalog, and reasons. Divergence is
  demonstrated by the need for parallel checks in `ActionCalculus.cs`,
  `OntologyInverseContractAnalyzer.cs`, and `WorkflowBindingProofAnalyzer.cs`.
- **Independent-guarantee/duplication attempt:** Cross-front-end semantic equivalence in row 2 tests
  one law; it does not guarantee ownership of the implementation or prevent a copied solver from
  drifting on a different formula. Conversely, source linking the kernel does not establish the
  higher-level inverse law. The rows are complementary.
- **Named-proof attempt:** Compile links show common neutral sources, and rooted SymbolKey catalog
  construction is shared among analyzer work. Dissent: the claim overstates one authority: runtime
  does not consume the Roslyn catalog, higher-level orchestration remains triplicated, and no
  ownership guard/common corpus covers status precedence, frame, authority, and reason assembly.
- **Verdict:** **SURVIVED.** The wording needs precision, but the underlying drift obligation is real;
  refuting wording would violate the lens rule.

### 25. `packed-consumer-loads-and-enforces-typed-compensation-warning-free` — SURVIVED

- **Reachability/premise attempt:** NuGet packing, analyzer dependency layout, isolated restore, and a
  fresh consumer compilation are outside in-repo project tests. `scripts/verify-generator-consumer-build.sh:41-119`
  binds package identities/digests and isolated feed/cache; lines 143-163 enable warnings-as-errors;
  lines 349-415 assert cause-specific AGWF041 and AGWF044 negatives.
- **Independent-guarantee/duplication attempt:** Clean code generation does not prove analyzer DLLs
  and dependencies are present in nupkgs, and Contracts tests do not load the packages as a consumer.
  Diagnostic-catalog parity cannot substitute for actual analyzer activation.
- **Named-proof attempt:** `final-artifact-checks.md` binds 14 local nupkgs and hashes, warning-free
  legal compile, and exclusive negative diagnostics. It also records restore failure as exit 3
  Indeterminate. Dissent: required-check reachability and the protected wrapper's preservation of the
  three-way result are not established.
- **Verdict:** **SURVIVED.** Shipped composition remains a unique reachable failure boundary.

### 26. `generated-diagnostic-authority-is-consistent` — SURVIVED

- **Reachability/premise attempt:** Consumers observe enum values, catalog entries, schema, Markdown,
  and live Roslyn descriptors. AGWF044/045 are derived into generated representations, but
  `src/Strategos.Generators.Tests/Diagnostics/AgwfCatalogParityTests.cs:15-77` explicitly says live
  descriptor metadata is hand-authored and reflects every descriptor for comparison.
- **Independent-guarantee/duplication attempt:** Clean TypeSpec generation cannot prevent a developer
  changing `WorkflowDiagnostics` title, severity, or message alone. Packed activation proves a code
  fires, not that every public representation agrees. No other obligation owns this closed vocabulary.
- **Named-proof attempt:** Clean regeneration plus parity tests can establish the combined claim.
  Dissent: R1 generation by itself cannot establish the hand-authored live root; parity is an R4-style
  execution artifact and lacks protected binding.
- **Verdict:** **SURVIVED.** The independent hand-authored root defeats the “already generated”
  refutation.

### 27. `public-api-authority-covers-issue-169` — SURVIVED

- **Reachability/premise attempt:** The diff adds public inverse, rollback, and compensation members
  consumed from two assemblies. Strategos selectively re-enables PublicApiAnalyzers for named files
  in `src/Strategos/.editorconfig:1-94`, and reflection baselines enumerate additional builder
  surfaces; an omitted new file or removed allowlist entry can therefore escape a partial gate.
- **Independent-guarantee/duplication attempt:** The C# compiler establishes type correctness, not
  compatibility with a prior public surface, intended nullability policy, or removal classification.
  Packed-consumer compilation samples use rather than inventory the whole API.
- **Named-proof attempt:** API analyzer, selective allowlist, and reflection inventory are useful
  structural controls. Dissent: the ledger assigns R2 to a combined claim that includes human
  compatibility intent; the shell gate invokes an analyzer rather than the compiler proving the
  baseline, and the required tracked-member removal mutant is absent.
- **Verdict:** **SURVIVED.** Selective-policy co-drift leaves a real public compatibility obligation.

### 28. `docs-and-migration-build-and-state-exact-boundaries` — SURVIVED

- **Reachability/premise attempt:** Documentation is the only consumer-facing source for migration and
  operational limits. The action-calculus and API docs explicitly distinguish typed/legacy,
  SagaDocument-only, serial inverse workers, retained Failed/OutcomeUnknown, stable IDs, and
  at-least-once idempotency (for example `docs/src/content/docs/reference/action-calculus.md:469-582`).
  Incorrect prose can therefore drive incompatible persisted-state rollout even when code is sound.
- **Independent-guarantee/duplication attempt:** Code, schema, and analyzer diagnostics cannot force a
  user to drain/version sagas or implement idempotency. The general trust-boundary row constrains
  claims about arbitrary effects; it does not cover links, schema/hash migration, or all lifecycle
  states.
- **Named-proof attempt:** A Starlight build can prove syntax/link inclusion and token presence.
  Dissent: it cannot prove semantic honesty; the named “claim audit” requires human comparison and
  no protected semantic review is bound.
- **Verdict:** **SURVIVED.** Documentation has an independent deployment consequence despite the
  named proof's limited reach.

### 29. `downstream-adoption-is-bound-before-claimed` — SURVIVED

- **Reachability/premise attempt:** Basileus and Exarchos consume Strategos contracts and can reject,
  drop, or mis-handle the new closed enum/DTO fields. Their issues #495 and #1895 are open,
  coordination-only records with no implementation SHA, package digest, or protected compatibility
  result. Thus actual adoption is neither implied nor presently established.
- **Independent-guarantee/duplication attempt:** Strategos-local tests and packages cannot constrain a
  different repository's deployed parser or release. General evidence-binding rules say what a
  valid external claim needs, but they do not produce consumer proof; row 30 binds Strategos's final
  delivery subject, not downstream subjects.
- **Named-proof attempt:** A protected compatibility suite in each consumer against exact package
  bytes would establish adoption. Dissent: the current claim is phrased partly as reporting policy,
  while the named proof establishes actual compatibility; the open issues only establish that no
  adoption claim may yet be made. This mismatch is not enough to kill the underlying external
  compatibility obligation.
- **Verdict:** **SURVIVED.** External consumer behavior is reachable, distinct, and currently
  Indeterminate; no adoption percentage is inferable.

### 30. `protected-final-subject-and-review-are-bound` — SURVIVED

- **Reachability/premise attempt:** Local results, hosted checks, review, package bytes, and merge
  authorization can refer to different SHAs after a push. `final-artifact-checks.md` correctly leaves
  protected CI, review, publication, and merge authorization Indeterminate for this exact subject;
  `.github/workflows/ci.yml` establishes declared triggers, not a result or branch-protection policy.
- **Independent-guarantee/duplication attempt:** Evidence-binding rules define subject identity but do
  not enforce repository required checks, rerun affected jobs, dispose review findings, or grant
  human merge authority. Row 25 validates local packed bytes and row 29 validates external consumers;
  neither binds the final PR head.
- **Named-proof attempt:** A hosted record pinned to SHA/tree/package digests plus review disposition
  and explicit authorization can establish delivery. Dissent: R5 is not the rung for human authority,
  and the source instruction says a single CodeRabbit round *may* be used, not that exactly one is
  mandatory. If used, it must bind the final subject; zero is not refuted by “exactly one.”
- **Verdict:** **SURVIVED.** The core final-subject/freshness/authority obligation remains real after
  correcting that overstatement.

### 31. `executable-inverse-effects-remain-a-consumer-trust-boundary` — SURVIVED

- **Reachability/premise attempt:** The analyzer proves ontology declarations and routing identity,
  while arbitrary compensation C# and external systems remain opaque. The packed legal probe in
  `scripts/verify-generator-consumer-build.sh:190-259` deliberately uses a CLR step body that returns
  state unchanged despite a declared ontology inverse, directly discriminating static acceptance
  from real effect restoration.
- **Independent-guarantee/duplication attempt:** A real-host fixture proves one implementation, not
  all consumer step bodies; stable rollback ID enables idempotency but does not implement it. Docs
  presence cannot replace the release-level discipline against universal semantic claims.
- **Named-proof attempt:** Explicit prose, the no-op-vs-behavioral-fixture contrast, and adversarial
  human review are appropriate R6 evidence. Dissent: no automatic signal can universally prove this
  negative claim, and final public wording/review is not bound.
- **Verdict:** **SURVIVED.** The demonstrable no-op consumer makes this trust boundary concrete and
  prevents the proof portfolio from overstating what it knows.

## Candidate claims rejected before promotion

These claims are not active obligations. I evaluated them independently rather than inheriting the
ledger's rejection.

### C1. “Inverse execution is exactly once.” — REFUTED

- **Reachability/premise:** Retry and redelivery are ordinary Wolverine transport behavior, so the
  premise would need a transactional exactly-once effect boundary that the product neither controls
  nor claims.
- **Independent evidence:** Generated rollback IDs are stable correlation keys, not delivery
  suppression; external effects occur inside arbitrary consumer C#.
- **Discriminating proof:** `docs/src/content/docs/reference/action-calculus.md:565-570` explicitly says
  delivery is at-least-once and requires consumer idempotency/reconciliation. The no-op packaged probe
  further demonstrates that static proof cannot govern the effect.
- **Verdict:** **REFUTED.** The supported contract is stable identity under at-least-once delivery,
  not exactly-once execution.

### C2. “Parallel rollback workers execute concurrently.” — REFUTED

- **Reachability/premise:** `ActionRollbackPlan` preserves a parallel structural group, but that says
  nothing about the generated saga's worker scheduling.
- **Independent evidence:** The runtime keeps fork lane heads structurally, then serializes generic
  state folds and dispatches only one inverse at a time.
- **Discriminating proof:** `SagaCompensationComponentEmitter.cs:1754-1772` takes one next inverse;
  `docs/src/content/docs/reference/action-calculus.md:557-560` explicitly states that inverse workers
  are serialized even when structural planning admitted parallel branches.
- **Verdict:** **REFUTED.** Parallelism is a plan/noninterference property, not concurrent execution.

### C3. “Legacy `Compensate<T>()` receives static proof.” — REFUTED

- **Reachability/premise:** Omission of an inverse action identity is intentionally represented as the
  Legacy program kind rather than a typed action reference.
- **Independent evidence:** `CompensationTopology.GetProgramKind` classifies all Missing identities as
  Legacy at `CompensationTopology.cs:161-190`, and the emitter selects the separate legacy path at
  `SagaCompensationComponentEmitter.cs:38-47`.
- **Discriminating proof:** The docs distinguish typed proof from runtime-only legacy behavior at
  `docs/src/content/docs/reference/action-calculus.md:512-538`; the typed workflow analyzer reports the
  missing identity when a proof claim would otherwise include it (`WorkflowBindingProofAnalyzer.cs:750-765`).
- **Verdict:** **REFUTED.** Legacy compatibility is deliberately not promoted into static proof.

### C4. “Typed EventSourced compensation is sound when `ApplyEvent` compiles.” — REFUTED

- **Reachability/premise:** Compilation proves only that the consumer fold is type-correct, not that
  it reconstructs the live inverse state or ontology facts.
- **Independent evidence:** Legacy EventSourced compensation remains available, while the typed mode
  has a distinct structural persistence restriction rather than trying to inspect arbitrary folds.
- **Discriminating proof:** `WorkflowBindingProofAnalyzer.cs:147-151` emits AGWF045 for typed
  EventSourced programs; `mutation-evidence.md` records that disabling this rejection made the exact
  negative test false-green; the docs repeat the restriction at
  `docs/src/content/docs/reference/action-calculus.md:575-582`.
- **Verdict:** **REFUTED.** Even a compiling or no-op fold remains outside the static proof boundary.

### C5. “Successful rollback makes the workflow successful.” — REFUTED

- **Reachability/premise:** Rollback is entered because forward execution failed; undoing completed
  effects does not erase that initiating failure or its audit record.
- **Independent evidence:** Failure and OutcomeUnknown remain explicit terminal journal/lifecycle
  states, and a configured failure handler starts only after rollback finishes.
- **Discriminating proof:** `SagaCompensationComponentEmitter.cs:1928-1944` marks rollback finished,
  clears the active scope, and sets `Phase = Failed` before starting the failure handler; only the
  no-handler lifecycle cleanup calls `MarkCompleted`. The docs state rollback precedes failure
  handling rather than converting the outcome to success.
- **Verdict:** **REFUTED.** Saga lifecycle completion without a handler is not semantic workflow
  success.

## Passes and uncertainties

- Every active ledger slug received its own three-angle examination and explicit verdict. No active
  obligation met the high-tier majority standard for refutation; several named proofs are narrower
  than their claims, and those dissents are recorded above without reclassifying them.
- All five pre-promotion candidates were independently refuted by executable branching, generated
  runtime behavior, or explicit consumer-boundary documentation.
- This lens did not settle protected CI membership/results, branch-protection policy, CodeRabbit or
  human review freshness, merge authorization, Wolverine/Marten crash-window semantics, downstream
  consumer implementation revisions, or arbitrary external-effect correctness. Under
  `evidence-binding.md`, those remain Indeterminate rather than inferred success.
