---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: mechanism inventory across inverse calculus, typed workflow proof, generated durable runtime, wire contract, and package consumer closure
updated: 2026-09-07
skipped: execution and verdict assignment; this lens inventories the immutable mechanism and seeds Stage 2 obligations
lens: mechanism
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative intended compensation mechanism
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: occurrence identity and closed workflow proof consumed by this mechanism
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: effective-guarantee and finite-proof semantics consumed by inverse analysis
---

# Stage 1 survey — mechanism

## Result boundary

This lens describes what revision `42b4ed741c1f406a2de2fdcdaf602ca53dd91dab` is designed to do.
It does not infer correctness from names, comments, generated text, or test existence, and it does not
report any check as passing. Every semantic statement below is either a code-path observation or an
explicit candidate obligation for later proof.

## Before/after mechanism

At the base revision, compensation was a configured CLR step plus an optional ontology
`CompensatedBy` name. The public rollback helpers returned lists of action-name strings, and generated
runtime routing could remember or invoke compensation without deriving its semantic contract from the
forward action. A second `.Compensate<T>()` call silently replaced the first configuration.

The diff replaces that model for statically claimed rollback with four connected mechanisms:

1. a pure, public inverse-contract analysis and immutable rollback syntax tree;
2. a typed workflow occurrence that names both forward and inverse ontology actions;
3. a compilation-time proof that the authored executable inverse is the derived inverse and that
   compensability closes over the rollback scope; and
4. a generated durable protocol that records completed forward work and computes the executable
   rollback prefix only after a failure has acquired persisted authority.

The no-argument `.Compensate<T>()` overload remains as a deliberately separate legacy runtime facade.
It cannot silently contribute a leaf to the typed proof program.

## Mechanism A — inverse contract derivation

`ActionCalculus.AnalyzeInverse` first delegates the forward descriptor to
`ActionContractProofEngine.Analyze`. Invalid forward contracts become `Invalid`; a surviving `Custom`
predicate becomes `Opaque`; and an authority that is not present in the supplied domain lattice is
also invalid. For a valid closed forward action it constructs:

```text
inverse requirement = forward effective guarantee
inverse guarantee   = forward hard requirement
inverse authority   = forward semantic authority
inverse frame       = canonical forward touched-resource frame
inverse subject     = forward ActionSubject
```

The effective guarantee is the #168 value containing explicit guarantees plus sound derived facts and
requirements preserved outside the write frame. Thus the inverse does not merely swap the raw
`Preconditions` and `Ensures` collections.

For an empty frame, omission of an authored inverse produces a distinct proved identity leaf, but an
unresolved explicit `CompensatedBy` declaration is still refuted. A non-empty frame without executable
authored inverse code is `Missing`. When an authored descriptor is supplied, analysis accumulates
failures for declared action-name mismatch, invalid or opaque authored contract, subject mismatch,
semantic-authority mismatch, exact-frame mismatch, and both implication directions for requirement
and effective-guarantee equality. The status precedence is `Invalid`, then `Refuted`, then `Opaque`,
then `Proven`. Solver refutations carry the existing stable symbolic counterexample facts.

`ActionInverseAnalysis` closes the public result algebra to `Proven`, `Missing`, `Refuted`, `Opaque`,
or `Invalid`; closes the obligation set; snapshots failures into an immutable array; and rejects
impossible result shapes. This makes “proved with failures,” “non-proved without reason,” and
“identity with authored code” unrepresentable through its internal constructor.

## Mechanism B — rollback program algebra

`ActionRollbackPlan` is an immutable tree with `Identity`, `Leaf`, `Sequence`, `Parallel`, and `Scope`
variants. Construction validates its closed kind, same-subject children, and exact structural shape;
it derives the union frame, inverse-contract read footprint, recursive compensability, and stable
ordered list of noncompensable leaves.

- A completed leaf wraps its `ActionInverseAnalysis`; the failed forward leaf is absent unless a
  caller incorrectly includes it in the completed input.
- Sequential derivation reverses the supplied completed children, removes identities, and flattens
  nested sequences after reversal while retaining explicit scope nodes.
- Parallel derivation retains branches but rejects write/write intersections and both directions of
  write/read interference. Shared reads alone are admitted. The selected conflict is sorted by
  resource kind/name and branch indexes for deterministic reporting.
- Scoped derivation preserves exactly one nested compensation boundary.
- The empty program is a subject-typed identity rather than an ordinary action whose predicates are
  both `True`.

The public structural plan deliberately preserves parallelism. The generated generic-state runtime,
however, executes inverse workers and folds their returned states serially in reverse completion order
because it has no sound merge algebra for arbitrary `TState`. That is an explicit refinement boundary,
not a second public rollback law.

## Mechanism C — graph freeze and AONT216

`OntologyGraphBuilder.ValidateActionInverses` builds a subject/action lookup, resolves every named
`CompensatedBy` action, calls the runtime calculus, and adds fatal AONT216 when the proof is not
`Proven`. AONT214 retains ownership of malformed authority lattices to avoid duplicate root-cause
diagnostics.

The netstandard `OntologyInverseContractAnalyzer` performs a separate source-time implementation of
the same high-level checks for statically closed forward/inverse descriptors. It consumes the shared
`OntologyActionCatalog`, Roslyn predicate bridge, and finite-domain solver linked into the analyzer,
then reports the enabled-by-default Error descriptor AONT216. Dynamic or unreadable selector syntax
does not become a fabricated inverse; graph freeze remains the runtime-construction backstop.

The proof kernel and syntax catalog are shared source, but the high-level orchestration in
`ActionCalculus` and `OntologyInverseContractAnalyzer` is not one source implementation. That
duplication is a Stage 2 parity obligation.

## Mechanism D — typed workflow authoring and wire shape

`IStepConfiguration<TState>` and every relevant continuation builder expose
`.Compensate<TCompensation>(WorkflowActionReference inverseAction)`. The immutable
`CompensationConfiguration` keeps the CLR compensation step type for execution and the ontology
triple for proof as different fields. Its factory rejects null inverse identity. The builder tracks
whether either compensation overload has already been invoked and throws `InvalidOperationException`
on a second declaration, eliminating last-write-wins divergence between executable step and proof
identity. `RequiredOnFailure` defaults to `true`; a typed declaration with `false` is later rejected.

The TypeSpec `CompensationConfiguration` makes `compensationStepType` required and nonblank with
`@minLength(1)` plus `@pattern(".*\\S.*")`, and adds optional
`inverseAction: ActionReferenceV1`. Generated Contracts C# validates the moniker during both
serialization and deserialization; standalone and bundled JSON-schema projections carry the same
length/pattern constraints under package version 0.12.0. The hand-written `WireDto`, dependency-free
`MinimalJsonReader`, and `WireToModelBridge` preserve a separate resolution state for the CLR moniker
and the exact three-name inverse identity. A present malformed object, incomplete/blank identity, or
unresolvable compensation type fails import rather than being converted to legacy or omitted state.
Legacy documents continue to omit the additive inverse field.

This alignment is the result of the Stage 1 survey: the initial tree allowed a whitespace-only moniker
at the TypeSpec/schema boundary while the importer rejected it. Commit `42b4ed7` adds the constraints,
generated validation, and `ApprovalFailureConfigTests` cases for schema metadata plus empty/whitespace
read and write rejection. The mismatch is resolved in the bound product tree; later stages still must
execute and mutation-check the guard.

## Mechanism E — closed workflow proof

`WorkflowBindingProofAnalyzer` consumes the same compilation-local action catalog and exact
occurrence `WorkflowActionReference` introduced by #167. It first identifies typed/dynamic
compensation boundaries. A typed program must have a statically closed `CompensationTopology`, use
`SagaDocument` persistence, and share a single ontology subject with a closed bound workflow action.
Typed event-sourced workflows receive AGWF045 because the generator cannot prove a consumer-defined
`ApplyEvent` fold during both live handling and replay.

For every compensation declaration the analyzer:

1. rejects `RequiredOnFailure = false` for typed rollback;
2. keeps legacy, dynamic, invalid, missing, and ambiguous inverse identity distinct;
3. resolves the forward occurrence and inverse through the exact catalog triple;
4. proves forward and inverse contracts closed;
5. compares action name when `CompensatedBy` names one, subject, exact frame, semantic authority, and
   both directions of requirement/effective-guarantee equivalence; and
6. emits AGWF044 on disagreement.

Once a typed compensation declaration or a bound action's `CompensatedBy` makes a rollback claim,
the analyzer derives a topology and requires every rollback-reachable executable occurrence with a
non-empty frame to have a proved inverse. It emits AGWF045 for an unclosed topology, cross-subject or
unsupported persistence boundary, or incomplete scope rather than emitting the provable subset. Each
bound action specification is proved independently; the shared occurrence compensation program is
deduplicated and checked once.

This is a third high-level implementation of inverse equivalence above the shared parser and finite
solver. Runtime calculus, ontology analyzer, and workflow analyzer must therefore be tested as three
front ends to one intended contract.

## Mechanism F — topology ownership

`CompensationTopology.Build` converts the workflow IR into closed occurrence and scope records. Its
closed kinds distinguish root, loop iteration, branch path, and fork lane; stable keys incorporate
scope ancestry, lane/path identity, ordinal position, and forward/inverse action identities. It
records ambiguities instead of picking one candidate, retains loop templates, detects parent cycles,
and assigns the deepest applicable structural owner.

The model is consumed by both workflow proof and saga emission, but each emitter still owns routing
logic for its message family. The reverse closure therefore includes top-level steps, nested branch
and loop paths, fork dispatch/join, approval rejection/escalation, confidence diversions, failure
handlers, terminal approvals, and diagnostic-fork paths. A callback shape accepted by the fluent
builder but absent from topology or a trigger emitter is the recurrent failure class inherited from
#135/#140/#143/#144/#187/#196.

## Mechanism G — durable runtime protocol

Typed or mixed source input causes the saga generator to emit the typed compensation state and
message protocol; pure legacy compensation stays on the prior path and a mixed/unprovable program is
rejected at compilation rather than partially trusted.

The generated saga persists:

- a schema version and monotonically increasing compensation journal sequence;
- `ForwardDispatchClaim` records created before a worker is yielded;
- pending and consumed `FailureTriggerClaim` records;
- `CompensationJournalEntry` records created only when an exact forward completion claim is consumed;
- exact occurrence, scope, scope kind, lane/fork/path/ordinal, forward and inverse step/action
  identities, forward execution ID, stable injective rollback ID, timeout, and rollback status; and
- active rollback scope, failed occurrence, fork quiescence, terminal failure, outcome-unknown, and
  rollback-finished state.

Forward start handlers mint a nonempty execution identity and establish a durable claim before
publishing the worker. Completion checks that identity and topology, applies the configured forward
state reducer, converts the claim to a journal entry, then chooses the successor. Reducer or routing
failure after completion first establishes a post-completion failure capability. Failures before
completion consume the matching dispatch claim. A topology-shaped message without a persisted claim,
a duplicate identity, a journal gap, a cross-scope key, or a stale completion is rejected or ignored;
it does not mint rollback authority.

On a valid trigger, the saga selects the exact innermost scope, waits for fork lanes to become
terminal, validates journal continuity and canonical scope histories, and selects completed entries
in descending sequence. Identity leaves are marked rolled back without an inverse worker. Other
leaves get distinct inverse start/completion/failure/timeout messages and a rollback ID derived from
the forward execution ID. A successful inverse result is folded through the configured state reducer
before the next inverse starts. Only after the selected rollback succeeds do workflow failure handlers
run.

The first terminal rollback outcome is monotonic under redelivery. Inverse failure becomes `Failed`;
timeout or unmatched outcome becomes the appropriate failed/unknown reconciliation state; neither is
recursively compensated or interpreted as success. The saga and journal are retained. Structural
validation rejects a missing or unknown schema version and corrupted claim/journal ledgers, which is
the documented in-flight migration behavior.

## Mechanism H — package proof boundary

`scripts/verify-generator-consumer-build.sh` packs exact local Strategos artifacts, resolves their
versions and digests through nuspec/package bytes, uses an isolated local feed and package cache, and
builds a fresh consumer. The positive source now contains a legal typed compensation program. One
negative variant must fail exclusively with AGWF041, and a contradictory inverse variant must fail
exclusively with AGWF044 and the intended witness text. Nullable warnings are errors in the consumer.
Restore runs separately: failure to obtain dependencies exits 3 as `INDETERMINATE`, while a legal
consumer compilation failure exits 2 as a product `FAIL`. This distinction was added after a fresh
probe exposed three generated CS8604 warnings; the final emitter snapshots and narrows the journal
before dereference, and a generator compilation assertion rejects CS86xx warnings.

## Candidate obligations seeded by this lens

1. The three inverse-proof front ends classify every shared vector identically, including precedence
   and stable counterexamples, or document and prove a deliberate boundary.
2. A non-empty frame is never `Proven` without executable authored inverse code; an empty-frame
   identity cannot hide a broken named inverse.
3. Sequential, parallel, scoped, and empty rollback plans preserve same-subject shape, exact reverse
   order, noninterference, immutability, and upward noncompensability.
4. Every accepted fluent or imported typed declaration has one exact forward/inverse identity and
   enters both proof and emission through the same closed topology.
5. AGWF044 owns authored-inverse disagreement; AGWF045 owns incomplete/unrealizable rollback scope;
   unrelated earlier root causes do not create duplicate or misleading compensation errors.
6. No persisted message can create or consume rollback work without the unique authority chain
   dispatch claim -> completed/failure claim -> selected scope -> rollback ID.
7. Completed-prefix selection excludes the failed action, unwinds only the intended nested scope,
   waits for fork quiescence, and preserves enclosing history.
8. Failure/unknown/timeout/corruption are monotonic retained states and successful rollback precedes
   the ordinary failure handler.
9. Contracts 0.12, generated schema/C#, parser/bridge, public API, diagnostic catalogs, and package
   bytes agree on the closed wire and diagnostic vocabulary.
10. The packed consumer proves analyzer loading and contract enforcement without using checkout
    project references, treats nullable warnings as errors, and reports infrastructure inability as
    indeterminate rather than as either pass or product failure.
