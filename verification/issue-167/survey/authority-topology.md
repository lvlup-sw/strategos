---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, and documentation surface against merge-base 45c86a6
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: immediate downstream consumer of the workflow/action contract added here
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: merged typed action-calculus contract that this change consumes
---

# Survey lens 3 — authority topology (issue #167)

This is a **historical discovery record** of the pre-hardening working tree. Present-tense findings
below preserve the snapshot in which they were found and do not override the current ledger. AT-1,
AT-3, and AT-4 were closed by the complete API baseline, generated AGWF039–AGWF043 equality, and shared
runtime/generator vectors. AT-2 was closed at current commit `98fabb4` by giving
`WorkflowDefinitionV1.name` the exact nonwhitespace pattern, regenerating both schema forms and C#,
and adding read/write semantic tests. AT-5 remains an intentionally multi-representation boundary
with executable guards. Affected product obligations remain `Indeterminate` pending protected
execution and review; downstream adoption and #169 remain `Unproven`.

The lens counts representations even when
they agree today. Generated artifacts count as representations but not as independent
authorities when regeneration and a drift gate mechanically bind them to their source.

## Counting rule

A representation unit below is one independently mutable declaration/model, generated artifact,
executable validation site, fixture/test file, public-API baseline, or hand-maintained prose file.
A three-field tuple in one model counts once; each separately shipped union arm or schema file
counts once. A generated file counts once even when it repeats a token in an enum member and its
converter. Repeated uses inside one fixture file do not inflate its count. Binder/adaptor code is
listed separately unless it also re-declares validation semantics. Consequently, category totals
are surface-unit totals rather than unique-file totals: one file can contain both a static type and
an executable validator, which are explicitly different representations under this lens.

There is no product UI in the #167 scope, so the UI-text count is **0 for every boundary**.

## Findings

### AT-1 — the established public-API gate covers only two of five newly added interface methods

**High — the advertised cross-product surface is not mechanically represented.** The diff adds
exactly five public builder-interface methods:

1. `IStepConfiguration<TState>.Performs(...)` at
   `src/Strategos/Abstractions/IStepConfiguration.cs:42-55`;
2. `IForkJoinBuilder<TState>.Join(...configure)` at
   `src/Strategos/Abstractions/IForkJoinBuilder.cs:46-61`;
3. `ILoopForkJoinBuilder<TState>.Join(...configure)` at
   `src/Strategos/Abstractions/ILoopForkJoinBuilder.cs:52-68`;
4. `IApprovalRejectionBuilder<TState>.Then(...configure)` at
   `src/Strategos/Abstractions/IApprovalRejectionBuilder.cs:55-66`; and
5. `IApprovalEscalationBuilder<TState>.Then(...configure)` at
   `src/Strategos/Abstractions/IApprovalEscalationBuilder.cs:61-72`.

Only the first two are present in the core baseline
(`src/Strategos/PublicAPI/PublicAPI.Unshipped.txt:2-3`). The other three declaring interfaces are
deliberately outside the seven-file analyzer allowlist
(`src/Strategos/.editorconfig:35-46`), and the reflection backstop deliberately repeats that same
exclusion (`src/Strategos.Tests/PublicApi/BuilderApiBaselineTests.cs:41-80,165-190`). The gate's
adherence for this diff is therefore **2/5 = 40%**, with **3/5 = 60%** of new interface methods
unrepresented and unenforced.

This is a demonstrated decay of an existing pattern, not merely an absent ideal. The two changes
made in already tracked interfaces adopted the baseline; the three changes made in continuation
interfaces bypass it. The release note nevertheless says all five changes affect external
implementations and the Exarchos mirror and that “The new signatures are staged in
`PublicAPI.Unshipped.txt`” (`CHANGELOG.md:16-25`).

The new `WorkflowActionReference` is also outside the assembly-wide-off/seven-files-on gate. It
contains **1 public type plus 5 explicit public members** (constructor, three getters, and
`Deconstruct`) at `src/Strategos/Definitions/WorkflowActionReference.cs:17-55`, with **0/6** of
those explicit declarations represented in a PublicAPI baseline. Compiler-synthesized record
members make the ungated emitted surface larger, but are not included in that explicit count.

By contrast, the ontology project demonstrates the intended mechanism: its whole public assembly
is guarded (`src/Strategos.Ontology/Strategos.Ontology.csproj:29-47`), and this diff records **33
added and 2 removed** API lines, including typed workflow binding and refinement, in
`src/Strategos.Ontology/PublicAPI.Unshipped.txt:65-66,242,261,470-471,517-539,623,909-911`.

### AT-2 — the canonical wire workflow identifier accepts whitespace that every executable path rejects

**Medium — published schema validity and Strategos validity disagree at the lookup key.** The
TypeSpec root gives `WorkflowDefinitionV1.name` only `@minLength(1)`
(`src/Strategos.Contracts/Workflow/WorkflowDefinitionV1.tsp:24-30`). The emitted standalone schema
therefore accepts a whitespace-only identity
(`src/Strategos.Contracts/schemas/json-schema/WorkflowDefinitionV1.json:11-15`), and generated C#
enforces only minimum length
(`src/Strategos.Contracts/Generated/WorkflowDefinitionV1.g.cs:140`). The bundled schema carries the
same weaker rule in both its root and retained definition
(`src/Strategos.Contracts/schemas/workflow-definition-v1.schema.json:11-15,1024-1028`).

Six hand/executable authorities reject the same value as invalid:

- the new binding reference (`src/Strategos.Ontology/Descriptors/WorkflowBindingReference.cs:7-14`);
- `Workflow<T>.Create` (`src/Strategos/Builders/Workflow.cs:35-40`);
- `WorkflowDefinition<T>.Create` (`src/Strategos/Definitions/WorkflowDefinition.cs:94-102`);
- `[Workflow]` extraction (`src/Strategos.Generators/WorkflowIncrementalGenerator.cs:343-350,381-392`);
- imported workflow lowering (`src/Strategos.Generators/Import/WireToModelBridge.cs:110-127`); and
- the closed C# identity comparison
  (`src/Strategos.Generators/Helpers/TopologyClosureInspector.cs:159-187`).

Thus a language-neutral consumer can validate and emit `"name": "   "`, and the generated
Contracts record can deserialize it, but Strategos runtime creation throws and the import path
reports the empty-name diagnostic. The correct TypeSpec idiom already exists immediately in this
diff: all **3/3** `ActionReferenceV1` identity fields use both `@minLength(1)` and
`@pattern(".*\\S.*")` (`src/Strategos.Contracts/Workflow/ActionReference.tsp:12-27`). Across the
four workflow/action identity strings, schema-level nonblank adherence is therefore **3/4 = 75%**;
the older workflow-name copy is the lone unbound representation.

**Current disposition:** the paragraph above is the historical reproduction. At `98fabb4`, all four
identity strings carry the exact nonwhitespace constraint, generated `WorkflowDefinitionV1` enforces
it on read and write, Contracts tests pass 185/185, and codegen is stable. The mismatch is no longer a
current finding; protected execution remains pending.

### AT-3 — AGWF039–AGWF042 have two canonical TypeSpec authorities with no equality gate

**High — the published closed enum can diverge from the generated analyzer/catalog vocabulary.**
The same four IDs are authored in **5 independent TypeSpec sites**: once in the `AgwfCode` enum
(`src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:57-60`) and once in each of four entry-model
`id` literals (`:383-421`). `AgwfCatalogEmitter` says it owns both families, but its executable path
enumerates only `AgwfEntry*.json` (`src/Strategos.Contracts.Codegen/AgwfCatalogEmitter.cs:13-30,40-41,62-87`)
and emits the C# enum, constants, JSON catalog, and Markdown from those entries (`:89-112`). The
general emitter excludes both the enum and entry schemas
(`src/Strategos.Contracts.Codegen/RecordEmitter.cs:63-70`). No test compares the emitted
`AgwfCode.json` member set with the entry-schema ID set.

The four-code set has **30 counted representations**:

- **5 authored TypeSpec sites**: one enum map plus four entry literals;
- **9 generated artifacts**: `AgwfCode.json`, four `AgwfEntry*.json` files,
  `AgwfCode.g.cs`, `AgwfCodes.g.cs`, `agwf-catalog.json`, and generated
  `docs/diagnostics/agwf.md`;
- **1 hand descriptor file** containing four live Roslyn descriptors
  (`src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:638-676`);
- **8 test files containing hand-authored code literals**: the four Contracts diagnostic tests at
  `src/Strategos.Contracts.Tests/Diagnostics/{AgwfCatalogEmitterTests.cs:23-33,AgwfCatalogSchemaTests.cs:26-37,AgwfCodeEnumTests.cs:56-60,AgwfMarkdownTests.cs:18-28}` and the four workflow/import proof tests at
  `src/Strategos.Generators.Tests/{Import/ImportRejectionTests.cs:38,Proof/OntologyActionCatalogFailClosedTests.cs:24-47,Proof/TopologyClosureProofTests.cs:229,Proof/WorkflowBindingProofAnalyzerTests.cs:68-203}`; and
- **7 hand documentation/release files**: `CHANGELOG.md:34-41,64-68`,
  `src/Strategos.Contracts/CHANGELOG.md:19-28,137-140`,
  `src/Strategos.Contracts/README.md:178-200`,
  `docs/src/content/docs/guide/ontology/migration-v2-13.md:23-26,346-363,407-408`,
  `docs/src/content/docs/reference/action-calculus.md:514-518`,
  `docs/src/content/docs/reference/api/workflow.md:160-183`, and
  `docs/src/content/docs/reference/diagnostics/agwf-agsr.md:23-47`.

The nine generated artifacts are internally bound to one of the two TypeSpec branches by
`scripts/contracts-codegen.sh:1-46` and the regeneration/diff job
(`.github/workflows/contracts-codegen-guard.yml:27-63`). The live descriptors bind IDs to generated
constants (`WorkflowDiagnostics.cs:640,650,660,670`), while
`src/Strategos.Generators.Tests/Diagnostics/AgwfCatalogParityTests.cs:14-77` binds their
hand-authored severity/title/message to the catalog.
Those are healthy local mechanisms. They do not bind the two TypeSpec roots.

A concrete drift survives those mechanisms: adding an entry model but omitting the `AgwfCode`
enum member makes the emitter produce the new C# enum, constant, catalog, analyzer descriptor, and
generated docs while the published `AgwfCode.json` continues to reject that token. The independent
ground-truth arrays can all be updated to agree with the entry branch without ever inspecting the
enum schema. All **4/4** new codes use this split pattern, so single-authority adherence is **0/4**.

### AT-4 — runtime and build-time refinement have two semantic decision authorities

**High structural risk — no present disagreement established, but parity is not enforced.** The
dependency-free Boolean/numeric proof kernel is correctly single-sourced: `LogicFormula.cs` and
`FiniteDomainSolver.cs` are compiled in the runtime project and source-linked into both analyzer
projects (`src/Strategos.Ontology.Generators/Strategos.Ontology.Generators.csproj:30-38` and
`src/Strategos.Generators/Strategos.Generators.csproj:37-53`). The Roslyn action-predicate grammar is
also one source linked into the workflow generator (`Strategos.Generators.csproj:45-48`). These are
**2 solver source files deployed as 6 compiled copies** and **2 parser source files deployed in 2
analyzer assemblies**, each with one source authority.

The contract semantics above that shared kernel are independently implemented:

- runtime derives requirement, declared/effective guarantee, satisfiability, semantic forgetting,
  and frame realizability in `ActionContractProofEngine`
  (`src/Strategos.Ontology/ActionLogic/ActionContractProof.cs:71-110` and the remainder of that
  method), then performs requirement/guarantee/frame/authority refinement in
  `ActionCalculus` (`src/Strategos.Ontology/Descriptors/ActionCalculus.cs:413-600`) against
  `AuthorityLattice.IsAtMost` (`src/Strategos.Ontology/Descriptors/AuthorityLattice.cs:93-116`);
- build-time proof independently derives the same contract facts in `TryProveContract`
  (`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:417-542`), checks frame and
  authority in `ProveFrame`/`ProveAuthority` (`:894-980`), and uses an independently parsed and
  ranked `OntologyAuthorityLattice`
  (`src/Strategos.Generators/Proof/OntologyActionCatalog.cs:690-799,1040-1129`).

There are therefore **2 decision authorities for each of 3 overlapping obligation families**:
contract validity/effective guarantee, frame containment, and authority ordering. **0/3** of those
orchestration-level obligations is source-shared, despite **3/3** ultimately calling the shared
finite solver or ordinal collections where applicable. Runtime refinement has **14 tests** in
`src/Strategos.Ontology.Tests/Descriptors/ActionRefinementTests.cs`; workflow refinement has **23
separate tests** in
`src/Strategos.Generators.Tests/Proof/WorkflowBindingProofAnalyzerTests.cs`. No shared refinement
vector corpus feeds both. The existing shared vectors
(`src/Strategos.Ontology.Tests/Shared/ActionCompositionProofVectors.cs:3-29`) cover sequential seam
logic, with runtime/analyzer runners, not the new refinement glue.

Because the two decisions are externally observable as `ActionRefinementAnalysis` versus
AGWF041/AGWF042, a future change to opaque/invalid precedence, declared versus effective
guarantees, empty authority, or frame projection can produce build/runtime disagreement while all
current project-local tests remain green. A neutral source-shared refinement kernel, or at minimum
one shared vector corpus interpreted by both paths, is required to make either implementation the
authority.

### AT-5 — the action-occurrence identity invariant is independently reimplemented six times

**Medium structural risk — copies agree in the current diff, but no complete parity mechanism
binds them.** The intended invariant is an ordinal tuple of exactly three nonblank strings. It has
**6 authority roots**:

1. TypeSpec constraints (`src/Strategos.Contracts/Workflow/ActionReference.tsp:12-27`);
2. the public runtime constructor (`src/Strategos/Definitions/WorkflowActionReference.cs:17-44`);
3. the isolated generator model constructor
   (`src/Strategos.Generators/Models/WorkflowActionReferenceModel.cs:20-55`);
4. the C# source extractor (`src/Strategos.Generators/Helpers/StepExtractor.cs:1688-1745`);
5. the dependency-free JSON reader
   (`src/Strategos.Generators/Import/MinimalJsonReader.cs:667-705`); and
6. the import bridge's second validation
   (`src/Strategos.Generators/Import/WireToModelBridge.cs:730-747`).

The generated C# validation at
`src/Strategos.Contracts/Generated/ActionReferenceV1.g.cs:42-59` is not a seventh authority because
it is mechanically emitted from TypeSpec. `WireDtoSchemaConformanceTests.cs:181-217` binds the
schema's three property names/types/constraints to the hand DTO, and
`ProjectionTests.cs:26-48` checks value-preserving runtime-to-wire projection. Neither test binds
the public runtime type, generator model, proof `ActionIdentity`, and both executable parsers to
one common set of accepted/rejected vectors. The copies agree now; this finding is the unbound
topology that permits a future fourth field, trimming rule, or normalization policy to drift.

### AT-6 — one removed property name remains as an orphan analyzer vocabulary

**Low.** `ActionDescriptor.BoundWorkflowName` is removed from the runtime/public baseline and
replaced by `BoundWorkflow` (`src/Strategos.Ontology/PublicAPI.Unshipped.txt:470-471`), but the shared
Roslyn bridge still recognizes an initializer named `BoundWorkflowName` as a fallback
(`src/Strategos.Ontology.Generators/Analyzers/ActionCompositionAnalyzer.WorkflowBridge.cs:581-582`).
There is **1 orphan syntax alias**, **0 current public members** that can author it, and **0 stated
compatibility bridge** in the migration contract, which instead instructs callers to replace the
initializer (`docs/src/content/docs/guide/ontology/migration-v2-13.md:451-452`). A current-source
consumer still gets a compiler error, so this does not silently make invalid source build; it is an
unbound semantic copy whose intended old-reference scenario is not documented or tested.

## Representation ledger and binding mechanisms

### Workflow identity and action-to-workflow binding

The boundary has **17 static/model/artifact representations**, **8 validation representations**,
**11 changed/new focused test files**, **5 hand documentation files**, **1 public-API baseline**, and
**0 UI representations**. The category total is **42 surface units**.

The 17 static/model/artifact units are:

- runtime binding: `WorkflowBindingReference.WorkflowId` and
  `ActionDescriptor.BoundWorkflow`
  (`src/Strategos.Ontology/Descriptors/WorkflowBindingReference.cs:4-14` and
  `src/Strategos.Ontology/Descriptors/ActionDescriptor.cs:39-45`);
- two public fluent declarations:
  `IActionBuilder.BoundToWorkflow(WorkflowBindingReference)` and its generic counterpart, recorded
  at `src/Strategos.Ontology/Builder/IActionBuilder.cs:15` and
  `src/Strategos.Ontology/Builder/IActionBuilderOfT.cs:18`;
- runtime workflow declaration and result: `Workflow<T>.Create(name)` and
  `WorkflowDefinition<T>.Name` (`src/Strategos/Builders/Workflow.cs:28-40` and
  `src/Strategos/Definitions/WorkflowDefinition.cs:26-32`);
- the `[Workflow]` authoring identity read by the generator
  (`src/Strategos.Generators/WorkflowIncrementalGenerator.cs:343-350`);
- TypeSpec root name, standalone schema name, two bundled-schema name sites, generated C# root name,
  and the hand analyzer DTO name
  (`src/Strategos.Contracts/Workflow/WorkflowDefinitionV1.tsp:24-30`,
  `src/Strategos.Contracts/schemas/json-schema/WorkflowDefinitionV1.json:11-15`,
  `src/Strategos.Contracts/schemas/workflow-definition-v1.schema.json:11-15,1024-1028`,
  `src/Strategos.Contracts/Generated/WorkflowDefinitionV1.g.cs:36-41`, and
  `src/Strategos.Generators/Import/WireDtos.cs:48-55`);
- `WorkflowModel.WorkflowName`
  (`src/Strategos.Generators/Models/WorkflowModel.cs:21-45`);
- the parser and catalog copies `WorkflowActionContractSyntax.BoundWorkflowName` and
  `OntologyActionContract.BoundWorkflowName`
  (`src/Strategos.Ontology.Generators/Analyzers/ActionCompositionAnalyzer.WorkflowBridge.cs:1276-1325`
  and `src/Strategos.Generators/Proof/OntologyActionCatalog.cs:896-949`); and
- the canonical graph byte slot
  (`src/Strategos.Ontology/Internal/OntologyGraphHasher.cs:238-258`).

The eight validators are TypeSpec minimum length, generated C# minimum length, the two runtime
workflow factories, the binding-reference constructor, attribute extraction, imported lowering,
and the constant/equality topology check. AT-2 identifies the only semantic disagreement.

The 11 focused changed/new test files are
`ImportRejectionTests.cs`, `OntologyActionCatalogFailClosedTests.cs`,
`TopologyClosureProofTests.cs`, `WorkflowBindingProofAnalyzerTests.cs`,
`ActionBuilderOfTTests.cs`, `ActionBuilderTests.cs`, `IActionBuilderTests.cs`,
`ActionDescriptorTests.cs`, `WorkflowBindingReferenceTests.cs`, ontology `InvariantGuardTests.cs`,
and `OntologyGraphVersionTests.cs`; representative anchors are
`src/Strategos.Generators.Tests/Proof/WorkflowBindingProofAnalyzerTests.cs:38-203` and
`src/Strategos.Ontology.Tests/Descriptors/WorkflowBindingReferenceTests.cs:5-49`.
The five hand docs are the root changelog, migration guide, action-calculus reference, AGWF/AGSR
reference, and graph-versioning reference.

Outside AT-2, the binding mechanisms are soundly directed:

- both string fluent overloads immediately construct the typed reference
  (`src/Strategos.Ontology/Builder/ActionBuilder.cs:40-48` and
  `ActionBuilderOfT.cs:68-76`);
- the topology inspector requires a constant `Workflow.Create` name equal under ordinal comparison
  to `[Workflow]` (`TopologyClosureInspector.cs:159-187`);
- the proof groups and resolves `WorkflowModel.WorkflowName` under `StringComparer.Ordinal`
  (`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:23-30,40-77`); and
- the hasher reads `BoundWorkflow.WorkflowId` directly without a second formatter
  (`OntologyGraphHasher.cs:248-253`).

### Action occurrence identity

This boundary has **29 static/model/artifact representations**, **6 executable validators**, **16
changed/new focused test files**, **7 hand documentation files**, **1 partial public-API baseline**,
**0 hash representations**, and **0 UI representations**: **59 surface units** total.

The 29 static/model/artifact units break down as follows:

- **3 runtime authoring units**: `WorkflowActionReference`, `StepDefinition.Action`, and
  `IStepConfiguration.Performs` (`src/Strategos/Definitions/WorkflowActionReference.cs:17-55`,
  `src/Strategos/Definitions/StepDefinition.cs:74-81`, and
  `src/Strategos/Abstractions/IStepConfiguration.cs:42-55`);
- **2 TypeSpec sites**: the identity model and the one shared optional step slot
  (`src/Strategos.Contracts/Workflow/ActionReference.tsp:12-27` and
  `src/Strategos.Contracts/Workflow/StepDefinition.tsp:22-48`);
- **12 checked-in JSON Schema sites**: one standalone identity schema, five standalone arm `$ref`
  sites, one bundled identity definition, and five bundled arm `$ref` sites
  (`src/Strategos.Contracts/schemas/json-schema/ActionReferenceV1.json:1-30`, each of
  `{Skill,Handler,Gate,Delegate,Approval}Step.json:34-35`, and
  `src/Strategos.Contracts/schemas/workflow-definition-v1.schema.json:101-128,257-259,392-394,666-668,719-721,893-895`);
- **6 generated C# sites**: `ActionReferenceV1` plus the five arm properties
  (`src/Strategos.Contracts/Generated/ActionReferenceV1.g.cs:19-59` and
  `{Skill,Handler,Gate,Delegate,Approval}Step.g.cs:61-65`); and
- **6 analyzer/proof units**: hand wire DTO identity, hand wire DTO step slot, isolated generator
  identity, its closed resolution enum, `StepModel`'s identity/resolution state, and proof
  `ActionIdentity` (`src/Strategos.Generators/Import/WireDtos.cs:113-133`,
  `src/Strategos.Generators/Models/WorkflowActionReferenceModel.cs:20-55`,
  `src/Strategos.Generators/Models/WorkflowActionReferenceResolution.cs`,
  `src/Strategos.Generators/Models/StepModel.cs:55-64,122-185`, and
  `src/Strategos.Generators/Proof/OntologyActionCatalog.cs:1003-1037`).

The six executable validators are the generated TypeSpec-derived callback and the five hand-coded
paths listed in AT-5. Five binding/adaptor paths transport the tuple: the step configuration's
single-assignment `ApplyTo` path (`src/Strategos/Builders/StepConfigurationBuilder.cs:20-55`),
runtime-to-wire projection (`src/Strategos/Contracts/WorkflowDefinitionProjection.cs:150-190`), C#
extraction (`StepExtractor.cs:1424-1494,1688-1745`), JSON reading
(`MinimalJsonReader.cs:667-705`), and wire-to-model bridging
(`WireToModelBridge.cs:726-775`). Every configurable builder path calls the same `ApplyTo` method;
there is no per-topology storage copy.

The 16 focused test files are `PackagingTests`, `StepDefinitionSchemaTests`,
`ApprovalExtractorTests`, `StepExtractorActionReferenceTests`, `RoundTripIrFidelityTests`,
`WireDtoSchemaConformanceTests`, generator `InvariantGuardTests`, the three proof suites,
`ApprovalEscalationBuilderTests`, `ApprovalRejectionBuilderTests`,
`WorkflowActionReferenceTests`, `WorkflowBuilderAwaitApprovalTests`,
`ProjectionExhaustivenessTests`, and `ProjectionTests`. Representative anchors are
`src/Strategos.Contracts.Tests/Workflow/StepDefinitionSchemaTests.cs:83-245`,
`src/Strategos.Generators.Tests/Helpers/StepExtractorActionReferenceTests.cs:24-215`, and
`src/Strategos.Tests/Contracts/ProjectionTests.cs:26-71`. The seven hand docs are the root and
Contracts changelogs, Contracts README, migration guide, action-calculus reference, workflow API
reference, and AGWF/AGSR reference.

The TypeSpec-to-schema-to-generated-C# branch is mechanically bound by
`scripts/contracts-codegen.sh:1-46` and the CI drift gate
(`.github/workflows/contracts-codegen-guard.yml:27-63`). The standalone and bundled arm copies both
originate in the single TypeSpec `StepCommon.action` slot. AT-5 concerns the five hand validators,
not those derived outputs. AT-1 concerns the partial public-API representation. The tuple is not an
ontology-graph property, so it correctly has no `OntologyGraphHasher` slot.

### Predicate, frame, and authority semantics

The closed wire vocabulary has an exact **30 TypeSpec declarations → 30 standalone JSON Schemas →
29 generated C# types** topology. The missing thirtieth C# type is intentional: the
`ActionLinkPathSegmentV1` scalar is represented as `string` in generated records. The source is
`src/Strategos.Contracts/Ontology/ActionPredicates.tsp:7-252`; the corresponding checked-in schema
and generated directories contain those exact counts. One generated runtime projection,
`src/Strategos.Ontology/Contracts/Generated/ContractOntology.g.cs:25-35`, demonstrates that
TypeSpec `@requires`, `@ensures`, `@relation`, and `@authority` metadata lower to the runtime typed
model. Regeneration and CI diff bind all of these copies.

For the #167 proof path, the additional representation counts are:

- **predicate contracts:** 1 runtime `ActionPredicate` hierarchy, 1 runtime `ActionDescriptor`
  container, 1 shared Roslyn grammar/projection, 2 analyzer carrier layers
  (`WorkflowPredicateSyntax` and `OntologyPredicateContract`), and 2 proof carriers
  (`ActionContractProof` and `ProvenContract`);
- **frames:** 3 runtime carriers (`ActionDescriptor.TouchedResources`, `ActionResource`, and
  `ActionFrame`), 5 TypeSpec resource declarations, 5 resource schemas, 5 generated C# resource
  types, 3 analyzer/proof frame fields, and 2 independent containment decision sites;
- **authority:** 5 runtime carriers (`RequiredAuthority`, `AuthorityAxisDescriptor`,
  `AuthorityDescriptor`, `AuthorityRequirement`, and `AuthorityLattice`), 3 TypeSpec-derived
  metadata representations (decorator declaration, emitted schema extension, generated runtime
  descriptor), 3 analyzer carriers (`WorkflowActionContractSyntax`, `OntologyActionContract`, and
  `OntologyAuthorityLattice`), and 2 independent order/join decision sites.

The central runtime container is at
`src/Strategos.Ontology/Descriptors/ActionDescriptor.cs:5-130`; the shared Roslyn carriers are at
`src/Strategos.Ontology.Generators/Analyzers/ActionCompositionAnalyzer.WorkflowBridge.cs:1201-1325`;
the catalog carriers are at `src/Strategos.Generators/Proof/OntologyActionCatalog.cs:896-1000`; and
the analyzer proof carrier is at
`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:1184-1194`. Four hand docs describe
the new refinement semantics: root changelog, migration guide, action-calculus reference, and
AGWF/AGSR reference. The two principal test representations are the disjoint 14-test runtime and
23-test analyzer suites counted in AT-4. There is no UI copy.

AT-4 is the authority defect: the syntax and finite solver are mechanically shared, while the
semantic orchestration and authority lattice are not.

### AGWF diagnostic identity

The exact **30-unit** count and split authority are in AT-3. For traceability, the generated enum
schema carries the four tokens at
`src/Strategos.Contracts/schemas/json-schema/AgwfCode.json:38-41`; generated public enum/converter
at `src/Strategos.Contracts/Generated/AgwfCode.g.cs:153-167,216-219,263-266`; source-linked constants
at `src/Strategos.Contracts/Generated/AgwfCodes.g.cs:114-124`; catalog at
`src/Strategos.Contracts/Generated/agwf-catalog.json:262-292`; and generated table at
`docs/diagnostics/agwf.md:47-50`.

### Public API

The core change adds **5 interface methods** and **1 public identity type with 5 explicit members**.
The core baseline represents **2/5 methods and 0/6 explicit identity declarations**. Four gate
artifacts participate: `PublicApi.globalconfig`, the scoped `.editorconfig`, the two baseline text
files treated as one baseline set, and `BuilderApiBaselineTests`. Their scope agrees exactly; that
agreement is the problem for the three excluded changed interfaces. The ontology public API is
healthy: one full-assembly analyzer authority and one baseline set, with **33 additions/2 removals**
in this diff. See AT-1.

### Graph hash

The workflow-binding hash boundary has **6 representations**: **1 production byte encoding**, **2
focused tests**, and **3 hand documentation copies**. The production authority is
`OntologyGraphHasher.WriteAction` (`src/Strategos.Ontology/Internal/OntologyGraphHasher.cs:238-258`).
`Version_RebindingActionWorkflow_ChangesHash` and the fixed legacy-hash fixture are at
`src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs:290-333`. The prose copies are
`CHANGELOG.md:60-63`, the migration guide at `:413-433`, and graph versioning at `:78-88`.

The mechanism is direct rather than textual: the hasher writes exactly
`BoundWorkflow?.WorkflowId` in the prior string slot (`OntologyGraphHasher.cs:248-253`), and the
typed wrapper preserves the raw ordinal string (`WorkflowBindingReference.cs:7-14`). The golden
test pins `17f5da54ed8eeb9aa31e2796c6f5dce69d07cbf36d6c289564f8a5f641fc4a00`
(`OntologyGraphVersionTests.cs:319-332`). No duplicate production hash authority was found.

### Contracts package version and shipping

This ancillary boundary has **9 representations**: **1 MSBuild authority**, **1 release tag gate**,
**1 packaging-test constant**, and **6 hand docs**. `ContractsVersion` drives both `Version` and
`PackageVersion` in one property group
(`src/Strategos.Contracts/Strategos.Contracts.csproj:35-45`); the publish workflow compares the
tag to that property (`.github/workflows/publish-contracts.yml:47-58`); and packaging checks both
the file and nuspec at `src/Strategos.Contracts.Tests/PackagingTests.cs:73-120`. The six prose
copies are the root changelog, Contracts changelog and README, migration guide, action-calculus
reference, and workflow API reference. The package globs all schemas
(`Strategos.Contracts.csproj:61-64`) and separately includes the diagnostic catalog (`:67-70`), so
the new action-reference and AGWF entry schemas are not dependent on a hand-maintained item list.
No shipping-authority defect was found here.

## What else was read

- The complete tracked diff from `45c86a63a9437abd920240b4dc95b235c0f72d37`
  (**126 tracked files, 3,860 insertions, 247 deletions** at this lens's snapshot) and every then
  untracked #167 source, test, documentation, and verification file.
- `verification/issue-167/stage0.md`, plus the mechanism and intent/claims survey artifacts only as
  navigation aids; their conclusions were not imported.
- All changed builder implementations and every configured top-level, branch, loop, fork, failure,
  approval, and confidence path that calls `StepConfigurationBuilder.ApplyTo`.
- The complete `StepExtractor`, topology inspector, ontology catalog, workflow binding proof,
  shared action-parser bridge, runtime contract proof/refinement, authority lattice, import reader,
  import bridge, projection, graph hasher, code generator, and codegen/publish gate paths.
- All added proof suites (18 catalog fail-closed tests, 24 topology-closure tests, 23 workflow
  binding tests), all 14 runtime refinement tests, action-reference extraction/import/projection/
  schema/builder tests, AGWF catalog/parity tests, public-API configuration and reflection gate,
  both changelogs, and every changed documentation page.

No build or test was run by this lens. That is deliberate: this Stage 1 artifact maps authority and
drift mechanisms; later verification stages own execution evidence.

## Assumptions and open questions

- Line anchors name the historical working tree as read on 2026-09-06. They are discovery provenance,
  not anchors for current final subject `98fabb4`.
- TypeSpec is treated as the intended wire authority because the repository explicitly labels it
  canonical and regenerates schema/C# from it. A generated artifact is counted, but is not promoted
  to a second authority while the regen/diff gate remains mandatory.
- Source-linked files are one source authority even though they produce multiple compiled copies.
  Conversely, two methods that merely call the same solver remain separate authorities when they
  independently choose formulas, precedence, and error classification.
- Imported `WorkflowDefinitionV1.name`, runtime `WorkflowDefinition.Name`, `[Workflow]`, and
  `WorkflowBindingReference.WorkflowId` are treated as copies of one lookup boundary because #167
  compares them by exact ordinal value. Current commit `98fabb4` aligns all four on nonblank input.
- It is unresolved whether the `BoundWorkflowName` parser fallback intentionally supports an old
  referenced Strategos assembly. No comment, fixture, or migration claim establishes that intent.
- Exarchos and Basileus repositories were not inspected by this historical lens. Basileus #493 and
  Exarchos #1893 now establish coordination, but this report still does not establish downstream
  implementation parity.
- The legacy hash fixture is consistent with the byte-path inspection, but its literal was added in
  the same working-tree diff. A later execution stage should, if provenance matters, compare the
  base binary and new binary on the same graph rather than relying only on the new golden.
- No protected PR result or completed review evidence is currently bound to final subject `98fabb4`.
  External issue text remains coordination/intent rather than proof of behavior.
