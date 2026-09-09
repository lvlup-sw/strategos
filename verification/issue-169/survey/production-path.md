---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: end-to-end production trace from C#/JSON authoring through analyzer proof and generated saga persistence, dispatch, failure, rollback, and reconciliation
updated: 2026-09-07
skipped: live production execution; external Wolverine/Marten transaction behavior is retained as an explicit assumption boundary
lens: production-path
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: intended completed-prefix and nested-scope production behavior
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: source-visible occurrence identity and workflow proof path reused here
---

# Stage 1 survey — production path

## Result boundary

This trace follows actual producers and consumers in the fixed product tree. It distinguishes source
authoring, compile-time acceptance, generated source, and the runtime persistence/message boundary.
The trace does not claim a green build or database run; those belong to later stages.

## Path 1 — C# authoring to a proved compensation program

1. A workflow occurrence calls `.Performs(new WorkflowActionReference(domain, objectType, action))`
   and `.Compensate<TInverse>(new WorkflowActionReference(domain, objectType, inverseAction))`.
   `StepConfigurationBuilder` stores both the forward occurrence identity and a
   `CompensationConfiguration` containing executable CLR type plus inverse ontology identity. A second
   compensation declaration throws before the definition is built.
2. Runtime builder evaluation creates the immutable `StepDefinition` path used by ordinary runtime
   consumers and contract projection. Independently, the source generator reads the syntax rather
   than executing arbitrary user code. `StepExtractor` and `FluentDslParser` accept only a closed,
   constant form and preserve `Missing`, `Dynamic`, `Invalid`, `Ambiguous`, and exact resolved states.
3. `TopologyClosureInspector` verifies that accepted callback shapes are rooted in the visible
   workflow definition. `CompensationTopology.Build` assigns each executable occurrence an exact key,
   structural scope, lane/path/ordinal, and parent relationship. It retains ambiguity/cycle failures
   for fail-closed diagnostics rather than selecting a candidate.
4. `OntologyActionCatalog.Build` scans the same Roslyn compilation and resolves exact
   `(DomainName, ObjectTypeName, ActionName)` triples from rooted ontology declarations. As established
   by #167, this is a compilation-local proof boundary; referenced assemblies and runtime
   `IOntologySource` contributions are not silently proved.
5. `WorkflowBindingProofAnalyzer` first performs the existing forward workflow refinement. It then
   proves every typed forward/inverse pair and the closure of each rollback-claimed scope. An
   authored mismatch becomes AGWF044. An unclosed topology, noncompensable reachable leaf,
   cross-subject program, typed opt-out, or typed EventSourced program becomes AGWF045. Both are
   enabled-by-default errors, so success of the consumer compilation is the acceptance gate.
6. Multiple ontology action specifications may bind one workflow. Each binding gets its own forward
   refinement and rollback-claim check, while the occurrence-level compensation program is deduplicated
   and proved once. This avoids duplicate AGWF044/045 from identical generated execution code.
7. Only an accepted model reaches meaningful typed saga use. Source generation can still emit trees
   in a compilation that ultimately contains errors; emitted text alone must never be treated as
   acceptance.

## Path 2 — imported JSON to the same proof and emitter

1. `*.workflow.json` enters through the generator's AdditionalFiles path.
2. TypeSpec, generated Contracts C#, and both JSON-schema forms require a nonblank
   `compensationStepType`: `@minLength(1)`/`@pattern(".*\\S.*")`, schema `minLength`/`pattern`, and
   generated read/write `RequireNonWhitespace` agree with `MinimalJsonReader`. The reader accepts
   compensation only from an object and—when present—requires an object-shaped `inverseAction` with
   all three nonblank strings. Null/scalar/array/incomplete values remain errors; omission remains
   legacy. `ApprovalFailureConfigTests` directly encodes the moniker schema and generated read/write
   boundary added in `42b4ed7`.
3. `WireToModelBridge` resolves the CLR simple-name moniker against the consumer compilation and maps
   the inverse identity to the same isolated generator model used by C# extraction. Unknown/ambiguous
   type resolution is retained rather than replaced with a guessed type.
4. The imported `WorkflowModel` joins the same topology, action catalog, AGWF044/045 proof, and saga
   emitter as C# input. The production claim therefore depends on front-end equivalence tests, not on
   maintaining a second JSON-only runtime.

## Path 3 — ontology declaration to AONT216 and graph freeze

1. An ontology action names its compensating action through the existing `CompensatedBy` surface.
2. In source-visible closed declarations, `OntologyInverseContractAnalyzer` resolves both descriptors
   through the shared rooted action catalog and performs its analyzer-native equivalence proof. A
   definite disagreement is AONT216 at compilation end.
3. During actual ontology graph construction, `OntologyGraphBuilder` drains registered domains and
   constructs the real subject/action set. `ValidateActionInverses` resolves the named inverse from
   that set and calls `ActionCalculus.AnalyzeInverse`; non-proven results become fatal AONT216 graph
   diagnostics. This covers dynamic/runtime construction that the source analyzer cannot certify.
4. A forward action without a `CompensatedBy` name is not rejected merely because a future workflow
   might author an inverse. The workflow pair is checked at the occurrence/binding boundary. A named
   but unresolved inverse cannot use the empty identity escape hatch.

## Path 4 — start, dispatch authority, and forward completion

For a typed program, the generated saga owns a durable capability chain rather than accepting a
topology-shaped completion at face value:

1. A forward start handler checks that compensation has not reached a terminal/active rollback state,
   that claims and journal are structurally valid, and that the same occurrence/scope was not already
   started or completed.
2. It creates a nonempty forward execution ID and records a `ForwardDispatchClaim` containing the
   exact occurrence key, concrete scope key/kind, fork/lane/path/ordinal data, forward and inverse
   identities, and journal high-water value.
3. Only after that mutation does the handler yield/publish the forward worker command carrying the
   execution ID. The source ordering is clear; atomic persistence relative to message dispatch is an
   external Wolverine/Marten transaction assumption that needs behavioral evidence.
4. A forward completion must match exactly one live dispatch claim and compiled topology. It is
   ignored or converted to retained failure state when stale, forged, duplicate, or structurally
   corrupt.
5. For a valid completion, the configured state reducer applies the worker's returned state first.
   The claim is converted into a `CompensationJournalEntry` with a new monotonic sequence and a stable
   rollback ID injectively derived from the forward execution ID, then removed. Fork/path terminal
   bookkeeping and successor dispatch occur only after the authoritative completion is recorded.
6. A reducer or successor-routing failure after forward completion creates a separate durable
   post-completion failure claim so rollback authority remains tied to the completed journal entry.

## Path 5 — failure ingress and authority validation

The reverse closure contains more than ordinary worker failure. Worker terminal failure, validation,
timeout, forward reducer failure, approval rejection/timeout/escalation, confidence diversion,
branch/loop routing, fork lanes, diagnostic forks, and terminal approval can all initiate or influence
failure flow. The generated handlers normalize them into one of two evidence classes:

- **pre-completion failure:** consumes the still-live forward dispatch claim for the exact occurrence;
  the failed occurrence has no completion journal entry and therefore cannot be rolled back; or
- **post-completion failure:** consumes a failure capability tied to an exact already-completed
  journal entry.

The trigger also carries the expected occurrence/scope/fork metadata and journal high-water. Duplicate
redelivery of the same accepted claim is handled idempotently; a claim whose kind or scope contradicts
its persisted authority, a never-dispatched trigger, a gap in the journal sequence, duplicated
execution/rollback identity, or a corrupted ledger causes retained non-success rather than a guessed
plan.

For a fork lane, failure stops successor dispatch and records the failed lane. Compensation waits
until every lane has become terminal; delayed starts and multiple failed lanes must not race a partial
plan. The selected failure scope must agree with the topology's exact structural owner.

## Path 6 — deriving the concrete completed prefix

1. `BeginCompensationScope` verifies schema version, active claims, journal continuity, canonical
   parent/descendant scope history, and exact failed occurrence.
2. It filters journal entries to the selected concrete scope. Nested branch/loop occurrence keys keep
   one iteration/path distinct from another. An inner failure selects only the inner completed prefix;
   its enclosing history remains available for a later enclosing failure.
3. It orders completed leaves in descending journal sequence. The failed leaf is absent on a
   pre-completion failure because it never produced a completion entry.
4. In fork scopes, structural lanes are already proved noninterfering and must be terminal before
   selection. The public plan retains parallel structure, but the generated runtime serializes state
   folds by reverse completion sequence.
5. An empty-frame proved identity entry is marked rolled back without dispatching executable inverse
   code. A non-empty leaf cannot reach this path unless AGWF044/045 previously accepted a resolved
   authored inverse.

## Path 7 — inverse execution, state fold, and completion

1. The next pending journal entry yields a distinct inverse worker command carrying the persisted
   rollback ID, journal sequence, inverse step/action identity, and timeout.
2. The rollback ID is stable across redelivery and distinct from the forward execution ID. It is the
   consumer-visible correlation/idempotency key for external inverse effects.
3. A matching inverse completion is accepted only for the active journal sequence/rollback ID and
   exact topology. Its `UpdatedState` is applied through the configured saga-document state reducer
   before the entry becomes `RolledBack` and before the next inverse starts.
4. When the selected prefix is exhausted, the saga marks rollback finished and only then invokes the
   workflow's ordinary failure handler. This preserves the policy that handling the failure is not a
   substitute for undoing the completed prefix.
5. Redelivered completion for an already terminal entry is ignored; a completion for the wrong
   rollback ID or journal sequence cannot advance the plan.

## Path 8 — failure, timeout, and reconciliation

- An inverse worker failure marks the active entry/saga failed and records a message.
- An inverse timeout or an outcome whose status cannot be established enters the retained
  `OutcomeUnknown`/failure path.
- A reducer exception, mismatched inverse outcome, invalid journal schema, corrupt claims, or broken
  topology also retains the saga and its evidence.
- No inverse failure recursively creates compensation for the inverse.
- The first terminal non-success or rollback-finished result is monotonic under later deliveries.

This path is intentionally fail closed, but operational recovery is outside the generated algorithm:
an operator/application must reconcile the retained saga and any external effect. The migration guide
instructs deployments to drain or version workflows when the generated journal schema changes.

## Path 9 — package consumer

1. The verification script packs the local product revision and verifies expected nupkg names,
   versions, hashes, and analyzer contents.
2. It creates an isolated local NuGet source/cache and a fresh net10 consumer project with nullable
   warnings as errors.
3. The positive consumer authors a legal #167 binding and #169 typed inverse; a clean build would show
   the packaged generator loaded and the new public/wire surface usable.
4. One mutation creates an illegal forward seam and must fail exclusively with AGWF041. A second
   creates a semantically contradictory inverse and must fail exclusively with AGWF044 plus the
   expected reason.
5. Restore failure exits 3 as `INDETERMINATE`. After restore, a positive compile failure exits 2 as a
   product regression. The script must not reuse checkout project references or a warm global cache
   that could mask a missing packaged analyzer dependency.

## Production-path findings

### PP-1 — contract proof stops before arbitrary inverse implementation behavior

The static path proves that a compensation occurrence names an ontology action whose **declared
contract** is the derived inverse. The CLR type stored beside that identity is checked for resolution
and used to generate the worker, but its `ExecuteAsync` body is not semantically proved against the
ontology predicates. This is unavoidable for arbitrary code and external effects, but it is a
material trust boundary. The packed positive fixture can prove analyzer/package wiring even if its
step body returns unchanged state; the real Postgres fixture is the stronger behavioral backstop for
one concrete program. Documentation must not collapse “contract-correct identity” into a proof of all
implementation effects.

### PP-2 — durability crosses external transaction semantics

The emitter mutates the dispatch claim before yielding the worker message, and the saga model is
Marten-backed. Whether an independently running worker can observe the command before the claim is
durably committed depends on the supported Wolverine/Marten transactional outbox/session semantics.
Static source and in-memory generated tests cannot alone prove the “persisted pre-dispatch” claim.
Stage 2 must require the real host rung and retain the external-runtime assumption.

### PP-3 — public parallel plan and serial generated execution are deliberately different

`DeriveParallelRollbackPlan` preserves independent branches. The saga runtime serializes inverse
workers and state reducers because generic `TState` has no merge. The workflow proof's noninterference
is necessary but not by itself a proof that every external inverse implementation commutes. Stage 2
must verify the stated contract: structural plan algebra remains parallel; generated shared-state
execution is deterministic serial reverse-completion order; external effects still obey the
idempotency/contract assumptions.

## Candidate obligations

1. C# and JSON inputs that represent the same typed workflow produce equivalent IR, topology, proof
   diagnostics, and generated compensation protocol.
2. The source analyzer and graph-freeze paths both reject an authored inverse disagreement while
   allowing dynamic graph construction to receive the runtime backstop.
3. Every worker dispatch is preceded by one exact durable claim; only its matching completion/failure
   can consume it; journal sequence and rollback IDs remain injective under redelivery.
4. Every failure ingress either supplies exact persisted authority or fails closed without beginning
   rollback.
5. The derived plan contains exactly the completed prefix of the concrete failed scope, excludes the
   failed action, waits for fork quiescence, and preserves enclosing history.
6. Inverse reducers run between leaves; ordinary failure handlers run only after successful rollback;
   failure/unknown/timeout/corruption remain monotonic and retained.
7. The SagaDocument restriction is enforced; typed EventSourced is rejected and legacy behavior is
   not accidentally rerouted through the typed protocol.
8. A real Wolverine/Marten host demonstrates A/B complete, C fails, and exactly UndoB/UndoA execute
   with durable state; this supplements rather than replaces adversarial generated-source checks.
9. The packed consumer uses exact local bytes, proves AGWF044 is active, rejects nullable warnings,
   and preserves product-failure versus infrastructure-indeterminate outcomes.
