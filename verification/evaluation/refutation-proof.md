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
lens: refutation-named-proof
angle: named proof artifact vs claim
evaluator: independent high-tier attempt (1 of 3)
---

# Refutation — named proof

Angle: for each active obligation, does the **named proof artifact** establish the **claim**? An obligation can be real while its named artifact does not establish it. That is a rung refinement, not a kill. Kill only when the claim itself is not real.

Read at `324768f`: the tests and gates the ledger names, plus the production call sites they purport to lock. Did not implement fixes. Did not re-run the suite; this pass judges what those artifacts *can fail for*.

## Method

Five kill arguments (evaluation-lenses §3). This attempt uses only the last-mile of the fourth rule: **read the proof as well as the claim**. Competing explanation, named before looking: the ledger cites a test, a scan, or a pair of texts that already lock the claim, so the obligation is either already proved (survived) or not a real gap (refuted). The usual miss: the named artifact locks a cheaper, adjacent fact (IR graph, committed file, substring, injected fixture) and lets the failure the obligation names through.

Verdicts:

- **refuted** — the claim is not a real obligation at this revision.
- **survived** — the claim is real and the named artifact establishes it.
- **refine-rung** — the claim is real; the named artifact does not establish it (missing, aspirational, or bound to the wrong subject).

## Ledger-wide

Most Active rows name the *desired* lock, then note that today's tests do something weaker. That is honest inventory. It is not a proof. Treating those rows as already proved would collapse unproven into verified (evidence-binding).

Two patterns repeat.

1. **Injected subject.** AGWF035 under-reach positives pass `PhaseGraph.WithoutSuccessor`. They establish that the guard can fire when the *graph argument* lacks an edge. They do not establish saga emission, instance-share, or a newly failing consumer `[Workflow]`.
2. **List append / substring / version pin.** Catalog identity, pack 0.7.0, INV-3 greps, schema-diff skip, PublicAPI Unshipped, and the Hosting pin all have a green shape that cannot fail for the reason the obligation exists.

Supported-claim rows (`agwf035-all-complete-silent`, `agwf035-overreach-preserved`, `agwf037-reject-not-dedup`, `claim-clr-free-xor-docs`) are the exception: the named tests and pages do what the claim says.

---

## Per obligation

### agwf035-underreach-ir-not-emission — refine-rung

**Claim.** Under-reach fires when a rejoin last step does not dispatch the declared terminal in the **shipped saga**, not only when a test-injected `PhaseGraph` lacks an IR edge.

**Named artifact.** Closure of `PhaseGraph` rejoin edges ↔ emitted `Start{target}`. Current positives inject `WithoutSuccessor`.

**What the named tests do.** `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires` and `Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires` build `PhaseGraph.Build(model).WithoutSuccessor(...)` and call `TerminalReachabilityGuard.Report` directly (`TerminalReachabilityDiagnosticTests.cs:456–498`). The generator call omits `phaseGraph` (`WorkflowIncrementalGenerator.cs:1038–1043`), so production rebuilds from the model. No test reads emitted saga `Start{Finally}` against `PhaseGraph` rejoin successors.

**Discriminating fact.** The positives fail only when the *injected graph* lacks the edge. An emitter that forgets `Start{Finally}` while `PhaseGraph.Build` still has the edge stays green. Claim is real (#184 class). Named closure does not exist.

### phasegraph-type-not-instance — refine-rung

**Claim.** Guard and `ValidTransitions` resolve successors from **one** `PhaseGraph` instance.

**Named artifact.** Pass one `Build` result into both `Report` and `Emit`, or an edge-equality lock.

**What exists.** `TransitionsEmitter.cs:56` does `PhaseGraph.Build(model)`. `TerminalReachabilityGuard.cs:127` does `phaseGraph ?? PhaseGraph.Build(model)`. The pipeline call does not pass argument 6. `Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline` inspects only argument 1 (classification) and ignores the graph argument (`TerminalReachabilityDiagnosticTests.cs:671–676`).

**Discriminating fact.** Two independent `Build` calls, no shared instance, no edge-equality lock. Type-share is present; instance-share is not. Claim is real (CHANGELOG Residue overclaims “share one PhaseGraph”). Named artifact does not exist.

### agwf035-catalog-polarity-lie — refine-rung

**Claim.** An under-reach report describes a missing dispatch, or the catalog sentence is rewritten.

**Named artifact.** Widen `messageFormat` (or a second catalog member).

**What exists.** One sentence, over-reach polarity, used for both arms:

```564:564:src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs
        messageFormat: "Step '{0}' in workflow '{1}' chains to '{2}', which is not on the workflow's main flow. ...
```

Same text in `AgwfCatalog.tsp:344` and `agwf-catalog.json:234`. Under-reach `Report` passes `{0}` = declared terminal, `{2}` = dispatcher (`TerminalReachabilityGuard.cs:157–163`). Positives `Contains` both names (`:470–473`, `:495–498`) and stay green under inverted polarity.

**Discriminating fact.** Named widen/second-member does not exist. Substring tests cannot fail the lie. Claim is real.

### agwf035-error-still-emits — refine-rung

**Claim.** AGWF035 Error must not pair with a generated saga, or the split is machine-readable.

**Named artifact.** Add AGWF035 to `hasErrors`, or an explicit split table.

**What exists.** `hasErrors` (`WorkflowIncrementalGenerator.cs:933–938`) includes AGWF037 via `hasDuplicatePermittedForkTrigger`. It does not include AGWF035. `TerminalReachabilityGuard.Report` runs **after** that gate (`:1038`), so appending AGWF035 to the current `hasErrors` expression cannot see it. Emission is `if (result.Model is not null) EmitWorkflowSources` (`:84–86`). No split table.

**Discriminating fact.** The named “add to `hasErrors`” site is the wrong seam: Report is later. Claim is real (Error + model both set). Named artifact as specified does not establish fail-closed.

### agwf035-json-import-unreached — refine-rung

**Claim.** Every authoring front that emits `ValidTransitions` also runs `TerminalReachabilityGuard.Report`.

**Named artifact.** Call-graph: every `EmitWorkflowSources` path calls `Report`.

**What exists.** C# path: `TransformToResult` calls `Report`, then `EmitWorkflowSources`. Import path: `BridgeImportFile` → `EmitWorkflowSources` (`WorkflowIncrementalGenerator.cs:102–111`) with no `Report`. AGWF037 *does* run on import (`WireToModelBridge.cs:469–491`). The existing call-site scan only requires that *some* `WorkflowIncrementalGenerator` invocation exists with a `MainFlowClassification` argument; it does not close every `EmitWorkflowSources` predecessor.

**Discriminating fact.** Import emits the table without the guard. The named call-graph lock does not exist; the shipped scan would stay green.

### agwf035-all-complete-silent — survived

**Claim.** All-`Complete()` branch plus `Finally<T>()` does not fire AGWF035.

**Named artifact.** `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` (direct `Report` + `RunGenerator`).

**What the test does.** Parses `AllCompleteBranchSource`, calls `Report` without an injected graph, then `GeneratorTestHelper.RunGenerator` on the same source (`TerminalReachabilityDiagnosticTests.cs:508–522`). Both arms assert no AGWF035.

**Discriminating fact.** The named test drives the real generator over the authored shape and would fail on a false-positive. That is the claim.

### agwf035-overreach-preserved — survived

**Claim.** AGWF035 still fires for not-last terminal or construct-owned successor. Corpus stays silent.

**Named artifact.** Existing over-reach fixtures + corpus never-fires.

**What the tests do.** `Diagnostic_TerminalNotLastMainFlowStep_Fires` (unclassified step after terminal), `Diagnostic_SuccessorResolvesOffMainFlow_Fires` (empty classification into a fork path), `Diagnostic_ForkWorkflowAsClassified_DoesNotFire`, `Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug`, `Diagnostic_ExistingCorpus_NeverFires` (reflection sweep of `SourceTexts`, ≥30 sources, real generator).

**Discriminating fact.** These fixtures fail if the shipped over-reach arm or the corpus silence regresses. They do not inject `WithoutSuccessor`. That is the claim.

### agwf037-reject-not-dedup — survived

**Claim.** Two same-trigger `PermitTrigger` declarations fail AGWF037 on C# extract and JSON import. Distinct triggers stay clean. Generation is gated.

**Named artifact.** Extractor / generator / import twins.

**What the tests do.**

| Path | Duplicate | Distinct | Gate |
|---|---|---|---|
| Extractor | `Extract_DuplicatePermitTrigger_RejectsEdgeAndReportsAgwf037` — empty models, one AGWF037 | `Extract_DistinctPermitTriggers_YieldsModelWithoutAgwf037` | no model |
| C# generator | `CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga` | `CsharpTwin_DistinctPermitTriggers_DoesNotFireAgwf037` | no `Saga.g.cs` |
| JSON import | `ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga` | `ForkDistinctTriggers_IsNotRejected_AndLowersSaga` | no `Saga.g.cs` |

`hasErrors` includes `hasDuplicatePermittedForkTrigger` (`WorkflowIncrementalGenerator.cs:930–938`), so the generator twins can fail if the report is present and a saga still emits.

**Discriminating fact.** The three twins fail on first-wins or emit-anyway. That is the claim. (Empty trigger names skipped is a different, open, obligation.)

### contracts-0-7-0-pack-incomplete — refine-rung

**Claim.** A green 0.7.0 pack test means the nupkg is versioned 0.7.0 **and** contains `agwf-catalog.json` plus `AgwfEntryDuplicatePermittedForkTrigger.json`.

**Named artifact.** Named-entry asserts on those two pack paths. Version assert already exists.

**What the test does.** `Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent` asserts filename `LevelUp.Strategos.Contracts.0.7.0.nupkg`, nuspec `<version>0.7.0</version>`, and family files `SdlcEventEnvelope.json` / `WorkflowDefinitionV1.json` / `InvariantEntry.json` (`PackagingTests.cs:105–137`). It never names `agwf-catalog.json` or `AgwfEntryDuplicatePermittedForkTrigger.json`.

**Discriminating fact.** Source pin is real (`ContractsVersion` 0.7.0; `Content Include` for `Generated/agwf-catalog.json` and `schemas/**/*.json`). The pack test can pass on a 0.7.0 nupkg that dropped those two entries. Named-entry asserts do not exist.

### contracts-changelog-contradicts-0-7-0 — refine-rung

**Claim.** The 2.11.0 record states 0.6.0→0.7.0 and names AGWF037.

**Named artifact.** The three texts.

**What the texts say.** Product lede (`CHANGELOG.md:17`) states **0.4.0 → 0.6.0** (AGWF035, then AGWF036). Residue (`:182`) states 0.6.0 → 0.7.0 and names AGWF037. Packaged `src/Strategos.Contracts/CHANGELOG.md` has no 0.7.0 / AGWF037 section (Unreleased still ends at AGWF036 / 0.6.0).

**Discriminating fact.** Reading the three texts *exhibits the contradiction*; it does not *lock* the desired lede/package record. No structural check fails when they drift. Claim is real.

### schema-diff-skip-succeeds — refine-rung

**Claim.** `contracts-schema-diff` is non-success when it did not run the structural diff, and it compares against `contracts-v*` not product `v*`.

**Named artifact.** `have_prev=false` ⇒ non-success; match `contracts-v*`.

**What the workflow does.** `.github/workflows/contracts-schema-diff.yml` uses `git describe --match 'v*'`. On empty/missing schemas it sets `have_prev=false` and prints “no diff to run.” The `node` step is `if: have_prev == 'true'`. A skipped step is not a job failure.

**Discriminating fact.** Named fail-closed / `contracts-v*` match do not exist. `JsonSchemaDiff` unit tests do not run when the node step is skipped. Claim is real.

### mcp-resulttype-and-pin — refine-rung

**Claim.** Hosting pins MCP 2.2.0 so every constructed `CallToolResult` emits `resultType: complete`. INV-3 denies the pre-2026-07-28 shape on the protected path.

**Named artifact.** Production-csproj pin assert; per-construction `ResultType` assignment; INV-3 job that cannot skip-as-pass.

**What exists.** `Strategos.Ontology.MCP.Hosting.csproj` has `VersionOverride` 2.2.0. No test reads that production csproj (test project pins 2.2.0 independently). `MapTraversalResult` / `ErrorResult` assign `ResultType = CompletedResultType` (`OntologyServerToolFactory.cs:384–412`). Four tools (`ontology_explore` / `_query` / `_action` / `_validate`) go through `McpServerTool.Create` wrap (`:132`). INV-3 check 3.4 is `grep -L ResultType` over files that mention `CallToolResult` (comment-satisfiable). `.github/workflows/ci.yml` has no INV-3 job.

**Discriminating fact.** Pin exists; production-csproj assert does not. Two constructions assign `ResultType`; the wrap path is unasserted. INV-3 cannot skip-as-pass because it is not in CI at all. Claim is real.

### icons-null-when-unset — refine-rung

**Claim.** `Icons` stays null when unset. Non-null `Icons` → `Tool.icons` is reachable from `AddOntologyTools` if a consumer supplies icons.

**Named artifact.** Discovery asserts null. Non-null mapping is test-only via `CreateServerTool`.

**What exists.** `OntologyToolDiscovery.Discover` builds four descriptors and never assigns `Icons` (`OntologyToolDiscovery.cs:48–116`). Factory tests assert `descriptor.Icons` and `protocolTool.Icons` are null (`OntologyServerToolFactoryTests.cs:59–61`). `CreateServerTool_WithIcons_MapsOntoProtocolTool` constructs a descriptor in the test and calls `CreateServerTool` (`:65–92`). `ApplyIcons` maps when non-null (`OntologyServerToolFactory.cs:248–262`). No public `AddOntologyTools` / `Discover` overload accepts icons.

**Discriminating fact.** Null-when-unset is established because nothing assigns. The second conjunct (consumer-supplied icons through the public factory) is explicitly test-only. Named artifact does not establish reachability from `AddOntologyTools`.

### handauthoredcontract-unreached — refine-rung

**Claim.** `HandAuthoredContract = 2` is assigned by a shipped authoring surface, survives merge, and is treated as hand-side by AONT201/203/204.

**Named artifact.** Production assignment-site scan; `MergeTwo` preserves 2; exhaustive `IsHandSide`.

**What exists.** Enum member `= 2` is real. Production assignment: tests stamp 2 (`HandAuthoredContractMergeTests`, `AONT205Tests`, `IOntologyBuilderInvariantTests`). `MergeTwo.cs:67` sets `Source = DescriptorSource.HandAuthored`. The merge test **asserts the collapse** (`HandAuthoredContractMergeTests.cs:87`). `OntologyGraphBuilder.cs:329/:408/:565` compare `==` / `!=` `HandAuthored` only. `IsHandSide` in `OntologyBuilder.cs:164` includes both 0 and 2, but merge has already restamped 2 → 0.

**Discriminating fact.** Named scan / preserve-2 / exhaustive hand-side lock do not exist. Merge test locks the opposite of “survives merge.” Claim is real.

### descriptor-source-docs-omit-member-2 — refine-rung

**Claim.** Document which authoring surface maps to which `DescriptorSource` value.

**Named artifact.** The two lists.

**What the lists say.** `source.md:65–66` and `ontology-sources.md:42–43` list `HandAuthored` and `Ingested` only.

**Discriminating fact.** The named lists omit member 2. Reading them exhibits the gap; nothing fails when they stay two-valued. Claim is real.

### requires-obsolete-observable — refine-rung

**Claim.** `Requires` is obsolete, still compiles and still writes Preconditions, and a clean in-repo test compile is not evidence that consumers see CS0618.

**Named artifact.** `[Obsolete]` + unchanged body. Compile of a `NoWarn`-free subject that fails CS0618.

**What exists.** `[Obsolete]` on interface and impl (`IActionBuilderOfT.cs:38`, `ActionBuilderOfT.cs:77–90`). Body still appends `ActionPrecondition` (`ActionBuilderOfT.cs:83–89`). `Requires_AddsPreconditionWithPropertyPredicate` and `Requires_IsObsolete_PointingAtActionDescriptorPreconditions` cover those two facts. `Directory.Build.targets:4–5` adds `CS0618` to `NoWarn` for every test and benchmark. No `NoWarn`-free compile subject exists.

**Discriminating fact.** Obsolete + body are established. The named consumer-visible CS0618 compile does not exist; the suite cannot fail for a dropped attribute plus a still-green in-repo build.

### renovate-resolve-unasserted — refine-rung

**Claim.** Renovate resolves the organisation’s dotnet preset.

**Named artifact.** None in this repo. Path-token suffix is a weaker, cheaper claim that holds.

**What exists.** `renovate.json` second `extends` is `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`. The `tools/` suffix matches the rename note. No resolve probe, no HTTP check, no Renovate dry-run.

**Discriminating fact.** The ledger already names the artifact as **None**. Path suffix ≠ resolve. Claim is real (#181 class).

### aont205-analyzer-unreached — refine-rung

**Claim.** A shipped analyzer `DiagnosticDescriptor` is reported, or it is not a compile-time control.

**Named artifact.** Analyzer call-graph must `Diagnostic.Create` that descriptor.

**What exists.** `OntologyDiagnostics.IngestedContributesToIntentOnly` and `OntologyDiagnosticIds.IngestedContributesToIntentOnly = "AONT205"` are the only production hits. `OntologyDefinitionAnalyzer` has many `Diagnostic.Create` sites; none take this descriptor. Runtime AONT205 is a different root (`HandAuthoredContractMergeTests.Merge_IngestedActions_StillFailAONT205`).

**Discriminating fact.** Named call-graph lock does not exist. A scan would currently fail. Claim is real (unused `static readonly` compiles).

### compat-agwf035-breaking — refine-rung

**Claim.** The under-reach arm is a breaking diagnostic for `[Workflow]` compilations that previously succeeded.

**Named artifact.** Fire/silent fixtures. Current tests inject `WithoutSuccessor`.

**What the fire fixtures do.** Same injected-graph positives as `agwf035-underreach-ir-not-emission`. Production-`Build` shapes (`Diagnostic_RejoiningConstructsAsEmitted_DoNotFire`, `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire`, `Diagnostic_ExistingCorpus_NeverFires`) stay silent. No generator run exhibits a previously legal authored workflow that now reports AGWF035 without a stripped graph.

**Discriminating fact.** Named fire fixtures do not exhibit a production `[Workflow]` that newly fails. The breaking-diagnostic claim is a real class of risk; the named tests do not establish that this revision newly fails any consumer compilation.

### compat-validtransitions-nonreversing — refine-rung

**Claim.** A generator revert is not a revert of already-emitted consumer `ValidTransitions` tables.

**Named artifact.** Emitter tests. No equality lock vs `4d060f4`.

**What the tests do.** `TransitionsEmitterUnitTests` asserts the dictionary *exists* (`Contains("ValidTransitions")`). `TransitionGraphLoweringTests` lock current-shape edges. None compare emitted sets to merge-base `4d060f4`.

**Discriminating fact.** Emitter tests do not speak to revert/rebuild or to whether this lift changed sets. The compatibility claim is real if the table moved; the named artifact does not establish that movement or lock it.

### compat-publicapi-omits-obsolete — survived

**Claim.** RS0016/RS0017 cannot prove `Requires` is obsolete.

**Named artifact.** Unshipped diffs. Analyzer has no Obsolete column.

**What the files say.** `PublicAPI.Unshipped.txt:108` records `IActionBuilder<T>.Requires(...)` as a signature only. `Shipped.txt` is empty. No Obsolete marker appears on that line. PublicAPI analyzers report add/remove, not attribute changes.

**Discriminating fact.** The named Unshipped line *is* the proof that dropping `[Obsolete]` does not fail RS0016. That is the claim.

### diagnostic-fork-ctor-open — refine-rung

**Claim.** Empty anchors, duplicate/empty triggers, empty seed, `MaxForks < 1` must not be constructible except via `Create`.

**Named artifact.** Private primary constructor; `Create` only.

**What exists.** `internal sealed record DiagnosticForkModel(...)` exposes the primary constructor inside the assembly. `Create` throws on the invalid cases (`DiagnosticForkModelTests` covers `Create` throws). Nothing stops `new DiagnosticForkModel(...)` from bypassing `Create`. `PermittedForkTriggerModel` is the same shape.

**Discriminating fact.** Named private constructor does not exist. `Create` throws are one factory, not a type. Claim is real (#151 lowering can switch on a hand-built IR).

### traversal-result-flags-independent — refine-rung

**Claim.** `IsError: false` + `Error` present (or the inverse) must not be representable.

**Named artifact.** Discriminated `Success | Error`.

**What exists.** `TraversalResult` is a single record with independent `IsError` and `Error` (`TraversalResult.cs:45–51`) — the evidence-binding forbidden shape. `MapTraversalResult` keys on `result.IsError` and uses `result.Error ?? "traversal failed validation."` (`OntologyServerToolFactory.cs:378–380`). No discriminated union.

**Discriminating fact.** Named `Success | Error` does not exist. The type still permits contradictory flags. Claim is real.

### agwf037-catalog-identity — refine-rung

**Claim.** Catalog tests extended to AGWF037 fail if the id is missing from a freshly compiled catalog, or if `agwf.md` has only a mention.

**Named artifact.** Emitter: regenerate-then-compare. Markdown: parse table id cells.

**What the tests do.** `AgwfCatalogEmitterTests` reads the **committed** `Generated/agwf-catalog.json` and matches a hand-authored `GroundTruthCodes` list that includes AGWF037. `AgwfMarkdownTests` selects lines with `l.Contains(c)` (`AgwfMarkdownTests.cs:58–60`) — a mention satisfies. `AgwfCatalog_HandEdit_FailsGuard` regenerates and compares the *whole* catalog to committed (freshness), not that AGWF037 is required independently of the hand list. `AgwfCatalogSchemaTests` compiles TypeSpec and compares entry `id` consts to the same hand list.

**Discriminating fact.** Updating `GroundTruthCodes` in lockstep with an unwiring keeps identity tests green. Markdown `Contains` is mention-satisfiable. Named regenerate-then-compare-for-id and table-cell parse do not exist as AGWF037 locks.

### claim-clr-free-xor-docs — survived

**Claim.** Docs name `ObjectTypeFromDescriptor` / `ApplyDelta` as the CLR-free path; SymbolKey-only interface fan-out is not expressible.

**Named artifact.** Review that the pages state the limit the types already enforce.

**What the pages say.** `source.md:80–83` names both seams and states a SymbolKey-only interface fan-out is **not expressible**, citing `RationaleCorpusParityTests`. `polyglot-descriptors.md:126–143` repeats the first-class path and the CLR-free ⊕ polymorphic limit. `RationaleCorpusParityTests` exists at `src/Strategos.Ontology.Npgsql.Tests/Parity/RationaleCorpusParityTests.cs`. Residue (`CHANGELOG.md:198–199`) matches.

**Discriminating fact.** Human review of the named pages: they state the limit the types already enforce. That is the rung-6 artifact. (A substring test would be the weaker shape the obligation already rejects.)

### claim-issue-185-tracker — refuted

**Claim.** Issue 185 comment 2 still lists under-reach / #181 / #163 / #115 / #156 / #176 / #177 as open-by-design. This branch claims to implement them.

**Named artifact.** Issue comment vs Residue subsection.

**Why the claim is not a real obligation at this revision.** `verification/obligations/claim-issue-185-tracker-close.md:20` already records “This file is not an obligation.” Ledger **Assumptions** state that issue 185 “still open by design” is tracker state at comment time, not proof this branch left the work undone. The named “issue comment” is live GitHub state, not an artifact bound to `324768f` (this pass could not re-fetch it: GitHub API 403 rate limit). Residue (`CHANGELOG.md:170–199`) is the revision-bound delivery record. Two texts at different times can both be true; that is not a verification obligation on the diff.

**Discriminating fact.** Premise is unbound comment-time tracker state; the inventory file that derived the row disclaims obligation status. Kill the row. Process hygiene (do not title the PR “Close #185”) is a guard, not this claim.

---

## Tally

| Verdict | Count | Slugs |
|---|---|---|
| refuted | 1 | `claim-issue-185-tracker` |
| survived | 5 | `agwf035-all-complete-silent`, `agwf035-overreach-preserved`, `agwf037-reject-not-dedup`, `compat-publicapi-omits-obsolete`, `claim-clr-free-xor-docs` |
| refine-rung | 20 | all other Active rows |

## Passes

- Read `evaluation-lenses.md` §3, `proof-ladder.md`, `evidence-binding.md`, `validating-claims.md`.
- Read every named test/gate cited above at `324768f`.
- Supported-claim twins for AGWF037 (extractor + C# + JSON) and the AGWF035 all-complete / over-reach / corpus fixtures do what those claims say.
- Guide pages this wave edited do name the CLR-free XOR limit.
- PublicAPI Unshipped really cannot see `[Obsolete]`.

## Uncertainties

- Issue 185 comment 2 was not re-fetched (API rate limit). Refutation of `claim-issue-185-tracker` does not depend on the comment’s current wording; it depends on unbound subject + the inventory’s own disclaimer.
- Whether any *out-of-repo* producer stamps `DescriptorSource.HandAuthoredContract = 2` (open question on `handauthoredcontract-unreached`). In-repo production assignment is still empty.
- Whether `PhaseGraph.Build` and `EnumerateRejoinDispatchersOf` can disagree on a real authored shape. That would make `compat-agwf035-breaking` exhibitable without `WithoutSuccessor`. No such fixture exists in this revision.
- Whether a pack of this revision actually embeds `agwf-catalog.json` / `AgwfEntryDuplicatePermittedForkTrigger.json`. Source `Content` items exist; the named pack test does not lock them. This pass did not pack.
- `ErrorResult` `{resultType: complete, isError: true}` protocol legality is INV-3’s assertion, not a named proof on `mcp-resulttype-and-pin`.
