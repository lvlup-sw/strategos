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

# Survey lens 1 — Mechanism (issue #167)

This is a **historical discovery record** of the pre-hardening working-tree diff against `45c86a6`.
Present-tense findings M1–M6 preserve the defects as found; they do not describe the reconciled
current subject. Each was repaired by immutable catalog/Create identity, fail-closed delegate and
topology handling, PhaseName occurrence identity, malformed-action presence retention, exact nonblank
validation, and total `ActionRefinementAnalysis` invariants/precedence. The complete product-code
portfolio passed at exact-current `98fabb4`, including the later workflow-name nonblank repair,
fresh package/consumer proof, and hardened pack/Basileus path. Protected execution and review remain
pending.

## Findings

### M1. The analyzer can prove a catalog ID that the executable definition does not have

**High — silent false acceptance.** A C# workflow has two independent names, but only one reaches
the proof catalog:

1. `TransformToResult` reads `[Workflow(...)]` at
   `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:343-350`, stores that value as
   `validName` at `:381-392`, and constructs `WorkflowModel.WorkflowName` from it at `:1028-1036`.
2. `WorkflowBindingProofAnalyzer` groups and resolves workflows solely by that model property at
   `src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:23-30,63-77`.
3. The executable definition instead gets its name from the independent
   `Workflow<TState>.Create(name)` argument
   (`src/Strategos/Builders/Workflow.cs:35-40`). `WorkflowBuilder` persists `_name` into the runtime
   definition at `src/Strategos/Builders/WorkflowBuilder.cs:338-349`.
4. `TopologyClosureInspector` only establishes that the fluent chain originates at *some* `Create`
   call; it never reads, closes, or compares the argument
   (`src/Strategos.Generators/Helpers/TopologyClosureInspector.cs:108-149`).

Consequently, `[Workflow("catalog-id")]` paired with
`Workflow<T>.Create("runtime-id")` lets an action bound to `catalog-id` resolve exactly once and be
proved, although evaluating the definition produces `runtime-id`. This defeats the new typed
reference at its last lookup boundary. No equal/mismatch/dynamic-`Create` identity fixture exists in
the #167 proof tests.

### M2. A legal executable delegate occurrence is absent from `WorkflowModel`, `PhaseGraph`, and proof

**High — silent false acceptance.** `IWorkflowBuilder<TState>.Then(string,
StepDelegate<TState>)` remains public at `src/Strategos/Abstractions/IWorkflowBuilder.cs:180-209`.
The runtime builder creates the lambda step and connects it into the definition at
`src/Strategos/Builders/WorkflowBuilder.cs:220-267`.

Both static occurrence extractors require a generic method name and return false for this
non-generic `Then`:

- names: `src/Strategos.Generators/Helpers/StepExtractor.cs:343-368`;
- models/actions: `src/Strategos.Generators/Helpers/StepExtractor.cs:1353-1373`.

The topology-closure switch checks branch, loop, fork, failure, confidence, and approval callbacks,
but has no ordinary `Then` arm
(`src/Strategos.Generators/Helpers/TopologyClosureInspector.cs:53-100`). The proof then validates
only distinct entries already present in `workflow.StepNames`
(`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:219-305`). It therefore constructs a
fictional direct edge between the surrounding typed steps and can prove that graph while the
runtime definition contains an extra executable delegate with no `.Performs(...)` contract.

This contradicts the documented fail-closed promise: the docs acknowledge that delegate steps
cannot be proved (`docs/src/content/docs/reference/api/workflow.md:152-164`), but the mechanism does
not emit AGWF040/AGWF042 for them.

### M3. JSON lowering collapses distinct action occurrences by CLR step type

**High — false rejection and occurrence data loss.** The wire contract permits `instanceName` and
`action` on the same step (`src/Strategos.Contracts/Workflow/StepDefinition.tsp:27-47`). Import
mapping preserves both in each `StepModel`
(`src/Strategos.Generators/Import/WireToModelBridge.cs:711-775`), and the identity gate deliberately
groups by `EffectiveName` (`:266-319`). Thus two top-level uses of one CLR step type with distinct
instance names are valid input occurrences.

The two final model collections use different identities:

- `ComposeStepNames` retains each `PhaseName` at
  `src/Strategos.Generators/Import/WireToModelBridge.cs:1077-1137`;
- `ComposeStepModels` deduplicates with `existing.Add(step.StepName)` at `:1149-1195`.

For `{ stepName: "AnalyzeStep", instanceName: "First" }` and the same type named `Second`, the
topology contains both `First` and `Second`, but only the first executable model/action survives.
`BuildOccurrenceMap` indexes surviving models by `PhaseName` (`WorkflowBindingProofAnalyzer.cs:544-627`),
so the proof emits AGWF040 for `Second` as having “no executable StepModel occurrence”
(`:222-233`). A valid occurrence-scoped wire graph is rejected and its second action identity is
lost before proof. Existing import identity tests cover this reuse across fork structures, not two
independent top-level named occurrences.

### M4. A present malformed JSON `action` token is silently reclassified as omission

**Medium — fail-closed only when some bound action happens to select the workflow.** `ReadStep`
delegates to `ReadObject` at
`src/Strategos.Generators/Import/MinimalJsonReader.cs:642-664`; `ReadObject` returns null when the
member exists but is a scalar, array, or JSON null (`:897-899`). The bridge initializes action
resolution to `Missing` and only considers invalid fields when the DTO is non-null
(`src/Strategos.Generators/Import/WireToModelBridge.cs:728-748`).

Accordingly, `"action":"x"`, `"action":[]`, and `"action":null` are indistinguishable from an
absent member. A selected bound workflow still gets an AGWF040, but with the wrong missing route;
an unbound import silently discards the malformed declaration. Presence and validity need distinct
states before DTO binding.

### M5. Published and analyzer-side action identities disagree on “nonblank”

**Medium — boundary false positive.** Runtime authoring rejects whitespace-only components at
`src/Strategos/Definitions/WorkflowActionReference.cs:26-35`, and JSON lowering marks them invalid at
`src/Strategos.Generators/Import/WireToModelBridge.cs:732-746`. TypeSpec specifies only
`@minLength(1)` (`src/Strategos.Contracts/Workflow/ActionReference.tsp:13-24`), and generated C#
validates raw length only
(`src/Strategos.Contracts/Generated/ActionReferenceV1.g.cs:19-56`). The published contract therefore
accepts `" "` that the consumer generator rejects.

The DTO/schema conformance control does not close this gap: it compares property names and coarse
JSON categories only (`src/Strategos.Contracts.Tests/WireDtoSchemaConformanceTests.cs:63-117`) and
does not list `ActionReferenceV1` among its critical twins (`:155-178`). The hand analyzer DTO also
uses nullable members (`src/Strategos.Generators/Import/WireDtos.cs:122-133`) while the published
schema requires all three. Current schema tests cover empty strings, not whitespace-only strings.

### M6. Runtime refinement is parallel to, not the implementation used by, binding proof

**Medium — drift and false-status routes on the public runtime API.** The generator source-links the
finite solver and Roslyn action parser
(`src/Strategos.Generators/Strategos.Generators.csproj:37-53`), but
`WorkflowBindingProofAnalyzer` implements its own graph/refinement orchestration; there is no call to
public `ActionCalculus.AnalyzeRefinement`. This is a shared proof kernel, not one refinement
mechanism.

Two observable runtime routes merit obligations:

- `ActionRefinementAnalysis` has a public constructor that accepts any status/failure combination
  (`src/Strategos.Ontology/Descriptors/ActionRefinementAnalysis.cs:73-83`), while `IsRefinement`
  checks only `Status == Proven` (`:85-92`). Callers can construct `Proven` with failures.
- `ActionCalculus` returns `Opaque` before checking independently decidable subject, authority, and
  frame obligations (`src/Strategos.Ontology/Descriptors/ActionCalculus.cs:450-460` versus
  `:462-530`). The result is fail-closed, but definite violations disappear from its diagnostic
  surface when any predicate is opaque.

### M7. The nominal positive path is otherwise wired end to end

The following claims survived this lens:

1. `ActionDescriptor.BoundWorkflowName` is replaced by typed
   `WorkflowBindingReference? BoundWorkflow`
   (`src/Strategos.Ontology/Descriptors/ActionDescriptor.cs:39-42`); the value object rejects blank
   IDs (`WorkflowBindingReference.cs:4-14`). Both action builders retain the string overload by
   forwarding to the typed overload and store the typed value
   (`src/Strategos.Ontology/Builder/ActionBuilder.cs:40-48,141-158`, with the generic twin following
   the same route).
2. Hashing writes `BoundWorkflow.WorkflowId` in the exact old string slot
   (`src/Strategos.Ontology/Internal/OntologyGraphHasher.cs:238-258`). The hard-coded compatibility
   vector is in `src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs:319-333`.
3. Runtime `Performs` stores one names-only `WorkflowActionReference` on the occurrence through
   `StepConfigurationBuilder.ApplyTo`
   (`src/Strategos/Builders/StepConfigurationBuilder.cs:20-54`). Configured top-level, loop, fork,
   branch, failure, approval, and join builders all route through this helper.
4. C# extraction carries `Action` plus Missing/Resolved/DynamicOrInvalid state into `StepModel`; JSON
   maps the same pair. Runtime-to-wire projection maps all three names at
   `src/Strategos/Contracts/WorkflowDefinitionProjection.cs:128-191`.
5. Both C# and imported `WorkflowModel`s join one compilation-wide proof input at
   `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:116-134`. AGWF039–AGWF043 are Error
   diagnostics (`src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:638-676`).
6. The proof closes action references before building the shared `PhaseGraph`, then checks entry,
   seams/exits, failure/approval ingress, frame, authority, and fork noninterference
   (`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:183-371`). The graph is also the
   transition-emission graph, but it silently ignores an edge whose source is absent from
   `StepNames` (`src/Strategos.Generators/Models/PhaseGraph.cs:67-94,510-535`); findings M2/M3 show why
   upstream occurrence parity remains a required precondition.

## Deletions and changed authority

- No tracked product file is deleted in the current diff. The material public deletion is the
  string-valued `ActionDescriptor.BoundWorkflowName` property; it is replaced, not shadowed, by the
  typed property. The legacy **builder method** remains, but direct property consumers do not.
- Proof authority moves from no workflow/action build gate to Error diagnostics AGWF039–AGWF043.
  The proof is registered after both front ends have already registered source emission; errors fail
  the consumer build but do not prevent the generator from producing trees
  (`WorkflowIncrementalGenerator.cs:64-134`).
- TypeSpec/generated contract output is bumped to 0.11.0. Most generated-file churn is mechanical;
  the new semantic surface is optional `StepCommon.action`, `ActionReferenceV1`, and the exact
  nonwhitespace constraint on `WorkflowDefinitionV1.name`.

## Candidate correctness obligations

1. A C# workflow's closed `Workflow<T>.Create` ID must equal its `[Workflow]` catalog ID ordinally;
   mismatch or dynamic identity must be an Error before lookup/proof.
2. Every executable runtime occurrence and transition must have a 1:1 representation in
   `WorkflowModel` and `PhaseGraph`. Delegate steps must be modeled or make a bound workflow
   unprovable; no legal construct may disappear before the `StepNames` loop.
3. Occurrence collections must key by `PhaseName`/stable occurrence ID, not CLR `StepName`; two
   distinct named uses of one type must preserve both action identities through JSON and C# paths.
4. A present `action` member must retain presence. Wrong JSON kind, null, absent required child,
   blank child, and omitted member require distinct or deliberately normalized fail-closed results.
5. Core constructors, TypeSpec/schema, generated validation, hand DTOs, and import parsing must agree
   on required/nonblank/ordinal identity semantics, and the conformance guard must check those
   constraints for `ActionReferenceV1`.
6. `Proven` must be unconstructible with failures. Independently decidable subject, authority, and
   frame failures must remain visible even when predicate proof is opaque.
7. Add kill fixtures for attribute/Create mismatch and dynamic `Create`; an intervening delegate;
   two top-level same-type/distinct-instance JSON occurrences with different actions; malformed
   action token kinds; whitespace-only fields; contradictory runtime refinement results; and opaque
   predicates combined with definite structural violations.
8. Assert C#/JSON parity over the full reachable closure (main, branch, loop, fork, failure,
   approval, confidence) by comparing occurrence IDs, action identities, and emitted/proved edges,
   not merely generated source snapshots.

## What else I read

- `verification/issue-167/stage0.md`, full `git diff --name-status`, and all #167 public API,
  parser/bridge, IR, graph, analyzer, generated-contract, projection, docs, and focused test diffs.
- `WorkflowBindingProofAnalyzerTests`, `TopologyClosureProofTests`,
  `OntologyActionCatalogFailClosedTests`, `StepExtractorActionReferenceTests`, import identity and
  round-trip tests, builder action-reference tests, projection tests, schema tests, graph hash tests,
  and runtime action-refinement tests.
- Runtime builder implementations for every configured step surface; C# branch/loop/fork/failure,
  approval, confidence and compensation extraction; JSON rejection/mapping/composition; ontology
  catalog direct/fluent/standalone binding inventory; shared finite proof and authority paths.

No test command was run for this historical survey lens; execution and mutation adequacy belonged to
later verification stages. `final-evidence.md` records the complete exact-current local evidence;
protected evidence and review remain pending.

## Assumptions and unsettled questions

- The authoritative runtime workflow identity is `WorkflowDefinition.Name`, as documented by
  `Workflow<T>.Create` and persisted by `WorkflowBuilder`; if `[Workflow]` is intentionally a distinct
  generator-only alias, that distinction needs an explicit mapping contract instead of implicit
  equality.
- Imported top-level same-type occurrences with distinct `instanceName`s are treated as legal
  because the schema and identity gate explicitly support them. If runtime lowering intentionally
  forbids that shape, the bridge must reject it before composition with a dedicated diagnostic.
- Cross-assembly ontology/workflow catalogs were not assumed. The current proof catalog is
  compilation-source-local; package-boundary federation requires a separately specified mechanism.
