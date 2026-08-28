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
lens: 4. Production Path
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

# Survey lens 4 — Production Path

A present type, method, or enum member is not a registration. Every path below
names the public root, the real caller, and the observable effect, or says the
path is unreached through the shipped composition.

## Composition roots in this scope

| Root | Kind | Where it is registered / packed |
|---|---|---|
| `WorkflowIncrementalGenerator` | Roslyn `[Generator]` | `[Generator]` at `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:19`. Packed as `analyzers/dotnet/cs` in `LevelUp.Strategos.Generators` (`Strategos.Generators.csproj:73`). **Not** a ProjectReference of `LevelUp.Strategos` (`Strategos.csproj` has no generator reference). Consumer must take the generator package. |
| `OntologyMcpServerBuilderExtensions.AddOntologyTools` | DI / MCP builder | `src/Strategos.Ontology.MCP.Hosting/OntologyMcpServerBuilderExtensions.cs:28` and `:54`. Both call `OntologyServerToolFactory.CreateServerTools`. Assembly ships both overloads (`Hosting.Tests/PackagingTests.cs:21-39`). |
| `IOntologyBuilder.ObjectTypeFromDescriptor` / `ApplyDelta` | public ontology API | `src/Strategos.Ontology/Builder/IOntologyBuilder.cs:87` and `:103`. Fluent `Object<T>` stays CLR-generic (`:19`). |
| `IActionBuilder<T>.Requires` | public fluent API | Interface `IActionBuilderOfT.cs:39-40`; implementation `ActionBuilderOfT.cs:77-90`. Still in `PublicAPI.Unshipped.txt:109`. |
| `renovate.json` | external bot config | Repo root. No in-process registration. |

Reflection / convention used as selection (not prose):

- JSON import participates when an `AdditionalFile` path ends with `.workflow.json` (`WorkflowIncrementalGenerator.cs:29`, `:245-246`). There is no Directory.Build.props / targets glob that adds those files. A consumer that never declares `<AdditionalFiles Include="*.workflow.json" />` never hits the import pipeline.
- `OntologyToolDiscovery.Discover` reflects over result-record types (`OntologyToolDiscovery.cs:25-31`, factory remarks at `OntologyServerToolFactory.cs:68-70`). Declared with `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`.
- `AddOntologyTools()` (no-args) selects `OntologyGraph` by `ServiceType == typeof(OntologyGraph)` and requires an **instance** singleton (`OntologyMcpServerBuilderExtensions.cs:61-73`). A factory-registered graph throws.

---

## 1. WorkflowIncrementalGenerator → TerminalReachabilityGuard (under-reach) and TransitionsEmitter (PhaseGraph)

### 1a. AGWF035 under-reach arm — C# `[Workflow]` — **reached**

```
[Workflow] class
  → WorkflowIncrementalGenerator.Initialize RegisterSourceOutput (WorkflowIncrementalGenerator.cs:75)
  → TransformToResult (335)
  → TerminalReachabilityGuard.Report (1038-1043)
  → ReportUnderReach (119-128) with phaseGraph ?? PhaseGraph.Build(model)
  → Diagnostic.Create(WorkflowDiagnostics.UnreachableTermination) (425-430)
  → spc.ReportDiagnostic (78-81)
```

Registration site: `WorkflowIncrementalGenerator.cs:1038`. The production call omits `phaseGraph`; the under-reach arm therefore always builds `PhaseGraph.Build(model)` (`TerminalReachabilityGuard.cs:127`). Tests inject a stripped graph via the optional parameter (`:56-59`, `:116-118`).

`MainFlowClassification.For(model)` is used inside the guard (`:226`, `:340`) and inside `PhaseGraph.Build` (`PhaseGraph.cs:71`). Shared graph type; two `Build` call sites in production: guard (default) and emitter (below).

Observable effect: AGWF035 (`AgwfCodes.UnreachableTermination`, `AgwfCodes.g.cs:103`) on the consumer compile. The call sits **after** the `hasErrors` gate (`WorkflowIncrementalGenerator.cs:933-1045`). Under-reach does not suppress `EmitWorkflowSources`. Over-reach is the same: report-and-still-emit.

### 1b. AGWF035 under-reach arm — JSON import — **unreached**

```
AdditionalFiles *.workflow.json
  → Initialize importReads / bridgedImports (54-113)
  → BridgeImportFile (209) → WireToModelBridge.Bridge (230)
  → RegisterSourceOutput → EmitWorkflowSources (102-112)
```

`WireToModelBridge.Bridge` has no `TerminalReachabilityGuard` call. `TransformToResult` is the only production caller of `Report` (confirmed: the other hits are tests in `TerminalReachabilityDiagnosticTests.cs`). A JSON-imported workflow that would fail under-reach still emits a saga and a `ValidTransitions` table.

### 1c. TransitionsEmitter / PhaseGraph / ValidTransitions — **reached** (C# and JSON)

```
EmitWorkflowSources (127)
  → TransitionsEmitter.Emit (142)
  → PhaseGraph.Build (TransitionsEmitter.cs:56)
  → stamped {PascalName}Transitions.g.cs (143)
```

Both authoring fronts call `EmitWorkflowSources` (C# at `:86`, JSON at `:111`). The emitted `ValidTransitions` / `IsValidTransition` pair is public generated API (`TransitionsEmitter.cs:18-21`, `:61-110`).

Packaged: `PhaseGraph`, `TerminalReachabilityGuard`, and `TransitionsEmitter` compile into `Strategos.Generators.dll`, packed at `analyzers/dotnet/cs` (`Strategos.Generators.csproj:73`). Capability is in the analyzer nupkg. Consumer rebuild is required for already-emitted sagas (stage0 reversal note).

---

## 2. DiagnosticForkExtractor + WireToModelBridge → AGWF037

### 2a. C# extract — **reached**

```
TransformToResult
  → FluentDslParser.ExtractDiagnosticForkModels (WorkflowIncrementalGenerator.cs:494)
  → DiagnosticForkExtractor.Extract (FluentDslParser.cs:310)
  → TryParseDiagnosticFork PermitTrigger arm (DiagnosticForkExtractor.cs:138-151)
  → WorkflowDiagnostics.DuplicatePermittedForkTrigger
  → hasDuplicatePermittedForkTrigger (930-941) → return null model
```

Registration of the gate that blocks emission: `WorkflowIncrementalGenerator.cs:930-941`. Extractor rejects the edge (`return false` at `:177-179`) and does not call `DiagnosticForkModel.Create`. Duplicate detection is a local `HashSet` (`:119`), not `FindDuplicateTriggerNames`.

Observable effect: AGWF037 error, no saga. Descriptor id is `AgwfCodes.DuplicatePermittedForkTrigger` (`WorkflowDiagnostics.cs:611-612`, `AgwfCodes.g.cs:109`).

### 2b. JSON import — **reached**

```
BridgeImportFile → WireToModelBridge.Bridge
  → CollectImportRejections (WireToModelBridge.cs:130-133)
  → DiagnosticForkModel.FindDuplicateTriggerNames (469)
  → Diagnostic.Create(DuplicatePermittedForkTrigger) (486-491)
  → rejections.Count > 0 → BridgeResult(null, rejections) (131-133)
```

`MapDiagnosticForks` (`:916`) runs only after the rejection scan. `DiagnosticForkModel.Create` (`:125-131`) still throws on duplicates as a second floor if that scan is skipped.

### 2c. Catalog / Contracts 0.7.0 packaging — **reached as packed content**

- Authority: `AgwfCatalog.tsp:55` (`DuplicatePermittedForkTrigger: "AGWF037"`).
- Linked into the generator: `Strategos.Generators.csproj:33-34` compiles `AgwfCodes.g.cs`.
- Package version pin: `Strategos.Contracts.csproj:37-40` (`ContractsVersion` 0.7.0, `MinVerSkip`).
- Catalog packed at `contentFiles/any/any/diagnostics/` (`:68-73`).
- JSON Schema packed via `schemas/**/*.json` (`:59`), including `AgwfEntryDuplicatePermittedForkTrigger.json`.
- Pack test asserts nupkg name `LevelUp.Strategos.Contracts.0.7.0.nupkg` and schema/fixture/lib entries (`PackagingTests.cs:84-151`). It does **not** assert `agwf-catalog.json` inside the nupkg; catalog presence in the pack is declared by the csproj Content item. Catalog contents are asserted on the source tree (`AgwfCatalogEmitterTests.cs`).

A published `contracts-v0.7.0` tag is not created by this branch (stage0). Whether nuget.org already has 0.7.0 is out of this lens.

### 2d. Downstream of a clean model — **reached**

`SagaStepHandlersEmitter` constructs `DiagnosticForkHandlerEmitter` (`SagaStepHandlersEmitter.cs:41`) and always calls `EmitDecisionSiteHandler` (`:86`). That method no-ops when `!model.HasDiagnosticForks` (`DiagnosticForkHandlerEmitter.cs:80-83`). Comments on `DiagnosticForkExtractor` / `MapDiagnosticForks` that say saga lowering is “deferred (#151)” are stale relative to this call. AGWF037’s reject-before-Create is on the live lowering path.

---

## 3. OntologyServerToolFactory → `CallToolResult.resultType`

Public root: `AddOntologyTools` → `CreateServerTools` (`OntologyMcpServerBuilderExtensions.cs:33`, `:75`).

### 3a. `ontology_traverse` — **reached** (factory assigns)

`CreateServerTools` always appends `CreateTraverseTool` (`OntologyServerToolFactory.cs:91`). Handler returns `MapTraversalResult` / `ErrorResult`, both construct `CallToolResult` with `ResultType = CompletedResultType` (`"complete"`) at `:386` and `:412`.

### 3b. Four discovered tools — **reached via SDK wrap, not factory assignment**

`BuildHandler` (`:138-149`) returns typed results (`ExploreHandler` `:155`, `QueryHandler` `:159`, etc.). The factory never constructs `CallToolResult` for those names. Hosting pins `ModelContextProtocol` `VersionOverride="2.2.0"` (`Strategos.Ontology.MCP.Hosting.csproj:18-20`) because CPM is still `1.3.0` (`Directory.Packages.props:52`), and 1.3.0 has no `CallToolResult.ResultType` (comment at factory `:51-53`).

`ProviderBoundDispatchTests.cs:130-131` asserts `result.ResultType == CompletedResultType` on `ontology_query` through `AddMcpServer().AddOntologyTools` + a real client/server loop. That is the observable `tools/call` effect for the four tools, produced by the 2.2.0 SDK wrapper, not by a factory field set.

`Strategos.Agents.Mcp` references `ModelContextProtocol` **without** override (`Strategos.Agents.Mcp.csproj:21`) and therefore binds CPM 1.3.0. Different package; not this composition. Named so it is not mistaken for the Hosting pin.

Packaged: Hosting assembly ships `CreateServerTools` and both `AddOntologyTools` overloads (`Hosting.Tests/PackagingTests.cs:21-39`).

---

## 4. `OntologyToolDescriptor.Icons` → MCP `list/tools`

### 4a. Adapter wiring — **reached** (null path)

`CreateServerTool` (`:113`, used by `CreateServerTools` `:85`) calls `ApplyIcons(options, descriptor.Icons)` (`:130`). `ApplyIcons` (`:249-262`) returns immediately when `icons is null` (`:251-254`). That is the shipped invariant: do not invent a placeholder.

`OntologyToolDiscovery.Discover` (`:31-43`) builds four descriptors (`:48-116`). None set `Icons`. Default is null (`OntologyToolDescriptor.cs:43`).

`CreateTraverseTool` (`:293-311`) never calls `ApplyIcons`. Traverse has no icons slot.

Observable `list/tools` effect through `AddOntologyTools`: `ProtocolTool.Icons` unset on all five tools.

### 4b. Non-null icons → protocol Tool.icons — **unreached** through shipped public composition

The only production assignment of `Icons =` in this repo is tests (`OntologyServerToolFactoryTests.cs:80`, `OntologyToolDescriptorTests.cs:33`). The test that proves protocol mapping uses **internal** `CreateServerTool` with a hand-built descriptor (`OntologyServerToolFactoryTests.cs:83-84`), not `CreateServerTools` / `Discover`.

`CreateServerTools` is the only public factory. It does not accept a consumer-supplied descriptor list. A host can set `OntologyToolDescriptor.Icons` (public init, `PublicAPI.Unshipped.txt:126-127`) and nothing in the shipped composition reads that instance.

`ToolIcon` is packed on `LevelUp.Strategos.Ontology.MCP` (`PublicAPI.Unshipped.txt:194-203`). Presence of the type is not reachability.

---

## 5. OntologyBuilder / OntologyGraphBuilder → HandAuthoredContract / AONT205

### 5a. AONT205 on `Ingested` — **reached** (runtime)

```
IOntologyBuilder.ApplyDelta (OntologyBuilder.cs:195)
  → ValidateIngestedIntentInvariant (202, 207)
  → IngestedIntentInvariant.FindOffendingField (257)
  → if Source != Ingested return null (IngestedIntentInvariant.cs:22-25)
  → else OntologyCompositionException AONT205 (263-276)
```

Freeze-time second surface: `OntologyGraphBuilder.cs:485-504` uses the same `FindOffendingField`. `IsHandSide` treats `HandAuthored` **or** `HandAuthoredContract` as the hand lattice (`OntologyBuilder.cs:164-165`).

### 5b. `DescriptorSource.HandAuthoredContract = 2` assignment — **unreached** in shipped composition

Enum member exists (`DescriptorSource.cs:63`), PublicAPI (`PublicAPI.Unshipped.txt:328`). Default on `ObjectTypeDescriptor.Source` is `HandAuthored` (`ObjectTypeDescriptor.cs:99`).

No production `Source = DescriptorSource.HandAuthoredContract` in `src/` outside tests. This repo has **no** `IOntologySource` implementation except test doubles. `ObjectTypeFromDescriptor` preserves whatever `Source` the caller set (`IOntologyBuilder.cs:81-82`) but nothing in-repo sets `2`.

`MergeTwo.Merge` restamps the merged object to `Source = DescriptorSource.HandAuthored` (`MergeTwo.cs:67`). A `HandAuthoredContract` descriptor that later folds with `Ingested` loses the `2` at object level. Actions still come from the hand side (`:78`).

### 5c. Compile-time AONT205 analyzer — **unreached**

`OntologyDiagnostics.IngestedContributesToIntentOnly` is defined (`OntologyDiagnostics.cs:355-361`, id `AONT205` at `OntologyDiagnosticIds.cs:63`). `OntologyDefinitionAnalyzer.ReportDiagnostics` (`:1205`) never references that descriptor. The analyzer cannot fire AONT205. Runtime builder/freeze is the only reached AONT205.

---

## 6. `IActionBuilder<T>.Requires` obsolete — **reached** (still callable)

```
consumer Object<T> fluent
  → IActionBuilder<T>.Requires (IActionBuilderOfT.cs:39-40)
  → ActionBuilder<T>.Requires (ActionBuilderOfT.cs:77-90)
  → _preconditions.Add(...)
  → Build() copies Preconditions (172-184)
  → ActionDescriptor.Preconditions
```

`[Obsolete]` is on both interface and implementation. There is no fluent successor. `ActionDescriptor.Preconditions` is the documented first-class field (`ActionDescriptor.cs:27-33`).

Still on the shipping surface: `PublicAPI.Unshipped.txt:109`. Configuration that acknowledges the obsolete: `src/Directory.Build.targets:4` suppresses `CS0618` for test/benchmark projects so tests can still call it.

No in-repo production caller of `.Requires(` (only tests and a doc comment). That is expected for a library API. The method is not a dead adapter: `Build()` still materializes the list.

---

## 7. `renovate.json` — **target file exists**; bot resolution **indeterminate**

This repo’s file (`renovate.json:1-7`):

```json
"extends": [
  "local>lvlup-sw/.github:renovate.json",
  "local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json"
]
```

Base `4d060f4` second path was `local>lvlup-sw/lvlup-claude:renovate-config/presets/dotnet.json` (no `tools/`). HEAD added the `tools/` prefix (commit `334f64c`).

Confirmed with `gh api` (follows GitHub rename redirects):

- `GET /repos/lvlup-sw/.github/contents/renovate.json` → file exists.
- `GET /repos/lvlup-sw/lvlup-claude/contents/tools/renovate-config/presets/dotnet.json` → file exists (`sha` `96de5f1d60b08be583eb5955c3bc97f39eb349ad`).
- `GET /repos/lvlup-sw/lvlup-claude` returns identity `full_name: lvlup-sw/exarchos` (repo was renamed). Same path exists on `lvlup-sw/exarchos`.

The preset file is real. Whether Renovate’s `local>owner/repo` resolver follows the rename from `lvlup-claude` to `exarchos` is not observed in this repo. No Renovate log or applied-preset evidence was read. External process; inert-if-404 class remains open until a bot run is inspected.

---

## Findings the lens is responsible for

1. **JSON import never calls `TerminalReachabilityGuard`.** Shared `PhaseGraph` is used for `ValidTransitions` on that path; AGWF035 under-reach is not. File:line: `WorkflowIncrementalGenerator.cs:98-113` vs `:1038`.
2. **`OntologyToolDescriptor.Icons` non-null path is unwired.** `Discover` never sets it; public factory does not accept a consumer descriptor. `ApplyIcons` is live only for the null early-return. File:line: `OntologyToolDiscovery.cs:48-116`, `OntologyServerToolFactory.cs:130`, `:249-254`.
3. **`DescriptorSource.HandAuthoredContract` is never assigned by shipped code.** AONT205 retarget (skip unless `Ingested`) is live. Merge restamps object `Source` to `HandAuthored`. Compile-time AONT205 descriptor has no `ReportDiagnostic`. File:line: `IngestedIntentInvariant.cs:22`, `MergeTwo.cs:67`, `OntologyDiagnostics.cs:355` with no analyzer caller.
4. **Factory `ResultType` assignment covers only traverse `CallToolResult` constructions.** Four-tool `resultType` depends on the Hosting `VersionOverride` 2.2.0 SDK wrap. CPM 1.3.0 remains the default for any other project that does not override. File:line: `OntologyServerToolFactory.cs:386`, `:412`; `Hosting.csproj:20`; `Directory.Packages.props:52`.
5. **JSON import is suffix-convention + consumer `AdditionalFiles`.** No shipped MSBuild registration of `*.workflow.json`. File:line: `WorkflowIncrementalGenerator.cs:29`, `:245-246`.
6. **Generator capability is not on the `LevelUp.Strategos` metapackage graph.** AGWF035/037 only run where the consumer references `LevelUp.Strategos.Generators`. File:line: `Strategos.csproj` (no generator reference); `Strategos.Generators.csproj:18`, `:73`.

---

## Assumptions

- Roslyn loads the packed analyzer DLL as the generator; no second registration mechanism exists.
- MCP `list/tools` serializes `McpServerTool.ProtocolTool.Icons` (the Hosting test treats that as the protocol field).
- GitHub API rename redirect `lvlup-claude` → `exarchos` is the same object Renovate would fetch; that is an assumption, not a Renovate observation.

## Open questions

1. Does Renovate `local>lvlup-sw/lvlup-claude:...` resolve after the repo rename to `exarchos`, or does the bot 404 the old slug?
2. Do any in-production consumers declare `*.workflow.json` AdditionalFiles? If yes, AGWF035 under-reach is a hole on that front-end.
3. Is there an out-of-repo TypeSpec/JSON ontology ingest (Exarchos or similar) that is supposed to set `DescriptorSource.HandAuthoredContract = 2`? Nothing in this repo does.
4. Has `contracts-v0.7.0` been published? Source pin and pack test exist; a published tag was not verified.
5. Does MCP SDK 2.2.0 set `resultType: complete` for every non-`CallToolResult` handler return, or only when `UseStructuredContent` is true? Only `ontology_query` was observed in `ProviderBoundDispatchTests`.
