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

# Lens 5 — Existing Proof Inventory

What each surrounding check **asserts**, which ladder rung it sits on, whether it
binds this revision’s shipped composition, and what it lets through. A present
test is not a proof of the named claim.

Rungs (proof-ladder): **1** generation · **2** compiler/types · **3** structural
analysis · **4** contract/component tests · **5** production-path integration ·
**6** human judgment.

## What this lens read

- `verification/stage0.md`; `references/survey-lenses.md` §5; `proof-ladder.md`
- Changed `*Tests*` files in `4d060f4...324768f`
- Surrounding un-changed gates: `.github/workflows/{ci,contracts-codegen-guard,contracts-schema-diff,public-api-drift}.yml`, `scripts/check-builder-api-stability.sh`, `src/Directory.Build.{props,targets}`
- Production wiring: `WorkflowIncrementalGenerator` AGWF037 suppress, `TerminalReachabilityGuard`, `IngestedIntentInvariant`, `OntologyServerToolFactory` `CallToolResult` sites
- Skill corpus: `INV-3-mcp-first-class-latest-spec.md`, `deterministic-checks.md`
- Leads (not facts): issue #185, remainder plan, correctness-core spec, CHANGELOG Residue

`docs/{designs,plans,research}/2026-06-16-edge-*` are out of scope.

---

## S1. AGWF035 route arm + shared `PhaseGraph`

### P1. `TerminalReachabilityGuard.Report` called with injected classification / graph

- **Where:** `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs` `Report` at 792–807; positives at 357, 380, 456, 481
- **Asserts:** given a parsed `WorkflowModel` plus an *injected* `offMainFlowStepNames` and optional `PhaseGraph`, `TerminalReachabilityGuard.Report` emits `Id == "AGWF035"` (const at 46) and the message `Contains` named steps
- **Rung:** **4** (component). Tagged `[Property("Category", "Integration")]` at 42 — the category is a label, not a host run
- **Subject binding:** **no** for the under-reach positives. 456–474 and 481–498 call `PhaseGraph.Build(model).WithoutSuccessor(...)` then `Report(...)` directly. They do not run `WorkflowIncrementalGenerator`. They prove: *if* the graph lacks that edge, the guard fires. They do not prove the emitter would omit that edge, or that the generator would pass that graph
- **Can pass while claim is false:** yes. Unwire the guard from the generator, or stop passing the shared `PhaseGraph`, and these arms stay green. The class comments this at 598–600

### P2. Over-reach counterfactual (empty classification)

- **Where:** same file 380–392, 433–448
- **Asserts:** empty `offMainFlowStepNames` ⇒ AGWF035 names `IntakeClaim`/`AssessDamage`; `MainFlowClassification.For(model)` ⇒ silent
- **Rung:** **4**
- **Subject binding:** classification is the production type; the empty list is a test double, not a shipped composition
- **Lets through:** a generator that classifies correctly but never calls the guard

### P3. All-`Complete()` + `Finally` stays silent (one arm uses the real generator)

- **Where:** 508–523
- **Asserts:** `Report(...)` is silent **and** `GeneratorTestHelper.RunGenerator(AllCompleteBranchSource)` reports no AGWF035
- **Rung:** **4** (in-process generator driver, not a consumer pack)
- **Subject binding:** **yes** for the negative on this fixture. The generator is this revision’s `WorkflowIncrementalGenerator`
- **Lets through:** any other exclusive-path shape that should stay silent; any under-reach that this fixture does not contain

### P4. Rejoining constructs as emitted stay silent

- **Where:** 531–553
- **Asserts:** `Report` on fork / mixed branch / mixed loop-exit with `MainFlowClassification.For` is silent
- **Rung:** **4**
- **Subject binding:** **no** — `Report` direct, not the generator (except the all-Complete arm in P3)
- **Lets through:** a generator that never calls `Report`

### P5. Existing corpus never fires AGWF035

- **Where:** 566–590
- **Asserts:** every `const string` on `SourceTexts` (≥30 sources) run through `GeneratorTestHelper.RunGenerator` yields no `AGWF035`
- **Rung:** **4**
- **Subject binding:** **yes** — real generator, fixture corpus
- **Lets through:** the defect class this wave adds (under-reach / stripped Finally). The corpus is the *silent* half. A vacuous pass is guarded by `sources.Count >= 30` (574), not by a kill fixture. Comments at 604–611 state no consumer-visible under-reach workflow exists

### P6. Guard call-site source scan

- **Where:** 629–646; `GuardCallSitesAsync` 653–681
- **Asserts:** some `.cs` under `src/Strategos.Generators` (walk from `AppContext.BaseDirectory`, skip `bin`/`obj`) contains an invocation whose expression text is `TerminalReachabilityGuard.Report`, whose owning type name is `WorkflowIncrementalGenerator`, and whose **second argument** `ToString()` `Contains("MainFlowClassification")`
- **Rung:** **3** (structural analysis implemented as a test). Not rung 1 — it matches syntax text, not a generated artifact
- **Subject binding:** **partial**. Binds the generator *source* at this revision, not the compiled call graph
- **Can pass while claim is false:**
  - a commented-out call fails (trivia, 620–621) — good
  - `phaseGraph:` omitted or replaced with a private graph still passes: only `arguments[1]` is checked (671–676). Under-reach wiring is invisible to this proof
  - a call that passes `MainFlowClassification.For(otherModel)` as argument text still matches `Contains`
  - walk failure throws (703–704) — fail-closed if the tree is missing

### P7. Shared `PhaseGraph` / `MainFlowClassification` types

- **Where:** `src/Strategos.Generators/Models/PhaseGraph.cs` (class at 36; `WithoutSuccessor` at 120); `MainFlowClassification.cs`
- **Asserts (compiler):** `internal sealed` graph; tests compile against `WithoutSuccessor`
- **Rung:** **2** for shape; **no rung-4** dedicated `PhaseGraph` / `TransitionsEmitter` equivalence test found (`find` returned only the production files)
- **Lets through:** diagnostic graph and emitted `ValidTransitions` can drift. Stage 0 names that drift a published-API lie. No proof here asserts the two consumers see the same edges

### P8. AGWF035 catalog identity

- Covered under S2 catalog proofs (P16–P22). Those assert **id presence and metadata strings**, not route-analysis semantics

---

## S2. Contracts 0.7.0 + AGWF037

### P9. C# generator twin — duplicate `PermitTrigger`

- **Where:** `src/Strategos.Generators.Tests/Diagnostics/DuplicatePermittedForkTriggerTests.cs` 27–45
- **Asserts:** `GeneratorTestHelper.RunGenerator` on a workflow whose `AllowDiagnosticFork` chain has two `PermitTrigger(ForkTrigger.RatificationFailure, ...)` calls ⇒ some diagnostic `Id == "AGWF037"`, message `Contains("RatificationFailure")` and `Contains("duplicate-fork-trigger")`, and no generated tree path ending `Saga.g.cs`
- **Rung:** **4**
- **Subject binding:** **yes** for the C# authoring path through this revision’s generator. `ForkTrigger` is a **local test enum** (79–83), not a production type
- **Lets through:** a first-wins-dedup that still reports AGWF037 after dropping a schema (the test does not inspect retained evidence fields). Distinct-trigger negative is P10

### P10. C# generator twin — distinct triggers stay silent and emit a saga

- **Where:** same file 51–65
- **Asserts:** no AGWF037 **and** a `Saga.g.cs` tree exists
- **Rung:** **4**
- **Lets through:** wrong-id rejection (a different AGWF) would still satisfy “no AGWF037” + saga if the other diagnostic is non-error. The test does not assert the run is diagnostic-clean

### P11. Extractor unit — duplicate trigger

- **Where:** `src/Strategos.Generators.Tests/Helpers/DiagnosticForkExtractorTests.cs` 185–211
- **Asserts:** `DiagnosticForkExtractor.Extract(context, diagnostics)` on a syntax-only compilation (`typeof(object)` refs only, 296–300) returns empty models and exactly one `Id == "AGWF037"` whose message `Contains` trigger name and workflow name
- **Rung:** **4** (module public surface under controlled parse)
- **Subject binding:** **no** — not `WorkflowIncrementalGenerator`. Compilation has no Strategos references
- **Lets through:** generator path that ignores extractor diagnostics; import path (separate)

### P12. Extractor unit — distinct triggers; facade parity

- **Where:** 217–242; facade 253–287
- **Asserts:** one model, `PermittedTriggerCount == 2`, no AGWF037; `FluentDslParser.ExtractDiagnosticForkModels` matches extractor fields on a happy-path chain
- **Rung:** **4**
- **Lets through:** facade dropping the `diagnostics` list (parity test does not pass a diagnostic bag)

### P13. IR floor — `DiagnosticForkModel.Create` rejects duplicate trigger names

- **Where:** `src/Strategos.Generators.Tests/Models/DiagnosticForkModelTests.cs` 110–122; `FindDuplicateTriggerNames` 128–134
- **Asserts:** `Create` with two `RatificationFailure` triggers **throws `ArgumentException`**; `FindDuplicateTriggerNames` returns only the repeated name
- **Rung:** **4**
- **Subject binding:** **no**. This is the IR factory, not AGWF037. Production C#/import paths must convert this to a diagnostic; if they let `Create` throw, the generator can CS8785 instead of AGWF037 (the import file’s own comment at `ImportRejectionTests.cs` 207–212 describes that class for empty evidence)
- **Lets through:** a path that first-wins-dedups *before* `Create` and never throws

### P14. JSON import — AGWF037 + no saga

- **Where:** `src/Strategos.Generators.Tests/Import/ImportRejectionTests.cs` 440–448, 457–467
- **Asserts:** `WorkflowIncrementalGenerator` over `*.workflow.json` AdditionalText with two same-trigger / different-evidence entries ⇒ AGWF037, message contains `$.diagnosticForks[0].permittedTriggers[1]` and `RatificationFailure`, no `Saga.g.cs`. Distinct-trigger twin: no AGWF037 and a saga
- **Rung:** **4** (real generator, synthetic AdditionalFiles — not a packed analyzer on a consumer project)
- **Subject binding:** **yes** for the JSON-import composition of this revision
- **Lets through:** first-wins that still reports AGWF037 (schemas not re-read from IR). `EachRejectedCase_HasItsOwnDistinctDiagnosticId` (475–507) enumerates AGWF027–034 only — **AGWF037 is absent** from that uniqueness sweep

### P15. TypeSpec → generated catalog / enum / schema (generation)

- **Where:** `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp` (source); `Generated/AgwfCode.g.cs`, `AgwfCodes.g.cs`, `agwf-catalog.json`; `schemas/json-schema/AgwfEntryDuplicatePermittedForkTrigger.json`
- **Asserts (by construction):** one TypeSpec entry yields those artifacts **when codegen runs**
- **Rung:** **1**
- **Subject binding:** **yes** if `contracts-codegen-guard` actually ran on this change (path filter includes `src/Strategos.Contracts/**`). The artifacts are what Exarchos extracts
- **Lets through:** hand-edit of generated files if the guard is skipped (path filter miss, or required-check not configured — open)

### P16. `AgwfCatalogSchemaTests` — tsp compile + entry schemas

- **Where:** `src/Strategos.Contracts.Tests/Diagnostics/AgwfCatalogSchemaTests.cs` 43–74
- **Asserts:** `TspToolchain.CompileAsync` exit 0; exactly 31 `AgwfEntry*.json` files; each has `id`/`severity`/`summary`/`remediation`/`since` as JSON Schema `const`; the set of `id` consts equals the **hand-authored** `GroundTruthCodes` array (26–35), which includes `AGWF037`
- **Rung:** **4** sitting on a **1** pipeline. Asserts **identity and field presence**, not diagnostic behavior
- **Subject binding:** compiles this revision’s TypeSpec. Does not pack a nupkg
- **Lets through:** a catalog whose AGWF037 summary/remediation is wrong relative to `WorkflowDiagnostics` (that is P21); a live descriptor with no schema file (iterates files, not descriptors)

### P17. `AgwfCatalogEmitterTests` — committed `agwf-catalog.json`

- **Where:** `AgwfCatalogEmitterTests.cs` 40–75
- **Asserts:** committed `Generated/agwf-catalog.json` exists, has `catalog_version`, exactly 31 entries, each with six keys, ids positional-equal to the same hand-authored list
- **Rung:** **4**
- **Subject binding:** **reads the committed file**. Does **not** regenerate (no `TspToolchain` / codegen invoke)
- **Can pass while claim is false:** yes. Stale committed catalog that still lists the 31 codes passes even if `.tsp` diverged, **unless** P15’s CI guard ran. Comment at 12–13 says “after the full codegen pipeline runs”; the test does not run it

### P18. `AgwfCodeEnumTests` — generated enum wire map

- **Where:** `AgwfCodeEnumTests.cs` 62–95
- **Asserts:** `Strategos.Contracts.Generated.AgwfCode` has exactly 31 members; each `Expected` pair (including `DuplicatePermittedForkTrigger` → `"AGWF037"` at 54) JSON-round-trips via `ContractsJson.Options`
- **Rung:** **4** on a **1** artifact
- **Lets through:** a generator that never uses `AgwfCodes.DuplicatePermittedForkTrigger` (hand-authored literals are P20)

### P19. `AgwfMarkdownTests` — docs table rows

- **Where:** `AgwfMarkdownTests.cs` 34–71
- **Asserts:** `docs/diagnostics/agwf.md` exists; some line looks like an `id` header; 31 lines `Contains` a ground-truth code, in that list’s order
- **Rung:** **4**, assertion is a **substring**
- **Can pass while claim is false:** a footnote or prose mention of `AGWF037` counts as a “data row” (58–60). Method name still says `TenRows` (34). Does not assert column cells equal catalog fields

### P20. `AgwfSingleSourceTests` — no production `AGWF0xx` literals

- **Where:** `src/Strategos.Generators.Tests/Diagnostics/AgwfSingleSourceTests.cs` 38–68
- **Asserts:** no `src/Strategos*` production `.cs` (exclude `*.Tests`, `Generated/`, `bin/`, `obj/`) matches regex `AGWF0[0-9]{2}`
- **Rung:** **3**
- **Subject binding:** this revision’s source tree as found from `AppContext.BaseDirectory`
- **Lets through:** `AGWF100+`; codes in non-`Strategos*` dirs; test projects (explicit). Regex matches `AGWF037`

### P21. `AgwfCatalogParityTests` — catalog strings vs `WorkflowDiagnostics`

- **Where:** `AgwfCatalogParityTests.cs` 38–77
- **Asserts:** for **each catalog entry**, a `WorkflowDiagnostics` `DiagnosticDescriptor` exists with the same `Id`, and `severity` / `Title` / `MessageFormat` equal catalog `severity` / `summary` / `remediation`
- **Rung:** **3** (reflection over committed JSON + static fields). Comment at 34 still says “all 10 codes”
- **Subject binding:** committed catalog + this revision’s `WorkflowDiagnostics` (AGWF037 field at `WorkflowDiagnostics.cs` 611–612)
- **Lets through:** a descriptor with **no** catalog entry (map is catalog→descriptor only); semantic lie if `MessageFormat` and catalog `remediation` are forced equal but the runtime message is built differently; does not fire the diagnostic

### P22. `AgwfCodegenGuardTests` + CI codegen-guard

- **Where:** `AgwfCodegenGuardTests.cs` 27–43 (YAML substring); 51+ (hand-edit vs regen); `.github/workflows/contracts-codegen-guard.yml` 48–60
- **Asserts (test):** workflow file `Contains` `"docs/diagnostics"` and `"Generated"`; a local severity flip of committed catalog ≠ codegen output
- **Asserts (CI job):** `scripts/contracts-codegen.sh` then `git diff --exit-code` on `Generated/`, `schemas/`, `docs/diagnostics`
- **Rung:** CI job is **1+3**. The YAML-substring test is **4** asserting a **comment/path token**, not that the job ran
- **Can pass while claim is false:**
  - YAML test passes if those strings appear in a comment
  - CI job is **path-filtered** (contracts / codegen / `docs/diagnostics` / script). A generator-only AGWF037 wiring change does not run it
  - `npm ci || npm install` (46) can succeed on a drifted lockfile

### P23. `contracts-schema-diff.yml`

- **Where:** `.github/workflows/contracts-schema-diff.yml` 37–62
- **Asserts:** when `have_prev == true`, `scripts/contracts-schema-diff.mjs` classifies HEAD schemas vs previous `v*` tag as additive-only
- **Rung:** **3**
- **Can pass while claim is false:** `have_prev=false` (no `v*` tag, or tag predates `schemas/`) **skips** the diff step (58) and the job succeeds. Path filter is `schemas/**` only — a `.tsp` change without committed schema files never starts the job. This is the skip-and-pass shape

### P24. `PackagingTests` (Contracts)

- **Where:** `src/Strategos.Contracts.Tests/PackagingTests.cs`
- **Asserts:**
  - 25–30: `typeof(ContractsMarker).Assembly.GetName().Name == "Strategos.Contracts"`
  - 39–64: `dotnet pack` produces a nupkg with **some** `contentFiles/any/any/schemas/*.json` (`IsNotEmpty`)
  - 84–157: packed file name is exactly `LevelUp.Strategos.Contracts.0.7.0.nupkg`; nuspec contains `<version>0.7.0</version>`; **named** schemas include `SdlcEventEnvelope`, `WorkflowDefinitionV1`, `InvariantEntry` — **not** `AgwfEntryDuplicatePermittedForkTrigger.json` or `agwf-catalog.json`; ≥100 builder fixtures; `lib/**/Strategos.Contracts.dll`
- **Rung:** **5** for pack contents; **4** for assembly name
- **Subject binding:** **yes** for this revision’s pack command
- **Lets through:** 0.7.0 nupkg missing the AGWF037 schema/catalog files. `Nupkg_Contains_SchemasUnderContentFiles` is satisfied by any one schema

### P25. CI `contracts-test` vs skipped `*Contracts.Tests*`

- **Where:** `.github/workflows/ci.yml` 21–23 skip-patterns; 43–79 `contracts-test`
- **Asserts:** main `build-test` reusable workflow does **not** run `Strategos.Contracts.Tests`; a sibling job `dotnet run`s that project after Node 24 + `npm ci || npm install`
- **Rung:** pipeline structure, not a claim proof
- **Can appear to run while excluded:** the suite is excluded from the job named like the default test gate and run elsewhere. If `contracts-test` is not a required check, P16–P19/P24 never gate merge. **Open:** branch-protection required checks

---

## S3. MCP `resultType` + `Icons`

### P26. Traversal hosting — wire `resultType`

- **Where:** `src/Strategos.Ontology.MCP.Hosting.Tests/TraversalToolHostingTests.cs` 98–113, used at 134/173/208/242
- **Asserts:** in-process `McpServer` + `McpClient` over paired streams; `CallToolResult.ResultType == OntologyServerToolFactory.CompletedResultType`; `JsonSerializer.Serialize(..., McpJsonUtilities.DefaultOptions)` `Contains("\"resultType\":\"complete\"")`; deserialize round-trip keeps that value. Also `_meta` on structured content (136–137)
- **Rung:** **5** (real SDK client/server, in-process transport — not a packed nupkg, not a foreign MCP client)
- **Subject binding:** **yes** for `ontology_traverse` via `AddOntologyTools` / factory of this revision, **if** tests pin the same SDK as Hosting (`VersionOverride="2.2.0"` in both csprojs)
- **Lets through:** `ontology_query` / `ontology_action` / `ontology_explore` / abstain / error paths that skip this helper. `ProviderBoundDispatchTests` is the query cousin (P27) and does **not** serialize

### P27. Provider-bound query — in-memory `ResultType`

- **Where:** `ProviderBoundDispatchTests.cs` 130–131
- **Asserts:** `result.ResultType` equals `CompletedResultType` after `CallToolAsync("ontology_query", ...)`
- **Rung:** **5** transport, **4** assertion (object property, no JSON)
- **Lets through:** SDK defaulting `ResultType` in-memory while omitting it on the wire (P26 is the one that serializes)

### P28. Factory — discovery icons stay null; explicit icons map

- **Where:** `OntologyServerToolFactoryTests.cs` 59–61, 66–91
- **Asserts:** discovery-derived `OntologyToolDescriptor.Icons` and `ProtocolTool.Icons` are null; a hand-built descriptor with `Icons = [icon]` maps `Source`/`MimeType`/`Theme`/`Sizes` onto `ProtocolTool.Icons`
- **Rung:** **4**
- **Subject binding:** **yes** for `CreateServerTools` / `CreateServerTool` on a test graph
- **Lets through:** discovery inventing icons on a tool **not** in `descriptors` (factory also registers `ontology_traverse`; that tool is filtered out at 24–26). Null-when-unset for traverse is unasserted here

### P29. `OntologyToolDescriptor` record store

- **Where:** `src/Strategos.Ontology.MCP.Tests/OntologyToolDescriptorTests.cs` 8–51
- **Asserts:** two-arg ctor leaves `Icons` null; `with { Icons = [icon] }` stores fields; setting `Title` only leaves `Icons` null
- **Rung:** **4** (and for the `with` arm, **2** would already make the property exist)
- **Can pass while claim is false:** yes for “discovery does not invent a placeholder.” This constructs the record in the test. It does not run discovery or the factory

### P30. Hosting packaging / sealed-type guard

- **Where:** `Hosting.Tests/PackagingTests.cs` 16–68; `HostingInvariantGuardTests.cs` 19–34
- **Asserts:** factory + two `AddOntologyTools` overloads exist; assembly references some name containing `ModelContextProtocol` and `Strategos.Ontology.MCP`. All non-compiler-generated concrete types in the Hosting assembly are `sealed`
- **Rung:** **4** / **3**
- **Lets through:** Hosting still on CPM `1.3.0` (no `ResultType`) if tests’ own `VersionOverride="2.2.0"` (`Hosting.Tests.csproj` 11) is what makes `CallToolResult.ResultType` compile. **No test asserts the production csproj pin is 2.2.0.** `Directory.Packages.props` still has `ModelContextProtocol` **1.3.0**; Hosting overrides at `Strategos.Ontology.MCP.Hosting.csproj` 20

### P31. PublicAPI analyzers (MCP / Hosting)

- **Where:** `PublicAPI.Unshipped.txt` lists `OntologyToolDescriptor.Icons` (126–127) and `ToolIcon` (194–203); analyzers referenced in those csprojs
- **Asserts:** public surface membership matches the baseline files (RS0016/RS0017)
- **Rung:** **2** if warnings fail the build; **6** if they are warnings. `Directory.Build.props` has **no** `TreatWarningsAsErrors`. The fail-closed script `scripts/check-builder-api-stability.sh` builds **only** `src/Strategos/Strategos.csproj` (20) with `/warnaserror`
- **Lets through:** Ontology/MCP/Hosting PublicAPI drift on a green `builder-api-stability` job. `public-api-drift.yml` tracks the **seven** `Strategos.Builders` entrypoints only (lines 21–23), on **push to main**, and **fail-soft** on a missing PAT (80–84)

### P32. INV-3 skill greps (not CI)

- **Where:** `.agents/skills/strategos-design-invariants/references/deterministic-checks.md` Check 3.4 (112–124), 3.5 (126–133); INV-3 spec acceptance questions
- **Asserts (if someone runs them):** every Hosting file that matches `CallToolResult` also contains the substring `ResultType`; `OntologyToolDescriptor.cs` contains the substring `Icons`
- **Rung:** **6** (human checklist). The grep shape is **3** but it is **not a pipeline job**
- **Can pass while claim is false:** file-level `grep -L ResultType` — a comment or unused identifier satisfies. A new `new CallToolResult` that omits `ResultType` in a file that already mentions it passes. Check 3.5 does not assert null-when-unset

### P33. Factory constructions (compiler, this revision)

- **Where:** `OntologyServerToolFactory.cs` 384–386 (`new CallToolResult { ResultType = ... }`) and 410–412 (`new() { ResultType = ... }` error path)
- **Asserts:** those two object initializers assign `ResultType` or the project does not compile against 2.2.0
- **Rung:** **2** for the property existing; assignment is not exhaustive over future sites
- **Lets through:** a third construction site; CPM 1.3.0 if the override is dropped (tests still override 2.2.0)

---

## S4. `DescriptorSource.HandAuthoredContract` + AONT205 retarget

### P34. Enum ordinal tests

- **Where:** `src/Strategos.Ontology.Tests/Descriptors/DescriptorSourceTests.cs` 8–33
- **Asserts:** `default == HandAuthored`; `(int)HandAuthored == 0`; `Ingested == 1`; `HandAuthoredContract == 2`
- **Rung:** **4** restating a **2** fact (`public enum` at `DescriptorSource.cs` 47–70)
- **Lets through:** AONT205 still treating `2` as ingested; merge dropping contract actions. Ordinals ≠ retarget

### P35. `IngestedIntentInvariant` + `ApplyDelta`

- **Where:** production `src/Strategos.Ontology/Internal/IngestedIntentInvariant.cs` 18–52 (`Source != Ingested` ⇒ null); tests `IOntologyBuilderInvariantTests.cs` 30–362
- **Asserts:** `ApplyDelta(Add/Update ObjectType)` with `Source == Ingested` and a populated intent field throws `OntologyCompositionException` containing `Id == "AONT205"` and a message `Contains` that field name. `HandAuthored` and `HandAuthoredContract` with Actions do **not** throw; contract case also asserts `Actions[0].Name == "Trade"` (229)
- **Rung:** **4**
- **Subject binding:** **yes** for `OntologyBuilder.ApplyDelta` of this revision
- **Lets through:** `ObjectTypeFromDescriptor` bypass (that is P36); analyzer-only consumers that never call `ApplyDelta`

### P36. Graph-freeze AONT205 + contract negative

- **Where:** `AONT205Tests.cs` class `AONT205GraphFreezeTests` 25–146
- **Asserts:** hand-fed `ObjectTypeFromDescriptor` ingested+intent ⇒ `Build()` throws AONT205 with domain/type or field name; `HandAuthoredContract` + Actions ⇒ graph builds, `Source == HandAuthoredContract`, one `Trade` action
- **Rung:** **4**
- **Subject binding:** **yes** for `OntologyGraphBuilder.Build` freeze scan. **No** for TypeSpec/JSON contract ingest — descriptors are constructed in the test
- **Lets through:** a real contracts-ingest pipeline that tags `Ingested` instead of `HandAuthoredContract`

### P37. Merge: contract action survives ingested structure

- **Where:** `HandAuthoredContractMergeTests.cs` 51–88, 91–134
- **Asserts:** `AddDomain<ContractPositionOntology>` + ingested `AddObjectType` with `Properties` only ⇒ `Actions` still `Trade`, `Properties` has `Symbol`, and **`position.Source == DescriptorSource.HandAuthored`** (87) — not `HandAuthoredContract`. Ingested Actions alone still AONT205
- **Rung:** **4**
- **Subject binding:** **yes** for `OntologyGraphBuilder` merge of this revision
- **Lets through / note:** the surviving-action claim is tested. The provenance claim “stays HandAuthoredContract” is **not** what 87 asserts. If CHANGELOG/docs claim merge preserves `HandAuthoredContract`, this proof is at the **wrong rung/subject** for that claim (it would pass while that claim is false)

### P38. AONT205 Roslyn descriptor (unwired)

- **Where:** `OntologyDiagnosticIds.cs` 63; `OntologyDiagnostics.cs` 355–361 (`IngestedContributesToIntentOnly`)
- **Asserts:** a `DiagnosticDescriptor` with id `AONT205` **exists**
- **Rung:** none as a proof of behavior. `rg` over `src` at this revision found **no report site** — only the two definition files
- **Can pass while claim is false:** the analyzer can ship an inert AONT205 id while only the runtime invariant enforces. No `Strategos.Ontology.Generators.Tests` AONT205 case found

### P39. PublicAPI lists `HandAuthoredContract = 2`

- **Where:** `src/Strategos.Ontology/PublicAPI.Unshipped.txt` 328
- **Asserts:** the enum member is declared on the public surface (RS0016 if added without the line — same warn-vs-error caveat as P31)
- **Rung:** **2** (membership), not AONT205 semantics

---

## S5. `IActionBuilder<T>.Requires` obsolete

### P40. Reflection on `[Obsolete]`

- **Where:** `IActionBuilderTests.cs` 65–76
- **Asserts:** `IActionBuilder<>.Requires` has `ObsoleteAttribute`; `Message` `Contains("Preconditions")` and `Contains("no fluent successor")` (ignore case)
- **Rung:** **4** checking a **2** attribute (`IActionBuilderOfT.cs` 39)
- **Subject binding:** **yes** for the interface metadata of this revision
- **Lets through:** consumers compiling with CS0618 suppressed; implementation without the attribute; `RequiresSoft` / `RequiresLink` still current (42–46 have no `[Obsolete]`)

### P41. NSubstitute fluent-return tests

- **Where:** `IActionBuilderTests.cs` 9–62
- **Asserts:** `Substitute.For<IActionBuilder>()` configured with `.Returns(substitute)` returns that substitute
- **Rung:** **4** shape, **wrong subject**
- **Can pass while claim is false:** **yes**. These tests pass whatever the mock is told to do. They do not touch `ActionBuilder` / `Object<T>`

### P42. Existing `Requires` lowering tests (unchanged callers)

- **Where:** e.g. `ActionBuilderOfTTests.cs`, `ConstraintStrengthTests.cs`, `ObjectTypeBuilderGenericTests.cs` still call `.Requires(...)`
- **Asserts:** preconditions still lower onto `ActionDescriptor` (pre-existing component tests)
- **Rung:** **4**
- **Subject binding:** **yes** for lowering
- **Lets through:** CS0618 as a consumer-visible warning. `src/Directory.Build.targets` 3–5 sets `NoWarn` **CS0618** on **every** test/benchmark project, so in-repo tests cannot fail on the obsolete warning. Comment says this is so tests can still exercise Preconditions lowering

### P43. PublicAPI still ships `Requires`

- **Where:** `PublicAPI.Unshipped.txt` 109
- **Asserts:** the method remains public API (obsolete is not a remove)
- **Rung:** **2** membership. `builder-api-stability` does **not** include `IActionBuilder<T>` (workflow builders only)

---

## S6. Renovate preset path

### P44. None found

- **Where:** `renovate.json` 3–6 — second `extends` token `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`
- **Asserts:** nothing in this repo asserts the remote preset exists, is applied, or differs from the old path
- **Rung:** **absent** (would be external-process **5** or human **6**)
- **Can pass while claim is false:** any green CI. This is the #181 inert-if-404 class. Stage 0 already ranks it as a control that looks present

---

## S7. Docs, CHANGELOG, INV-3 skill — proofs or prose?

| Artifact | What it asserts | Rung | Proof? |
|---|---|---|---|
| `CHANGELOG.md` Residue (#185) | narrative of all tracks | **6** | **No** — claim corpus for Stage 2 |
| `docs/diagnostics/agwf.md` | generated table | **1** artifact; P19 is a substring test | identity only |
| INV-3 spec (`INV-3-mcp-first-class-latest-spec.md`) | acceptance questions + file pointers | **6** | **No** |
| `deterministic-checks.md` 3.4 / 3.5 / 5.1 / 5.1b | grep recipes | **6** unless executed as a gate | **Not in CI**. 5.1/5.1b overlap P20 |
| Ontology / platform guide edits | CLR-free XOR, first-class descriptor path | **6** | **No** |
| Remainder plan / issue 185 / correctness-core spec | intended tests and deferred work | **6** leads | **No** |

---

## Pipeline gates that surround the scope (not product tests)

| Gate | Asserts | Rung | Skip / wrong-subject / appear-to-run |
|---|---|---|---|
| `ci.yml` `build-test` | `src/*Tests/*.csproj` except `*Benchmarks.Tests*` and `*Contracts.Tests*` | **4** runner | Contracts suite **excluded here**, run in `contracts-test` |
| `ci.yml` `contracts-test` | full Contracts.Tests exe | **4** | `npm ci \|\| npm install`; required-check **open** |
| `ci.yml` `coverage-gate` | aggregate ≥80% on **PRs** only; filters `-*Tests;…` | coverage, not behavior | push-to-main skips; new untested path can stay under 80% |
| `ci.yml` `pack-verify` | consumer resolves `IPhaseAwareSaga` from packed Generators | **5** | **wrong subject** for this wave |
| `ci.yml` `basileus-smoke` | Agents consumed surface | **5** | **wrong subject** |
| `ci.yml` `quality-gates` | AGAG / catch / prose scripts | **3** | **wrong subject** |
| `ci.yml` `builder-api-stability` | RS0016/17 on **7** workflow builder interfaces | **2** | Ontology `Requires` / `DescriptorSource` / MCP Icons **out of scope** |
| `ci.yml` `dbsf-parity-guard` | Qdrant oracle | **4** | **wrong subject** |
| `contracts-codegen-guard.yml` | regen == committed Generated/schemas/docs/diagnostics | **1+3** | path-filtered; not on generator-only diffs |
| `contracts-schema-diff.yml` | breaking schema vs previous `v*` | **3** | **skips and succeeds** when `have_prev=false`; path `schemas/**` only |
| `public-api-drift.yml` | notify Exarchos if 7 builder shipped lines changed | notify | **push to main only**; fail-soft on PAT; mocked-gh job asserts **script flags**, not live Exarchos |
| `publish-contracts.yml` | publish on `contracts-v*` tag | release | does not prove this branch published 0.7.0 (Stage 0: tag not created by this branch) |
| `Directory.Build.props` Coverlet `Threshold` 80 | per-project coverage during test | metric | can fail a local test run; not a semantic proof |
| `Directory.Build.targets` CS0618 `NoWarn` | tests compile despite `[Obsolete]` | anti-proof for CS0618 | obsolete warning **cannot** fail in-repo tests |

---

## Types / generators / contracts that *are* the mechanism (not the proof)

- **Generator (shipped):** `WorkflowIncrementalGenerator` registers AGWF037 as a codegen-suppressing error (930–938) and still hosts `TerminalReachabilityGuard` (P6 scans this).
- **Generated contract:** `AgwfCodes.DuplicatePermittedForkTrigger` is what production diagnostics must use (P20).
- **Runtime invariant:** `IngestedIntentInvariant.FindOffendingField` is the AONT205 retarget (P35–P37). The Roslyn `AONT205` descriptor is not a second enforcement site (P38).
- **SDK pin:** Hosting `VersionOverride="2.2.0"` is what makes `CallToolResult.ResultType` representable (P30/P33). CPM remains 1.3.0.

---

## Wrong-rung / “passes whatever the named behavior does” (this lens’s job)

1. **Under-reach AGWF035** is claimed as a compile-time lock on the next dropped Finally edge. Existing positives inject `WithoutSuccessor` (P1). That is a proof that the *guard function* fires, not that the *shipped generator composition* would see a dropped edge. Rung 4 on a test double; the claim wants the generator (still 4) or a structural lock that the diagnostic graph **is** the emitted `ValidTransitions` graph (3 or 1).
2. **P6** is a source substring for `MainFlowClassification`, not for `phaseGraph`. Under-reach can be unwired and stay green.
3. **P41** NSubstitute tests pass by construction.
4. **P29** “does not invent placeholder” asserts a record the test built.
5. **P17 / P19 / P22 YAML / P32** assert file/substring/comment presence.
6. **P23 / coverage-gate / public-api-drift / contracts skip** can skip, warn, or run a different subject and still look like a pass.
7. **P37** asserts merge `Source == HandAuthored`. If the claim is “provenance stays HandAuthoredContract”, the proof is at the wrong subject.
8. **P34 / P39 / P43** are enum/API membership. They do not prove AONT205 retarget or obsolete consumer signal.
9. **INV-3 / deterministic-checks / CHANGELOG / plan / issue 185** are **prose**. They seed claims. They are not proofs.

---

## Assumptions

- `GeneratorTestHelper.RunGenerator` drives this revision’s `WorkflowIncrementalGenerator` in-process (same helper the corpus and AGWF037 C# twin use). Not re-read line-by-line this pass.
- Branch protection / required-check names are **not** in-repo. `contracts-test` and `contracts-codegen-guard` may or may not be merge-required.
- `Lvlup.Build` might elevate RS0016; `Directory.Build.props` itself does not set `TreatWarningsAsErrors`.
- No `PhaseGraph` ↔ `ValidTransitions` test was missed under another name; `find` showed only production files.

## Open questions

1. Is `contracts-test` a required GitHub check? If not, P16–P19/P24 can be skipped at merge.
2. Does merge **intend** to collapse `HandAuthoredContract` → `HandAuthored` (P37:87)? Claim vs proof split.
3. Is there an AONT205 analyzer report site this search missed (generated, or different symbol)?
4. Are error/abstain/explore/action `CallToolResult` paths covered for **wire** `resultType`, or only traverse (P26) + query in-memory (P27)?
5. Does any out-of-repo Exarchos test bind `agwf-catalog.json` 0.7.0 / AGWF037? Not in this repo.
6. Does the Renovate GitHub App resolve `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`? No in-repo proof.
7. Was a `contracts-v0.7.0` tag cut? Publish workflow does not run on this branch.
