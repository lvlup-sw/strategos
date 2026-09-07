---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
fingerprint_command: git rev-parse 98fabb410e4432cc39fd71ec92651e6ceecfcc7c^{tree}
cost_setting: high
scope_rule: production-path trace of every #167 authored, imported, generated, proof, diagnostic, package, and runtime boundary in the reverse dependency closure
updated: 2026-09-06T18:22:34-07:00
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria and machine-checked build-time enforcement claim
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: merged predicate/proof semantics consumed by the refinement implementation
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: deferred compensation-path consumer of this workflow contract
lens: production-path
---

# Stage 1 survey — production path

This is a **historical discovery record**. It traced the pre-hardening implementation from public
authoring and wire input to the consumer-visible build result and runtime graph. PP1 identified a real
compilation-local boundary that is now explicitly documented and tracked by #204, not a claim of
portable proof. PP2 reproduced an unsafe unrooted catalog admission; commit `595a949` includes its
repair and the valid-rooted-bound-action/missing-occurrence AGWF040 kill. Present-tense reproduction
prose below is historical. The complete local portfolio passed at current subject `98fabb4`;
protected execution and review remain pending.

## End-to-end path trace

### Ontology action binding

1. `WorkflowBindingReference` is an immutable, names-only value object and rejects a blank
   `WorkflowId` (`src/Strategos.Ontology/Descriptors/WorkflowBindingReference.cs:4-14`). Both action
   builders keep the legacy string overload by constructing this value and store it alongside
   `ActionBindingType.Workflow` (`src/Strategos.Ontology/Builder/ActionBuilder.cs:40-48` and
   `ActionBuilderOfT.cs:68-76`). `ActionDescriptor` exposes the typed reference at
   `src/Strategos.Ontology/Descriptors/ActionDescriptor.cs:39-45`.
2. The runtime `OntologyGraphBuilder` executes only domains explicitly added to its
   `_domainOntologies` collection, then calls each selected domain's `Define`
   (`src/Strategos.Ontology/OntologyGraphBuilder.cs:49-59,121-126`). The DI path likewise copies only
   `OntologyOptions.Domains` into the graph builder
   (`src/Strategos.Ontology/Configuration/OntologyServiceCollectionExtensions.cs:20-56`). Source
   descriptors arrive later through `IOntologySource` and `ObjectTypeFromDescriptor`
   (`OntologyGraphBuilder.cs:128-137,1851-1903` and
   `src/Strategos.Ontology/Builder/OntologyBuilder.cs:129-148,217-226`).
3. Graph hashing writes the typed reference's `WorkflowId` in the old binding-name slot
   (`src/Strategos.Ontology/Internal/OntologyGraphHasher.cs:238-258`). No graph-build branch validates
   `BindingType`/`BoundWorkflow` consistency or invokes refinement: a repository search for
   `AnalyzeRefinement` outside tests finds only the public methods in
   `ActionCalculus.cs`, while runtime `BoundWorkflow` uses outside builders/descriptors are the
   hasher. The build-time generator is therefore the sole binding-enforcement boundary.

### C# workflow authoring and extraction

1. A step occurrence receives a names-only `WorkflowActionReference`; the constructor rejects blank
   domain/object/action components (`src/Strategos/Definitions/WorkflowActionReference.cs:17-55`).
   `IStepConfiguration<TState>.Performs` is the public authoring root
   (`src/Strategos/Abstractions/IStepConfiguration.cs:39-55`). Its runtime builder permits exactly
   one declaration and copies it onto `StepDefinition.Action`
   (`src/Strategos/Builders/StepConfigurationBuilder.cs:20-55`; property at
   `src/Strategos/Definitions/StepDefinition.cs:74-81`). Configured overloads route top-level,
   branch, loop, fork-path, join, failure, approval rejection, and approval escalation occurrences
   through this builder.
2. Independently of evaluating the runtime builder, the source generator discovers `[Workflow]`
   declarations and creates a `WorkflowModel`
   (`src/Strategos.Generators/WorkflowIncrementalGenerator.cs:68-89`). `StepExtractor` recognizes
   generic class-step occurrences, inspects their own configure lambda, requires one direct
   `Performs`, and reduces only a direct `new WorkflowActionReference(...)` with three constant,
   nonblank strings (`src/Strategos.Generators/Helpers/StepExtractor.cs:1343-1410,1424-1493,1688-1745`).
   Missing, resolved, and dynamic/invalid remain distinct in `StepModel`
   (`src/Strategos.Generators/Models/StepModel.cs:55-64`).
3. `TopologyClosureInspector` checks that a proof-selected fluent definition originates at a
   visible `Workflow<T>.Create`, that its identity equals `[Workflow]`, and that executable callback
   forms not represented by the static grammar fail closed
   (`src/Strategos.Generators/Helpers/TopologyClosureInspector.cs:31-111,114-205`). The resulting
   closure failures are attached to `WorkflowModel` before proof
   (`src/Strategos.Generators/WorkflowIncrementalGenerator.cs:515-523`).
4. The ordinary runtime-to-contract projection also preserves the three action names on both
   delegate and skill wire arms (`src/Strategos/Contracts/WorkflowDefinitionProjection.cs:128-191`).
   That projection is a serialization path, not the generator's C# proof input; the proof reads the
   analyzer's syntax-derived `WorkflowModel`.

### JSON workflow authoring and import

1. TypeSpec defines an optional action on every step arm and a required, nonblank three-string
   `ActionReferenceV1` (`src/Strategos.Contracts/Workflow/StepDefinition.tsp:27-48` and
   `ActionReference.tsp:12-27`). The generated C# callbacks enforce required/nonblank fields
   (`src/Strategos.Contracts/Generated/ActionReferenceV1.g.cs:19-59`), and the emitted JSON Schema
   carries the same `required`, `minLength`, and `pattern` constraints
   (`src/Strategos.Contracts/schemas/json-schema/ActionReferenceV1.json:1-30`). These artifacts ship
   from independently versioned Contracts 0.11.0
   (`src/Strategos.Contracts/Strategos.Contracts.csproj:30-45`).
2. `*.workflow.json` AdditionalFiles enter the generator at
   `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:47-66`. `MinimalJsonReader` rejects a
   present action of the wrong JSON kind and rejects missing, non-string, empty, or whitespace-only
   child names (`src/Strategos.Generators/Import/MinimalJsonReader.cs:642-705`); the outer import
   path turns that parse failure into AGWF023
   (`WorkflowIncrementalGenerator.cs:275-327`).
3. A supported document is bridged against the consumer compilation. `MapStep` resolves the CLR
   step moniker, maps the structured action to the same `WorkflowActionReferenceModel`, and retains
   its resolution state (`src/Strategos.Generators/Import/WireToModelBridge.cs:711-775`). Rejected
   carriers return no model; accepted C# and JSON models then join the same proof input
   (`src/Strategos.Generators/WorkflowIncrementalGenerator.cs:91-134`).

### Catalog resolution, saga emission, and refinement proof

1. Every accepted C# or JSON model is sent through the one emission routine for phase, commands,
   events, transition table, saga, workers, and DI
   (`src/Strategos.Generators/WorkflowIncrementalGenerator.cs:137-195`). Occurrence action identity is
   not emitted or enforced by the saga handlers: repository search finds `StepModel.Action` and
   `ActionResolution` consumers only in import/extraction and `WorkflowBindingProofAnalyzer`, not in
   `Emitters/`. The emitted runtime path therefore relies on the Error diagnostic to gate the
   consumer build.
2. The proof callback builds `OntologyActionCatalog` from the current Roslyn `Compilation`, groups
   C#/JSON workflow models by exact ordinal `WorkflowName`, and analyzes only workflow-bound actions
   present in that action catalog
   (`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:18-78`). Zero/multiple workflow
   matches produce AGWF039. Each reachable phase must then have a resolved occurrence identity and
   exactly one catalog action, or AGWF040 is emitted (`WorkflowBindingProofAnalyzer.cs:219-305`).
3. The source-linked action parser lowers closed requirements, guarantees, frames, authorities,
   created-link facts, and supported predicates
   (`src/Strategos.Ontology.Generators/Analyzers/ActionCompositionAnalyzer.WorkflowBridge.cs:172-437`).
   `TryProveContract` rejects invalid/opaque/contradictory/unrealizable contracts and computes the
   effective guarantee by semantic forgetting (`WorkflowBindingProofAnalyzer.cs:417-542`).
4. The proof uses the same `PhaseGraph` as the generated transition table
   (`src/Strategos.Generators/Models/PhaseGraph.cs:67-95` and
   `src/Strategos.Generators/Emitters/TransitionsEmitter.cs:53-80`). It checks bound requirement to
   entry, every internal seam, every successful edge to the bound guarantee, failure/approval
   ingress, frame subset, authority join, and fork noninterference
   (`WorkflowBindingProofAnalyzer.cs:357-415,672-1035,1201-1317`). The saga emitter consumes the same
   `WorkflowModel` but has its own `SagaEmissionContext` routing tables rather than calling
   `PhaseGraph` (`src/Strategos.Generators/Emitters/Saga/SagaEmissionContext.cs:168-185`); focused
   topology/proof tests passed, and this lens found no concrete supported-topology divergence.
5. AGWF039-AGWF042 are enabled-by-default Error diagnostics
   (`src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs:638-676`). Source emission is
   registered before the aggregate proof callback, so generated trees may exist in a failed build;
   only a successful compilation is the acceptance signal.
6. `ActionCalculus.AnalyzeRefinement` is a separate public, caller-invoked runtime analysis surface
   (`src/Strategos.Ontology/Descriptors/ActionCalculus.cs:10-84,413-560`). It shares formula/solver
   semantics with the source-linked generator kernel but is not invoked by graph construction or
   generated saga dispatch, so it is not a runtime backstop for missed workflow bindings.

## Findings

### PP1 — High: moving the ontology action to a referenced assembly removes the proof entirely

`OntologyActionCatalog.Build` enumerates only `compilation.SyntaxTrees`
(`src/Strategos.Generators/Proof/OntologyActionCatalog.cs:40-55`) and derives every fluent/direct
action from syntax in those trees (`:55-224`). It never inspects metadata-reference assemblies or a
published ontology action-contract manifest. `WorkflowBindingProofAnalyzer` then starts exclusively
from `catalog.Actions.Where(action => action.HasWorkflowBinding)`
(`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:23-40`).

Concrete two-project false-pass:

1. Project A references `LevelUp.Strategos.Ontology` and defines `OrdersOntology` with
   `obj.Action("Fulfill").BoundToWorkflow("missing-or-unsound-flow")`; compile it as a class library.
2. Project B references A plus `LevelUp.Strategos` and `LevelUp.Strategos.Generators`, and either
   defines no workflow of that name or defines a workflow whose leaf guarantees do not establish
   `Fulfill`'s guarantee.
3. During B's build the binding is metadata, not a member of B's `Compilation.SyntaxTrees`, so no
   bound action enters the loop and none of AGWF039-AGWF042 can be emitted. During A's build there is
   no workflow generator requirement or workflow model to resolve: the ontology package has no
   dependency on the workflow generator (`src/Strategos.Ontology/Strategos.Ontology.csproj:1-49`),
   and the workflow generator is a separately installed development dependency
   (`src/Strategos.Generators/Strategos.Generators.csproj:18-22,89-93`; package guidance at
   `docs/src/content/docs/reference/packages.md:80-116,295-315`).

There is no later safety net. Runtime graph composition accepts the descriptor and the generated
saga does not retain the occurrence identity, as traced above. A referenced or source-ingested
workflow-bound descriptor can therefore reach the shipped graph without a refinement result.

**Claim boundary.** Issue #167 does not literally say “cross-assembly” or require catalog federation;
this is a newly discovered limitation. It does, however, state the unqualified acceptance criterion
“An action bound to a non-existent workflow fails the build” and requests resolution against the
workflow catalog at build time. Current user documentation is likewise unqualified: it says a bound
action “is accepted only when the generator can construct and prove a closed contract”
(`docs/src/content/docs/reference/diagnostics/agwf-agsr.md:35-47`). The implementation and internal
comments call the proof “compilation-local,” but the migration/API docs disclose only that authored
expressions must be analyzer-visible, not that the ontology action and every leaf contract must be
source-co-located in the workflow compilation
(`docs/src/content/docs/guide/ontology/migration-v2-13.md:322-363` and
`docs/src/content/docs/reference/api/workflow.md:152-165`). If same-compilation-only is intended,
that must become an explicit supported-topology restriction and the broad acceptance/documentation
claim must be narrowed; otherwise the build needs a portable contract/catalog input from referenced
assemblies.

### PP2 — High: the “ontology action catalog” accepts declarations that can never reach the runtime graph

The static catalog is intentionally a declaration-syntax inventory, not the executable ontology
catalog:

- every `DomainOntology` subclass in the compilation is scanned without checking
  `OntologyOptions.AddDomain<T>()` (`src/Strategos.Generators/Proof/OntologyActionCatalog.cs:49-181`);
- every direct `new ActionDescriptor(...)` anywhere in every syntax tree is added, with no owner,
  `ObjectTypeDescriptor`, domain registration, or source reachability requirement (`:183-200`);
- direct descriptor support is deliberate—the shared parser documents it as “a direct, statically
  immutable ActionDescriptor construction”
  (`src/Strategos.Ontology.Generators/Analyzers/ActionCompositionAnalyzer.WorkflowBridge.cs:105-169`),
  and `OntologyActionCatalogFailClosedTests.DirectDescriptorWithDynamicGuarantee_ReportsAgwf042`
  explicitly supplies a static descriptor field
  (`src/Strategos.Generators.Tests/Proof/OntologyActionCatalogFailClosedTests.cs:73-98`).

An executed Roslyn-generator reproducer used one bound fluent `fulfill` action and a two-step `flow`.
The two `.Performs(...)` references named `leaf` and `done`, while the only matching contracts were
static `ActionDescriptor` fields in a `DeadCatalogEntries` class. Neither field was passed to
`ObjectTypeFromDescriptor`, returned by `OrdersOntology.Define`, nor emitted by a source. The
generator printed:

```text
binding diagnostic count=0
```

The runtime definition in that same source registers only `fulfill`; `OntologyGraphBuilder` cannot
contain `leaf` or `done`. Thus `catalog.Resolve(identity)` at
`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:268-300` reports a unique proof leaf
that is not dispatchable or graph-reachable. The same false green occurs when the matching leaf is
inside an unregistered `DomainOntology` subclass. The shipped saga contains no runtime check to
repair the mismatch.

The resolution catalog must be tied to a declared executable ontology/catalog root, or static
resolution must conservatively reject descriptors whose graph ownership/reachability cannot be
proved. At minimum, direct descriptors cannot count as leaf implementations solely because their
constructor syntax occurs somewhere in the compilation.

## Commands and evidence

- `gh issue view 167 --json number,title,body,url,state,labels` — fetched the authoritative issue;
  confirmed the four acceptance criteria and source-generator/analyzer enforcement class.
- `git status --short`, `git diff --name-only`, `git diff --stat`, `git rev-parse HEAD`, and
  `git branch --show-current` — established the historical working-tree target and base.
- `rg -n "AnalyzeRefinement|BoundWorkflow|BindingType.Workflow|WorkflowBindingReference|AddSource|IOntologySource|ObjectTypeFromDescriptor|AddDomain" ...` plus focused `nl -ba` reads — traced
  public authoring, compiler inventory, runtime registration/source drain, proof, and absence of a
  runtime refinement call.
- `rg -n "\\.Action|ActionResolution|WorkflowActionReference" src/Strategos.Generators/Emitters src/Strategos.Generators -g '*.cs'` — confirmed occurrence identities stop at proof/import rather
  than generated saga enforcement.
- `dotnet build src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj --no-restore --disable-build-servers -m:1 -v minimal` — passed, 0 warnings and 0 errors.
- Focused TUnit runs via `dotnet test --project ... --no-build -- --treenode-filter ...`:
  `WorkflowBindingProofAnalyzerTests` 23/23, `OntologyActionCatalogFailClosedTests` 18/18,
  `TopologyClosureProofTests` 24/24, `StepExtractorActionReferenceTests` 14/14,
  `RoundTripIrFidelityTests` 12/12, and `ImportFrontEndRobustnessTests` 9/9 all passed.
- In-memory F# Interactive/Roslyn execution loaded the built `GeneratorTestHelper`, ran the dead
  direct-descriptor source described in PP2, filtered AGWF039-AGWF042, and observed exactly
  `binding diagnostic count=0`. No repository file was created for the reproducer.
- The first sandboxed `dotnet test` attempt failed because the test host could not bind its local
  IPC pipe; the required reruns outside the sandbox produced the passing counts above. An earlier
  concurrent project-reference property query also returned an empty MSBuild failure; the serialized
  `-m:1 --disable-build-servers` build then completed cleanly.

## Historical conclusion and current disposition

**Historical finding:** the pre-hardening syntax catalog both missed referenced/runtime bindings and
certified unreachable leaf declarations. **Current disposition:** compilation-local scope is explicit
and linked to #204; unrooted declarations cannot satisfy occurrences; and the later rooted negative
closes selective-vacuity in candidate code. Formal proof remains `Indeterminate` until the final
revision runs on protected CI.
