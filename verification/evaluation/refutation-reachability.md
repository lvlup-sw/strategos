---
repo_path: /home/reedsalus/.cursor/worktrees/strategos/891j
target_kind: diff
revision: 324768f4d4f6d292e7d86045f711c6c50946b8c9
base_revision: 4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa
target_ref: cursor/c801a047
cost_setting: high
scope_rule: reverse dependency closure of changed surfaces vs merge-base 4d060f4
updated: 2026-08-27
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/185
    why: residue tracker and “still open by design” list
  - path: /home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md
    why: the dispatch plan this follow-up is verifying
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/docs/specs/2026-08-22-correctness-core.md
    why: AGWF035 / termination-class design
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/CHANGELOG.md
    why: claims the diff makes about itself (Residue (#185) subsection)
---

# Refutation — reachability

Lens 3, reachability angle only. Try to kill each active obligation by showing
the named failure cannot occur on any reachable path, or that something else
already guarantees the claim. Default to refuted when uncertain. The obligation
is the target, not its wording. The proof was read with the claim.

Production `TerminalReachabilityGuard.Report` never receives a graph
(`WorkflowIncrementalGenerator.cs:1038-1043`). It builds
`phaseGraph ?? PhaseGraph.Build(model)` (`TerminalReachabilityGuard.cs:127`).
`EnumerateRejoinDispatchersOf` and `PhaseGraph.Build` walk the same constructs
(forks, branches, loops, approvals, confidence, linear predecessor). For a
C# extract whose last steps sit in `model.StepNames`, `Build` records the
rejoin edges the enumerator expects. The only fixtures that fire under-reach
inject `PhaseGraph.WithoutSuccessor` (`TerminalReachabilityDiagnosticTests.cs:459, :484`).
That fact is reused below; it is not itself a verdict.

## Verdicts

### [agwf035-underreach-ir-not-emission] — SURVIVED

The named failure is a shipped saga whose IR still lists the rejoin while the
emitter omits `Start{Finally}` (#184). That pair is two walks, not one.

`ReportUnderReach` asks `graph.SuccessorsOf(lastStep)`
(`TerminalReachabilityGuard.cs:150-164`). Production `graph` is
`PhaseGraph.Build(model)`. The saga is a separate lowering
(`SagaStepHandlersEmitter` path-end / approval handlers). A one-sided edit to
the saga walk drops `Start{Finally}` while `EdgeBuilder.AddBranch` /
`AddLoopEdges` still `AddRouted(last, RejoinStepName)`. The guard cannot see
that miss. `#184` was that miss; the emitter fix does not make the two walks
one walk.

`WithoutSuccessor` is not the production miss. It is the test stand-in for an
IR hole the fluent extractors do not produce. The reachable class is the
second walk.

Killing argument that failed: “the emitter is already fixed, so the class
cannot occur.” A lock that only holds while nobody edits `SagaStepHandlersEmitter`
is not a lock. The claim is that AGWF035 decides dispatch in the shipped saga.
It decides successors in `PhaseGraph`.

### [phasegraph-type-not-instance] — REFUTED

The real claim is no-drift between AGWF035 and `ValidTransitions`, not object
identity.

`PhaseGraph.Build` is a pure function of the model (`PhaseGraph.cs:67-94`).
`TransformToResult` constructs one `WorkflowModel`, calls `Report` (first
`Build`), and returns that model (`WorkflowIncrementalGenerator.cs:1013-1045`).
`RegisterSourceOutput` then `EmitWorkflowSources` → `TransitionsEmitter.Emit`
→ second `Build` (`TransitionsEmitter.cs:56`) on the same unchanged object.
Two pure calls on one model cannot publish disagreeing successor sets.

`WithoutSuccessor` is never passed from the generator. Instance-share would
be a stronger lock against a future impure `Build`; it is not required for
the current composition. Something else already guarantees the claim: purity
plus a single model snapshot.

### [agwf035-catalog-polarity-lie] — REFUTED

The inverted sentence is only rendered inside `ReportUnderReach` when
`SuccessorsOf` lacks the terminal (`TerminalReachabilityGuard.cs:150-163` →
`WorkflowDiagnostics.cs:564`). Production `Build(model)` records that edge
for every last step `EnumerateRejoinDispatchersOf` lists (same constructs;
`Add` no-ops only when the last step is absent from `StepNames`, which the
C# extract does not do). Emitter-miss does not trip this arm. Authors of a
`[Workflow]` therefore never see `{0}` = terminal / `{2}` = last step.

Over-reach uses the same template with the original polarity and does fire
(`Diagnostic_TerminalNotLastMainFlowStep_Fires`). That path is a true
sentence, not the lie.

The lie is test-only (`WithoutSuccessor`). An author-invisible catalog
sentence is not a reachable remediation failure.

### [agwf035-error-still-emits] — SURVIVED

Over-reach is a reachable AGWF035 Error. The generator reports it after
`hasErrors` and still returns a non-null model
(`WorkflowIncrementalGenerator.cs:933-1045`). `RegisterSourceOutput` emits
when `result.Model is not null` (`:84-87`). Remarks say “a workflow that
cannot reach its termination does not run” (`WorkflowDiagnostics.cs:556-558`).
AGWF037’s remarks state the opposite policy and implement it (`:603-606`,
`hasDuplicatePermittedForkTrigger` at `:930-938`).

`DiagnosticSeverity.Error` fails a default `dotnet build`. It does not
implement the emit-or-gate policy the remarks sell. `.editorconfig` /
`NoWarn` / severity override is a reachable suppress-and-ship path. The
split is in the same method as AGWF037; it is not an accident of types.

### [agwf035-json-import-unreached] — REFUTED

The named failure is imported under-reach with no AGWF035.

`WireToModelBridge` sets `Loops: null` and `Branches: null`
(`WireToModelBridge.cs:239-240`). There is no `Finally` extraction on
import (`FluentDslParser.ExtractDeclaredTerminalStepName` is C# only).
`ReportUnderReach` is skipped when `declaredTerminalStepName` is null
(`TerminalReachabilityGuard.cs:119`). Fork joins that import does map
always get `AddRouted(path.LastStepName, fork.JoinStepName)`
(`PhaseGraph.cs:171-172`).

The missing `Report` call cannot hide an imported under-reach: that IR
cannot be built. AGWF037 on import is a different diagnostic with a
different subject. Whether a consumer declares `*.workflow.json` is then
beside the point for this claim.

### [agwf035-all-complete-silent] — SURVIVED

All-`Complete()` plus `Finally<T>` is a reachable authored C# shape. The
false-positive (AGWF035 on that shape) is the failure the issue and plan
named. `AddBranchRejoinDispatchers` skips `IsTerminal` cases
(`TerminalReachabilityGuard.cs:254-257`). `CollectConstructDispatchers`
keeps the predecessor out of the linear scan (`:366-371`).
`Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` drives production
`Build` and `RunGenerator`. Silence is not vacuous: a naive “someone must
list the terminal” rule would fire on this path.

### [agwf035-overreach-preserved] — SURVIVED

Over-reach is reachable without a test seam.
`Diagnostic_TerminalNotLastMainFlowStep_Fires` and
`Diagnostic_SuccessorResolvesOffMainFlow_Fires` use the production
classification. The under-reach arm sits beside that scan in the same
`Report`. A regression of the already-shipped half is a reachable
`[Workflow]` compile. Existing fixtures are the lock, not a tautology.

### [agwf037-reject-not-dedup] — SURVIVED

Two same-trigger `PermitTrigger` declarations are reachable on both
authoring fronts. The extractor reports and `return false`
(`DiagnosticForkExtractor.cs:174-180`). Import scans
`FindDuplicateTriggerNames` before `Create` (`WireToModelBridge.cs:459-492`).
`hasDuplicatePermittedForkTrigger` joins `hasErrors`
(`WorkflowIncrementalGenerator.cs:930-938`). JSON has no CS0152. First-wins
would drop one evidence schema. The named-trigger claim is not rescued by
the empty-name residual.

### [contracts-0-7-0-pack-incomplete] — SURVIVED

The claim is what a green pack test proves, not what this revision’s
csproj happens to pack.

`PackagingTests.Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent`
asserts the 0.7.0 filename, the nuspec version, and three older schema
basenames (`SdlcEventEnvelope`, `WorkflowDefinitionV1`, `InvariantEntry`).
It does not name `agwf-catalog.json` or
`AgwfEntryDuplicatePermittedForkTrigger.json` (`PackagingTests.cs:107-137`).

The csproj does pack `schemas/**/*.json` and `Generated/agwf-catalog.json`
(`Strategos.Contracts.csproj:59, :68-73`). Deleting either `Content`
include is a one-line edit. The test this wave updated stays green. Exarchos
extracts from the nupkg. Current-pack-includes-the-files is not the same
as the test locking them.

### [contracts-changelog-contradicts-0-7-0] — SURVIVED

A reader of the 2.11.0 lede or of the packaged Contracts CHANGELOG is a
reachable consumption path.

`CHANGELOG.md:17` still says Contracts **0.4.0 → 0.6.0**. Residue at
`:182` says **0.6.0 → 0.7.0**. `src/Strategos.Contracts/CHANGELOG.md`
Unreleased names AGWF036 / 0.6.0 and is `Pack="true"`
(`Strategos.Contracts.csproj:53`). The csproj pin cannot be read from
those two documents. The contradiction is the texts, not a future edit.

### [schema-diff-skip-succeeds] — REFUTED

The skip-success YAML branch exists (`contracts-schema-diff.yml:44-46`,
`:52-54`, compare gated at `:58`). This project’s job uses
`fetch-depth: 0` (`:27-30`), which fetches history including tags. This
repository has `v*` tags that contain `src/Strategos.Contracts/schemas`.
On that checkout `have_prev=true` and the structural diff runs.

The named consequence — this wave’s AGWF037 schema classified by a green
job that printed “no diff to run” — is not the path the committed workflow
takes against this repo. A tag-less fork is a different subject. Whether
the job is a required check is then a badge question, not a skipped
compare of this PR. Default: the skip is not a reachable failure of
*this* composition.

### [mcp-resulttype-and-pin] — SURVIVED

`MapTraversalResult` and `ErrorResult` assign `ResultType`
(`OntologyServerToolFactory.cs:384-386, :410-412`). Hosting
`VersionOverride` 2.2.0 (`Strategos.Ontology.MCP.Hosting.csproj:18-20`)
is required to compile those assignments.

`CreateServerTools` also registers `ontology_explore`, `ontology_query`,
`ontology_action`, and `ontology_validate` (`:82-86, :138-148`). Those
handlers return domain objects (`:155-225`). This repository never
constructs their `CallToolResult`. The wire shape is the 2.2.0 SDK wrap.
CPM remains 1.3.0. The Hosting test project pins 2.2.0 on its own.

Removing the Hosting override *and* the two assignments is a reachable
edit: Hosting compiles against 1.3.0; the four tools still wrap; tests
keep their pin and stay green. INV-3 check 3.4 is a file-level grep, not
a CI job. The class “every constructed `CallToolResult` emits
`resultType: complete`” is not closed on the four-tool path.

### [icons-null-when-unset] — SURVIVED

The delivered claim is null-when-unset, no placeholder. That path is
reached.

`CreateServerTools` always `Discover`s (`OntologyServerToolFactory.cs:78-80`).
`Discover` never assigns `Icons`. `ApplyIcons` returns on null (`:249-254`).
Factory tests assert both descriptor and protocol icons are null after
`Discover` + `CreateServerTools`. A placeholder cannot appear on the
public root.

The non-null mapping is test-only (`CreateServerTool` internal). That is
not a violation of “null when unset.” It is also not a reason to drop the
obligation: Discover growing a default icon is a reachable edit, and the
existing assertion is the lock. Consumer-supplied icons were not this
wave’s delivery.

### [handauthoredcontract-unreached] — REFUTED

No production path assigns `DescriptorSource.HandAuthoredContract`.
`rg` in `src/` finds the enum member, remarks, `IsHandSide`, and tests.
The only `Source = HandAuthoredContract` writes are test object
initializers.

`MergeTwo.cs:67` restamping `HandAuthored` cannot run on a value nothing
produces. Unwidened `== HandAuthored` at `OntologyGraphBuilder.cs:330, :409, :566`
cannot skip a live `2`. AONT205 skip-unless-Ingested
(`IngestedIntentInvariant.cs:22-24`) **is** reached for `Ingested`.

The CHANGELOG sentence “TypeSpec / JSON contract-authored actions survive
graph merge” names a producer that is not in this repository. Out-of-repo
assignment is an unvalidated premise (`needs human input`). An unused
enum member is not a reachable product failure. Default: refuted.

### [descriptor-source-docs-omit-member-2] — REFUTED

`source.md:65-66` and `ontology-sources.md:42-43` list `HandAuthored` and
`Ingested`. Those are the two values any in-repo path can stamp.

Documenting member `2` would describe an assignment no shipped surface
performs. Authors who follow the pages stamp `Ingested`; AONT205 on
ingested+intent is the intended, reached invariant. The omitted bullet
cannot cause a wrong `Source` on a reachable path.

### [requires-obsolete-observable] — SURVIVED

`[Obsolete]` on `IActionBuilder<T>.Requires` (`IActionBuilderOfT.cs:39-40`)
is the consumer signal. This wave also added `CS0618` to `NoWarn` for
every test and benchmark project (`Directory.Build.targets:3-5`). The
implementation still appends an `ActionPrecondition` (`ActionBuilderOfT.cs:77-90`).

Removing the attribute is a reachable edit. The suite this wave silenced
cannot observe it. The packaged ontology README still demos `.Requires`
with no obsolete note — a reachable copy-paste path that hides the
attribute from the same readers the guide caution is for.

“Consumers with default warnings see CS0618” is true today. It is not a
lock that the attribute stays. The obligation is that a clean in-repo
compile is not that lock. The `NoWarn` this wave added makes that exact
failure reachable.

### [renovate-resolve-unasserted] — REFUTED

The #181 failure was a path that 404’d. The second `extends` token now
names `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`
(`renovate.json:4-6`). Survey reports that file exists on the renamed
repo. The wrong-path 404 is not reachable after the token matches the
file.

Whether the GitHub App still resolves the `lvlup-claude` slug is an
unvalidated external-process premise. Nothing in this repository can 404.
Default: the path-token defect is closed; “resolves” as a bot run is not
a demonstrated in-repo failure.

### [aont205-analyzer-unreached] — REFUTED

`OntologyDiagnostics.IngestedContributesToIntentOnly` has no
`Diagnostic.Create` site. Runtime AONT205 **is** reached:
`OntologyBuilder.ValidateIngestedIntentInvariant` (`:263-276`) and freeze
(`OntologyGraphBuilder.cs:494-496`).

Every composition that produces an `OntologyGraph` hits that invariant.
`Define()` without `ApplyDelta` / `Build()` produces no graph, so
ingested+intent cannot ship. This wave retargeted the runtime scan; it
did not claim to wire the leftover analyzer field. An unused
`static readonly DiagnosticDescriptor` cannot fail a consumer build or
allow a bad graph. Package text still says “AONT001–AONT035”; it does
not advertise compile-time AONT205.

### [compat-agwf035-breaking] — REFUTED

A breaking diagnostic requires a previously-successful `[Workflow]` that
now fails. Production `Report` uses `PhaseGraph.Build(model)`. That graph
contains the rejoin edges `EnumerateRejoinDispatchersOf` requires. The
only red fixtures strip an edge with `WithoutSuccessor`
(`TerminalReachabilityDiagnosticTests.cs:456-490`). No consumer
compilation passes a graph.

JSON import never calls `Report` and cannot represent Finally / loops /
branches. There is no newly-failing authored C# shape on this revision.
The “breaking” presentation is the catalog id reuse, not a reachable
consumer compile break.

### [compat-validtransitions-nonreversing] — REFUTED

Already-emitted consumer source does not reverse on a generator revert.
That is the standing source-generator contract, not a defect this wave
introduced.

This lift did not change `ValidTransitions` / `IsValidTransition`
signatures (`TransitionsEmitter.cs:68-109`). The algorithm moved; it was
not re-specified. No persisted table exists. No evidence successor sets
changed versus `4d060f4`. A faithful move cannot change the published
pair. Non-rebuild after revert is true of every generated file.

### [compat-publicapi-omits-obsolete] — REFUTED

RS0016/RS0017 track add/remove of members. They have no Obsolete column.
`Requires` remains in `PublicAPI.Unshipped.txt`. Consumers see CS0618
from `[Obsolete]`, not from Unshipped.txt. Empty `Shipped.txt` is a
standing convention.

No consumer path reads Unshipped to decide whether the method is
obsolete. Dropping the attribute is invisible to RS0016; it is visible
to the compiler on a `NoWarn`-free subject. That is a property of the
analyzer, not a reachable shipping miss of this wave. The live lock-gap
for the attribute is `requires-obsolete-observable`.

### [diagnostic-fork-ctor-open] — REFUTED

The primary constructor is assembly-visible. Production does not use it.

C# extract: on duplicate trigger, report AGWF037 and `return false`; on
success, `DiagnosticForkModel.Create` (`DiagnosticForkExtractor.cs:174-189`).
JSON: reject first (`CollectImportRejections`); `MapDiagnosticForks` calls
`Create` only when rejections are empty (`WireToModelBridge.cs:459-492, :934`).
`Create` itself is the only `new DiagnosticForkModel` / `new PermittedForkTriggerModel`
in production (`DiagnosticForkModel.cs:134, :241`).

Invalid IR is not reachable from either authoring front. `#151` lowering
is out of wave. A future `new` or `with` is not a current path.

### [traversal-result-flags-independent] — REFUTED

The mixed `{ IsError: false, Error: "…" }` object is representable. It is
not constructed.

The only production constructors are `OntologyTraverseTool` success
(`:173-181`, neither flag) and `Error` (`:184-185`, both flags).
`MapTraversalResult` keys on `IsError` (`OntologyServerToolFactory.cs:378-381`).
It never sees a success object that still carries `"error"`. Public `init`
does not make the mixed state a current path. No other production
constructor exists.

### [agwf037-catalog-identity] — REFUTED

Something else already guarantees freshness.

`AgwfCatalog_HandEdit_FailsGuard` regenerates and requires
`regenerated == committed` (`AgwfCodegenGuardTests.cs:51-86`).
`contracts-codegen-guard.yml` path-filters `src/Strategos.Contracts/**`
and `docs/diagnostics/**` — this wave’s surface — and
`git diff --exit-code`s `Generated/`, `schemas/`, and `docs/diagnostics`
(`:11-16, :57-59`). A stale catalog or a mention-only `agwf.md` that
diverges from codegen fails that guard.

The `GroundTruthCodes` appends are identity lists on top of that
control. `Contains` in `AgwfMarkdownTests.cs:58-60` is a weaker local
check; it is not the freshness lock. Unwiring `ReportDiagnostic` is
`agwf037-reject-not-dedup`, not these lists. Whether the guard job is
required is unvalidated; the in-repo test already regenerates. Default:
the stale-file / mention hole is not a current miss.

### [claim-clr-free-xor-docs] — REFUTED

The types already make a fluent twin of `ObjectTypeFromDescriptor` /
`ApplyDelta` unrepresentable. Guide pages cannot fail a compile. A
missing sentence cannot create a representable invalid state. Something
else (the type system) already guarantees the polymorphic limit. The
pages are pedagogy, not a control on a reachable invalid path.

### [claim-issue-185-tracker] — REFUTED

No compile, pack, generate, or runtime path reads GitHub issue 185
comment 2. A stale “still open by design” list cannot ship a wrong saga,
nupkg, or protocol payload. “Close #185” auto-close is a GitHub process
risk, not a reachable defect in this revision’s code. Tracker state is
not a product invariant.

## Passes

- Production AGWF035 registration is a single `Report` call on the C#
  `[Workflow]` transform. JSON import does not call it. That composition
  was read, not assumed.
- `PhaseGraph.Build` and `EnumerateRejoinDispatchersOf` were compared
  construct-by-construct. They agree on well-formed C# IR. Under-reach
  fire fixtures all inject `WithoutSuccessor`.
- AGWF037 reject-and-gate is reached on C# extract and JSON import, and
  joins `hasErrors`. That is the working twin of the AGWF035 emit split.
- Hosting’s two `new CallToolResult` sites assign `ResultType`. Discover
  never assigns `Icons`. `Requires` still lowers to Preconditions.
  Those delivered paths match the code.
- `HandAuthoredContract = 2` has no in-repo producer. Runtime AONT205
  skip-unless-Ingested is reached. Analyzer AONT205 is not.

## Uncertainties

- Whether a rejoin last step can be absent from `model.StepNames` on some
  C# extract. If it can, `PhaseGraph.Add` no-ops and production
  under-reach would fire. No such fixture exists; extractors put construct
  steps in `StepNames`. Verdicts that depend on “production under-reach
  never fires” (`agwf035-catalog-polarity-lie`, `compat-agwf035-breaking`)
  inherit this residual.
- Whether MCP SDK 2.2.0 wrap sets `resultType` on the four domain-object
  tools. Unvalidated. `mcp-resulttype-and-pin` survived because those
  constructions are in-repo-unassigned, not because wrap-omits was
  exhibited.
- Whether `contracts-codegen-guard` or `contracts-schema-diff` is a
  required check. Unvalidated. `agwf037-catalog-identity` still has the
  in-repo regenerate test; `schema-diff-skip-succeeds` was killed on the
  fetch-depth-0 + existing-tags path, not on branch protection.
- Whether any out-of-repo TypeSpec/JSON ingest stamps
  `DescriptorSource.HandAuthoredContract`. Unvalidated. Defaulted to
  refuted for `handauthoredcontract-unreached`.
- Whether Renovate still resolves `lvlup-claude` after the `exarchos`
  rename. Unvalidated. Defaulted to refuted for
  `renovate-resolve-unasserted`.
- Whether AGWF035-without-gating is intentional house style. Does not
  change that Error+emit is reachable for over-reach.
