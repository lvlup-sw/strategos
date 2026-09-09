---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 263cc5720818b13268214c5df84d7575fd74a6d7
base_revision: 362c45f1ebc812ba2e2abb419e47fef29cb1d622
target_ref: codex/169-derived-compensation
implementation_fingerprint: 24be1d97dfaedd88bebc7f33dda831d9b36b1ba1
cost_setting: high
scope_rule: recurrence-to-guard conversion for issue-169 inverse proof, topology, durable rollback, timeout/context metadata, wire, packaged-consumer closure, delivery binding, and semantic claim limits
updated: 2026-09-08
skipped: none; guard protected-path effectiveness remains Unproven until final CI binding
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: present derived-compensation program
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: prior rooted-catalog, topology, identity, and proof recurrence evidence
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: shared predicate/solver authority consumed by inverse proof
---

# Issue #169 guard register

## Ranking

| Rank | Guard class | Primary obligations closed | Current disposition |
|---:|---|---|---|
| 1 | topology-closure-omission | topology, scope, fork, durable ingress | Unproven on protected path |
| 2 | durable-authority-forgery | dispatch authority, journal integrity, scope, IDs | Unproven at R5 |
| 3 | contract-representation-drift | wire, diagnostics, API/docs projections, inverse timeout | Same-tree pre-rebase codegen/parity/tests pass; final-revision/protected binding absent |
| 4 | proof-authority-drift | derivation, equivalence, subject/frame/authority, cancellation, precedence/witnesses | Local mutant killed; shared corpus/protected binding absent |
| 5 | terminal-state-resurrection | terminal lifecycle, failure/unknown retention | Unproven at R5 |
| 6 | packaged-evidence-and-nullability-drift | complete hermetic feed, both analyzer products, warning-free consumer, tri-state evidence | Exact-final local executions pass; protected binding absent |
| 7 | compensation-program-kind-collapse | single mandatory typed mode, mixed/dynamic fail closed, SagaDocument restriction | Unproven on protected path |
| 8 | persistence-fold-divergence | SagaDocument restriction, serial inverse lowering, reducer order, replay boundary | Unproven at R5/R4 split |
| 9 | parallel-rollback-interference | declared noninterference, nested read footprints | Unproven on protected path |
| 10 | inverse-forward-role-confusion | role/message isolation, no recursive rollback | Unproven on protected path |
| 11 | delivery-evidence-subject-drift | final subject, publication, release/adoption claim gating | Missing policy-as-data evidence gate; future events Indeterminate |
| 12 | inverse-semantics-claim-overreach | contract-region wording, concrete-state/event-history limitation, arbitrary effects | Final-source wording corrected; human semantic control remains unbound |

No entry below is called `Verified` merely because its mechanism or local kill fixture exists. A guard
becomes Verified only when its protected path runs against the exact final subject and distinguishes
Pass, Fail, and Indeterminate.

## Final-subject rebase ratchet disposition

Rebasing onto the issue-167 PR head activated three existing structural guards. Same-tree pre-rebase
revision `cdaa73a` passed the 1,915-test generator suite after each was corrected. The branch then
rebased byte-identically onto merged-main base `362c45f`; that pre-rebase execution is not promoted to
a final-revision or protected `Verified` result.

1. The sanctioned-completion-stem guard rejected free-hand rollback completion names in
   `SagaCompensationComponentEmitter`; Events, Worker, and Saga emitters now derive rollback and
   legacy completion event names with `NamingHelper.GetCompletedEventName`.
2. The unvalidated-entrypoint ratchet found five legacy calls in `ConfidenceLoweringTests` against a
   ceiling of six; the policy ceiling ratcheted down to five rather than preserving slack.
3. The step-configuration parity guard found typed `Compensate(WorkflowActionReference)` absent from
   occurrence-metadata policy; it now cites running `StepExtractorResilienceTests` extraction and
   `RoundTripIrFidelityTests` wire proofs.

Independent review then found a P1 outside those three ratchets: AGWF044/045 could be configured away.
Both now carry `NotConfigurable`, with the existing fail-closed descriptor guard extended from
AGWF041-043 to AGWF041-045.

## proof-authority-drift

**class:** Runtime calculus, ontology analyzer, and workflow analyzer independently change inverse
contract normalization, resolution, status precedence, equivalence, or witness semantics.

**first instance:** #167 verification found the workflow proof catalog could resolve an unrooted or
decoy ontology declaration rather than the real `Define(IOntologyBuilder)` root.

**second instance:** #169 has three high-level inverse orchestrators above a shared lower kernel; the
hardening work added effective-guarantee, both-direction equivalence, semantic-authority, and decoy
`Define` corrections independently across them.

**earliest sound layer:** Layer 2 for generated/source-linked shared kernel ownership, completed by
Layer 6 property/mutation vectors for the deliberately separate orchestrators.

**policy data location:** The closed `ActionInverseStatus`/`ActionInverseObligation` domains,
`ActionContractProofEngine`, `OntologyActionCatalog`, and a neutral inverse-vector corpus containing
forward descriptor, authored inverse, lattice, cancellation point, expected status/obligations,
diagnostic owner/count, and normalized witnesses. The corpus is the missing explicit policy-as-data
artifact; until it exists, the class remains open.

**mechanism:** Source-link the normalization/solver/catalog into both analyzer targets; execute the
same neutral vectors through `ActionCalculus`, AONT216, and AGWF044 adapters; compare classifications,
obligation sets, diagnostic precedence/count, canonical counterexamples, and cancellation propagation.
Reject copied solver/catalog implementations with a dependency/source-ownership check.

**kill fixture:** Remove authored-to-derived guarantee implication in
`ActionCalculus.AddEquivalenceFailures`. Historical precursor `mutation-evidence.md` records that
`AuthoredGuaranteeMustRestoreTheForwardRequirement` failed locally there. Additional fixture: allow a decoy
`Define(int)` to hide the ontology root; `DefineOverload_DoesNotHideInverseDisagreement_RegardlessOfSourceOrder`
must fail.

**guard self-test:** Each adapter must distinguish (a) all expected vectors accepted, (b) one
deliberately wrong expected classification/witness/diagnostic owner, and (c) analyzer load/crash or
mid-analysis cancellation. Deleting one adapter, swallowing cancellation, or returning no vectors
must be Indeterminate/fail, never zero-success.

**protected paths:** Required PR job over changes to `src/Shared/Analyzers/Proof/**`,
`src/Strategos.Ontology/**`, both analyzer projects, action/workflow proof tests, and project compile
links; blocks package/merge.

**pass signal:** Exact final SHA/tree; all three adapters ran every vector; no parity differences;
local/source restoration clean; tool versions recorded.

**fail signal:** Any vector classification, obligation, witness, root resolution, or source-ownership
difference; mutation survives or produces an unexpected success.

**indeterminate signal:** Adapter absent, analyzer failed to load, zero vectors discovered, timeout,
cancellation, wrong SHA, or dependency restore failure.

**resource limits:** Exact finite proof with Roslyn cancellation; no semantic atom cap or wall-clock
success gate. Test harness may have a job timeout, which reports Indeterminate.

**temporary exceptions:** None for semantic parity. A deliberate front-end distinction requires a
versioned vector entry with reason, owner, issue, and expiry; it cannot be an inline test skip.

**owner:** Strategos ontology and generator maintainers jointly.

**expiry:** Permanent while more than one inverse front end exists; review at any neutral-kernel
consolidation, never silently remove.

## topology-closure-omission

**class:** Fluent/imported configuration is accepted, but an executable occurrence or failure ingress
is absent from compensation topology, proof, claim creation, or generated routing.

**first instance:** #135 introduced resilience lowering; #140 then fixed a dead `OnFailure` handler
and compensation/failure trigger ordering.

**second instance:** #187/#196 corrected branch/fork/approval termination and identity routing;
#167 subsequently found a low-confidence handler missing from fork proof footprint.

**earliest sound layer:** Layer 4, deterministic integration-graph closure. Constructor/type closure
cannot prove independently emitted routes consume the central topology.

**policy data location:** A machine-readable closed inventory derived from workflow executable-node
kinds and ingress kinds (main step, branch path, loop iteration, fork lane/dispatch/join, approval,
confidence, diagnostic fork, failure handler, terminal), mapped to required topology owner, claim
producer, claim consumer, and emitter route.

**mechanism:** Build the occurrence/ingress graph from generator IR and reject any accepted executable
node without exactly one topology record and every failure-capable route without one authenticated
trigger route. Run per-kind real-generator fixtures, not substring-only checks.

**kill fixture:** Remove the low-confidence or terminal-approval occurrence from topology while
leaving fluent extraction accepted; the AGWF045 topology-semantics fixtures at
`WorkflowBindingTopologySemanticsTests.cs:403` onward must fail. Remove diagnostic-fork trigger
emission; the topology-complete diagnostic-fork runtime test must fail.

**guard self-test:** Feed one deliberately unclosed node kind and require AGWF045 plus no trusted
derived program. Feed an unknown policy enum member and require failure, proving exhaustive policy
handling.

**protected paths:** Required generator topology/closure suite for changes to builders, extractors,
IR, topology, every saga emitter, diagnostics, and generated behavioral workflows.

**pass signal:** Every policy node/ingress has exactly the required graph edges; positive generated
programs compile/invoke; all kill fixtures are rejected.

**fail signal:** Missing, duplicate, partially keyed, or unauthenticated edge; an accepted node has no
topology/route; a kill fixture compiles as trusted.

**indeterminate signal:** Policy inventory empty/unreadable, generator crash, unsupported source not
classified, test discovery zero, timeout, or wrong revision.

**resource limits:** Linear graph inventory over the compiled IR; Roslyn cancellation honored; no
wall-clock correctness gate.

**temporary exceptions:** Unsupported syntax must become a diagnostic, not an allowlist. Any temporary
node-kind exclusion requires an issue, owner, expiration release, and an analyzer error that prevents
typed execution.

**owner:** Strategos workflow-generator topology and emitter maintainers.

**expiry:** Permanent; update policy on every new executable node or failure ingress.

## compensation-program-kind-collapse

**class:** Typed, legacy, dynamic, missing, invalid, or ambiguous compensation states collapse into a
single permissive Boolean or source-order-dependent mode.

**first instance:** Before #169, a second `.Compensate<T>()` silently replaced the first declaration,
allowing executable configuration to depend on call order.

**second instance:** #169 hardening added two-order fixtures after typed+legacy declarations could be
collapsed differently depending on extraction order; dynamic-only derived runtime also needed an
explicit fail-closed path.

**earliest sound layer:** Layer 1/3 closed algebraic `CompensationProgramKind` plus state-machine
restriction; Layer 4 analyzer closure verifies whole-program mixtures.

**policy data location:** Closed program-state table over `{None, Legacy, Typed, Dynamic/Invalid,
Mixed}`, persistence mode, `RequiredOnFailure`, and presence/resolution of inverse identity.

**mechanism:** Classify every occurrence before emission, fold classifications with an associative,
order-independent join, and permit derived runtime only for the closed Typed/SagaDocument case.
Duplicate fluent declarations fail at construction; mixed/dynamic/event-sourced typed cases are
AGWF044/045 errors.

**kill fixture:** Typed-first/legacy-first pair in
`CollapsedTypedLegacyCompensation_WithoutBinding_FailsClosedInBothOrders`; dynamic-only program; typed
EventSourced no-op fold; pure legacy positive.

**guard self-test:** Iterate every ordered pair of program states and compare the fold to the policy
table; an unknown state or noncommutative result fails. Delete EventSourced exclusion and require the
recorded local mutant kill to recur.

**protected paths:** Required analyzer/generator/model tests for changes to fluent APIs, extraction,
import, `CompensationTopology`, persistence model, or emitter activation.

**pass signal:** Full state table evaluated; duplicate/mixed/dynamic/typed-event-sourced negatives
produce intended diagnostics; legal typed and pure legacy positives compile.

**fail signal:** Any unsupported mixture emits trusted derived runtime, source order changes outcome,
or a legal pure mode is rejected.

**indeterminate signal:** A state/vector is skipped, unknown kind defaults to success, analyzer does
not run, or test discovery/restore fails.

**resource limits:** Finite closed-state cross product; no skip-on-size path.

**temporary exceptions:** None may enable typed runtime. A temporary unsupported configuration must
be an explicit blocking diagnostic with owner/issue/expiry.

**owner:** Strategos workflow proof/model maintainers.

**expiry:** Permanent through v2.13; revisit only when legacy is removed or a new persistence proof is
designed.

## parallel-rollback-interference

**class:** A parallel rollback admits overlapping writes or either direction of write/read
interference, or omits reads hidden inside nested plan nodes.

**first instance:** #167 verification found a fork confidence-handler footprint omitted resources,
allowing the forward noninterference proof to miss a path.

**second instance:** #169 hardening added the missing reverse write/read direction and propagated
nested inverse read footprints before parallel rollback could be considered sound.

**earliest sound layer:** Layer 4 deterministic structural analysis over canonical policy data; the
resource sets are constructed at Layer 1.

**policy data location:** Canonical `ActionResource` frame/read sets on every rollback node and the
closed conflict relation `{W/W, left-W/right-R, right-W/left-R}`.

**mechanism:** Fold complete nested frame/read footprints, compare every branch pair against all three
conflict relations, deterministically report the least resource/branch conflict, allow read/read.

**kill fixture:** Remove either write/read direction or nested read propagation. ActionInverse cases
at `:462`, `:487`, and `:511` discriminate illegal and legal behavior.

**guard self-test:** A table-driven/property corpus includes each conflict direction, nested variants,
shared-read positive, disjoint positive, and within-one-branch repeated-resource positive. Each mutant
must make at least one vector red.

**protected paths:** Required ontology/workflow proof tests on calculus, rollback plan, predicate/read
footprint, topology, or fork analyzer changes.

**pass signal:** Every conflict vector rejected with stable resource/branch witness and every legal
vector accepted.

**fail signal:** Illegal pair accepted, legal shared-read pair rejected, or witness/order nondeterminism.

**indeterminate signal:** Incomplete vector enumeration, cancellation/timeout, solver/analyzer load
failure, or wrong subject.

**resource limits:** Exact finite pairwise analysis; no atom/branch cutoff that returns success.

**temporary exceptions:** None. An opaque predicate/resource prevents static proof and must block the
typed parallel claim rather than enter an allowlist.

**owner:** Strategos action-calculus and workflow-proof maintainers.

**expiry:** Permanent while parallel rollback is public.

## durable-authority-forgery

**class:** A message creates, consumes, or advances rollback without the exact saga-minted dispatch or
failure capability and complete stable identity.

**first instance:** Earlier #189/#190/#191/#196 routing fixes (summarized in the #167 dossier) showed
phase or CLR-type identity was insufficient for reused occurrences and handler roles.

**second instance:** #169 hardening added exact execution/occurrence checks, post-completion failure
claims, and corrupt-ledger validation after topology-shaped messages remained too weak an authority.
Independent review then found that binding an already-consumed failure authority to the first signal's
exception kind let a competing timeout/worker-failure signal poison active rollback instead of acting
as an idempotent duplicate.

**earliest sound layer:** Layer 3 state-machine restriction for exact claims, plus Layer 7
production-path crash/redelivery verification for durable commit ordering.

**policy data location:** Versioned capability records `ForwardDispatchClaim`, `FailureTriggerClaim`,
and `CompensationJournalEntry`; exact identity-field schema and transition table from start through
dispatch-claim consumption, reducer, completion journal, post-completion failure capability,
rollback selection, step-visible rollback identity/role, competing-signal idempotency, and terminal
outcome. Pending failure capabilities retain exact cause matching; once consumed into active rollback,
execution/topology authority controls idempotency and the first cause remains durable.

**mechanism:** Require one nonempty exact claim at every ingress; compare execution, occurrence,
scope/kind, lane/fork/path/ordinal, forward/inverse identities, pending failure kind, timeout metadata,
and high-water; match an already-consumed failure by execution/topology authority independent of a
later competing cause; validate uniqueness/continuity before mutation; carry the exact rollback
ID/role into public `StepContext` and reject absent/incoherent metadata before user code; commit each
transition before command visibility under supported host semantics.

**kill fixture:** Weaken completed occurrence comparison to self-comparison. Historical precursor
`mutation-evidence.md` records that `CompletedExecutionIdRejectsDifferentStableOccurrence` failed
locally there. Additional fixtures
cover never-dispatched trigger, forged post-completion trigger, a wrong kind before capability
consumption, both timeout/worker-failure arrival orders after consumption, gaps, duplicates, and
corrupt claims.

**guard self-test:** Each identity/metadata field gets a wrong-field fixture; exact valid redelivery
is a no-op with the same step-visible rollback ID; removing any comparison or the pre-execution guard
must make the corresponding fixture red. Crash/host inability is Indeterminate, not pass.

**protected paths:** Required R4 generated-runtime matrix plus R5 supported-host transaction/redelivery
job for all saga/message/emitter/topology changes; blocks merge/publication.

**pass signal:** All forged/stale/corrupt cases dispatch no unauthorized inverse; valid exact messages
advance once; crash fixture recovers with claim/message consistency.

**fail signal:** Unauthorized state mutation/command, duplicate active rollback, missing retained
failure, or a surviving identity mutant.

**indeterminate signal:** PostgreSQL/Wolverine unavailable, crash injection not reached, test skipped,
timeout, wrong SHA, or missing message family.

**resource limits:** Bounded finite identity mutation matrix; real-host job has explicit timeout that
maps to Indeterminate.

**temporary exceptions:** No authority bypass. Unsupported/migrating saga versions remain retained
non-success with issue/owner/expiry for any operational waiver.

**owner:** Strategos generated-runtime and persistence integration maintainers.

**expiry:** Permanent for every durable compensation schema version.

## terminal-state-resurrection

**class:** At-least-once/stale messages reopen finished, Failed, or OutcomeUnknown compensation or
convert uncertain external effects into success.

**first instance:** #135/#140 compensation and failure-trigger ordering required explicit retry and
terminal handling after the initial lowering left routes live/dead in the wrong order.

**second instance:** #196 and #169 hardening added stale approval/join/diagnostic/start/completion,
multiple-failed-lane, timeout, and inverse-failure monotonicity cases across new ingress families.
The final review cycle also found that two different forward terminal signals for the same consumed
execution could turn active compensation into failure; the corrected handler treats the second signal
as an idempotent no-op and preserves the first durable cause.

**earliest sound layer:** Layer 3 generated state-machine restriction, validated at Layer 7 for real
transport timeout/redelivery.

**policy data location:** Closed compensation status/terminal transition table: Completed/InProgress
may advance only through exact active work; RolledBack/finished, Failed, and OutcomeUnknown are
monotonic; Failed and OutcomeUnknown stay distinct.

**mechanism:** Generate one terminal predicate consumed by every ingress before mutation/dispatch;
inventory all ingress families against it; exercise delayed/duplicate message permutations and real
timeout redelivery.

**kill fixture:** Remove the terminal check from diagnostic fork, approval, join, forward completion,
or inverse completion; or restore cause-sensitive matching for an already-consumed failure claim.
Named runtime cases include both worker-failure-then-timeout and timeout-then-worker-failure orders and
must observe one rollback dispatch, unchanged first-cause metadata, and no compensation failure.

**guard self-test:** Policy matrix tries every ingress against each terminal state and requires no
state/command change; deliberately delete one ingress guard and observe a specific failure.

**protected paths:** Required generated-runtime matrix and supported-host timeout/redelivery job for
all saga handler/message/state changes.

**pass signal:** Terminal snapshots remain byte/structurally unchanged and emit no work for every
ingress; valid active flow still advances.

**fail signal:** Any terminal mutation/dispatch, Failed/OutcomeUnknown collapse, recursive inverse, or
late success transition.

**indeterminate signal:** Ingress inventory incomplete, timeout scheduler/host unavailable, skipped
case, generator failure, or wrong revision.

**resource limits:** Finite state-by-ingress cross product; production job bounded, timeout is
Indeterminate.

**temporary exceptions:** Operator reconciliation may append external audit under a separate API but
must not transition the generated saga; any future recovery transition needs versioned policy,
owner, issue, and expiry/review release.

**owner:** Strategos saga state-machine maintainers.

**expiry:** Permanent; update for every new ingress or terminal status.

## inverse-forward-role-confusion

**class:** CLR step type, phase, or message shape is reused as semantic handler identity, routing
inverse work/results/failures through forward or ordinary failure flow.

**first instance:** #140 fixed `Compensate`/`OnFailure` interop and imposed one ordered failure trigger
because shared configuration paths confused routing responsibility.

**second instance:** #169 explicitly introduced role-aware failure-handler identities and distinct
inverse messages after an inverse CLR type could also be used as an `OnFailure` step. Rebase-activated
completion-stem enforcement then found free-hand inverse completion names that could diverge from
the shared generated naming authority.

**earliest sound layer:** Layer 1/2 distinct generated message types and closed role identity, plus
Layer 5 generated conformance execution.

**policy data location:** Closed handler-role enum/value (`Forward`, `FailureHandler`, `Inverse`),
message-to-handler registration table keyed by role plus occurrence, and public step-context metadata
schema for exact forward/rollback execution identity and compensation role, never CLR type alone.

**mechanism:** Generate distinct inverse command/completion/failure/timeout types through shared
`NamingHelper` completion-name derivation; include role in handler identity/configuration; expose exact
inverse identity/role in `StepContext`; reject missing or incoherent inverse metadata before user code;
prohibit inverse failure from entering compensation trigger; compile and invoke a workflow reusing the
same CLR type in multiple roles.

**kill fixture:** `Emit_InverseTypeAlsoUsedByOnFailure_RoutesInverseErrorsWithoutRecursion`,
`FailureHandlerRoleIdentityTests`, and
`CompletedStemSpelling_EveryEmitterFile_CitesSanctionedSourceOrIsAllowlisted`; change identity to CLR
type only, write a completion stem free-hand, or route inverse failure through the generic trigger.

**guard self-test:** One fixture deliberately uses the same type in all roles and asserts exact handler
registrations/messages/context metadata. Removing the role, distinct type, or pre-execution metadata
guard must dispatch an observable wrong message or invoke the body and fail the fixture.

**protected paths:** Required generator/component suite for message/event/worker/failure handler,
role model, and saga emitter changes.

**pass signal:** Exact role receives each message once; inverse failure retains state and emits no
recursive compensation.

**fail signal:** Wrong successor/handler, duplicate registration, recursive trigger, or type/role
collision.

**indeterminate signal:** Generated assembly fails to compile/load, handler not discovered, fixture
does not exercise the collision, timeout, or wrong revision.

**resource limits:** One minimal collision workflow per role combination; no wall-clock gate.

**temporary exceptions:** None for typed derived compensation. Unsupported role combinations fail
closed; any exception requires issue/owner/expiry and cannot enable execution.

**owner:** Strategos generator handler-routing maintainers.

**expiry:** Permanent while CLR types may be reused across workflow roles.

## persistence-fold-divergence

**class:** Generated inverse state appears correct live but persisted/replayed state or next inverse
observes a different value because folding is absent, late, or consumer-defined without proof.

**first instance:** Prior generated saga/event-sourced migration work required explicit live/replay
state and phase compatibility controls, as recorded in the v2.11 migration history.

**second instance:** #169 must reject typed EventSourced even with a no-op `ApplyEvent` and must fold
UndoB state before UndoA/failure handler in SagaDocument mode.

**earliest sound layer:** Layer 4 structural persistence-mode rejection for unsupported EventSourced;
Layer 7 supported-host state/reload test for SagaDocument fold ordering.

**policy data location:** Closed persistence-mode policy `{SagaDocument: typed allowed,
EventSourced: typed rejected}`; public-parallel/generated-serial refinement; and generated inverse
transition order `quiesce -> choose descending completion sequence -> validate -> reduce -> mark ->
dispatch one next inverse/failure handler`.

**mechanism:** AGWF045 rejects typed EventSourced before trust; compiled generated component asserts
one-active-inverse, descending multi-lane completion order, and reducer ordering without mutating the
public parallel plan; real host persists/reloads state between inverse workers and requires UndoA to
observe UndoB output.

**kill fixture:** Disable EventSourced predicate; historical precursor `mutation-evidence.md` records
the no-op-fold AGWF045 test failed locally there. Move reducer after next dispatch;
generated/behavioral stage trace must fail.

**guard self-test:** Typed EventSourced negative, legacy EventSourced positive, multi-lane
SagaDocument serial-order positive, reducer carrier, public-plan immutability, and ordering mutant.
Host outage is Indeterminate.

**protected paths:** Required analyzer/generator tests and PostgreSQL behavioral job for persistence,
reducer, compensation component, workflow model, or migration changes.

**pass signal:** Typed EventSourced rejected exactly; legacy positive preserved; persisted SagaDocument
state flows UndoB -> UndoA before failure handler.

**fail signal:** Typed EventSourced accepted, legacy broken, next inverse sees stale state, or handler
starts early.

**indeterminate signal:** Database/host unavailable, fixture skipped, generated code fails to load,
timeout, or wrong revision.

**resource limits:** Minimal two-inverse host workflow; bounded job timeout maps to Indeterminate.

**temporary exceptions:** No typed EventSourced allowlist in v2.13. A future design must version the
policy with owner/issue/release and provide replay proof before expiry.

**owner:** Strategos persistence and generated-runtime maintainers.

**expiry:** SagaDocument fold guard permanent; EventSourced prohibition expires only with a proved
event-fold design and migration.

## contract-representation-drift

**class:** TypeSpec, generated C#/schemas/catalog/docs, hand parser/bridge/projection, analyzer
descriptors, topology, or persisted journal disagree on compensation identity or diagnostic tokens.

**first instance:** `9b0df25` added fail-closed import hardening after schema/JSON monikers and generator
resolution could diverge; #167 repeated the boundary for exact action identity.

**second instance:** #169 Stage 1 found TypeSpec accepted blank `compensationStepType` while the reader
rejected it; precursor `42b4ed7` (rebased as `0a34ee9`) added a nonblank pattern and regenerated
artifacts. The final review cycle found the same class at compensation timeout: authored C# validation
did not cover the compensation slot, and imported malformed/non-string values could bypass semantic
validation by becoming omission.

**earliest sound layer:** Layer 2 generated contracts from TypeSpec, completed by Layer 5 shared
contract tests for hand-written readers/projections/live descriptors.

**policy data location:** TypeSpec `StepConfiguration.tsp` and `AgwfCatalog.tsp` for wire/generated
diagnostics; exact `ActionReferenceV1`; compensation timeout default/custom/range policy; explicit
malformed-kind and wrong-JSON-kind vector data for hand parser/bridge; generated/live descriptor
contract including non-configurable proof failures; occurrence-metadata parity policy; and public API
baselines for the C# surface.

**mechanism:** Regenerate and require clean tree; compare enum-entry/catalog/schema/live descriptor
sets; run valid/invalid JSON through schema, reader, bridge, IR, projection, fingerprint, and analyzer;
carry default/custom inverse timeout through topology, journal, scheduled message, and validator;
reject malformed, non-string, zero, and negative imported timeouts rather than downgrade them to
omission; require typed `Compensate` to name extraction and round-trip fixtures.

**kill fixture:** Remove `@pattern(".*\\S.*")`, one inverse identity component, AGWF045 enum member,
live descriptor parity/`NotConfigurable` tag, typed-`Compensate` occurrence policy, custom timeout
propagation, default-timeout assignment, or imported wrong-kind/non-positive rejection. The
corresponding schema/import/catalog/component/ratchet test must fail.

**guard self-test:** Mutate each representation root separately; clean regeneration must expose
generated edits, while hand-root parity/malformed fixtures expose reader/descriptor edits. A generator
that emits nothing is Indeterminate.

**protected paths:** Required contracts-codegen clean-tree job, contracts/wire/import/projection/API
tests, and package probe for TypeSpec/schema/DTO/reader/bridge/diagnostic/public API changes.

**pass signal:** Regeneration ran with recorded TypeSpec version and clean tree; all valid round trips
exact; all malformed vectors fail closed; generated/live sets equal.

**fail signal:** Unexplained generated diff, acceptance mismatch, lost identity, unknown diagnostic,
or malformed input lowers a saga.

**indeterminate signal:** Code generator missing/no-op/crash, tool version absent, fixture not run,
dependency restore failure, or wrong revision.

**resource limits:** Deterministic finite schema/vector set; no network required after tool restore.

**temporary exceptions:** Compatibility exceptions live as versioned vector entries with owner,
issue, reason, and expiry release; unknown discriminators/blank identity cannot be excepted to success.

**owner:** Strategos Contracts and generator import maintainers jointly.

**expiry:** Permanent for every published contract version.

## packaged-evidence-and-nullability-drift

**class:** In-repo/project-reference checks observe different bytes, an incomplete local dependency
feed, a missing analyzer product, or a different compilation policy than a fresh consumer; or
infrastructure failure/warnings are collapsed into success.

**first instance:** The initial #169 package probe exercised #167 seams but omitted typed compensation
and AGWF044, so analyzer presence could look green without feature enforcement.

**second instance:** The new typed positive exposed generated CS8604 warnings; follow-up work made
all warnings fatal and separated restore `INDETERMINATE` (exit 3) from product `FAIL` (exit 2). The
exact-precursor Basileus smoke then failed because its feed packed Agents without the exact core
dependency, COV-05 found the workflow probe omitted the separately shipped ontology analyzer, and a
later stale fixed-version cache showed why a fresh feed alone was insufficient provenance.

**earliest sound layer:** Layer 7 fresh packaged-consumer integration with a three-outcome result.

**policy data location:** Script-owned complete in-repository package/dependency manifest
(names/versions), exact nupkg SHA-256 set, consumer source variants for both analyzer products,
expected exclusive AONT216/AGWF041/AGWF044 codes/reasons, fatal warning prefixes, and exit-code enum
`{0 Pass, 2 Fail, 3 Indeterminate}`.

**mechanism:** Pack the complete dependency closure at coherent versions; reject/remove stale feed
artifacts; verify identities, dependency metadata, bytes, and analyzer contents; restore through a
per-run empty cache mapped exclusively to the fresh Strategos feed for Strategos IDs; compare every
restored Strategos nupkg byte-for-byte with its source-feed artifact; run the Basileus-shaped consumer;
build legal workflow and ontology programs warning-free; build exclusive AONT216, AGWF041, and
AGWF044 negatives; propagate the three-way exit to CI without tolerance.

**kill fixture:** Omit the exact core dependency from the Basileus feed, seed a stale same-ID package,
omit either packaged analyzer, disable AONT216/AGWF044, reintroduce emitted CS8604, or force dependency
restore offline. Each must produce the specific fail/indeterminate signal, never exit 0.

**guard self-test:** The legal build must fail if any warning/error exists; each negative must fail
with exactly its expected diagnostic and no unrelated error; a seeded stale same-ID package must be
rejected or bypassed by exact source/restored-byte equality; an intentionally impossible restore must
exit 3; missing package/digest subject is Indeterminate.

**protected paths:** Required pack/consumer job for all product, generator, analyzer, contracts,
package metadata, and verification-script changes; blocks publication/merge.

**pass signal:** Exact SHA/tree and all nupkg digests recorded; Basileus-shaped smoke and both legal
analyzer consumers run warning-free; exclusive AONT216, AGWF041, and AGWF044 negatives observed;
script exit 0.

**fail signal:** Exit 2 with missing/wrong package, positive compile warning/error, analyzer negative
not caught, or unexpected diagnostic.

**indeterminate signal:** Exit 3 for restore/network/tool infrastructure, timeout, missing artifact
binding, or wrong revision; wrapper must preserve it.

**resource limits:** Isolated package cache and bounded restore/build timeout; timeout/network is
Indeterminate, not product Fail or Pass.

**temporary exceptions:** No warning/diagnostic exceptions. External vulnerability-feed unavailability
may be recorded separately as Indeterminate with owner/expiry, never used to certify artifact bytes.

**owner:** Strategos packaging and generator maintainers.

**expiry:** Permanent for every generator/analyzer package release; update positive/negative source on
each new protected diagnostic feature.

**exact-final local execution:** At revision `263cc57`/tree `24be1d97`, fresh packing produced 14
binary and 12 symbol packages. The legal typed consumer compiled with zero warnings/errors; the three
negative consumers failed exclusively with AONT216, AGWF041, and AGWF044; exact source/restored-byte
provenance passed from an empty package cache; and the Basileus-shaped smoke passed 2/2. All verifier
directories were ephemeral and removed. Pack's NU1900 vulnerability-feed warnings remain a separate
Indeterminate environmental signal. This local execution does not satisfy the still-absent protected
path, so the guard is not `Verified`.

## delivery-evidence-subject-drift

**class:** Local, hosted, published, reviewed, or downstream evidence is used to support a delivery
claim about a different or unnamed source/artifact subject, or coordination is reported as adoption.

**first instance:** Stage 3 PF-12 found exact-precursor local package evidence while protected
CI/review/merge remained absent; one result could be mislabeled as the other without an explicit
evidence graph.

**second instance:** PF-11/COV-07 found open downstream issue links and local candidate packages but
no consumer implementation revision, producer publication event, or retrieved registry bytes.

**earliest sound layer:** Layer 4 deterministic evidence-graph validation; human disposition and
merge authority remain a Layer 8 input that the graph can require but not decide.

**policy data location:** Proposed machine-readable matrix of claim kind to required revision, tree,
artifact digest, environment/tool/policy version, hosted result, review record, consumer revision,
publication provenance, and release disposition.

**mechanism:** Before any merge/publication/adoption state transition, validate the evidence graph
against the exact final subject. Reject missing, stale, mismatched, skipped, or issue-link-only nodes;
preserve Fail separately from Indeterminate.

**kill fixture:** Supply a green check for the previous SHA; a semantic version with no digest; an
open Basileus/Exarchos issue with no consumer commit; or a release tag pointing at another tree. None
may advance the corresponding claim.

**guard self-test:** Exercise complete/failed/indeterminate evidence graphs and every missing/mismatch
edge. An empty policy, zero discovered required nodes, API outage, or wrong subject must not pass.

**protected paths:** Required final-evidence job before merge and publication; repeated at adoption
claim time and bound to final PR/tag/package/consumer subjects.

**pass signal:** Every required evidence node exists, is successful where success is required, and
matches the exact claim subject and artifact digests; required human disposition/authority is present.

**fail signal:** A check/product/consumer/publish verdict ran on the intended subject and failed, or a
deterministic SHA/tree/digest/policy mismatch exists.

**indeterminate signal:** Missing/skipped/timed-out/unreadable record, unavailable hosted service or
registry, absent consumer revision, or no final subject.

**resource limits:** Finite manifest/evidence graph; bounded hosted queries report timeout as
Indeterminate.

**temporary exceptions:** None may convert absent evidence to success. A deferred external adoption
remains explicitly open with owner, issue, and target release.

**owner:** Strategos release owner plus the user granting merge authority; downstream owners supply
their own consumer evidence.

**expiry:** Permanent for merge, publication, and every external adoption claim.

## inverse-semantics-claim-overreach

**class:** Public text calls unary contract-region re-establishment, may-touch-frame equality, or one
behavioral example a concrete state/event-history inverse or universal external-effect proof.

**first instance:** Precursor public XML says the hard requirement is “restored after rollback” and
calls the frame the exact frame the inverse “must restore,” without saying this is set membership and
a may-touch set.

**second instance:** Precursor docs use inverse/restoration language across calculus/API surfaces
while the non-singleton `x > 0` counterexample and append-only event frame show no concrete round-trip
law exists.

**earliest sound layer:** Layer 8 human semantic review. Term inventories can assist at Layer 4 but
cannot decide mathematical truth or product intent.

**policy data location:** Proposed normative claim-boundary section defining requirement-region
re-establishment, semantic authority, may-touch frame, absence of concrete pre-state/event-history
reversal, arbitrary-code boundary, and at-least-once consumer idempotency.

**mechanism:** Final release reviewer compares every public XML/docs/changelog/PR proof claim against
the normative boundary and the non-singleton/event/no-op discriminators. Mechanical docs build/link
and term scans are prerequisites, not the semantic verdict.

**kill fixture:** The precursor “restored concrete state/exact frame must restore” interpretation,
the `x > 0` forward/inverse pair that ends at a different positive value, an append-only event using
the same frame token, and a no-op CLR body with a valid descriptor.

**guard self-test:** Give the reviewer one deliberately overbroad exactly-once/concrete-restoration
claim among otherwise precise text and require an explicit failing disposition. No reviewer or no
completed corpus review is Indeterminate.

**protected paths:** Final semantic review after docs/XML/API generation and before merge/release;
rerun after any public claim change.

**pass signal:** Reviewer records the final subject and confirms every claim stays within the
normative boundary, with all discriminating examples considered.

**fail signal:** Any public claim asserts concrete state/event-history reversal, arbitrary body/effect
proof, or exactly-once delivery without a new sound model/proof.

**indeterminate signal:** Missing/stale reviewer, incomplete public-text inventory, absent normative
boundary, or review bound to another subject.

**resource limits:** Finite public-text/claim inventory; no automated semantic-success shortcut.

**temporary exceptions:** None for overclaiming. A future relational inverse design must version the
policy, proof, migration, owner, and release before changing this boundary.

**owner:** Strategos calculus/API/documentation maintainers and final release reviewer.

**expiry:** Permanent until the public proof model itself changes and is independently verified.
