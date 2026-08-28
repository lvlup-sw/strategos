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

# Survey lens 1 — Mechanism (diff form)

Structural changes in `4d060f4...324768f`. Ranked by the stage0 scope set. Descriptions
and CHANGELOG sentences are leads, not behavior.

No new concurrency was introduced on any ranked surface.

## Findings

### S1. AGWF035 route arm + shared PhaseGraph

1. **Nested `PhaseGraph` / `EdgeBuilder` moved, not rewritten.** The private nested types
   were deleted from `TransitionsEmitter` and reappeared as
   `src/Strategos.Generators/Models/PhaseGraph.cs:36`. A body-to-body compare of
   `EdgeBuilder` against `4d060f4` is identical. `Build` at
   `PhaseGraph.cs:67` adds only `ThrowHelper.ThrowIfNull` and `successors[stepName] = []`
   (was `new List<string>()`). Edge-construction order is unchanged
   (fork → branch → loop → failure → approval → confidence-chain → main-flow →
   confidence-gate → failed) at `PhaseGraph.cs:82-90`.

2. **Visibility widened on the terminal phase names.**
   `CompletedPhase` / `FailedPhase` were `private const` on `TransitionsEmitter`.
   They are now `internal const` on the shared type (`PhaseGraph.cs:41-46`).
   The emitter reads them at `TransitionsEmitter.cs:84-85`.

3. **New test-only mutator, not a production shared instance.**
   `PhaseGraph.WithoutSuccessor` at `PhaseGraph.cs:120` copies the successor map and
   `RemoveAll`s one edge. The production generator call at
   `WorkflowIncrementalGenerator.cs:1038-1043` does **not** pass a graph.
   The guard then does `phaseGraph ?? PhaseGraph.Build(model)` at
   `TerminalReachabilityGuard.cs:127`. The emitter independently calls
   `PhaseGraph.Build(model)` at `TransitionsEmitter.cs:56`. Shared type and algorithm;
   two builds; no shared instance.

4. **`TerminalReachabilityGuard.Report` signature widened.** New optional parameter
   `PhaseGraph? phaseGraph = null` at `TerminalReachabilityGuard.cs:67`. After the
   existing over-reach loop, a new branch at `:118-128` runs under-reach iff
   `declaredTerminalStepName is not null`.

5. **New under-reach error path reuses AGWF035 / the same three-argument descriptor.**
   `ReportUnderReach` at `TerminalReachabilityGuard.cs:143` calls the same `Report`
   helper (`:412`) with `(declaredTerminal, workflow, lastStep)`. Over-reach still
   passes `(stepWithWrongSuccessor, workflow, successor)`.
   `WorkflowDiagnostics.UnreachableTermination.messageFormat` at
   `WorkflowDiagnostics.cs:564` is **unchanged** and still describes over-reach
   ("chains to '{2}', which is not on the workflow's main flow"). Under-reach
   therefore emits that sentence with inverted argument roles.

6. **Dedup key on the shared `reported` set can drop a second pair.**
   `Report` keys on `$"{stepName}\u001f{successorStepName}"` at
   `TerminalReachabilityGuard.cs:420`. Over-reach runs first. Two under-reach
   dispatchers that miss the same terminal share argument 0, so they only collide
   if argument 2 also matches. Distinct last-steps still both report.

7. **New exclusion branch narrows the new arm.**
   `CollectConstructDispatchers` at `TerminalReachabilityGuard.cs:363` removes fork
   predecessors, branch predecessors, and loop-exit last-body steps from the linear
   predecessor walk (`AddLinearPredecessor` at `:337`). That is an added skip, not a
   removed check: those shapes stay silent by construction.

8. **AGWF035 still does not join the no-generation gate.**
   `hasErrors` at `WorkflowIncrementalGenerator.cs:933-938` is evaluated **before**
   the model is built. `TerminalReachabilityGuard.Report` runs **after** the model
   exists (`:1038`). The new under-reach arm inherits that placement: Error
   diagnostic, saga still emitted. This is not a deletion of a gate; no gate was
   added for the new arm either.

### S2. Contracts 0.7.0 + AGWF037

9. **Catalog / published contract widened.** `AgwfCatalog.tsp` count comment 30 → 31;
   new member `DuplicatePermittedForkTrigger: "AGWF037"` appended (existing names
   and ordinals unchanged). Generated `AgwfCode.g.cs:143-145` and
   `AgwfCodes.g.cs:108-109` gain the member. `Strategos.Contracts.csproj`
   `ContractsVersion` 0.6.0 → 0.7.0. New JSON Schema file
   `AgwfEntryDuplicatePermittedForkTrigger.json`. Packaging test retargeted
   0.6.0 → 0.7.0 (filename and nuspec assertions replaced, not deleted).

10. **New reject path on C# extract; the edge produces no model.**
    `DiagnosticForkExtractor.Extract` signature widened
    (`ICollection<Diagnostic>? diagnostics = null`). On a second
    `PermitTrigger` of the same name it records AGWF037
    (`DiagnosticForkExtractor.cs:142-151`) and later `return false`
    (`:177-180`) so `Create` is not called. `FluentDslParser.ExtractDiagnosticForkModels`
    (`FluentDslParser.cs:299-310`) now takes optional `diagnostics` + `workflowName`
    and passes `workflowName` into `FluentDslParseContext.Create` (was `null`).
    That is new diagnostic I/O on a path that previously only returned models.

11. **New throw inside `DiagnosticForkModel.Create`.**
    At `DiagnosticForkModel.cs:125-132`, duplicate trigger names now throw
    `ArgumentException`. At `4d060f4`, `Create` accepted the list. There was
    **no first-wins-dedup to delete**: the old extractor appended every parsed
    trigger and `Create` stored them. The comments that say "do not
    first-wins-dedup" describe the new reject, not a removed algorithm.

12. **`FindDuplicateTriggerNames` ignores empty names**
    (`DiagnosticForkModel.cs:158-161`). That is a hole in the new constraint,
    not a loosened old one.

13. **JSON import: new rejection before map; map still calls `Create`.**
    `CollectImportRejections` (`WireToModelBridge.cs:130-134`) already returned
    `BridgeResult(null, rejections)` with no saga. The new loop at `:455-493`
    adds AGWF037 using `FindDuplicateTriggerNames`. `MapDiagnosticForks`
    (`:214`) is unchanged and still calls `Create` — reachable only when
    the new rejection did not fire.

14. **AGWF037 *does* join the C# no-generation gate.**
    New `hasDuplicatePermittedForkTrigger` at
    `WorkflowIncrementalGenerator.cs:930-938` is OR-ed into `hasErrors`.
    That is a new generation-suppression branch. Asymmetry with finding 8:
    AGWF037 blocks the model; AGWF035 (both arms) does not.

### S3. MCP `resultType` + `Icons`

15. **New output on both `CallToolResult` construction sites.**
    `OntologyServerToolFactory.cs:386` (success) and `:412` (error) now set
    `ResultType = CompletedResultType` (`"complete"`, `:57`). Those objects
    previously had no `ResultType`. No third construction site exists in the
    Hosting factory.

16. **New optional input on the tool-create path.**
    `OntologyToolDescriptor.Icons` (`OntologyToolDescriptor.cs`,
    `IReadOnlyList<ToolIcon>?`, default unset/null). `ApplyIcons` at
    `OntologyServerToolFactory.cs:249` no-ops when `icons is null`; otherwise
    writes `options.Icons`. `CreateServerTool` visibility `private` →
    `internal` (`:113`). New public type `ToolIcon` (`ToolIcon.cs:12`).

17. **Changed package default.** Hosting
    `ModelContextProtocol` CPM pin overridden to `2.2.0`. Hosting tests also
    override `ModelContextProtocol` to `2.2.0` and
    `Microsoft.Extensions.Logging.Abstractions` to `10.0.10`.

### S4. `DescriptorSource.HandAuthoredContract` + AONT205

18. **Enum widened; `Ingested` stays 1.**
    `DescriptorSource.HandAuthoredContract = 2` at `DescriptorSource.cs:63`.
    PublicAPI unshipped adds the member. Default remains `HandAuthored`.

19. **Merge eligibility widened; merge *output* narrowed.**
    `OntologyBuilder.TryCrossProvenanceMerge` now uses `IsHandSide`
    (`OntologyBuilder.cs:164-165`) so `HandAuthoredContract` folds against
    `Ingested`. `MergeTwo.Merge` still assigns
    `Source = DescriptorSource.HandAuthored` (`MergeTwo.cs:67`). A contract-
    authored hand side that merges exits as `HandAuthored` (0).

20. **AONT205 scan tightened (two new fields).**
    Inline Actions/Events/Lifecycle scan deleted from
    `OntologyBuilder` and `OntologyGraphBuilder` and replaced by
    `IngestedIntentInvariant.FindOffendingField` (`IngestedIntentInvariant.cs:18`).
    The replacement still returns early unless `Source == Ingested` (`:22`),
    then adds `InterfaceActionMappings` (`:41`) and
    `ExternalLinkExtensionPoints` (`:46`). Same diagnostic id; more fields
    can fail the build. `HandAuthoredContract` is exempt because it is not
    `Ingested`.

21. **Existing `== HandAuthored` branches were not widened.**
    Unchanged sites still treat value `2` as not-hand:
    - `OntologyGraphBuilder.cs:330` — AONT201 skip unless
      `property.Source == HandAuthored`
    - `OntologyGraphBuilder.cs:409` — AONT203 hand-name set is
      `Source == HandAuthored` only
    - `OntologyGraphBuilder.cs:566` — AONT204 reference walk skips any
      descriptor whose `Source != HandAuthored`

    `IOntologyBuilder` signatures are unchanged (comment-only).

### S5. `IActionBuilder<T>.Requires` obsolete

22. **Attribute only; body and neighbors unchanged.**
    `[Obsolete(...)]` on the interface at `IActionBuilderOfT.cs:39` and the
    implementation in `ActionBuilderOfT.cs`. `RequiresSoft` (`:42`),
    `RequiresLink`, and `RequiresLinkSoft` are not obsolete. No fluent
    successor was added.

23. **Compiler constraint loosened for every test/benchmark project.**
    `Directory.Build.targets:5` adds `CS0618` to `NoWarn` for
    `IsTestProject` or `*Benchmarks*`. That is a deleted warning on a
    wide surface, not just the `Requires` tests.

### S6. Renovate preset path

24. **One path token changed.** `renovate.json:5`
    `local>lvlup-sw/lvlup-claude:renovate-config/presets/dotnet.json` →
    `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`.
    No other renovate structure changed.

### S7. Docs + CHANGELOG

25. **Out of this lens.** Prose and claim inventory. No structural
    control lives only in those files.

### Deletions (read with care)

- **No source file was deleted.** The nested `PhaseGraph`/`EdgeBuilder` block
  was moved. `EdgeBuilder` logic was not dropped.
- **No test file was deleted.** Packaging assertions were retargeted
  0.6.0 → 0.7.0. New tests were added (AGWF035 under-reach, AGWF037,
  HandAuthoredContract merge, AONT205 extra fields, MCP resultType/icons).
- **No first-wins-dedup was removed** (finding 11). The old path accepted
  duplicates into the model.
- **The deletion that carries risk is the CS0618 suppress** (finding 23)
  and the **un-updated `== HandAuthored` branches** after the enum widened
  (finding 21). The AONT205 inline scan was deleted but replaced by a
  *stricter* shared scan (finding 20).

## What else I read

- `verification/stage0.md` (scope set, ranking, cost).
- `git log --oneline 4d060f4..HEAD` and `git diff --stat/--name-status`.
- Full diffs for the ranked surfaces listed in stage0 (generator, contracts,
  MCP hosting/descriptor, ontology builder/merge/enum, renovate, Directory.Build.targets).
- Body compare of old nested `PhaseGraph`/`EdgeBuilder` vs `PhaseGraph.cs`.
- `DiagnosticForkModel.Create` at `4d060f4` (no duplicate throw).
- `WireToModelBridge.Bridge` rejection-before-map order.
- CHANGELOG Residue (#185) as a lead only; not used as behavior.
- Did not treat issue 185, the dispatch plan, or
  `docs/specs/2026-08-22-correctness-core.md` as facts. Did not read the
  untracked `docs/2026-06-16-edge-*` files.

## Assumptions and unsettled questions

- Assumption: HEAD `324768f` is the analyzed revision and the working
  tree matches it for the ranked surfaces (untracked edge docs ignored).
- Unsettled: whether any `CallToolResult` is constructed outside
  `OntologyServerToolFactory` in a Hosting helper this diff did not touch.
- Unsettled: whether a `HandAuthoredContract` property/`Source` ever
  reaches AONT201/203/204 in production, or those branches stay inert
  because merge already collapses `Source` to `HandAuthored` (finding 19).
- Unsettled: whether two empty trigger names on one JSON edge can still
  reach `MapDiagnosticForks` → `Create` because `FindDuplicateTriggerNames`
  skips `IsNullOrEmpty` (finding 12).
- Unsettled: whether any consumer already compiled against Contracts 0.6.0
  will see converter throw-on-unknown when 0.7.0 is published — packaging
  tag existence is a claim, not verified here.
- This lens does not evaluate whether the under-reach enumerator matches
  every rejoin construct the emitted table would list. That is obligation
  work, not a missing structural observation.
