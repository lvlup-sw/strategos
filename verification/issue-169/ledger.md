---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 263cc5720818b13268214c5df84d7575fd74a6d7
base_revision: 362c45f1ebc812ba2e2abb419e47fef29cb1d622
target_ref: codex/169-derived-compensation
implementation_fingerprint: 24be1d97dfaedd88bebc7f33dda831d9b36b1ba1
implementation_fingerprint_command: git rev-parse 263cc5720818b13268214c5df84d7575fd74a6d7^{tree}
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, persistence, wire, packaging, documentation, and external-consumer surface against merged-main issue-167 base 362c45f1ebc812ba2e2abb419e47fef29cb1d622
updated: 2026-09-08
skipped: none; historical Stage 1/3 evidence remains precursor-bound, and final-subject local results are not converted into Verified
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation intent and acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: v2.13 action-calculus operator program and failure-mode context
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: immediately preceding typed-workflow-binding scope and proof boundary
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate, effective-guarantee, and finite-proof semantics reused by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream adoption coordination only
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream adoption coordination only
---

# Issue #169 verification obligation ledger

## Verdict discipline

This ledger is bound to product commit `263cc5720818b13268214c5df84d7575fd74a6d7`, tree
`24be1d97dfaedd88bebc7f33dda831d9b36b1ba1`. `Verified` requires the assigned proof on a protected
path bound to this exact subject. A present test or local pass is not enough. `Unproven` means the
obligation is real but lacks that binding or its assigned proof. `Indeterminate` means the required
check has not run, could not reach its environment, or read another subject. Both Fail and
Indeterminate block a protected merge.

| State | Count |
|---|---:|
| Verified | 0 |
| Unproven | 42 |
| Indeterminate | 4 |
| Violated | 0 |
| **Total active** | **46** |

Stage 3 adversarial refutation remains historical evidence bound to precursor `42b4ed7`/tree
`2d0f3027`; it retained all 31 precursor active obligations. Proof-fit splitting and coverage/wildcard
closure add 15 active obligations below; the five independently rejected candidate claims remain in
`Refuted` and are not counted as active. The rebase and remediation introduced no genuinely distinct
active claim, so the 46-obligation cardinality is unchanged; their final-subject evidence is updated
below without retroactively rebinding the Stage 1/3 records.

## Active obligations

### inverse-contract-is-mechanically-derived — Derive the inverse from one forward proof

| | |
|---|---|
| **Claim** | For every valid closed forward action, the inverse requirement is the forward effective guarantee, its guarantee is the forward hard requirement, and its subject, semantic authority, and canonical frame come from that same analyzed contract. |
| **Scope** | `ActionCalculus.AnalyzeInverse` and `ActionContractProofEngine`. |
| **Consequence** | An accepted inverse can reject the real post-state or fail to restore a preserved requirement. |
| **State** | Unproven; construction exists, but no protected exact-tree result is bound. |
| **Proof rung** | R1 — Construction and generation. |
| **Proof artifact** | Construction from `ActionContractProof`; `DerivationSwapsEffectiveGuaranteeAndHardRequirement` is the kill backstop. |
| **Why not cheaper** | R1 is cheapest. |
| **Failure signal** | AONT216/AGWF044 or non-Proven runtime analysis; analyzer nonexecution is Indeterminate. |
| **Rollback** | Revert the typed inverse feature as one unit and drain/version persisted typed sagas. |
| **Lenses** | Mechanism, intent and claims, authority topology, existing proof, wildcard. |

**Open questions:** None.

### authored-inverse-is-bidirectionally-equivalent — Prove equality, not one-way refinement

| | |
|---|---|
| **Claim** | An authored inverse is accepted only when its effective requirement and guarantee each imply, and are implied by, the derived contract; invalid, opaque, missing, weaker, and stronger contracts cannot become Proven. |
| **Scope** | Runtime inverse calculus, AONT216, and AGWF044 over the closed predicate fragment. |
| **Consequence** | Rollback can accept too few states or restore too little state. |
| **State** | Unproven. The authored-to-derived implication mutant was killed locally on historical precursor `42b4ed7`; no exact-final mutation rerun, protected mutation binding, or cross-front-end parity is recorded. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Existing: two implication checks and independent examples in each proof front end. Proposed: one neutral corpus with positive, weaker, stronger, opaque, invalid, frame, subject, and authority vectors executed through runtime, AONT216, and AGWF044 adapters; precedence/witness vectors are owned by the dedicated parity row. |
| **Why not cheaper** | R1 cannot derive an independent descriptor; R2 cannot encode semantic predicate implication. |
| **Failure signal** | AONT216, AGWF044, or Refuted/Invalid/Opaque/Missing; skipped analysis is Indeterminate. |
| **Rollback** | Disable typed proof authoring and retain legacy runtime-only compensation. |
| **Lenses** | False-green shapes, promise against delivery, claim derivation, authority topology. |

**Open questions:** None about precursor sensitivity: `mutation-evidence.md` records a killed authored-to-derived implication mutant at its own historical SHA/tree. Exact-final and protected-path mutation execution plus cross-front-end parity remain unbound evidence, not silently answered success.

### inverse-subject-frame-and-authority-are-exact — Preserve exact rollback boundaries

| | |
|---|---|
| **Claim** | A Proven inverse has the same ontology subject, canonical touched-resource frame, and semantic authority as the derived inverse; only lattice-equivalent authority aliases compare equal. |
| **Scope** | Action subjects/resources, authority lattices, AONT216, and AGWF044. |
| **Consequence** | Rollback mutates another object, omits/adds writes, or exercises excess authority. |
| **State** | Unproven pending exact-subject protected execution and parity evaluation. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Subject/frame/semantic-authority comparisons with runtime/analyzer/workflow vectors. |
| **Why not cheaper** | R1 cannot control an authored inverse; R2 cannot prove cross-descriptor equality. |
| **Failure signal** | AONT216/AGWF044 or exact Refuted failure; analyzer absence is Indeterminate. |
| **Rollback** | Reject the typed binding or pin the prior analyzer/runtime package. |
| **Lenses** | Representable invalid states, exposure and compatibility, authority topology, history. |

**Open questions:** None.

### nonproven-inverse-is-never-executable — Make failed core proof incapable of producing a rollback leaf

| | |
|---|---|
| **Claim** | Missing, Refuted, Opaque, and Invalid analyses expose neither an executable compensation action nor an executable rollback leaf through the public inverse calculus; only Proven non-identity analyses can. |
| **Scope** | `ActionInverseAnalysis`, `ActionRollbackLeaf`, and public rollback factories. Generated-workflow emission is owned separately by `rejected-typed-inverse-cannot-reach-emitted-runtime`. |
| **Consequence** | A caller can obtain executable rollback work from an analysis that explicitly failed proof. |
| **State** | Unproven until the construction invariants and non-Proven leaf kill are protected and bound. |
| **Proof rung** | R1 — Construction and generation. |
| **Proof artifact** | Existing: closed validated `ActionInverseAnalysis` factories and noncompensable `ActionRollbackLeaf`/plan construction. |
| **Why not cheaper** | R1 is cheapest. |
| **Failure signal** | Noncompensable analysis/plan with no executable action; a factory/test that does not run is Indeterminate. |
| **Rollback** | Remove public rollback-plan exposure for non-Proven analyses while retaining the closed result state. |
| **Lenses** | False-green shapes, representable invalid states, mechanism, production path. |

**Open questions:** None. Stage 3 split the independent generator-emission seam from this R1 core invariant.

### identity-inverse-is-empty-frame-only — Restrict identity to effect-free actions

| | |
|---|---|
| **Claim** | Only a valid action with an empty frame and no broken explicit inverse may derive the distinct non-executable identity; a non-empty frame requires executable authored inverse code. |
| **Scope** | Calculus, graph freeze, workflow proof, and generated identity handling. |
| **Consequence** | A state-changing action is silently skipped or a misspelled named inverse is hidden. |
| **State** | Unproven pending protected semantic and generated-runtime cases. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Empty/missing/broken-inverse calculus vectors and generated identity-leaf execution. |
| **Why not cheaper** | R1/R2 close shapes and R3 checks source; R4 is required to exercise runtime dispatch omission. |
| **Failure signal** | Missing/Refuted/AONT216/AGWF044/045; unexpected identity dispatch is a component failure. |
| **Rollback** | Conservatively require explicit inverse code for every action. |
| **Lenses** | Promise against delivery, representable invalid states, mechanism, existing proof. |

**Open questions:** None.

### rollback-plan-is-immutable-and-subject-homogeneous — Make malformed plans unrepresentable

| | |
|---|---|
| **Claim** | Every plan snapshots inputs, has one closed kind and validated subject, and rejects mixed-subject or structurally impossible children. |
| **Scope** | `ActionRollbackPlan` factories, frame/read-footprint derivation, public collections. |
| **Consequence** | Caller mutation or subject mixing changes a proved plan after analysis. |
| **State** | Unproven until construction/API checks are protected and bound. |
| **Proof rung** | R1 — Construction and generation. |
| **Proof artifact** | Private validated construction, immutable arrays, and normalized factories. |
| **Why not cheaper** | R1 is cheapest. |
| **Failure signal** | Factory exception/noncompensable result; unsafe reflection bypass has no production signal. |
| **Rollback** | Reject composition; do not return to name-only rollback helpers. |
| **Lenses** | Representable invalid states, mechanism, authority topology. |

**Open questions:** None.

### completed-prefix-rolls-back-in-reverse-without-failing-leaf — Undo exactly completed work

| | |
|---|---|
| **Claim** | If A and B complete and C fails before completion, generated rollback executes UndoB then UndoA and never UndoC. |
| **Scope** | Journal, failure claim, `BeginCompensationScope`, inverse dispatch, Wolverine/Marten/PostgreSQL. |
| **Consequence** | Effects remain, undo order violates dependencies, or an uncompleted action receives a destructive inverse. |
| **State** | Unproven; the descending-order mutant was killed locally on historical precursor `42b4ed7`, and same-tree pre-rebase revision `cdaa73a` passed the 94-test behavioral host suite, but final-revision mutation/host reruns and protected R5 evidence are unbound. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | `Saga_TypedCompensation_DerivesAndExecutesReversedCompletedPrefix`, supported by R4 adversarial cases. |
| **Why not cheaper** | R1-R3 establish shape and R4 handler logic; none crosses real dispatch, persistence, and step bodies. |
| **Failure signal** | Retained failure or trace mismatch; unavailable PostgreSQL is Indeterminate, not pass. |
| **Rollback** | Disable new typed workflows and drain/version existing typed sagas. |
| **Lenses** | Promise against delivery, integration completeness, production path, existing proof. |

**Open questions:** Does protected CI fail, rather than skip, when its PostgreSQL service is absent?

### noncompensable-leaf-invalidates-whole-scope — Propagate rollback refusal upward

| | |
|---|---|
| **Claim** | Any reachable non-empty-frame leaf without a Proven inverse makes every containing sequence, parallel group, and scope noncompensable; a rollback-claimed workflow receives AGWF045 rather than a partial plan. |
| **Scope** | Rollback plan, compensation topology, workflow scope proof. |
| **Consequence** | A workflow is labeled rollback-safe although part of its prefix cannot be undone. |
| **State** | Unproven pending protected analyzer and calculus vectors. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Recursive plan state and AGWF045 closed-scope traversal. |
| **Why not cheaper** | R1 builds a truthful local plan; R2 cannot prove workflow graph closure. |
| **Failure signal** | AGWF045 or a noncompensable plan; skipped topology analysis is Indeterminate. |
| **Rollback** | Remove the rollback claim or supply proved inverses for every leaf. |
| **Lenses** | Claim derivation, integration completeness, mechanism, intent and claims. |

**Open questions:** None.

### parallel-rollback-is-noninterfering — Admit only independent branches

| | |
|---|---|
| **Claim** | Parallel rollback rejects write/write and both write/read directions, including nested read footprints, while allowing shared reads and preserving branch structure. |
| **Scope** | `DeriveParallelRollbackPlan`, resources, inverse predicates, workflow forks. |
| **Consequence** | One inverse invalidates another branch or races a shared write. |
| **State** | Unproven until both-direction mutation kills and protected vectors are bound. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Deterministic pairwise resource analysis and fork diagnostics. |
| **Why not cheaper** | R1/R2 represent frames but cannot decide composite interference. |
| **Failure signal** | Rollback construction/AGWF041/045; analyzer absence is Indeterminate. |
| **Rollback** | Serialize or reject fork compensation; never infer commutativity from order. |
| **Lenses** | Representable invalid states, promise against delivery, mechanism, history. |

**Open questions:** External-effect interference is owned by the explicit trust-boundary obligation.

### typed-compensation-is-single-and-mandatory-per-occurrence — One declaration, no opt-out

| | |
|---|---|
| **Claim** | Each occurrence has at most one compensation declaration; typed authoring records one CLR type and exact inverse action, defaults required, and cannot set `RequiredOnFailure=false`. |
| **Scope** | Fluent builders, configuration, source/import extraction, AGWF045. |
| **Consequence** | Last-write-wins separates proof identity from code or disables claimed rollback. |
| **State** | Unproven pending protected builder and analyzer cases. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Builder duplicate guard plus analyzer rejection of typed opt-out across front ends. |
| **Why not cheaper** | R1 construction handles fluent duplicates but does not govern imported/generator-extracted whole-program rules; R2 types still permit imported opt-out and typed/legacy declaration combinations; R3 is the first layer that can inspect and reject the complete extracted program. |
| **Failure signal** | `InvalidOperationException`, import error, or AGWF045; infrastructure errors remain Indeterminate. |
| **Rollback** | Use one legacy declaration while migrating; remove typed proof claims. |
| **Lenses** | Representable invalid states, compatibility, claim derivation, production path. |

**Open questions:** None.

### typed-program-has-one-closed-subject-boundary — Keep one ontology subject

| | |
|---|---|
| **Claim** | A typed program and every workflow binding that claims its safety resolve to one closed ontology subject; missing, ambiguous, opaque, dynamic, or cross-subject boundaries fail closed. |
| **Scope** | Rooted action catalog, bindings, topology, AGWF044/045. |
| **Consequence** | Generated rollback combines unrelated domain facts/effects or claims proof without resolution. |
| **State** | Unproven pending protected cross-subject, multiple-binding, and catalog cases. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | SymbolKey-only rooted catalog and workflow-group subject check. |
| **Why not cheaper** | R1/R2 validate names but cannot establish compilation-wide ownership/closure. |
| **Failure signal** | AGWF044/045 or earlier AGWF039/040/042; unreadable source is not guessed. |
| **Rollback** | Split subject boundaries or keep the path legacy/runtime-only. |
| **Lenses** | Integration completeness, representable invalid states, authority topology, #167 handoff. |

**Open questions:** None.

### compensation-topology-covers-every-executable-occurrence — Share one closed topology

| | |
|---|---|
| **Claim** | Every executable main, branch, loop, fork, approval, confidence, diagnostic, failure, and terminal occurrence accepted by typed authoring appears exactly once in the topology consumed by proof and saga emission. |
| **Scope** | `CompensationTopology`, closure inspector, all occurrence/failure emitters, AGWF045. |
| **Consequence** | Runtime executes a leaf omitted by proof or a failure route cannot mint/validate authority. |
| **State** | Unproven; recurrence requires a protected closure guard and kill fixtures. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Existing: exact-key topology construction, unjournaled-occurrence rejection, and per-route examples. Missing: machine-readable executable-node/ingress policy and exhaustive route-to-topology closure guard. |
| **Why not cheaper** | R1 centralizes topology and R2 closes kinds; neither proves every independent emitter consumes it. |
| **Failure signal** | AGWF045; an omitted route may otherwise surface only as missing rollback. |
| **Rollback** | Reject the whole typed program; never emit the provable subset. |
| **Lenses** | Integration completeness, recurrence to guard, authority topology, production path, history. |

**Open questions:** Will the guard read callback-family policy data rather than a prose list in one test?

### typed-legacy-and-dynamic-programs-never-mix — Separate proved and runtime-only modes

| | |
|---|---|
| **Claim** | Typed compensation cannot mix with legacy, dynamic, missing, invalid, or ambiguous compensation in one derived program; both source orders fail closed. |
| **Scope** | Source/import front ends, `CompensationProgramKind`, diagnostics, runtime activation. |
| **Consequence** | One scope silently combines strong durable semantics with weaker legacy behavior. |
| **State** | Unproven pending protected order-pair and import cases. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Closed program-kind classification and AGWF045, with both orders and pure-legacy positive. |
| **Why not cheaper** | R1/R2 cannot resolve dynamic states across the workflow. |
| **Failure signal** | AGWF044/045 and no trusted derived program; generator inability is Indeterminate. |
| **Rollback** | Make every occurrence typed or every occurrence legacy. |
| **Lenses** | False-green shapes, representable invalid states, compatibility, mechanism. |

**Open questions:** None.

### typed-compensation-is-saga-document-only — Reject unproved event folds

| | |
|---|---|
| **Claim** | Typed derived compensation is SagaDocument-only; typed EventSourced programs receive AGWF045 even with a compiling/no-op fold, while legacy event-sourced compensation stays on its old path. |
| **Scope** | Persistence classification, workflow proof, saga emitter, compatibility docs. |
| **Consequence** | Live inverse state and Marten replay can diverge while the analyzer claims safety. |
| **State** | Unproven; disabling the EventSourced predicate was killed locally on historical precursor `42b4ed7`, while exact-final mutation and protected typed-negative/legacy-positive binding remain absent. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Persistence-mode rejection before typed acceptance and generated-source cases. |
| **Why not cheaper** | R1/R2 cannot prove consumer `ApplyEvent`; the safe rule is structural rejection. |
| **Failure signal** | AGWF045; absent analysis is Indeterminate. |
| **Rollback** | Use SagaDocument typed compensation or legacy EventSourced compensation. |
| **Lenses** | Promise against delivery, compatibility, production path, intent and claims. |

**Open questions:** None.

### forward-result-requires-exact-durable-dispatch-authority — Consume one persisted claim

| | |
|---|---|
| **Claim** | Completion/pre-completion failure mutates the saga only by consuming one exact nonempty dispatch claim matching all identity fields, and that claim commits before its worker command becomes visible. |
| **Scope** | Start/completion/failure handlers, Wolverine session/outbox, Marten, PostgreSQL. |
| **Consequence** | Forged, stale, duplicate, or reordered messages invent completed work and destructive rollback authority. |
| **State** | Unproven; weakening exact stable-occurrence comparison was killed locally on historical precursor `42b4ed7`, but no exact-final mutation, protected binding, or crash-window real-host proof establishes durability. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Existing: exact-match R4 generated-runtime cases and local stable-occurrence mutant kill. Missing: fault-injected R5 host test around claim commit/message visibility and crash recovery. |
| **Why not cheaper** | R1 shows claim-before-yield; R2/R3 shape; R4 memory behavior; none proves transaction visibility. |
| **Failure signal** | Retained Failed/OutcomeUnknown and logs; host outage without verdict is Indeterminate. |
| **Rollback** | Disable typed dispatch and retain saga documents for reconciliation. |
| **Lenses** | False-green shapes, integration completeness, production path, authority topology, proof. |

**Open questions:** Which Wolverine/Marten transactional mode guarantees this? `(partial: source ordering exists; crash-window semantics are unexhibited)`

### journal-is-versioned-topology-bound-contiguous-and-unique — Reject corrupted history

| | |
|---|---|
| **Claim** | A journal has the supported version, canonical topology, contiguous monotonic sequence/high-water, unique execution/rollback IDs, and noncontradictory claims before rollback selection/advance. |
| **Scope** | Persisted saga, claim/journal validators, reload, rollback selection. |
| **Consequence** | Old/corrupt state skips or duplicates inverses and destroys reconciliation evidence. |
| **State** | Unproven; R4 corruption exists, no real persistence/reload malformed-document fixture. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Existing: generated-component in-memory corruption/continuity cases. Missing: R5 persisted malformed-document reload and recovery fixtures. |
| **Why not cheaper** | R1-R3 generate validators; R4 mutates objects, not serialization/storage reload. |
| **Failure signal** | Retained failure, no inverse dispatch, journal preserved; DB inability is Indeterminate. |
| **Rollback** | Drain/version typed workflows; never downgrade unfamiliar state to success. |
| **Lenses** | Invalid states, compatibility, production path, history. |

**Open questions:** Which malformed vectors will protected R5 inject after actual reload? `(partial: in-memory vectors exist)`

### rollback-selects-innermost-concrete-scope — Isolate nested failure rollback

| | |
|---|---|
| **Claim** | Failure selects the exact innermost concrete branch/loop/scope, rolls back only its completed descendants, and preserves enclosing history for a later enclosing failure. |
| **Scope** | Generated state-machine scope ancestry, concrete keys, failure claims, journal filtering, and preservation of enclosing history. Actual serialization/reload, if required, must be a separate R5 claim. |
| **Consequence** | Inner failure undoes unrelated outer work or erases history needed later. |
| **State** | Unproven; same-tree pre-rebase revision `cdaa73a` ran the R4 cases locally in the 1,915-test generator suite, but the later-enclosing-failure case is Missing and no final-revision/protected result is bound. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: compiled/invoked generated-saga inner branch, approval, loop, and deep-key cases. Proposed: later enclosing failure after successful inner rollback. |
| **Why not cheaper** | R1 constructs keys and R2 closes shapes, but neither proves selection/preservation behavior; R3 can inspect filtering but cannot execute the generated state transitions; R4 is the first sound layer for the stated component semantics. |
| **Failure signal** | Wrong inverse trace, wrong concrete-scope selection, or unavailable preserved outer history; component compilation/invocation nonexecution is Indeterminate. |
| **Rollback** | Reject typed nested scopes and preserve saga/journal. |
| **Lenses** | Claim derivation, integration completeness, production path, existing proof. |

**Open questions:** None for the stated component claim. Any desired persisted-reload guarantee must be inventoried as a separate R5 obligation rather than silently lifting this one.

### fork-rollback-quiesces-before-inverse-dispatch — Wait for every lane

| | |
|---|---|
| **Claim** | Fork failure stops successors and starts no inverse until every lane is terminal; delayed starts, late completions, and multiple failed lanes cannot race a partial plan. |
| **Scope** | Fork dispatch/join/lane handlers, claims, quiescence state, selection. |
| **Consequence** | Rollback races live forward effects, selects an incomplete prefix, or starts twice. |
| **State** | Unproven; R4 permutations exist, no real-host concurrent fork schedule is bound. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Existing: generated-saga message-order/quiescence permutations. Missing: fault-controlled R5 host fixture with concurrent lane scheduling and persistence. |
| **Why not cheaper** | R1-R3 construct the machine and R4 serially permutes messages; production races cross persistence. |
| **Failure signal** | Premature inverse/successor, duplicate rollback, or retained corruption; environment failure is Indeterminate. |
| **Rollback** | Reject typed fork compensation while retaining serial typed programs. |
| **Lenses** | Integration completeness, production path, history, existing proof. |

**Open questions:** No real-host fork race fixture is identified.

### inverse-message-role-is-isolated-from-forward-flow — Separate recovery routing

| | |
|---|---|
| **Claim** | Inverse start/completion/failure/timeout messages and handler roles are distinct from forward and ordinary failure flow; inverse failure never recursively starts compensation. |
| **Scope** | Generated types, role identity, handler configuration, failure routing. |
| **Consequence** | Inverse output advances forward flow or inverse failure recurses and duplicates effects. |
| **State** | Unproven until compiled/invoked role-collision fixtures are protected. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Same-CLR-type role collision execution plus identity/registration assertions. |
| **Why not cheaper** | R1/R2 create roles and R3 inspects wiring; only execution proves routing/suppression. |
| **Failure signal** | Retained compensation failure and no new trigger; collision appears as unexpected message/trace. |
| **Rollback** | Disable typed inverse dispatch for colliding roles and retain the saga. |
| **Lenses** | False-green shapes, integration completeness, production path, history. |

**Open questions:** None.

### inverse-state-folds-before-next-inverse — Serialize reducer output

| | |
|---|---|
| **Claim** | A matching inverse completion applies `UpdatedState` through the SagaDocument reducer before marking rollback and before the next inverse/failure handler. |
| **Scope** | Inverse completion, reducer, journal status, next dispatch, supported host. |
| **Consequence** | Later inverses/failure handlers observe stale or partly restored state. |
| **State** | Unproven; R4 ordering exists and same-tree pre-rebase revision `cdaa73a` passed the 94-test behavioral R5 carrier locally, but final-revision/protected binding is absent. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Real workflow whose UndoB output is required by UndoA, with ordering mutation; R4 is the fast backstop. |
| **Why not cheaper** | R1-R3 show source order and R4 controlled invocation; the claim includes persisted state across workers. |
| **Failure signal** | Inverse precondition/trace failure, reducer exception, or retained failure; host error is Indeterminate. |
| **Rollback** | Stop after the active inverse and retain the journal. |
| **Lenses** | Promise against delivery, production path, wildcard, existing proof. |

**Open questions:** None beyond protected execution.

### rollback-terminal-lifecycle-is-monotonic — Never resurrect terminal rollback

| | |
|---|---|
| **Claim** | After RolledBack/finished, Failed, or OutcomeUnknown, no stale/redelivered ingress reopens or advances rollback; Failed and OutcomeUnknown remain distinct retained non-success states. |
| **Scope** | All ingress guards, journal terminal states, saga retention, redelivery. |
| **Consequence** | At-least-once delivery duplicates uncertain external effects or turns failure into apparent success. |
| **State** | Unproven. Exact-final R4 tests cover both arrival orders for competing worker-failure and timeout signals and preserve the first durable cause without dispatching rollback twice; no protected or real-host failure/timeout redelivery path is bound. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Existing: generated monotonicity state-by-ingress matrix, including competing terminal-signal arrival orders after rollback authority is consumed. Missing: R5 host redelivery/fault/timeout transport fixture. |
| **Why not cheaper** | R1-R3 generate/check the state machine and R4 replays memory; delayed delivery/persistence is R5. |
| **Failure signal** | Any command after terminal is failure; missing outcome is OutcomeUnknown/Indeterminate, never success. |
| **Rollback** | Freeze the retained saga for operator reconciliation. |
| **Lenses** | False-green shapes, production path, authority topology, history. |

**Open questions:** No R5 failure/timeout redelivery fixture was identified.

### rollback-id-is-stable-distinct-and-injective — Correlate retries safely

| | |
|---|---|
| **Claim** | Every nonempty forward execution ID maps deterministically to one nonempty rollback ID stable under redelivery, distinct from the forward ID, and injective over distinct IDs. |
| **Scope** | Rollback ID transform, journal, inverse messages, idempotency docs. |
| **Consequence** | Retries look new or two executions share a key, duplicating/suppressing external compensation. |
| **State** | Unproven pending protected boundary/property and mutation evidence. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: direct construction argument for cyclic increment over the nonempty 128-bit GUID domain, boundary sample, journal equality, and redelivery cases. Proposed: property/metamorphic collision kill bound on the protected path. |
| **Why not cheaper** | R1 emits the transform but does not attest it; R2 only shapes GUIDs; R3 can establish the pure permutation by source analysis but cannot establish mapped identity through persisted component/redelivery behavior, so the combined claim first closes at R4. |
| **Failure signal** | Journal validation rejects bad IDs and retains failure; external dedupe is consumer-owned. |
| **Rollback** | Stop automatic retry and reconcile from forward/journal identity. |
| **Lenses** | Invalid states, production path, existing proof, Proof-Layer Fit PF-10, wildcard. |

**Open questions:** None about the pure mapping: cyclic increment over the nonempty 128-bit domain is a permutation. Protected behavioral/property sensitivity remains unbound.

### typed-compensation-wire-roundtrips-and-import-fails-closed — Preserve exact wire identity

| | |
|---|---|
| **Claim** | Contracts 0.12 preserves CLR moniker and exact inverse triple through projection/import/IR, keeps legacy omission, and rejects present malformed, blank, unresolved, or ambiguous typed values instead of downgrading them. |
| **Scope** | TypeSpec, generated C#/schemas, DTO/reader/bridge, projection, fingerprints. |
| **Consequence** | A document validates but proves/executes another inverse or silently becomes legacy. |
| **State** | Unproven. Same-tree pre-rebase revision `cdaa73a` passed the 1,915-test generator suite locally, including malformed, wrong-kind, non-positive, default, and explicit imported compensation-timeout cases plus wire identity round trips; final-revision and protected codegen/import binding remain absent. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Regeneration, schema conformance, malformed matrix, resolution, round-trip/fingerprint tests. |
| **Why not cheaper** | R1 derives generated files but does not govern the hand parser/resolver/projection; R2 types still represent malformed, unresolved, or silently omitted imported data; R3 source-shape inspection cannot establish round-trip and fail-closed behavior across the independent roots; R4 is the first shared-contract proof. |
| **Failure signal** | Stable import/schema diagnostic and no saga; missing codegen is Indeterminate. |
| **Rollback** | Read legacy omission but reject typed documents; pin Contracts/generator together. |
| **Lenses** | Compatibility, false-green, authority topology, production path, history. |

**Open questions:** Does the protected generator/import matrix bind malformed presence, wrong JSON kind, timeout range, symbol resolution, round trip, and fingerprint to the same final subject? Same-tree pre-rebase execution does not establish final-revision protected execution.

### proof-authority-is-shared-across-runtime-and-analyzers — Single-source the neutral kernel

| | |
|---|---|
| **Claim** | Analyzer targets consume the same source-linked normalization, finite solver, Roslyn bridge, and rooted SymbolKey-only catalog, while runtime/analyzer high-level inverse orchestration is explicitly separate and parity-tested rather than called single-sourced. |
| **Scope** | Shared analyzer proof sources, project compile links, runtime action proof boundary, and ownership policy. |
| **Consequence** | Front ends resolve/prove differently while their local suites stay green. |
| **State** | Unproven until source-link/build and drift kill are bound; high-level orchestration remains triplicated. |
| **Proof rung** | R1 — Construction and generation. |
| **Proof artifact** | Existing: project compile links and one neutral analyzer source tree. Missing: source/dependency ownership guard. Proposed R3/R4 backstop: one neutral inverse/status/witness corpus for deliberately separate orchestrators. |
| **Why not cheaper** | R1 is cheapest; shared vectors separately guard repeated orchestration. |
| **Failure signal** | Build/dependency guard failure or semantic parity mismatch; missing guard is Indeterminate. |
| **Rollback** | Remove divergent copies or source-link the neutral implementation. |
| **Lenses** | Authority topology, recurrence, history, existing proof. |

**Open questions:** The neutral inverse/status/witness corpus is Proposed; until it runs through all adapters, high-level classification parity remains Unproven rather than part of this narrow R1 ownership claim.

### packed-consumer-loads-and-enforces-typed-compensation-warning-free — Test shipped composition

| | |
|---|---|
| **Claim** | A fresh exact-byte package consumer loads the analyzer, compiles legal typed compensation warning-free, fails exclusively AGWF041 and AGWF044 for cause-specific negatives, and reports restore inability as Indeterminate. |
| **Scope** | Nupkgs, analyzer contents/dependencies, isolated feed/cache, package script. |
| **Consequence** | In-repo green masks absent analyzers, generated warnings, or infrastructure false-green. |
| **State** | Unproven. Exact-final package execution passed locally: 14 binary packages were digested, the legal consumer built with 0 warnings/errors, AGWF041/044 failed exclusively, and fresh-feed/source-restored-byte checks passed; protected result binding is absent. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Existing exact-final local evidence: strict isolated package probe with complete dependency closure, SHA-256/source-byte equality, legal warning-free positive, exclusive AGWF041/044 negatives, stale-artifact rejection, and exits 0/2/3. Pending: protected result. Ontology analyzer packaging is owned separately. |
| **Why not cheaper** | R1-R4 cannot prove package restore/analyzer load/generated consumer composition. |
| **Failure signal** | Exit 2 product Fail; exit 3 Indeterminate; only full success exits 0. |
| **Rollback** | Do not publish; pin prior packages. |
| **Lenses** | False-green, integration completeness, compatibility, wildcard, history. |

**Open questions:** Will protected CI preserve the three-way result and exact package digests?

### generated-diagnostic-authority-is-consistent — Derive the closed AGWF vocabulary

| | |
|---|---|
| **Claim** | AGWF044/045 enum members, entries, constants, catalog, schemas, and Markdown derive from the same TypeSpec authority and regenerate without unexplained drift. |
| **Scope** | `AgwfCatalog.tsp`, contracts code generation, and generated diagnostic artifacts. Hand-authored live descriptors are owned by `live-agwf-descriptors-match-generated-catalog`. |
| **Consequence** | Generated consumers see unknown or inconsistent diagnostic tokens even though source TypeSpec appears current. |
| **State** | Unproven. Generated/live diagnostic corrections compiled in the same-tree pre-rebase 1,915-test generator suite, including AGWF044/045 fail-closed descriptor checks; final-revision regeneration and protected binding are pending. |
| **Proof rung** | R1 — Construction and generation. |
| **Proof artifact** | Existing: clean contracts regeneration plus generated enum/catalog/schema/Markdown tests. |
| **Why not cheaper** | R1 is cheapest for representations derived from TypeSpec. |
| **Failure signal** | Unexplained regenerated diff; missing/no-op/crashed generator is Indeterminate. |
| **Rollback** | Remove codes/feature together before publication. |
| **Lenses** | Authority topology, compatibility, recurrence, existing proof. |

**Open questions:** None. Stage 3 split the independently hand-authored live descriptor root into an R4 obligation.

### public-api-authority-covers-issue-169 — Govern every public member

| | |
|---|---|
| **Claim** | Removal, widening, nullability drift, or omission from tracking for the full inverse/rollback and typed-compensation public surface fails a deterministic API gate. |
| **Scope** | Ontology API analyzer/baseline and Strategos allowlist/reflection baseline. Human compatibility intent is owned by `issue-169-public-api-intent-is-reviewed`. |
| **Consequence** | Consumers receive an unrecorded source/binary break or an incomplete public surface. |
| **State** | Unproven pending exact gates and a tracked-member removal mutation for both assemblies. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Existing: assembly API analyzer, public API baselines, Strategos allowlist/reflection inventory, and `scripts/check-builder-api-stability.sh`; Proposed: independent tracked-member removal kill for each assembly. |
| **Why not cheaper** | R1 cannot generate all handwritten C# signatures; R2 compiles a breaking or untracked signature and cannot compare it with the compatibility baseline; R3 is the first layer that can inventory and reject drift. |
| **Failure signal** | Build/API failure; omitted/nonrun gate is Indeterminate. |
| **Rollback** | Revert public surface and dependents together before publication. |
| **Lenses** | Compatibility, authority topology, false-green, existing proof. |

**Open questions:** Can the selective Strategos policy and reflection inventory drift together without an independently derived expected surface? The removal kill remains Proposed.

### docs-and-migration-state-exact-boundaries — Publish precise semantic guidance

| | |
|---|---|
| **Claim** | Public docs and migration guidance state the exact semantic boundary: typed proof re-establishes the forward requirement region and matches declared authority and may-touch frame, but does not prove concrete pre-state equality, append-only event-history reversal, arbitrary CLR effects, or exactly-once delivery; they also distinguish typed/legacy modes, SagaDocument restriction, schema/hash migration, serial folds, and retained Failed/OutcomeUnknown. |
| **Scope** | Starlight content, changelogs, Contracts README, and public XML/API wording. Mechanical build/link integrity is owned by `docs-and-migration-build-and-link`. |
| **Consequence** | Consumers deploy incompatible sagas or mistake unary contract-set compensation for a concrete state inverse, concurrent/exactly-once execution, or successful workflow completion. |
| **State** | Unproven. The exact-final docs, changelogs, Contracts README, and XML wording were reconciled with the issue-167 base and corrected after review; an exact-final semantic approval and protected docs result remain unbound. |
| **Proof rung** | R6 — Human judgment. |
| **Proof artifact** | Proposed: final semantic claim audit against the calculus, the non-singleton `x > 0` counterexample, event-frame semantics, generated runtime, and consumer trust boundary. |
| **Why not cheaper** | R1 can generate repeated text, R2 can type APIs, R3 can find terms, R4 can build/link docs, and R5 can exercise one implementation; none decides whether independent prose truthfully states the mathematical and operational limit. |
| **Failure signal** | Human claim-audit finding; misleading prose has no universal automatic production signal. |
| **Rollback** | Withhold release/docs and direct users to pin/drain/version. |
| **Lenses** | Promise/delivery, compatibility, intent/claims, wildcard. |

**Open questions:** None. Stage 3 resolved that `Proven` means contract-region re-establishment, not concrete pre-state restoration; exact reversal would require a different relational model.

### downstream-adoption-is-bound-before-claimed — Separate coordination from proof

| | |
|---|---|
| **Claim** | Basileus and Exarchos have adopted issue #169 only when exact consumer commits, package versions/digests, schema/API updates, protected compatibility tests, and release disposition demonstrate actual compatibility; issues alone are coordination. |
| **Scope** | Basileus #495, Exarchos #1895, #153 consumer rule. |
| **Consequence** | Strategos claims ecosystem completion while consumers reject/drop/mis-handle the new contract. |
| **State** | Indeterminate; no downstream revision, lock, build, or fixture is bound. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Missing: each consumer's protected compatibility suite against exact package bytes, plus the exact consumer commit and release/deployment disposition. |
| **Why not cheaper** | Strategos-local R1-R4 cannot prove external adoption/deployment. |
| **Failure signal** | Downstream build/schema failure; absent evidence is Indeterminate. |
| **Rollback** | Keep adoption issues open and consumers pinned. |
| **Lenses** | Compatibility, integration completeness, authority topology, intent. |

**Open questions:** Which exact consumer commits implement #495/#1895? `(needs human input)`

### protected-final-subject-and-review-are-bound — Deliver only final-revision evidence

| | |
|---|---|
| **Claim** | CI results, package digests, and any machine-readable review record bind the same final PR/merge revision; CodeRabbit cardinality is zero or one (never more), and post-result code changes invalidate stale evidence and rerun affected checks. |
| **Scope** | PR head/merge subject, required checks, machine review metadata, and package digests. Finding disposition and merge authorization are owned by `final-review-disposition-and-merge-authorization-are-current`. |
| **Consequence** | Merged code differs from tested/reviewed code or a missing/skipped check appears green. |
| **State** | Indeterminate; the exact product SHA/tree is frozen locally, but no #169 PR/protected-check/package/review record is yet bound to it. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Missing: hosted evidence-binding check that compares PR head/merge subject, SHA/tree, required check conclusions, optional CodeRabbit count/subject (0..1), machine-review revision, and package digests. |
| **Why not cheaper** | R1/R2 cannot observe hosted records or compare their subjects; R3 can deterministically reject stale/missing/mismatched metadata. Product-path R5 results are inputs, not the binding mechanism. |
| **Failure signal** | Failed check or SHA/digest mismatch is Fail; missing/skipped/timed-out/unreadable hosted evidence is Indeterminate. |
| **Rollback** | Do not merge; if published, deprecate/yank and issue a new version. |
| **Lenses** | False-green, integration completeness, evidence binding, intent. |

**Open questions:** The dossier must rebind if the final PR head differs from `263cc5720818b13268214c5df84d7575fd74a6d7`; hosted evidence is pending.

### executable-inverse-effects-remain-a-consumer-trust-boundary — Do not overstate proof

| | |
|---|---|
| **Claim** | Strategos claims proof only that declared unary contracts re-establish the forward requirement region and match declared authority/may-touch frame, plus generated routing/correlation; it never claims concrete pre-state or append-only event-history reversal, arbitrary compensation C#/external effects, or consumer idempotency. |
| **Scope** | Calculus/analyzer/package claims, docs/XML, non-singleton and event-frame counterexamples, real-host fixture, rollback identity, and application boundary. |
| **Consequence** | Green compile is mistaken for proof that the concrete prior state, an event history, or an external refund/restoration was recovered exactly once. |
| **State** | Unproven. The exact-final public wording now states the declared-contract and external-effect boundaries, but final human/review confirmation is not bound. |
| **Proof rung** | R6 — Human judgment. |
| **Proof artifact** | Proposed: explicit contract-set prose; non-singleton and event-frame claim-boundary examples; package no-op versus behavioral fixture distinction; adversarial release review. |
| **Why not cheaper** | R1-R5 prove declarations/routing/one implementation; none proves arbitrary code/effects. |
| **Failure signal** | No universal automatic signal; applications own assertions/reconciliation/idempotency. |
| **Rollback** | Correct claims before release; stop retries and reconcile a bad consumer by rollback ID. |
| **Lenses** | Wildcard, promise against delivery, intent and claims, production path. |

**Open questions:** None; this is a deliberate retained limitation. A true state-transition inverse would require relational old/new predicates or a persisted pre-state/delta and is not claimed by issue #169.

### rejected-typed-inverse-cannot-reach-emitted-runtime — Bind proof rejection to emission

| | |
|---|---|
| **Claim** | A typed workflow with any Missing, Refuted, Opaque, Invalid, mixed, or otherwise unproved inverse cannot produce an executable generated assembly or dispatch that inverse, even though proof diagnostics and source emission are separate generator outputs. |
| **Scope** | `WorkflowBindingProofAnalyzer`, generator output registration, AGWF044/045 severity, compensation program activation, and saga inverse emission. |
| **Consequence** | The build reports a rejected inverse while compiled generated runtime can still execute it. |
| **State** | Unproven. AGWF044/045 are enabled-by-default, non-configurable Errors and exact-final component negatives exist, but no protected analyzer-to-emitter closure guard is bound. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Existing local backstop: descriptor test proves AGWF041-045 are `NotConfigurable`. Proposed: analyzer-to-emitter reachability/closure check plus a kill fixture that removes or downgrades the blocking diagnostic while inverse source remains otherwise emit-capable. |
| **Why not cheaper** | R1 closes the core result/plan shape but does not govern the independent emitter; R2 types permit both output streams; R3 is the first layer that can prove every emitted executable inverse is dominated by successful proof. |
| **Failure signal** | A contradictory/mixed typed program produces a loadable executable inverse or a blocking diagnostic is absent/downgraded; generator/analyzer nonexecution is Indeterminate. |
| **Rollback** | Disable typed inverse emission while retaining blocking diagnostics and legacy runtime-only compensation. |
| **Lenses** | Proof-Layer Fit PF-1, refutation, false-green shapes, production path. |

**Open questions:** None; packaged analyzer activation remains a separate R5 composition obligation.

### live-agwf-descriptors-match-generated-catalog — Keep analyzer diagnostics aligned with TypeSpec

| | |
|---|---|
| **Claim** | Every generated AGWF catalog entry resolves to a live descriptor whose identifier, default severity, title, and message format match the catalog. |
| **Scope** | Hand-authored `WorkflowDiagnostics`, generated AGWF catalog, and descriptor parity tests. |
| **Consequence** | The analyzer emits behavior or text different from the closed public diagnostic vocabulary consumed by schemas and tools. |
| **State** | Unproven. Same-tree pre-rebase `Strategos.Generators.Tests` passed locally 1,915/1,915 and covers live/catalog parity plus AGWF044/045 `NotConfigurable`; final-revision/protected binding and live-root mutation evidence are absent. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: forward generated-catalog-to-live `AgwfCatalogParityTests` comparison. Proposed: mutate/remove one live severity/title/message entry and require a cause-specific parity failure. |
| **Why not cheaper** | R1 derives the catalog but not the hand-authored descriptors; R2 permits either metadata value; R3 could inventory members but a provider/consumer contract test is the first existing mechanism that compares complete runtime descriptor values. |
| **Failure signal** | Generated entry lacks a live descriptor or ID/severity/title/message parity fails; catalog/test nonexecution is Indeterminate. |
| **Rollback** | Correct the live descriptor or generated authority before packaging; never hand-edit generated catalog output. |
| **Lenses** | Proof-Layer Fit PF-2, coverage COV-02, compatibility, authority topology. |

**Open questions:** None.

### issue-169-public-api-shapes-are-compiler-enforced — Make invalid caller shapes unrepresentable

| | |
|---|---|
| **Claim** | Issue-169 public types expose closed/sealed variants, non-null required values, immutable collections, and strongly typed action/rollback/compensation signatures so callers cannot compile structurally invalid uses. |
| **Scope** | Public inverse analysis/contract/plan types, typed compensation configuration/builders, nullability annotations, collection exposure, and exhaustive enum consumers. |
| **Consequence** | Callers can mutate proved results, omit required identity, subclass closed result variants, or compile ambiguous/untyped rollback shapes. |
| **State** | Unproven; the same-tree pre-rebase Release solution build passed locally with 0 errors, but a final-revision rerun, focused compile-negative sensitivity, and protected binding are absent. |
| **Proof rung** | R2 — Compiler and type system. |
| **Proof artifact** | Existing: public declarations and strict compilation. Proposed: warnings-as-errors compile-negative consumers for mutation, null omission, subclassing, and signature misuse. |
| **Why not cheaper** | The declarations are handwritten and have no R1 generating authority; R2 is the first layer that can make invalid caller programs fail compilation. |
| **Failure signal** | A negative consumer compiles or a strict build accepts a nullability violation; compiler/test nonexecution is Indeterminate. |
| **Rollback** | Restore the closed typed shape before publication; do not compensate with runtime validation for a public shape regression. |
| **Lenses** | Proof-Layer Fit PF-3, representable invalid states, compatibility. |

**Open questions:** None.

### issue-169-public-api-intent-is-reviewed — Govern compatibility meaning

| | |
|---|---|
| **Claim** | A maintainer reviews every issue-169 public addition, removal, nullability/sealing choice, and compatibility classification against intended consumer behavior after mechanical API drift checks pass. |
| **Scope** | Ontology and Strategos public API deltas, compatibility policy, changelog/migration claims, and release review. |
| **Consequence** | A mechanically baselined API is still accidentally public, incomplete, or assigned the wrong compatibility meaning. |
| **State** | Unproven. Rebase-conflict resolution corrected stale Contracts/agents/package/migration claims, but no final-subject compatibility-intent review is bound. |
| **Proof rung** | R6 — Human judgment. |
| **Proof artifact** | Proposed: final public-delta review record enumerating all issue-169 members and deliberate removals with disposition. |
| **Why not cheaper** | R1 can generate inventories, R2 can enforce shapes, R3 can compare baselines, R4 can compile consumers, and R5 can exercise examples; none decides whether the exposed contract is the intended product API. |
| **Failure signal** | Review finding or absent final review; absence is Unproven/Indeterminate, never implicit approval. |
| **Rollback** | Revert or revise the public surface and its baselines before publication. Published source/binary breaks require a new corrective version. |
| **Lenses** | Proof-Layer Fit PF-3, compatibility, intent and claims. |

**Open questions:** Which final reviewer owns compatibility-intent signoff? `(needs human input)`

### docs-and-migration-build-and-link — Keep the documentation artifact mechanically complete

| | |
|---|---|
| **Claim** | The Starlight build succeeds with the issue-169 migration, calculus, API, diagnostics, changelog, and Contracts references included and all new internal links resolvable. |
| **Scope** | Docs content collection, link graph, root/Contracts changelogs and README, and documentation CI job. |
| **Consequence** | Required migration or API guidance is absent from the published site, or users land on broken references. |
| **State** | Unproven; the final source contains the reconciled docs and the same-tree pre-rebase build produced 80 pages, but final-revision/protected docs binding is absent. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: Starlight build path. Missing: explicit link checker. Proposed: required-page/link inventory for issue #169. |
| **Why not cheaper** | R1 does not generate all prose, R2 only type-checks code, and R3 can inventory paths but cannot establish the rendered content collection/link integration; R4 builds the documentation artifact through its public toolchain. |
| **Failure signal** | Build/link failure or missing required page; tool restore/build nonexecution is Indeterminate. |
| **Rollback** | Withhold the docs/release until the complete build succeeds; do not publish partial migration guidance. |
| **Lenses** | Proof-Layer Fit PF-4, coverage COV-07, compatibility, documentation. |

**Open questions:** None. Semantic truth is owned by `docs-and-migration-state-exact-boundaries` at R6.

### final-review-disposition-and-merge-authorization-are-current — Bind human authority

| | |
|---|---|
| **Claim** | Every actionable final-subject review finding has an explicit disposition and the user grants merge authorization after the last product change; any later product change invalidates that authority and requires renewed review as affected. |
| **Scope** | Human review record, finding disposition, final PR head, and explicit merge authorization. |
| **Consequence** | Code merges despite an unresolved finding or under approval for an earlier subject. |
| **State** | Indeterminate. An independent local review found the AGWF044/045 configurability P1 and it was fixed, but no post-fix hosted review/disposition or final merge authorization exists for this subject. |
| **Proof rung** | R6 — Human judgment. |
| **Proof artifact** | Missing: final-subject review disposition and explicit user merge authorization. A CodeRabbit round is optional; if used, its result must bind the same subject. |
| **Why not cheaper** | R1-R3 can bind record identity and R4/R5 can prove behavior, but no machine layer can decide the sufficiency of a finding disposition or grant user authority to merge. |
| **Failure signal** | Unresolved finding, stale review subject, or absent authorization blocks merge; unavailable review/authorization is Indeterminate. |
| **Rollback** | Do not merge. A post-authorization product change revokes the authorization until refreshed. |
| **Lenses** | Proof-Layer Fit PF-5, refutation, evidence binding, intent. |

**Open questions:** Who supplies final merge authorization and on which final SHA? `(needs human input)`

### release-claims-require-bound-adoption-evidence — Prevent coordination from becoming an adoption claim

| | |
|---|---|
| **Claim** | Strategos release/checklist state cannot mark Basileus or Exarchos adoption complete unless it references each exact consumer commit, package digest, protected compatibility result, and release disposition. |
| **Scope** | Release evidence/checklist state, downstream adoption records, Basileus #495, and Exarchos #1895. |
| **Consequence** | An issue link or local surrogate smoke is reported as actual ecosystem adoption. |
| **State** | Unproven; coordination records exist, but no reachable machine-readable release gate enforces the evidence predicate. |
| **Proof rung** | R3 — Deterministic structural analysis. |
| **Proof artifact** | Missing: policy-as-data release/adoption evidence gate. Proposed kill: supply issue URLs without consumer revisions/results and require the gate to refuse completion. |
| **Why not cheaper** | R1 cannot derive external execution evidence and R2 cannot encode hosted consumer results; R3 can deterministically validate that every adoption claim carries the required bound records. |
| **Failure signal** | Release gate rejects incomplete/mismatched adoption evidence; missing/unreadable external records are Indeterminate, never success. |
| **Rollback** | Keep adoption status open and consumers pinned until actual evidence exists. |
| **Lenses** | Proof-Layer Fit PF-11, coverage COV-07, compatibility, evidence binding. |

**Open questions:** Where will the machine-readable release/adoption policy live? `(needs human input)`

### inverse-analysis-cancellation-propagates — Do not turn cancellation into a semantic verdict

| | |
|---|---|
| **Claim** | Cancellation before or during inverse analysis propagates as `OperationCanceledException` through proof and solver calls before any Proven, Refuted, Opaque, Invalid, Missing, or partial rollback result is returned. |
| **Scope** | Public `ActionCalculus.AnalyzeInverse` overloads, `ActionContractProofEngine`, finite solver calls, and result construction. |
| **Consequence** | A cancelled or budget-exhausted proof is cached or executed as a semantic result, potentially authorizing or refusing rollback incorrectly. |
| **State** | Unproven; an entry cancellation test exists, but no protected mid-analysis propagation/anti-verdict fixture is bound. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: `ActionInverseTests.InverseAnalysisHonorsCancellation`. Proposed: deterministic mid-analysis cancellation through solver work, asserting no result/plan is produced. |
| **Why not cheaper** | R1/R2 permit a catch-and-convert implementation; R3 can see token plumbing but cannot establish behavior when cancellation races analysis; R4 drives the public component under controlled cancellation. |
| **Failure signal** | Any semantic result after cancellation, swallowed cancellation, or partial plan; fixture nonexecution/timeout is Indeterminate. |
| **Rollback** | Disable or fail closed the cancelled analysis path; never reuse a partial/cancelled proof. |
| **Lenses** | Coverage COV-01, mechanism, public calculus, wildcard. |

**Open questions:** None.

### inverse-failure-precedence-and-witnesses-are-stable — Share one failure decision table

| | |
|---|---|
| **Claim** | For every multi-fault inverse input, runtime, AONT216, and AGWF044 select the same status/obligation precedence and normalized counterexample; AGWF044 owns inverse disagreement, AGWF045 owns complete-scope failure, and earlier root causes suppress derivative compensation noise. |
| **Scope** | `ActionInverseStatus`/obligations, runtime calculus, ontology analyzer, workflow analyzer, diagnostic ordering/count, and normalized witnesses. |
| **Consequence** | Front ends disagree about the primary failure, produce unstable diagnostics, or report misleading duplicate errors while local example suites remain green. |
| **State** | Unproven; independent examples exist, but the neutral policy corpus and all-adapter execution are Proposed. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Proposed: policy-as-data multi-fault corpus executed through all three adapters, comparing status, obligation set, diagnostic owner/count, and canonical witness. |
| **Why not cheaper** | R1 does not derive the separate orchestrators; R2 closes enum shapes but not decisions; R3 can inspect branches but cannot establish behavioral agreement and normalized output across provider/consumer adapters; R4 is the shared-contract layer. |
| **Failure signal** | Any adapter disagreement, unstable witness/order, duplicate derivative diagnostic, or zero-vector adapter; absent/crashed adapter is Indeterminate. |
| **Rollback** | Reject the inverse/typed program and retain the previous diagnostic authority until parity is restored. |
| **Lenses** | Coverage COV-02, Proof-Layer Fit PF-8, authority topology, recurrence. |

**Open questions:** What checked-in file will carry the neutral decision table? `(needs human input)`

### durable-completion-transition-is-authorized — Carry authority through post-completion failure

| | |
|---|---|
| **Claim** | A valid forward completion consumes its exact dispatch claim, applies the reducer, commits one completion journal entry, and only then routes a successor; any reducer or successor-routing failure mints and consumes exactly one durable post-completion failure capability tied to that entry's exact kind, scope, occurrence, execution ID, and high-water. |
| **Scope** | Forward completion handlers, reducers, completion journal, `FailureTriggerClaim`, every approval/confidence/branch/loop/fork/diagnostic/successor producer, Wolverine/Marten transaction, reload, and rollback trigger. |
| **Consequence** | Completed work is absent from rollback, a failure invents authority, a duplicate trigger starts rollback twice, or a crash loses both successor and failure recovery. |
| **State** | Unproven; component cases exist, but the ingress matrix and supported-host crash/reload proof are Missing. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Missing: machine-readable post-completion producer matrix plus a fault-injected supported-host transition fixture; existing generated-runtime claim/order cases are R4 backstops. |
| **Why not cheaper** | R1-R3 can generate and inspect the transition, and R4 can invoke handlers in memory; none establishes transaction visibility, persistence/reload, and real successor/failure routing through the supported host. |
| **Failure signal** | Missing/duplicate journal or failure capability, wrong identity/kind/high-water, unauthorized inverse, or lost route; unavailable host/database is Indeterminate. |
| **Rollback** | Stop typed dispatch, retain the saga/journal for reconciliation, and drain/version affected persisted workflows. |
| **Lenses** | Coverage COV-03, production path, durable authority, history. |

**Open questions:** Which Wolverine/Marten transaction/outbox mode carries the post-completion transition atomically? `(partial: emitted order exists; host crash semantics remain unexhibited)`

### parallel-plan-lowers-to-serial-reverse-completion — Preserve the public/runtime refinement

| | |
|---|---|
| **Claim** | A quiesced fork may retain independent parallel structure in its public rollback plan, but generated generic-state rollback dispatches exactly one inverse at a time in descending completion-sequence order and folds each result before choosing the next, without mutating the public plan. |
| **Scope** | Parallel `ActionRollbackPlan`, fork completion journal, quiescence, generated rollback selection/dispatch, reducer ordering, and generic `TState`. |
| **Consequence** | Runtime order becomes nondeterministic, generic state updates race or disappear, or a serial runtime is falsely documented/tested as concurrent. |
| **State** | Unproven pending a protected multi-lane component fixture and ordering mutation. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Proposed: compiled generated workflow with at least two completed lanes, asserted descending completion sequence, one active inverse, reducer visibility between lanes, and unchanged public parallel plan. |
| **Why not cheaper** | R1 constructs the plan, R2 closes node kinds, and R3 can inspect generated selection, but none executes handler/reducer ordering; R4 is sufficient because the claim is generated component semantics, not real transport concurrency. |
| **Failure signal** | Multiple active inverses, wrong lane order, stale reducer state, or changed public plan; component nonexecution is Indeterminate. |
| **Rollback** | Reject typed fork compensation or serialize through the last known safe generated lowering. |
| **Lenses** | Coverage COV-04, refutation candidate 2, parallel mechanism, production path. |

**Open questions:** None. Arbitrary external-effect commutativity remains consumer-owned.

### packed-ontology-analyzer-loads-and-enforces-aont216 — Test the second shipped analyzer

| | |
|---|---|
| **Claim** | A fresh exact-byte consumer restores and loads `LevelUp.Strategos.Ontology.Generators`, compiles a legal `CompensatedBy` pair warning-free, fails exclusively with enabled-by-default Error AONT216 for a contradictory/missing named inverse, and reports restore inability as Indeterminate. |
| **Scope** | Ontology.Generators nupkg contents/dependencies, isolated feed/cache, Roslyn analyzer loading, shared proof sources, AONT216 metadata/semantics, and package verifier. |
| **Consequence** | Project-reference suites pass while the separately shipped ontology analyzer is absent, unloadable, disabled, or semantically ineffective. |
| **State** | Unproven. The exact-final verifier selected, hashed, source-verified, byte-compared, loaded, and exercised the ontology analyzer; the legal consumer was warning-free and the negative failed exclusively with AONT216. Protected binding is pending. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Exact-final local execution: isolated verifier selecting, hashing, source-verifying, byte-comparing, loading, and exercising the ontology analyzer with legal and exclusive AONT216 cases plus exits 0/2/3. Pending: protected binding. |
| **Why not cheaper** | R1-R3 can construct/package/inspect analyzer sources and R4 can run project-reference tests; none proves NuGet contents, dependency load, isolated restore, and consumer compilation through the shipped package. |
| **Failure signal** | Exit 2 for missing/wrong bytes, analyzer load failure after restore, legal warning/error, absent/wrong AONT216; exit 3 for restore/tool/environment inability. |
| **Rollback** | Do not publish the ontology analyzer; pin the prior package until the shipped composition passes. |
| **Lenses** | Coverage COV-05, package composition, compatibility, proof authority. |

**Open questions:** Does the protected wrapper preserve the verifier's three outcomes and package digests?

### inverse-timeout-configuration-propagates-exactly — Carry custom and default timeout authority

| | |
|---|---|
| **Claim** | Fluent and imported typed compensation preserve an explicit custom timeout, apply the single documented default when omitted, persist the exact ticks in compiled topology/journal metadata, schedule that exact timeout, and reject altered, invalid, stale, or forged timeout messages before state transition. |
| **Scope** | C#/JSON authoring, extraction, wire bridge, generator IR, topology, `InverseTimeoutTicks`, timeout command scheduling/validation, and terminal OutcomeUnknown behavior. |
| **Consequence** | Inverses time out too early/late, source and imported workflows differ, or a forged timeout turns live work into uncertain terminal state. |
| **State** | Unproven. Same-tree pre-rebase generator tests cover source/import default and custom propagation and fail-closed malformed, non-string, zero, and negative imported values; the 1,915-test suite passed locally, but final-revision/protected binding is absent. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: source and imported default/custom fixtures asserting exact extracted, persisted, scheduled, and validated ticks, with malformed/wrong-kind/non-positive/altered/stale/forged negatives. |
| **Why not cheaper** | R1 can generate fields and R2 can shape values, but neither governs independent source/import roots; R3 can inspect assignments but cannot establish default/custom behavior across generated component state; R4 is the first sound shared/component proof. |
| **Failure signal** | Tick mismatch, wrong default, accepted invalid/forged timeout, or missing scheduled message; generator/test nonexecution is Indeterminate. |
| **Rollback** | Reject typed configuration with unproved timeout propagation or revert to the prior documented default while retaining saga state. |
| **Lenses** | Coverage COV-06, contract representation, terminal lifecycle, production path. |

**Open questions:** None; protected execution must still bind both source and imported representations to this exact subject.

### published-packages-bind-approved-subject — Close the producer publication event

| | |
|---|---|
| **Claim** | Product and independent Contracts release tags select the approved commit/tree, trusted publication emits every intended package ID/version, and packages retrieved from NuGet carry matching provenance and approved artifact bytes or an explicitly recorded rebuild policy. |
| **Scope** | `v*` and `contracts-v*` tags, trusted publish workflows, OIDC, package IDs/versions, NuGet provenance, post-publish retrieval/digests, and deprecation/yank response. |
| **Consequence** | A different source is published, one package family is omitted/mismatched, or local candidate success is mistaken for immutable registry delivery. |
| **State** | Indeterminate; no issue-169 release tags, trusted publication, or retrieved registry bytes exist. |
| **Proof rung** | R5 — Production-path integration tests. |
| **Proof artifact** | Missing future evidence: tag-to-SHA/tree binding, trusted workflow result, published package manifest/provenance, and post-publish retrieval/digest comparison. |
| **Why not cheaper** | R1-R4 can construct and test candidate packages but cannot exercise the hosted trusted publisher or immutable external registry; R5 is the first layer reaching the production publication path. |
| **Failure signal** | Wrong tag subject, package/version/provenance/digest mismatch, or failed publish is Fail; registry/network/workflow unavailability is Indeterminate. |
| **Rollback** | Stop publication; if bytes are already published, deprecate/yank where possible and issue a corrected version because consumer-restored bytes cannot be recalled. |
| **Lenses** | Coverage COV-07, evidence binding, compatibility, production path. |

**Open questions:** What rebuild-versus-byte-identity policy applies to trusted tag publication? `(needs human input)`

### inverse-step-receives-durable-rollback-identity — Make consumer idempotency metadata explicit

| | |
|---|---|
| **Claim** | Before an inverse step body executes, its public `StepContext` exposes the exact nonempty durable rollback ID and compensation role minted for the journal entry; missing or incoherent inverse metadata is rejected before invocation, and redelivery presents the same ID. |
| **Scope** | Generated inverse command, worker adapter, public `StepContext`, pre-execution validation, redelivery, docs/API, and consumer idempotency boundary. |
| **Consequence** | A consumer cannot implement the documented durable deduplication contract, guesses from tracing-only `CorrelationId`, or executes compensation with absent/forged identity. |
| **State** | Unproven. Explicit context fields and the pre-execution metadata guard are present; same-tree pre-rebase tests exercised them, while final-revision/protected execution remains absent. |
| **Proof rung** | R4 — Contract and component tests. |
| **Proof artifact** | Existing: public context contract tests plus generated-worker invocation/redelivery cases for exact ID/role and missing/incoherent metadata rejection before the body. |
| **Why not cheaper** | R1 can generate fields and R2 can make their shapes explicit, but neither proves the worker supplies the journal identity or validates it before user code; R3 can inspect wiring but cannot establish invocation/redelivery behavior; R4 drives the public boundary under controlled effects. |
| **Failure signal** | Body invoked without exact rollback ID/role, changed ID on redelivery, or forward work marked as compensation; fixture/generator nonexecution is Indeterminate. |
| **Rollback** | Disable automatic inverse dispatch until explicit metadata is restored; retain the saga/journal for reconciliation by operators. |
| **Lenses** | Wildcard W-2, coverage, durable authority, consumer trust boundary. |

**Open questions:** None. Actual external idempotency remains consumer-owned; this obligation exposes the stable input Strategos promises.

## Refuted

These candidate claims never became active obligations. Stage 3 independently validated the
discriminating evidence and refuted them; retaining the record prevents later promotion.

1. **Inverse execution is exactly once.** Delivery is at-least-once; stable rollback ID plus consumer
   idempotency is the actual contract.
2. **Parallel rollback workers execute concurrently.** Only the structural plan is parallel; generic
   state folds are serial in reverse completion order.
3. **Legacy `Compensate<T>()` receives static proof.** It deliberately remains runtime-only.
4. **Typed EventSourced compensation is sound when `ApplyEvent` compiles.** AGWF045 rejects it because
   the consumer fold is not proved.
5. **Successful rollback makes the workflow successful.** Rollback precedes failure handling but does
   not erase the initiating failure/audit state.

## Run-wide assumptions and open questions

1. Wolverine/Marten transaction/outbox semantics remain external to emitted statement order.
2. Any product change after `263cc5720818b13268214c5df84d7575fd74a6d7` invalidates this ledger.
3. Downstream issues prove coordination only; no adoption percentage is reported.
4. Same-tree pre-rebase revision `cdaa73a` had no observed product failures in its local build and
   complete matrix, but 16 declared skips kept the aggregate Indeterminate. Exact-final package and
   Basileus verifiers passed locally; final-revision tests, PR, protected CI, post-fix review, merge
   authorization, publication, and downstream adoption are pending or unbound and are not `Verified`.
