---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
implementation_fingerprint_command: git rev-parse 98fabb410e4432cc39fd71ec92651e6ceecfcc7c^{tree}
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, package, and documentation surface for issue 167
updated: 2026-09-06
skipped: test execution, mutation execution, and CI-result inspection; this lens statically inspected the proof mechanisms and their assertions
lens: existing-proof-inventory
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative typed-binding acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: refinement semantics consumed by issue 167
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: deferred compensation consumer
  - path: https://github.com/lvlup-sw/strategos/issues/204
    why: compilation-local catalog boundary follow-up
---

# Stage 1 survey — existing proof inventory

## Reading rule and result boundary

This is a **historical discovery record**. Its present-tense findings describe the pre-hardening
snapshot in which gaps were found, not the reconciled current subject. Current disposition: the
topology/merged-front-end suites, shared subject-validating harness, hardened package probe, complete
API baseline, recursive schema/release gates, hash provenance, AGWF039–AGWF043 authority, docs/CI
pins, raw-import/approval migrations, rooted-descriptor and emitter-boundary kills, and fork-event/
`NotFound` alignment are committed. They passed the complete exact-current `98fabb4` local
portfolio, including schema/docs/packages/consumer and hardened pack/Basileus gates. Protected CI
and review, the future Contracts tag, downstream adoption implementation, and #169 remain
outstanding.

This lens inspected what each existing check actually executes and asserts. Test names, XML comments,
checked-in generated files, and workflow comments were not accepted as proof on their own. No test or
build command was run by this lens, so this record does **not** report any check as passing. It reports
the proof mechanisms present in the fingerprinted working tree, their proof-ladder rung, and what a
future green result from each mechanism would and would not establish.

At discovery time, the target was uncommitted and the stronger working-tree fingerprint was used to
avoid losing untracked files. The frontmatter now names current immutable subject `98fabb4`;
historical line anchors and counts below must not be treated as its current proof result.

## Findings

### EP1 — Critical: semantic workflow refinement is proved only for a linear pair and a narrow fork-interference subset

The main integration suite drives the real `WorkflowIncrementalGenerator`, but its semantic contract
examples are concentrated in one two-step linear source fixture. It proves a legal linear sequence,
one illegal seam, one failed final guarantee, an entry failure hidden behind a post-binding requirement,
one legal and one excessive authority join, external/event write-write fork collisions, low-confidence
handler property write/write and write/read collisions, and a disjoint-handler positive
(`WorkflowBindingProofAnalyzerTests.cs:21-379` and
`OntologyActionCatalogFailClosedTests.cs:149-213`).

The topology-closure suite's 24 cases are refusal tests for dynamic or structurally unsupported syntax.
They do not prove the action-calculus obligations of any legal branch, loop, approval rejection/timeout,
failure handler, low-confidence handler, or nested topology. `ConfiguredForkJoin_ProvidesClosedActionOccurrenceToProof`
uses closed occurrence names but vacuous contracts, so it does not establish a fork's joint guarantee.
The two transition-lowering additions prove only successor-set construction for consecutive exhaustive
and non-exhaustive branches (`TransitionGraphLoweringTests.cs:344-418`), not implication across those
edges.

Missing proof classes include:

- legal and refuted branch paths, including all exits establishing the bound guarantee;
- loop zero-iteration/iteration/back-edge/exit obligations;
- approval success, rejection, escalation, and timeout ingress/exit contracts;
- failure and low-confidence ingress contracts;
- multiple terminal edges and an empty workflow;
- fork joint guarantees and legal disjoint paths;
- fork link/relation interference, cross-kind resource interference, main-path versus handler
  interference, and the opposite property read/write direction (the property vectors currently use
  only low-confidence handlers);
- subject mismatch, a standalone bound-frame expansion failure, and invalid leaf/bound contracts in
  workflow context;
- a successful acyclic nested workflow-bound action and a cycle longer than two actions.

The inherited #168 calculus tests are strong component evidence for implication and forgetting, but
they cannot establish that #167 extracts every occurrence and gives the solver the correct control-flow
edges. The workflow graph-to-formula orchestration therefore remains largely unproven.

### EP2 — Critical: no test proves a normal JSON-imported bound workflow, an imported illegal seam, or mixed-front-end uniqueness

The import additions exercise real `AdditionalText` parsing for malformed action tokens, omission, and
three unsupported nested-handler shapes (`ImportFrontEndRobustnessTests.cs:258-299` and
`ImportRejectionTests.cs:529-612`). The only bound imported example is a root failure handler expected
to produce AGWF042; it is not a normally provable workflow
(`ImportRejectionTests.cs:543-573`).

`ActionIdentity_MatchesJsonFieldForField` and
`ReusedStepType_DistinctNamedOccurrences_PreserveBothActions` exercise builder projection, JSON,
`WireWorkflowReader`, and `WireToModelBridge`, but then stop at the model
(`RoundTripIrFidelityTests.cs:133-186,233-297`). They do not feed that model through the compilation-wide
binding analyzer. There is no positive imported binding, imported AGWF041 seam, imported AGWF040
zero/ambiguous action lookup, or C#/JSON duplicate workflow-name AGWF039 case. Consequently the claimed
merged-front-end proof path has no production-path integration example.

### EP3 — High: authored fixtures now fail closed, but generated-output and unexpected-diagnostic false greens remain

`GeneratorTestHelper.RunGenerator` calls `RunGeneratorsAndUpdateCompilation` but discards both
`outputCompilation` and driver diagnostics, returning only `GetRunResult()`
(`GeneratorTestHelper.cs:69-92`). All three #167 proof suites now route their fixtures through
`RunGeneratorWithValidInput`, which separately rejects authored-source compiler errors before generation
(`GeneratorTestHelper.cs:37-50`, `WorkflowBindingProofAnalyzerTests.cs:429-456`,
`TopologyClosureProofTests.cs:228-231,462-482`, and
`OntologyActionCatalogFailClosedTests.cs:393-409`). This closes the original invalid-input false green.
The suites still filter assertions to AGWF039-AGWF042 and never inspect the updated compilation, so
uncompilable generated output or an unexpected non-AGWF generator diagnostic can coexist with a passing
assertion. A positive “binding diagnostics empty” case also does not by itself prove the binding analyzer
ran rather than silently skipping the workflow.

The one compilation check, `ValidLinearWorkflow_WithStringBinding_CompilesAndReportsNoBindingDiagnostic`,
explicitly filters out generated `.g.cs` trees and locationless errors
(`WorkflowBindingProofAnalyzerTests.cs:29-38`). It establishes authored source compatibility for the
legacy string overload, but not that generated output compiles. Import tests use `RunGenerators`, not
`RunGeneratorsAndUpdateCompilation`, and likewise assert generated-tree presence/absence without
compiling the trees (`ImportFrontEndRobustnessTests.cs:391-409` and
`ImportRejectionTests.cs:804-823`).

Direct parser tests have the same fixture-validity weakness: `ParserTestHelper` constructs a
`SemanticModel` without ever checking compilation diagnostics (`ParserTestHelper.cs:24-53,141-164`).
This affects the occurrence/extractor evidence even though those component assertions are otherwise
specific.

### EP4 — High: the packed consumer gate never exercises any #167 API or analyzer behavior

CI packs the solution, then runs `verify-generator-consumer-build.sh`
(`.github/workflows/ci.yml:81-109`). The probe installs the packed core and generator packages but its
only source derives from `IPhaseAwareSaga` (`scripts/verify-generator-consumer-build.sh:57-103`). It does
not reference `Performs`, `WorkflowActionReference`, `WorkflowBindingReference`, a bound ontology, or
AGWF039-AGWF042. In-repo tests reference projects directly
(`Strategos.Generators.Tests.csproj:34-42`), so they cannot detect a package layout/dependency/analyzer
load failure specific to the new source-linked proof implementation.

The existing behavioral host suite is included by the broad test-project CI pattern, but a source
search found no `.Performs(...)` call in `Strategos.Generators.Behavioral.Tests`; it exercises generated
saga runtime behavior without the new proof-bearing occurrence metadata. `StepConfigParityTests`
explicitly excludes `Performs` from its real-host proof requirement and delegates it to extractor and
round-trip tests (`StepConfigParityTests.cs:35-45,61-65,696-719`). Thus no rung-5 test exercises the
feature through a packed artifact or a running workflow.

### EP5 — Medium: the core public-API gate now covers the authored #167 surface, but `StepDefinition.Action` remains a shape-only exception

The Ontology project tracks its whole public API and the new typed binding/refinement members appear in
`Strategos.Ontology/PublicAPI.Unshipped.txt` (notably lines 65-66, 242, 261, 470-539, and 909-911).
That is a real compiler/analyzer structural guard.

Core Strategos deliberately disables PublicApiAnalyzer diagnostics globally. The current #167 snapshot
re-enables them for the historical seven entrypoints, the three changed continuation interfaces, and
`WorkflowActionReference` (`src/Strategos/.editorconfig:35-61`). `PublicAPI.Unshipped.txt` now declares
the newly added overloads and typed reference, while pre-existing continuation members have been
promoted into `PublicAPI.Shipped.txt`; together the ledgers cover the complete reviewed surface
(`src/Strategos/PublicAPI/PublicAPI.Unshipped.txt:1-15`). Updated
baseline tests reflect the ten interfaces, assert the historical seven remain a distinct subset, bind
both editorconfig allowlists to existing files, restrict top-level ledger declarations, and check the
typed reference's members (`BuilderApiBaselineTests.cs:44-118,193-363`). This closes the initial silent
continuation/value-object gap.

`StepDefinition.Action` is still outside the analyzer allowlist and public API ledger. A new reflection
test pins that it exists, has exactly `WorkflowActionReference` type, and remains public init-only
(`BuilderApiBaselineTests.cs:344-357`), which is useful rung-4 shape evidence but weaker than the exact
compiler baseline used for other public members. Public concrete approval continuation builders also
remain outside the deliberately scoped ledger. The mutation gate removes one **shipped**
`IBranchBuilder` line and proves that particular RS0016 path (`GateFailClosedTests.cs:51-100`); it does
not mutation-check new Unshipped members, the definition-file section, or the reflection-only property.
Finally, cross-repo drift still runs only after a push to main and diffs `PublicAPI.Shipped.txt`
(`public-api-drift.yml:36-42,59-84`), so these Unshipped additions cannot trigger the notification. A new
documentary test checks workflow comments for the seven-entrypoint versus wider local-allowlist markers;
it proves the prose contains substrings, not that Exarchos accepts the changed package
(`PublicApiBoundaryTests.cs:26-47`).

### EP6 — High: the schema/codegen regeneration guard is strong, but compatibility classification and new emitter behavior are incomplete

The PR-triggered codegen workflow is a genuine rung-1 control: it provisions Node/.NET, runs the full
TypeSpec-to-schema-to-C# script, and fails on diffs across generated records, schemas, ontology generated
contracts, and emitted diagnostic docs (`contracts-codegen-guard.yml:27-63`; generation pipeline at
`scripts/contracts-codegen.sh:19-44`). The unit guard additionally regenerates C# from committed schemas,
checks exact equality, and mutation-checks an `init`→`set` hand edit
(`CodegenGuardTests.cs:40-103`). The AGWF twin does the same for the catalog
(`AgwfCodegenGuardTests.cs:45-92`).

The schema-diff gate is much weaker. It selects the latest product `v*` tag rather than the independently
versioned `contracts-v*` line and becomes a successful skip when no matching schema directory exists
(`contracts-schema-diff.yml:37-62`). Its classifier checks file/property removal, requiredness, simple
`type`, and enum tokens only (`contracts-schema-diff.mjs:42-138`). It ignores `minLength`, `pattern`,
`$ref` target changes, discriminator/const changes, union arms, and array/item constraints. Those are
exactly material parts of the new `ActionReferenceV1` contract.

The original collateral analysis overstated the emitter scope. The change recognizes only the exact
direct-property nonwhitespace pattern `.*\S.*`; it does not project generic `minLength`, referenced-
scalar, or array-item constraints. At this historical lens's snapshot, `ActionReferenceV1` received
the callback while `WorkflowDefinitionV1.Name` was a minLength-only nonmatch. Current commit
`98fabb4` intentionally adds the exact pattern and generated callback to that workflow-name identity,
with read/write tests. `RecordEmitter_NonWhitespaceBoundary_IsExact` independently synthesizes
required and optional exact matches, an unrelated regex, and a scalar-alias nonmatch, then compiles
and runs the emitted consumer. That generic kill passed in the complete exact-current `98fabb4`
generator portfolio; protected execution and review remain pending.

### EP7 — Medium: graph-hash compatibility is pinned by a constant without base-revision provenance

`Version_WorkflowBindingReference_PreservesSerializedHash` constructs the new typed reference and
asserts the exact hash `17f5…4a00`; the adjacent test proves different workflow IDs change the hash
(`OntologyGraphVersionTests.cs:291-333`). This is useful deterministic/sensitivity evidence. The
expected constant does not exist in the base revision and has no provenance record showing it was
calculated from the base `BoundWorkflowName` representation. Therefore static inspection cannot
distinguish a legitimate before-value from a newly captured after-value. No test builds the same graph
against both old and new serializers. The acceptance claim “unchanged binding keeps its hash” is only
as strong as the manually supplied constant.

### EP8 — Medium: diagnostic representations are well synchronized, but emitted diagnostic precision is sparsely asserted

AGWF039-AGWF042 are generated from the TypeSpec catalog into constants, enum/schema/catalog, and docs.
Four Contracts tests extend exact expected lists/mappings, generator catalog parity reflects generated
metadata against live descriptors, and the single-source grep prevents production C# literals. The
catalog emitter now fails if the enum schema and entry-schema ID sequences differ, with a direct parity
test over committed output. The live descriptors are enabled-by-default Errors
(`WorkflowDiagnostics.cs:638-676`). This is strong rung-1/3 evidence for ID, title, severity, message
template, ordering, and documentation synchronization.

Behavior tests assert many IDs and selected message substrings, but only AGWF039 and two AGWF041 cases
directly assert Error severity (`WorkflowBindingProofAnalyzerTests.cs:175-291`). Only the authority
diagnostic is generated twice and compared by full `Diagnostic.ToString()`
(`WorkflowBindingProofAnalyzerTests.cs:128-138`). No #167 test asserts diagnostic source location/span,
all-diagnostic ordering, exact counterexample stability across repeat runs, or the source-vs-import
location contract. Filtering exclusively to AGWF039-AGWF042 also permits unrelated generator errors to
coexist unnoticed.

### EP9 — Medium: CI runs the relevant projects, but several policy and release paths are not evidence-bound to #167

The main CI pattern includes `src/*Tests/*.csproj` except Contracts and Benchmarks; Contracts tests run
in a separate Node-provisioned job (`ci.yml:10-79`). This should select all new component suites, but
the build/test implementation is an external reusable workflow referenced by movable `@v1`
(`ci.yml:10-16`), so its exact commands, filters, and policy revision are not fixed by this repository.
The 80% aggregate coverage gate cannot establish topology/diagnostic semantics.

Additional gaps:

- documentation builds only on push to main or manual dispatch, not pull requests
  (`docs.yml:3-10`);
- the benchmark PR path filter excludes the proof kernel, generator proof, and ontology refinement
  paths (`benchmark-regression.yml:10-18`);
- product-tag publishing skips Contracts tests (`publish.yml:43-60`), while `publish-contracts.yml`
  runs only the fixture-export subset before packing and never reruns TypeSpec, Contracts tests, or the
  codegen diff (`publish-contracts.yml:47-91`);
- the Contracts publish path can therefore publish stale committed generated outputs if the earlier PR
  gate was not bound to the tagged revision;
- Basileus smoke covers existing Agents APIs, not workflow/action binding (`ci.yml:111-147`), and there
  is no Exarchos or Basileus compile fixture for the new contract.

## Detailed proof inventory

### Rungs 1–3: generated, compiler, and structural controls

| Mechanism | What it actually establishes | Boundary / false-green risk |
|---|---|---|
| TypeSpec `ActionReferenceV1` plus optional `StepCommon.action` | One canonical schema source for the three names and optional step slot. | The hand-written generator DTO remains a second representation; conformance tests bind only its shape. |
| Contracts codegen workflow | Full regeneration followed by exact working-tree diff over generated outputs. | Strong PR control; release tag workflow does not repeat it. |
| Source-linking `LogicFormula`, `FiniteDomainSolver`, and Roslyn action parser into the netstandard generator (`Strategos.Generators.csproj:37-53`) | Compile-time workflow proof uses the exact same source files as the ontology analyzer for formula/solver/parser semantics. | Workflow topology/refinement orchestration is separate code and needs its own proof. |
| Ontology PublicApiAnalyzer | Exact full public surface for typed binding and refinement APIs. | Does not cover core Strategos omissions listed in EP5. |
| Core PublicApiAnalyzer + shell gate | Exact signatures across the historical seven entrypoints, three #167 continuations, and `WorkflowActionReference`; real build fails when a shipped tracked line is removed. | `StepDefinition.Action` is reflection-only; concrete builders are outside the reviewed allowlist; post-main drift notification reads only Shipped. |
| `ProjectionExhaustivenessTests.EveryStepMember_IsProjectedOrExplicitlyExcluded` | Reflectively forces each settable `StepDefinition` property, including `Action`, to be projected or allowlisted. | It proves routing presence, not field semantics or import/analyzer use. |
| `InvariantGuardTests` additions | `WorkflowActionReferenceModel` joins sealed/init-only generator IR lists; ontology binding reference is sealed. | Shape only. |
| AGWF generation/parity/grep | One generated ID/metadata authority and no hand-authored production literals. | Does not exercise emission sites or source locations. |

No relevant #167 test is marked skipped or explicit. The TypeSpec/package/codegen suites and API
mutation suites use `NotInParallel` keys because they mutate or regenerate shared files; this is
serialization, not suppression.

### Rung 4: action calculus and runtime refinement

The inherited `TypedActionCalculusTests` has 36 component/property-style tests. Inspection confirms
coverage of semantic forgetting through AND/OR/NOT; may-write versus created-link guarantees; untouched
frame realizability; relation-path forgetting; contradictory and opaque contracts; arbitrary Boolean,
integer, decimal, null, string/symbol “other” cells; domain mismatch; associativity; intervening writes;
identity; subject isolation; invalid shapes; unknown authority; vacuity; witness minimization/stability;
cancellation; no machine-word atom limit; and mixed-domain/Boolean exhaustive reference-oracle checks.
These are direct public/kernel tests, not comments or snapshots.

`ActionRefinementTests` has 17 tests and directly exercises the new runtime API:

- positive requirement contravariance and guarantee covariance;
- stronger implementation requirement and weaker guarantee counterexamples;
- authority and frame expansion;
- `ModifiesProperty` versus `CreatesLink` guarantee semantics;
- opaque status and refutation/invalid precedence;
- result-shape constructor invariants for undefined/inconsistent status/failures, undefined obligations,
  null failure entries, and null counterexample entries;
- a sequential composite implementation;
- unknown specification authority.

Remaining runtime-refinement API examples include a standalone subject failure, unknown implementation
authority, blank failure messages, cancellation at this API overload, and status precedence on
opaque/refuted composite inputs. The eight shared integer comparison vectors are compiled into both
runtime and ontology-analyzer test assemblies and compare legal/refuted Boolean outcomes
(`ActionCompositionProofVectors.cs:8-21`), but do not compare diagnostics, witnesses, nonnumeric domains,
or #167 workflow orchestration.

### Rung 4: typed workflow authoring and extraction

`WorkflowBindingReferenceTests` (5) assert exact whitespace-preserving valid value semantics, null/blank
rejection, equality, sealedness, and a get-only property. The generic and non-generic action-builder
suites each assert string and typed overload storage plus null/whitespace rejection; the interface mock
test asserts typed fluent chaining. `ActionDescriptorTests` continues to cover binding enum/default and
basic workflow/tool descriptor state.

`WorkflowActionReferenceTests` (8) execute real builders and assert:

- the three-name value is sealed/read-only and rejects null/blank components;
- linear, branch, loop, fork-path, failure, and low-confidence configured steps retain action plus
  other configuration;
- top-level and loop configured joins retain action/configuration;
- null configuration/reference and duplicate `Performs` fail.

Approval builder additions assert configured `Then` steps preserve configuration/action and reject a
null callback in both rejection and escalation builders. The completed-workflow approval test asserts
action/configuration preservation through rejection, timeout, nested escalation, and the root step
collection (`WorkflowBuilderAwaitApprovalTests.cs:310-352`).

`StepExtractorActionReferenceTests` (14 test methods) first reflects every public builder interface and requires the
exact 12 overloads that accept an `IStepConfiguration` callback to match an explicit extraction
inventory; it then directly exercises extraction for linear steps, `Performs`
after other calls, missing/factory/multiple/blank resolutions, builder escape/alias/conditional use,
branch/loop/fork/failure/low-confidence paths, configured top-level/loop joins, and invalid model names.
`ApprovalExtractorTests.Extract_WithConfiguredContinuationSteps_PreservesFullStepModels` and
`Extract_WithNestedContinuationLambdas_DoesNotHoistNestedConfiguration` cover approval continuation IR;
`InvocationChainWalkerTests.CollectInvocationsInLambda_MixedChains_PreservesDeclarationOrder` covers
statement/fluent source order. These use the non-validating parser harness described in EP3.

### Rung 4/limited integration: workflow binding generator tests

`WorkflowBindingProofAnalyzerTests` contains 26 test methods. Their inspected assertions cover:

- `ValidLinearWorkflow_WithStringBinding_CompilesAndReportsNoBindingDiagnostic`;
- matching, mismatched, and dynamic `Workflow<T>.Create` identity;
- an intervening delegate occurrence;
- legal/excess reordered-named authority declarations, with one repeated diagnostic stability check;
- configured fork-join occurrence closure;
- external-resource and generic-event fork write/write conflicts, low-confidence handler property
  write/write and write/read conflicts, and a disjoint-handler positive;
- zero workflow resolution (not multiple workflow resolution);
- missing, dynamic, zero-match, and two-match step action identities;
- illegal linear seam and failed final bound guarantee with counterexample substrings;
- opaque custom leaf and configured compensation deferral;
- main/confidence and confidence/confidence occurrence identity collisions;
- self-recursive and two-member recursive workflow bindings.

`OntologyActionCatalogFailClosedTests` contains 20 tests. It covers dynamic and factored binding,
unknown fluent helpers, reordered named object/relation arguments, split partial domain identity,
dynamic direct-descriptor guarantee, rejection of unrooted direct descriptors as occurrence matches,
positive inline owned descriptors, post-binding requirement/guarantee/frame calls, factored ontology and
object helpers, conditional action/authority declarations, dynamic/multi-return domain names, default
generic metadata name, and descriptor `with` binding. This suite is the only test of declaration-root
reachability, but its filtered-only harness still permits generated-output compiler errors and
unexpected diagnostics.

`TopologyClosureProofTests` contains 24 tests. Twenty-three expect AGWF042 for dynamic branch cases,
loops, method-group or partial fork paths, builder escape, conditional fork topology, top-level unknown
extensions, workflow/fork failure handlers, nonterminal failure handling, low-confidence handlers,
approval configuration/rejection/timeout handlers, duplicate approval handlers, or nested escalation.
`DecoyFinallyOutsideDefinition_DoesNotAffectBoundProof` is the sole positive. These are syntax-closure
tests, not semantic topology refinement tests.

### Rung 4: projection, wire, schema, and package checks

The relevant projection/import/schema tests and their exact exercised path are:

| Test(s) | Executed path and assertion |
|---|---|
| `ToContract_PerformsAction_ProjectsNameTripleWithoutLoss` | Runtime definition → Contracts DTO; asserts all three names. |
| `ToContract_LegacyStepWithoutAction_OmitsActionFromJson` | Legacy definition → JSON; asserts `Action == null` and token absence, not a byte-for-byte old JSON baseline. |
| `ActionIdentity_MatchesJsonFieldForField` | Builder → projection → JSON → dependency-free reader → model; exact three names. |
| `ReusedStepType_DistinctNamedOccurrences_PreserveBothActions` | Manually decorates two same-CLR/different-instance wire DTOs, bridges, and checks both occurrence identities. |
| `ActionIdentity_BlankWireName_IsDynamicOrInvalid` | Manually decorates a DTO with whitespace and checks bridge classification. |
| `PresentMalformedActionToken_FailsClosed_WithStableDiagnostic` | Real AdditionalText parser for null/scalar/array/incomplete/blank; AGWF023 detail and no saga tree. |
| `OmittedAction_RemainsImportable` | Real AdditionalText parser; no AGWF023 and a saga tree exists. |
| `ActionReferenceTwin_PinsRequiredNonBlankIdentityContract` | Canonical schema exact property/required/minLength/pattern plus hand DTO string types. |
| `EveryTwin_HasMatchingSchemaFile`, `ObjectTwins_MatchSchemaPropertiesAndTypes_InEitherDirection`, `StepUnion_ArmsMatchTwinSubclasses`, critical-twin list | Reflection-based hand DTO ↔ checked-in schema names, coarse types, and union arms. Generic check does not compare requiredness/constraints; the dedicated action-reference test does. |
| `StepDefinition_ActionReference_IsOptionalAndNonEmptyBySchema` | Runs real TypeSpec compile, then checks three required constrained fields and optional `$ref` on all five step arms. |
| `StepDefinition_ActionReference_RoundTripsGeneratedModel` | Checked-in generated union JSON round trip. |
| missing/empty/whitespace generated-contract tests | JsonRequired plus read/write validation; whitespace is parameterized across all three names, while empty string is exercised only for `domainName`. `WorkflowIrRoot_RejectsWhitespaceOnlyName` separately pins both directions for the workflow identity. |
| `Package_Version_Is_0_11_0_WithWorkflowActionReferencesAndExistingContent` | Real Contracts pack; asserts filename/nuspec 0.11.0 and required schema/content entries including `ActionReferenceV1`. |

The root failure/fork failure/low-confidence approval import tests listed in EP2 additionally protect
known lossy import shapes, but do not establish the normal binding proof path. There is no action-bearing
case in the exported >=100 builder fixture corpus, so the schema/corpus equivalence gate does not cover
the new slot.

### Baselines and compatibility sentinels

- `OntologyGraphVersionTests.Version_WorkflowBindingReference_PreservesSerializedHash` is the only new
  exact graph-hash baseline; EP7 describes its provenance limitation.
- `LinearWorkflowSaga.baseline.txt` and its provenance file remain active. The regression suite compares
  a legacy non-action-bearing generated saga to the pre-off-main-flow golden and includes sensitivity
  mutations. It can catch accidental changes to ordinary linear saga output, but contains no
  `Performs`, binding, or AGWF behavior.
- Generated C#, individual JSON schemas, bundled workflow schema, AGWF catalog/enum/constants, and
  generated Markdown are checked-in baselines bound by the regeneration guard.
- `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` are compiler baselines with the scope limitation
  in EP5.
- There is no new generated-source snapshot for a bound workflow, no before/after JSON golden for the
  optional action member, and no diagnostic baseline including locations/order.

### CI and release gate inventory

| Gate | Trigger | Relevant evidence | Limitation |
|---|---|---|---|
| `ci.yml / build-test` | PR + main | Builds/tests broad non-Contracts test-project pattern; includes generator, ontology, core, and behavioral suites. | External movable reusable `@v1`; exact execution policy not local. |
| `ci.yml / contracts-test` | PR + main | Restores TypeSpec toolchain and runs all Contracts tests with Node 24/.NET 10. | Project tests, not tagged release artifact. |
| `ci.yml / pack-verify` | PR + main | Packs whole solution; installs core+generator packages into isolated consumer cache. | Probe is unrelated to #167 API/analyzer. |
| `ci.yml / coverage-gate` | PR | Aggregate 80% production coverage. | Coverage is not semantic adequacy; external `@v1`. |
| `ci.yml / builder-api-stability` | PR + main | Real strict build with PublicApiAnalyzer scoped to the historical seven interfaces, three changed continuation interfaces, and `WorkflowActionReference`. | `StepDefinition.Action` remains reflection-only; public concrete builders remain outside the allowlist. |
| Contracts codegen guard | relevant PR/main paths | Full regeneration + exact diff. | Not repeated at contracts tag. |
| Contracts schema diff | schema PR paths | Coarse structural diff versus selected tag. | Wrong release namespace and incomplete vocabulary; can skip. |
| Docs deploy/build | main push/manual | Astro build. | No PR build. |
| Benchmark regression | narrow subsystem PR paths | Targeted BenchmarkDotNet jobs. | #167 paths never trigger it. |
| Product `v*` publish | tag | Builds/tests non-Contracts, then packs. | Skips Contracts tests; comment incorrectly says contracts tag reruns them. |
| Contracts `contracts-v*` publish | tag | Version equality, fixture corpus count, pack/publish. | Does not run Contracts tests, TypeSpec, or regeneration diff. |
| Public API drift | main push | Diffs shipped seven-interface ledger and can open Exarchos issue. | Does not see Unshipped #167 additions; fail-soft on token. |
| Basileus smoke | PR + main | Packed Agents consumer surface. | No #167 surface. |
| CodeQL/dependency review/secret/quality grep gates | repository workflows | Generic security/dependency/hygiene evidence. | No action-composition semantic assertion. |

## Candidate correctness obligations for Stage 2

These are seeds, not final obligations or verdicts.

1. **Fixture compiler closure.** Every #167 generator fixture must start from compiler-clean authored
   source; the complete updated compilation must be free of unexpected errors for positive cases, and
   every negative case must distinguish the intended AGWF error from unrelated compiler/generator
   failures. Generated output expected to exist must compile. Cheapest sound rung: 4, by a shared test
   harness returning run result plus output compilation; rung 2 alone cannot validate synthetic fixture
   intent.
2. **Merged-front-end parity.** A legal and an illegal linear binding must produce equivalent proof
   outcomes through C# and JSON. A mixed C#/JSON duplicate workflow identity must deterministically emit
   AGWF039. Cheapest sound rung: 5 because the obligation crosses TypeSpec JSON, AdditionalText parsing,
   catalog merge, and the generator.
3. **Topology refinement matrix.** For branch, loop, fork, approval, failure, and low-confidence graphs,
   exercise at least one legal contract and each topology-specific refutation at entry/seam/exit/ingress.
   Compare the proof graph with the emitted transition graph. Cheapest sound rung: 4/5; the compiler
   cannot infer semantic implication from the fluent topology.
4. **Fork noninterference completeness.** Prove legal disjoint paths, joint guarantees, and write/write
   plus write/read conflicts for property, link/relation, event, and external resources, including stable
   path attribution. Cheapest sound rung: 4 with parameterized/differential graph fixtures.
5. **Binding diagnostic partition.** Zero and multiple workflow resolution map only to AGWF039;
   occurrence identity failures only to AGWF040; closed counterexamples only to AGWF041; opacity,
   invalidity, dynamic syntax, and deferred rollback only to AGWF042. Pin severity, exact stable message,
   source/import location, ordering, and cancellation behavior. Cheapest sound rung: 4 backed by the
   generated diagnostic authority.
6. **Catalog reachability and assembly boundary.** A proof leaf must correspond to an executable
   registered ontology action, and supported source shapes must be complete. Explicitly settle whether
   referenced-assembly bound actions are supported; if yes, add a portable manifest/consumer proof, and
   if no, enforce/document compilation-local scope. Cheapest sound rung: 3 for reachability plus rung 5
   for package/metadata boundaries.
7. **Packed #167 consumer.** Install only produced nupkgs into an isolated consumer, compile the typed
   and legacy binding APIs plus `.Performs`, prove a legal source, and make an illegal seam fail with
   AGWF041. Bind the result to nupkg digests. Cheapest sound rung: 5; project-reference tests cannot
   establish package contents or analyzer load.
8. **Complete public API authority.** Decide whether `StepDefinition.Action` and changed public concrete
   builders belong in the exact PublicApiAnalyzer ledger rather than a reflection-only shape check.
   Mutation-check an added/removed Unshipped overload, the definition-file analyzer section, and any
   deliberate shape-only exception. Cheapest sound rung: 2/3.
9. **Wire compatibility.** Pin old action-omitting JSON byte/semantic compatibility, all five action-bearing
   step arms, required/nonblank semantics, hand DTO parity, and package contents. Extend schema diff to
   constraints, refs, discriminators, and unions and baseline against `contracts-v*`. Cheapest sound
   rung: 1 for derivation, 3/4 for compatibility classification.
10. **Emitter collateral behavior.** For each newly recognized constraint form (direct minLength,
    referenced minLength, array-item minLength, non-whitespace pattern; required and optional), mutation-
    check read and write validation and exercise representative pre-existing generated contracts.
    Cheapest sound rung: 4; regeneration proves sameness, not behavioral compatibility.
11. **Graph-hash before/after equivalence.** Bind the same ontology graph under the base string serializer
    and new typed serializer and compare exact bytes/hash; retain changed-ID sensitivity. Store provenance
    with the golden. Cheapest sound rung: 4 because both implementations cannot coexist as one type-safe
    compile-time representation.
12. **Runtime refinement result totality.** Complete subject, authority-both-sides, frame, cancellation,
    composite, and malformed-public-constructor cases; retain shared vectors across analyzer/runtime for
    every scalar domain and witness, not only eight integer Boolean outcomes. Cheapest sound rung: 2 for
    invalid result shapes where possible, otherwise rung 4.
13. **Release evidence binding.** Pin reusable workflow policy by immutable SHA, run docs on PR, and make
    the Contracts tag rerun TypeSpec/tests/regeneration before publication. Bind pack/consumer results to
    revision and artifact digest. Cheapest sound rung: 3/5.
14. **Proof scalability and Roslyn cancellation.** Add non-wall-clock benchmark coverage for realistic
    workflow graphs and direct cancellation tests through the workflow analyzer, not only the finite
    kernel. Cheapest sound rung: 4; performance trend belongs in the benchmark suite rather than a flaky
    duration assertion.

## What else I read

- `verification/issue-167/stage0.md` and every other current Stage 1 survey artifact for navigation only;
  conclusions were rechecked against the current files.
- All new files under `Strategos.Generators.Tests/Proof`, the complete helpers they call, all new
  occurrence/extraction/import/projection/refinement/schema tests, and all changed assertions in existing
  suites.
- The #168 calculus and shared proof-vector suites because they are inherited proof for #167's semantic
  kernel.
- All relevant project files, public API ledgers/configuration, generated-contract inputs/outputs,
  packaging scripts, schema classifier, codegen scripts, and GitHub workflows.
- Existing generator baselines, fixture-export corpus source, and behavioral workflow source. A source
  search, excluding `obj`, found no action-bearing behavioral workflow or fixture-export case.

## Assumptions and unsettled questions

- A test method was counted as active when it has `[Test]` and no skip/explicit metadata in the inspected
  source. Actual discovery and execution remain Stage 3 evidence, not a fact asserted here.
- The external reusable workflow may currently execute the intended commands, but its movable tag and
  unavailable body prevent this repository snapshot from proving that policy.
- The graph-hash constant may in fact have been captured from the base implementation. No repository
  provenance proves that origin, so this lens records it as useful but not independently bound evidence.
- Compilation-local ontology discovery may be an intentional v1 boundary. Current acceptance/docs must
  either state that boundary or a package/metadata proof mechanism must close it; the existing checks do
  neither.
