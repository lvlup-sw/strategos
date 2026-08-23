# Spec: v2.11.0 Correctness Core — main-flow termination, and the hygiene lane

**Date:** 2026-08-22 · **Feature:** `v2110-correctness-core` · **Depth:** standard · **Revision:** 1
**Inputs:** issues #155 · #175 · #145 · #174 · #133 · #166 · milestone v2.11.0 (#8) · design record `docs/designs/2026-08-22-action-calculus.md` (PR #173, unmerged) · in-session grounding audit (2 workflows, 24 agents, source read at `4e95874`)

> One unified artifact: `## Requirements` is the DR-N source; `## Decomposition` maps tasks → DR-N within this same document.
> Scope note: this slice does **not** close v2.11.0 (which is now 10 open with #175 filed). #147 is not PR-closeable (nuget.org portal actions + two tag trains behind manual approval), and #156/#163/#115 are excluded for the reasons in Open Questions.

## Problem Statement

A C#-authored workflow that uses `Fork` or `Branch` does not terminate.
The defect is live on NuGet in every published version, including v2.10.0 (2026-08-07), with no DSL-level workaround.

The mechanism is a **classification failure, not an ordering failure** — the framing in #155 is one instance of a larger class.

`WorkflowModel.StepNames` is the list the saga emitter walks to decide each step's successor.
Five generator blocks append entries to it that are **not on the main flow**: failure handlers (`WorkflowIncrementalGenerator.cs:501-537`), fork paths (`:539-577`), loop-exit branches (`:579-614`), approval rejection/escalation steps (`:616-706`) and confidence handlers (`:753-783`).
There are **three** successor scans and none of them is adequate. `WorkflowModel.NextMainFlowStepName` (`Models/WorkflowModel.cs:317-343`) and its twin in `SagaStepHandlersEmitter.BuildHandlerContext` (`:186-193`) skip **only** confidence handlers. The third — `SagaApprovalComponentEmitter.BuildApprovalResumeContext` (`Emitters/Saga/SagaApprovalComponentEmitter.cs:69-76`) — takes `ctx.Model.StepNames[stepIndex + 1]` raw, with **no filter at all**.
Every other appended entry is therefore treated as a main-flow successor, so the declared terminal step never reaches `MarkCompleted()`, and an approval resumes onto whatever happens to sit at the next index.

Separately, `StepNames` disagrees with the other two representations of the same workflow.
`model.Steps` is document-ordered (`StepExtractor.cs:862-866`, `:882-886`), and the import path *observes* document order because the exported JSON lists path steps as top-level `steps` in that order — its own name-composition loop mirrors the C# tail-append, so the append is a dedupe no-op for a well-formed export rather than a weave. `StepNames` meanwhile is built by a walker that delegates `Fork`/`Branch` to helpers which `steps.Add(…)` (`StepExtractor.cs:640`, `:758`) inside a backward walk that `Insert(0, …)`s everything else.
This is why the JSON-import half of the round-trip runs green on a real host while the C# twin does not.

Three concrete consequences, all verified empirically against the current generator:

1. **Fork stalls.** The terminal's completed handler chains into a fork-path step, which gates on join readiness that never arrives. Machine-checked and red-by-declaration: `RoundTripBehavioralTests.cs:123` carries `[Skip("blocked on strategos#155…")]`.
2. **Branch loops forever** (#175, filed from this pass). The terminal cascades back into a branch-path step, which rejoins at the terminal — an unbounded cycle re-running the terminal on every lap, with the Marten saga document never deleted. `grep -c MarkCompleted` over the generated saga = **0**. There is no behavioral test for `Branch` anywhere in the repo.
3. **The generated public surface is wrong.** `TransitionsEmitter.cs:64-76` builds the public `ValidTransitions`/`IsValidTransition` as a flat linear chain over `StepNames`, so a branch workflow publishes sibling exclusive paths chained to each other. `MermaidEmitter` is fork-blind for the same reason.

**#145's premise is factually false.**
A probe run of the exact `DeclaredButInertTests` fixtures through the real generator shows intermediate fork-path *and* loop-body confidence gating **already lower** into the saga: `StepCompletedHandlerEmitter.cs:85-93` gates on `context.StepModel?.Confidence` with no position test, and `StepsByName` (`SagaEmissionContext.cs:195-204`) is built from `model.Steps`, which already carries configured `StepModel`s for every position.
The four `Deferred` entries at `StepConfigParityTests.cs:142-171` and the two AGWF022 emission blocks are **false positives**.

**Corrected at plan-review (revision 1).** An earlier draft named the inert case as an instance-named fork-path step missing the `StepsByName` lookup. That was wrong twice over, and both corrections matter. First, the lookup **hits**: `StepInfo.PhaseName => InstanceName ?? StepName` (`StepExtractor.cs:72,82`), the walker keeps the instance name (`:640`), and `ParseForkPathStepModels` keeps it (`:1065`), so `StepsByName` contains the instance name. Second, the shape is **unauthorable**: `IForkPathBuilder` exposes `Then<TStep>()`, `Then<TStep>(string instanceName)`, `Then<TStep>(Action<IStepConfiguration<TState>>)` and `OnFailure` (`src/Strategos/Abstractions/IForkPathBuilder.cs:36,60,99,118`) — no overload takes an instance name **and** a configure lambda, so no compiling workflow can produce a fork-path step that is both instance-named and confidence-gated.

The real defect is a **duplicate**, not a miss. `TryBuildConfiguredForkPathStepModel` strips `InstanceName` (`StepExtractor.cs:233-236`), so `ForkPathModel.StepNames` (`Models/ForkPathModel.cs:49`) is type-named while the walker's entry is instance-named. `WorkflowIncrementalGenerator.cs:556-566` then appends the type name because the dedupe set holds only the instance name — yielding *n+1* entries for *n* authored steps: a phantom duplicate phase, command and handler, carrying a null `StepModel` and therefore no gate, and it is also the name the fork dispatch targets.

## Constraints

Anchored to `.claude/skills/strategos-design-invariants/`:

- **INV-1** — Workflows lower into Wolverine + Marten via Roslyn SG. *Violated today in the shipped composition, twice; only a real-host run discharges it.*
- **INV-5** — Three-tiered validation with stable `AGWF*`/`AONT*` ids, preferring the earliest tier that **can** catch the error. *Load-bearing: this failure class is compile-time decidable and has no compile-time guard.*
- **INV-4** — Concrete domain nomenclature. *Governs the new test fixtures.*
- **INV-6 / INV-7** — Sealed-by-default; immutable record state. *Governs new step and state types.*
- **INV-3** — MCP tracks the latest protocol spec. *DR-10 amends the invariant's own statement of "latest".*

## Chosen Approach

**Classify, then order, then guard.**
The three moves are complementary, not alternatives — and the ordering fix alone is unsafe.

**1. Classify (DR-1).** Give `SagaEmissionContext` a complete off-main-flow step set and make all **three** successor scans consult it. `ForkPathSteps` (`SagaEmissionContext.cs:110`) is adjacent but not reusable as-is — it includes the join step, which is main-flow — and the missing siblings are branch-path (including loop-exit cases), failure-handler and approval-handler sets. The invariant becomes *"the terminal is the last **main-flow** step"*, which holds regardless of what any block appends.

**2. Order (DR-2).** Move the splice direction out of the path helpers and into their callers, so `StepNames` is document-ordered and agrees with `model.Steps` and the import path. This is what repairs the generated public surface (DR-7). It is **not** what fixes termination.

Ordering without classification is a regression, not a fix. Probing six shapes against a reorder-only patch: three regress, and two of the three become a **permanent saga hang** rather than a wrong-but-progressing route. Moving fork-path steps inline puts them directly after the step a confidence-rejoin or approval-resume targets, so the resume lands *on* a fork-path step, bypassing the fork dispatch handler — `Fork_{id}_PathNStatus` stays `Pending`, `CheckJoinReady` never returns true, and nothing is ever deleted. Classification is what makes ordering safe.

**3. Guard (DR-3).** The generator knows at emission time whether the declared terminal is the last main-flow entry. Nothing checks it. A new `AGWF` diagnostic closes the class at the earliest tier that can catch it, per INV-5 — and is the only thing that stops the sixth appending block someone adds next year from re-opening this bug silently.

`BranchExtractor` needs no work — `FindRejoinStepName` (`BranchExtractor.cs:466-505`) never reads the step list — but branch is not free: terminal `.Complete()` cases are excluded from `BranchPathInfo` (`SagaEmissionContext.cs:184-188`), so `isTerminalStep` cannot be set and the dedicated branch loop at `SagaStepHandlersEmitter.cs:112-161` is dead for the C#-authoring path (`processedStepNames` is built from `StepNames` at `:96-110` and short-circuits it at `:120-124`).

**#145 is re-scoped from a lowering task to a correction task (DR-6):** delete two false-positive AGWF022 blocks, retarget the diagnostic at the instance-name residue that is genuinely inert, flip the four parity entries against real proofs, and harden the parity guard that let four provably-false entries stand.

The **hygiene lane (DR-8..DR-10)** ships as a separate PR touching only `.github/**` and `docs/**`, with an empty source-file intersection against the correctness lane.

## Requirements

The DR-N identifiers below are the single source the decomposition traces against.

### DR-1: Off-main-flow steps are classified, not positional (#155)

`SagaEmissionContext` carries a complete `OffMainFlowSteps` classification — the union of fork-path, branch-path, failure-handler, approval-rejection/escalation and confidence-handler step names.
All three successor scans — `WorkflowModel.NextMainFlowStepName` (`:317-343`), `SagaStepHandlersEmitter.BuildHandlerContext` (`:186-193`) and `SagaApprovalComponentEmitter.BuildApprovalResumeContext` (`Emitters/Saga/SagaApprovalComponentEmitter.cs:69-76`, which has no filter at all today) — consult it instead of their current filters.
`IsLastStep` becomes *"no later main-flow step exists"*, independent of list position.

**The classification governs main-flow chaining only.** The same scan also chains intermediate steps *within* a fork path or branch case: only a path's **last** step is intercepted (`ForkPathInfo`/`BranchPathInfo` are keyed on the last step), so a non-last path step falls through to the generic completed handler and takes its successor from this scan. Skip the whole path uniformly and an intermediate path step's successor becomes the next main-flow step — or null, which sets `IsLastStep` and emits `MarkCompleted()` **in the middle of a path**. Inside a path the successor is the next step *in that path*; the skip set applies only when resolving a main-flow step's successor. The existing confidence-handler routing already works this way and is the shape to follow.

**Acceptance criteria:**
- A workflow whose terminal is followed in `StepNames` by any off-main-flow entry emits `MarkCompleted()` in the terminal's completed handler, for each of the five appending sources (generator unit test, one case per source).
- All **three** successor scans derive their skip set from one shared source — a duplicated literal in any of them is a review-blocking defect — and a test asserts the three agree on every fixture in the corpus.
- **A multi-step fork path and a multi-step branch case both still chain internally.** An intermediate path step's successor is the next step in its own path, never a main-flow step and never null. This needs a new fixture: every fork fixture in the corpus has single-step paths, and the one multi-step branch fixture is consumed only by a parser test, so nothing in-tree would catch a uniform skip emitting `MarkCompleted()` mid-path.
- The classification covers **loop-exit** branch cases, which live on `LoopModel.BranchOnExit` and are deliberately **absent** from `model.Branches` (`BranchExtractor` returns false for a `Branch` following a `RepeatUntil`). A branch-path set derived the obvious way — from `model.Branches`, as the existing lookups do — silently misses this appending source entirely.
- The fork's **join** step stays main-flow. `ForkPathSteps` (`SagaEmissionContext.cs:261-289`) is not a ready-made off-main-flow set: it deliberately includes `JoinStepName` for worker-command naming, and the join is precisely the step whose handler must chain to the terminal. Unioning it unfiltered classifies the join off-main-flow and feeds that error into the guard.
- `AwaitApproval` has **zero** behavioral coverage and gains a real-host proof that the terminal completes on the approved path. `OnFailure` does have coverage (`FailureHandlerWorkflow.cs`, registered at `Infrastructure/FailureHandlerHostFixture.cs:89`, exercised by `FailureHandlerChainTests`), but it proves only the **failure** path — its terminal is deliberately never reached — so the success-path terminal-completion case is untested and is what this adds. *(An earlier draft claimed both constructs had zero coverage; that was wrong for `OnFailure`.)*
- `SagaDocument` (non-EventSourced) output for a linear workflow with no off-main-flow steps is byte-unchanged (regression guard).

### DR-2: `StepNames` is document-ordered (#155)

`ParseForkPathStepsWithContext` (`StepExtractor.cs:604`) and `ParseBranchPathStepsWithContext` (`:649`) **return** their path steps instead of mutating a caller-owned list.
The backward caller (`:371`, `:376`) splices with `InsertRange(0, …)`; the forward caller (`:534`, `:539`) splices with `AddRange(…)`.
The direction decision moves to the call site, where it is already correct for every other construct.

**Acceptance criteria:**
- For `.StartWith<A>().Fork(…).Join<J>().Finally<Z>()` with no instance-named path steps, `model.StepNames` equals `model.Steps.Select(s => s.PhaseName)` — the two representations agree (generator unit test, asserted as an **ordered sequence**, not a set).
- The instance-named case is scoped out of that equality **only** until task 022 lands, because the two representations disagree on *cardinality* there, not order: `StepNames` carries *n+1* entries for *n* authored steps. `StepNames_InstanceNamedForkPathStep_HasNoTypeNamedTwin` is the oracle, and it turns green in task 022, not here.
- The same equality holds for the branch shape and for a fork nested inside `RepeatUntil` (the forward caller), proving the splice direction is per-caller.
- A patch that changes `:640`/`:758` to `Insert(0, …)` in place fails this suite — the fork-inside-`RepeatUntil` case is the discriminating test, and it discriminates by *staying* green, since it already passes today.
- `ExtractStepInfos`' dedupe (`StepExtractor.cs:126`, `GroupBy(PhaseName).Select(g => g.First())`) is asserted to preserve the intended `StepContext` for a name appearing both linearly and on a path — the reorder changes which occurrence wins.

### DR-3: The terminal-reachability failure class has a compile-time guard (#155, INV-5)

A new stable `AGWF` diagnostic fires at generation time when a workflow's declared terminal step is not the last main-flow step, or when any main-flow step's computed successor is an off-main-flow step.

**Acceptance criteria:**
- The diagnostic has a catalog entry with a stable id, and fires on a fixture reproducing each of the two conditions.
- Reverting DR-1 with DR-2 in place produces the diagnostic — i.e. the guard would have caught this bug before it shipped.
- The diagnostic does **not** fire on any existing fixture in `Strategos.Generators.Tests` or `Behavioral.Tests` (no false positives on the shipped corpus).
- **A new code is not Contracts-free** — an earlier draft had this backwards. `WorkflowDiagnostics.cs:7` is `using Strategos.Contracts.Generated;` and `AgwfSingleSourceTests.WorkflowDiagnostics_NoHandAuthoredAgwfLiterals_GrepGate` rejects any hand-authored `AGWF0[0-9]{2}` literal in production C#, so every id must come from a regenerated `AgwfCodes.g.cs`. Adding one therefore costs: an `AgwfCatalog.tsp` edit, a `Generated/` + `schemas/` regeneration that `contracts-codegen-guard` diffs, the Node-provisioned contracts test job, a matching descriptor for `AgwfCatalogParityTests`, and a `ContractsVersion` 0.4.0 → 0.5.0 bump. Because the emitted converter throws on an unknown member, the downstream consumer upgrade sequences **before** Strategos emits the new code. All of that is in scope for the task and stated in it.

### DR-4: `Branch` terminates, and is proven on a real host (#175)

The `Branch` mirror is tracked by #175 (filed 2026-08-22 against v2.11.0) and fixed here.
The decision must be made in `BranchHandlerEmitter.EmitPathEndHandler`, which today branches on the **branch-level** `branch.HasRejoinPoint` (`BranchHandlerEmitter.cs:235` → rejoin at `:266`, else `MarkCompleted()` at `:294-296`) and never consults the **case-level** `branchCase.IsTerminal`. Make it decide from `branchCase.IsTerminal`, falling back to `branch.HasRejoinPoint`.
Widening `BuildBranchPathInfo` (`SagaEmissionContext.cs:185`, which excludes terminal cases) is **also required** and is not sufficient on its own: that dictionary is the only live dispatch into `EmitPathEndHandler` on the C#-authoring path, so without the widening a terminal case's last step never reaches the handler at all — but with the widening and nothing else, `HasRejoinPoint` is true in the mixed shape and the terminal case is routed to the `Finally` step, which is what AC2 forbids. Both changes land together. *(The dedicated loop at `SagaStepHandlersEmitter.cs:132` computes the right value and discards it at `:147-150` without passing it on.)*

**Acceptance criteria:**
- A real-host branch twin (`Behavioral.Tests`) runs a `Branch(…).Finally<T>()` workflow to completion: the Marten saga document is deleted **and** per-step invocation counts are exact — the taken path runs once, the untaken path runs zero times, and the terminal runs **exactly once** (the count is what distinguishes completion from the current unbounded cycle).
- A terminal-case (`.Complete()`) branch workflow completes with the terminal case's last step calling `MarkCompleted()`, not the declared `Finally` step.
- Fixture nomenclature is concrete domain vocabulary per INV-4 (the house style is `ValidateOrder`/`ProcessOrder`/`ShipOrder`, not `PathA`/`PathB`); state and step types are `sealed` records per INV-6/INV-7.
- The twin extends the existing `RoundTripHostFixture` rather than adding a second Postgres container, and carries `[NotInParallel]` with a `WorkflowInvocationLog` reset.

### DR-5: Failure modes — every oracle in this slice must be able to fail

The existing proofs are weaker than the claims they will be asked to carry, and two of them cannot fail at all.
This requirement is about the tests, and it gates the others.

**Acceptance criteria:**
- **The twin's count-only oracle is strengthened before it is un-skipped.** `RoundTripBehavioralTests.cs:125-160` asserts only per-step counts and `TotalCount == 10`; a wrong fix yielding `[Start, Left, Right, End, Join]` passes it. An ordering assertion over the recorded invocation sequence is added, and a mutation of the fix that produces terminal-before-join is shown to fail it.
- **`RunWorkflowAsync` returning `true` is not accepted as proof of completion.** `RoundTripHostFixture.cs:131-136` returns `true` the moment `LoadAsync<TSaga>` yields null, so a saga that was never created is indistinguishable from one that completed. Every new twin asserts non-zero invocation counts alongside the boolean.
- **The parity guard learns to see `[Skip]`.** `StepConfigParityTests.cs:204-227` is a three-part substring check (path contains `Behavioral.Tests`, file exists, text contains the method name) — a skipped test, a commented-out test, or a name in a `<see cref>` all pass. It is hardened to reject a named proof that is skipped or absent, and the repo's existing instance of that failure mode (the `[Skip]`-ed twin) is used as its negative test case.
- **Docker absence is distinguishable from regression.** `PostgresFixture.cs:106` calls `StartAsync()` unconditionally, so a Docker-less developer sees the whole behavioral suite red — indistinguishable from a real break during this slice. The suite emits an unambiguous diagnostic when the daemon is unreachable, or the task explicitly records Docker as a dispatch prerequisite verified before implementation starts.

### DR-6: #145 is corrected, not implemented (#145)

The four `Deferred` parity entries and the two AGWF022 emission blocks assert something the generator does not do.

**Acceptance criteria:**
- The two false-positive AGWF022 emission blocks (`WorkflowIncrementalGenerator.cs:1070-1099`, `:1101-1127`) are deleted **in the same change** that retargets AGWF022 at the instance-named fork-path residue — deleting them alone leaves a declared-but-unreachable diagnostic, an INV-5 violation.
- The duplicate is closed: for an instance-named fork-path step, `StepNames` carries one entry per authored step and the fork dispatch targets the same name the handler is keyed on. `TryBuildConfiguredForkPathStepModel` stops stripping `InstanceName`, aligning it with `ParseForkPathStepModels`.
- AGWF022 is then either retargeted at an inert case that is still **reachable from the DSL**, or **retired outright** — its id never reused, per the never-renumber rule. Which of the two is a finding of the task, not a decision made in advance: retargeting it at a shape `IForkPathBuilder` cannot express would leave a declared control with no trigger, the same INV-5 violation as deleting the blocks and stopping there.
- The four `Deferred` entries at `StepConfigParityTests.cs:142-171` move to `Lowered`, each naming a real-host proof that runs (per DR-5's hardened guard) — two new behavioral proofs, one fork-path-intermediate, one loop-body-intermediate.
- `DeclaredButInertTests` and `docs/deferred-features.md:64,76` are corrected; the stale in-source reference to `#134` in the fork AGWF022 comment block is fixed to the issue that actually tracks it.

### DR-7: The generated public surface tells the truth (#155)

`ValidTransitions`/`IsValidTransition` (`TransitionsEmitter.cs:64-76`) and the Mermaid diagram are generated **public** API and are currently wrong for every fork and branch workflow.

**Acceptance criteria:**
- For a fork workflow, `ValidTransitions` contains no edge from the terminal to a fork-path step, and no edge chaining sibling exclusive paths.
- For a branch workflow, the same, plus each case's last step transitions to the rejoin target (or to `Completed` for a terminal case).
- `MermaidEmitter`'s `ToDictionary(l => l.LastBodyStepName, …)` (`:44-46`) does not throw on a nested/sibling-loop shape whose last body steps collide after the reorder — a duplicate key here is a generator crash, not a wrong diagram.
- The emitted `Phase` enum's member reordering is confirmed non-migrating by an assertion over raw `mt_doc_*` JSONB, not by resting on the serializer default.
- Every re-baselined golden is re-baselined by *reading the new output and confirming it is correct*, never by accepting the diff; the task names the files.

### DR-8: Auto-triage labels one scope, not all of them (#174)

The `auto-triage` job keyword-matches title and body and unions every match, so nearly every issue carries nearly every label.

**Acceptance criteria:**
- Replacement logic applies **at most one** `scope:` label, first match wins, matching on the **title** only — the decision recorded verbatim in the issue body.
- Proven by execution, not by lint. The job is gated `github.event_name == 'issues'`, the workflow declares no `workflow_dispatch`, and GitHub runs issue-triggered workflows **from the default branch** — so an issue filed while this change sits on a feature branch executes the *old* logic and would read as a false pass. **The route chosen is post-merge**, with the maintainer as the named owner — adding a `workflow_dispatch` trigger and an issue-number input purely to make one label assertion runnable early is more machinery than the check is worth. The task states this explicitly, and the PR is not treated as verified on lint alone.
- The backfill relabel over existing issues is presented as a diff for author approval before any board mutation; it is not applied as part of the PR.

### DR-9: Org CI is pinned, and the pin is maintained (#133)

Three jobs in `ci.yml` consume org reusable workflows at `@main`.

**Acceptance criteria:**
- The three `uses:` refs are pinned to the ref the maintainer selects — the issue comment offers `@v1` or `@v1.1` and this is a maintainer call, not an implementer one. Note a tag is *movable*, so only a full commit SHA is genuinely immutable; SHA-pinning is a third option and belongs on the menu. Note `@v1.5` is a strictly **older** tree than `@v1` (`v1` is 6 commits ahead, 0 behind); `@v1` and `@v1.5` are byte-identical to `@main` for the three consumed files, `@v1.1` differs in two of the three.
- The parity-gate runbook the maintainer explicitly asked for (*"run the parity gate (runbook) before merging"*) is run, or the maintainer signs off on substituting the read-only blob-SHA comparison for it. Blob evidence is not a substitute the implementer may choose unilaterally.
- The six bespoke gates — `pack-verify`, `basileus-smoke`, `builder-api-stability`, `dbsf-parity-guard`, `contracts-test`, `quality-gates` — are confirmed present and outside the bumped reusable, before and after.
- **The pin is maintained.** #133's third task asks for an update bot on the pin, and it is genuinely unsatisfied: there is no `.github/dependabot.yml`, and the org Renovate preset sets `github-actions` to disabled with the note that pins are owned by Dependabot. A static tag with nothing updating it is a worse steady state than the moving branch it replaces, so #133 does not close without this.

### DR-10: Docs state what is true, including the invariant that governs them (#166)

**Acceptance criteria:**
- The MCP pin moves to **2026-07-28** (verified as the latest non-draft upstream revision) at the two production docstrings (`ToolAnnotations.cs:4`, `OntologyToolDescriptor.cs:7`) **and** at `src/Strategos.Agents.Mcp/README.md:9`, which ships in the nupkg and is the highest-visibility stale pin — and which #166 does not name.
- `ToolAnnotations`' record shape is **unchanged**: the schema diff 2025-11-25 → 2026-07-28 shows `ToolAnnotations` identical, `Tool.execution` removed (Strategos never had it), `CallToolResult.resultType` added. The re-pin is a docstring change with no code change, and the issue's open question is answered rather than left open.
- INV-3's own catalog is amended (12 sites across three files), including the executable grep gate at `deterministic-checks.md:101`, whose deny-list must gain `2025-11-25` and whose path scope excludes the `Agents.Mcp` README it should cover.
  > **Defect found at implementation, 2026-08-22: this half cannot ship in the PR.** `.claude/` is gitignored (`.gitignore:81`) and `git ls-files .claude` is empty, so the invariant catalog is **not under version control** — it can never appear in a diff, and an isolated implementer cannot reach it. The amendment is therefore an out-of-band local change, applied and verified separately from the PR, and the lane produced a ready-to-apply patch rather than a commit. The wider implication is out of scope here and belongs in its own issue: the catalog that governs every design audit in this repo is untracked, unreviewable and un-shareable, which is why an 11-site staleness could accumulate unnoticed.
- `CallToolResult.resultType` (#176) and the pre-existing `Tool.icons` gap (#177) stay **filed, not folded in** — both are `src/Strategos.Ontology.MCP/**` code changes beyond a docs PR. #176 touches the surface #171 also touches; sequence them so they do not collide.
- **Both** `packages.md` files are corrected: `docs/packages.md` (named by #166, unpublished) and `docs/src/content/docs/reference/packages.md` (the page users actually read, not named by #166) — or the unpublished duplicate is deleted. Fixing only the one the issue names leaves the published page wrong.
  > **Corrected at implementation:** the listings were missing **nine** packages, not six. #166 names only the ontology family and misses `Contracts`, `Identity.Abstractions` and `Agents.Mcp`. Cross-check against the fourteen `.csproj` files carrying a `PackageId`, not against the issue's count.
- Historical records under `docs/designs/**`, `docs/plans/**` and `CHANGELOG.md` are **not** rewritten — they are dated statements that were true when made. The same treatment covers release-scoped prose in the published guide (for example `docs/src/content/docs/guide/ontology/mcp-integration.md:6`, which describes what a named release shipped): it is a historical claim, not a live pin, and is left alone.

## Technical Design

**Lane separation.**
The correctness lane (DR-1..DR-7) and the hygiene lane (DR-8..DR-10) have an empty source-file intersection.
Within the correctness lane, DR-1/DR-2/DR-3 are one branch — they share `StepExtractor.cs`, `WorkflowModel.cs` and `SagaEmissionContext.cs`, and DR-2 is unsafe without DR-1.
DR-6 is separable *in source* but couples semantically: DR-2 changes which step is `LastBodyStepName` for a fork inside a loop (`LoopExtractor.cs:333-341`), and that is the step DR-6's loop-body proof asserts against. Land DR-1..DR-3 first.

**The one shared file across all lanes is `CHANGELOG.md`**, which every lane must edit.
This repo has a recorded failure mode where the `weave` merge driver structurally mangles files two branches both edited.
Serialize CHANGELOG edits to the integration step, or have exactly one lane own the file.

**Why the guard is the deliverable.**
DR-1 fixes the five known appending sources. DR-3 is what makes the sixth one safe. The failure is compile-time decidable — the generator holds both the declared terminal and the computed successor at emission — and today the only thing that catches it is a Testcontainers real-host run that most contributors cannot execute. Per INV-5 that is the wrong tier.

## Alternatives

**Reorder only (the original "Option A").**
Rejected as unsafe on its own, not as wrong. It repairs the public generated surface and makes the three representations agree, so it is retained as DR-2 — but probing six shapes shows it regresses three, two into permanent saga hangs, because it relocates fork-path steps directly into the resume path of confidence-rejoin and approval-resume. It fixes the fork and branch instances and leaves failure-handler and approval-rejection steps appended at the tail, i.e. it does not close the class.

**Patch the successor predicate only (the original "Option B").**
Rejected. It fixes termination and leaves `ValidTransitions`, `IsValidTransition` and the Mermaid diagram permanently wrong for every fork and branch workflow — generated public API that lies. It also leaves `StepNames` disagreeing with `model.Steps` and the import path, which is the latent cause the next bug in this area will be built on.

**Fold #163/#115 in to close more of v2.11.0.**
Rejected for this slice. #163 ships inert (zero `op`/`interface`/`extern dec` in all 26 `.tsp` files, no `Contracts`↔`Ontology` dependency edge), and #115's only mechanical task needs an `[Obsolete]` with a named successor that #168 has not yet defined. Neither closes a live defect; both are groundwork with real design content of their own and deserve their own pass.

## Open Questions

1. ~~Author actions on the board.~~ **Done 2026-08-22:** #175 filed (Branch mirror, v2.11.0); #176 / #177 filed (MCP revision gaps); #145 re-scoped and retitled to match DR-6; #153's cluster index and decision log updated.
2. **#133's ref choice is yours** — `@v1` or `@v1.1` — and whether the runbook parity gate runs or blob-SHA evidence substitutes for it (DR-9).
3. **PR #173** is docs-only (+196/-0), `MERGEABLE` but behind, and seven open issues cite it as their grounding audit. Worth updating and merging independently of this slice.
4. **The workflow `rationale-claim-ontology`** has sat at `plan-review` since 2026-08-07 with 24 tasks, 0 complete, refuted 3/3 by two adversarial panels. Its tasks 013/014 are #115's remaining scope and are salvageable; the rest is blocked on the `RecordEmitter` extensibility question. Candidate for `/prune`.
5. **Does DR-3's diagnostic warrant a new `AGWF` code or a retarget of an existing one?** Either way the cost is the same and it is **not** free: both paths edit `AgwfCatalog.tsp` and regenerate, because production C# may not carry a hand-authored code literal. Default to a new code — a retarget would also change the meaning of an id already in the wild — and price in the regeneration, the Node contracts job, the `ContractsVersion` bump and the consumer upgrade from the start. *(An earlier draft called a new code "Contracts-free"; that was inverted, and DR-3 AC5 carries the corrected statement.)*

## Decomposition

### Scope

**Target:** Full design (DR-1 … DR-10).
**Excluded:** #147, #156, #163, #115 — see Open Questions. This slice closes #155, #175, #145, #174, #133 and #166, taking v2.11.0 from 10 open to 4; it does not close the milestone. Note #133 closes only with task 031 — without an update bot its own task list is unsatisfied.

**Repo conventions binding every task:** the TUnit runner is `cd src && dotnet test <proj> -- --treenode-filter "/*/*/*/MethodName"` (a bare `--filter` does not work here) and assertions are `await`ed; a process-wide static asserted by a test needs `[NotInParallel]` plus a reset hook, not per-test reset alone; every new hand-written public/internal record joins the owning project's sealed-type guard (emitted `Generated/` records are auto-covered by `EmitterShapeTests`); public-surface changes update `PublicAPI.Unshipped.txt` in the same task (note `Strategos.Generators` has no such file, and generated saga code is not tracked there); new diagnostic ids are monotonic via `AgwfCatalog.tsp` regeneration, and hand-edits to `schemas/` or `Generated/` are CI-rejected.

**Two conventions specific to this slice.** Tasks 002, 003, 007, 008, 012, 016, 017, 020 and 023 require a running Docker daemon — `PostgresFixture.InitializeAsync` calls `StartAsync()` unconditionally and *fails* rather than skipping, so verify the socket before dispatching them.
> **Verified working on this machine 2026-08-22** with podman 4.9.3 standing in for Docker. `DOCKER_HOST` is unset by default and `/var/run/docker.sock` points at the *rootful* socket, which is not running — so Testcontainers needs the rootless socket named explicitly:
> ```
> export DOCKER_HOST=unix:///run/user/1000/podman/podman.sock
> export TESTCONTAINERS_RYUK_DISABLED=true
> ```
> With those two set, `PostgresFixtureSmokeTests` passes in ~3.5s. Without them the whole behavioral suite fails in a way indistinguishable from a real regression, which is exactly the DR-5 hazard. And **`CHANGELOG.md` is owned exclusively by the integration step: no task edits it**, and task 032 writes the entry at the end. This repo's `weave` merge driver structurally mangles files two branches both edited, and four tracks would otherwise touch it.

### Traceability matrix (DR-N → tasks)

| DR | Requirement | Tasks |
|----|-------------|-------|
| DR-1 | Off-main-flow steps classified, not positional | 005, 006, 007, 008, 009, 012, 030 |
| DR-2 | `StepNames` is document-ordered | 004, 010, 011, 012 |
| DR-3 | Terminal-reachability has a compile-time guard | 013, 014 |
| DR-4 | `Branch` terminates, proven on a real host | 015, 016, 017 |
| DR-5 | Every oracle must be able to fail | 001, 002, 003, 004, 030 |
| DR-6 | #145 corrected, not implemented | 022, 023, 024 |
| DR-7 | Generated public surface tells the truth | 018, 019, 020, 021, 032 |
| DR-8 | Auto-triage labels one scope | 025, 026 |
| DR-9 | Org CI pinned, and the pin is maintained | 027, 031, 032 |
| DR-10 | Docs state what is true | 028, 029 |

### Tasks

Five tracks. **T (001–004)** hardens the oracles and must land first — every later acceptance claim rests on tests that can currently pass while wrong. **C (005–014)** is one branch: classify, then order, then guard, serialized because they share `StepExtractor.cs`, `WorkflowModel.cs` and `SagaEmissionContext.cs`. **B (015–017)** and **S (018–021)** fork off C once 010 lands. **P (022–024)** and **H (025–029)** run parallel to everything.

### Task 001: Parity guard rejects a skipped or absent proof

Teach the declared↔lowered parity guard to distinguish a real behavioral proof from one that is skipped, commented out, or present only in a doc-comment reference — the current three-part substring check cannot, which is how four provably-false entries stood.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-5
**Files:** `src/Strategos.Generators.Tests/Parity/StepConfigParityTests.cs`
**Verification:** a **synthetic** skipped-test fixture file is used as the negative case and is rejected by the hardened guard — not the live round-trip twin, whose skip task 012 removes, which would silently invert this assertion; a real, running proof is accepted; a name appearing only inside a `<see cref>` is rejected. `check_test_adequacy` kill-probe.
**Dependencies:** None · **Parallelizable:** Yes

### Task 002: Completion is proven by invocation counts, not by document absence

The behavioral harness reports success the moment the Marten document is gone, so a saga that was never created is indistinguishable from one that completed. Make the harness require positive evidence that the workflow ran, and make an unreachable Postgres daemon diagnosable as an environment fault rather than surfacing as a suite-wide regression.

**Risk Tier:** medium · **Test Layer:** integration
**Implements:** DR-5
**Files:** `src/Strategos.Generators.Behavioral.Tests/Infrastructure/RoundTripHostFixture.cs`, `src/Strategos.Generators.Behavioral.Tests/Infrastructure/PostgresFixture.cs`
**Verification:** a workflow whose start command is unrouted fails the harness instead of reporting completion; with the daemon unreachable the failure names the daemon, not the workflow. Integration suite green.
**Dependencies:** None · **Parallelizable:** Yes

### Task 003: The fork twin asserts order, not only counts

The twin's oracle is count-only, so a fix that runs every step once but places the terminal before the join passes it. Add an assertion over the recorded invocation sequence so the twin can fail on ordering.

**Risk Tier:** medium · **Test Layer:** integration
**Implements:** DR-5
**Files:** `src/Strategos.Generators.Behavioral.Tests/RoundTripBehavioralTests.cs`
**Verification:** the strengthened assertion is shown to reject a terminal-before-join sequence (fed as a constructed sequence, since the fix has not landed); the test remains skipped pending task 012. `check_test_adequacy`.
**Dependencies:** 002 · **Parallelizable:** Yes

### Task 004: An ordered step-list oracle at the parse tier

Nothing anywhere asserts the ordered step list of a C#-authored fork or branch workflow, in either direction — which is why no test can tell today's output from the fixed output at the cheapest tier that could catch it. Add ordered-sequence assertions comparing the two step representations for the fork shape, the branch shape, and a fork nested inside a repeat-until loop.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-2, DR-5
**Files:** `src/Strategos.Generators.Tests/Helpers/StepExtractorContextTests.cs`
**Verification:** the two **top-level** assertions (fork, branch) fail against the current generator — that red state is the point, so they land carrying `[Skip("red until task 010")]` with the observed failure recorded in the skip reason, and task 010 removes the skip. Without the quarantine the shared suite is red for four waves, and the likely repair is an implementer inverting the assertions to match today's output, after which task 010's criterion is satisfied before task 010 starts.
The **fork-inside-`RepeatUntil`** assertion is different and lands **green and unskipped**: the forward walkers already append in source order and both representations already agree there. It is the stay-green discriminator — an in-place `Insert(0, …)` edit to the shared helper turns it red, which is exactly the wrong fix being caught. Do not quarantine it and do not expect it to change state. `check_test_adequacy`.
**Dependencies:** None · **Parallelizable:** Yes

### Task 005: Classify every off-main-flow step

Give the saga emission context a complete set of step names that are not on the main flow — path steps for both branching constructs, failure-handler steps, approval rejection and escalation steps, and confidence-handler steps — derived from one source so no consumer can hold a partial copy.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-1
**Files:** `src/Strategos.Generators/Emitters/Saga/SagaEmissionContext.cs`, `src/Strategos.Generators/Models/WorkflowModel.cs`, `src/Strategos.Generators.Tests/Emitters/SagaEmissionContextTests.cs`
**Tests:** `OffMainFlowSteps_AllFiveSources_ContainsEveryAppendedName`, `OffMainFlowSteps_LinearWorkflow_IsEmpty`
**Verification:** for a workflow exercising all five sources, the classification contains exactly the off-main-flow names and no main-flow name; a duplicated literal skip-list in any consumer is a review-blocking defect. Integration suite across the emitter seam.
**Dependencies:** 004 · **Parallelizable:** No

### Task 006: All three successor scans consult the classification

Three places resolve a step's successor. Two carry their own filter that skips confidence handlers only; the third — the approval resume — has no filter at all and indexes the list raw. Route all three through the shared classification so that "last step" means no later main-flow step exists, independent of list position, while a step inside a fork path or branch case still chains to the next step in its own path.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-1
**Files:** `src/Strategos.Generators/Models/WorkflowModel.cs`, `src/Strategos.Generators/Emitters/Saga/SagaStepHandlersEmitter.cs`, `src/Strategos.Generators/Emitters/Saga/SagaApprovalComponentEmitter.cs`, `src/Strategos.Generators.Tests/Emitters/SagaStepHandlersEmitterTests.cs`
**Tests:** `Successor_TerminalFollowedByOffMainFlow_ResolvesToNull`, `SuccessorScans_AllThree_AgreeOnEveryStep`, `ApprovalResume_TargetFollowedByForkPath_SkipsToMainFlow`
**Verification:** a workflow whose terminal is followed by an off-main-flow entry emits completion in the terminal's handler, one case per source; all **three** scans produce identical successors for every fixture in the corpus. The approval scan is the one with no filter at all today (`SagaApprovalComponentEmitter.cs:69-76` indexes `StepNames` raw), so it is already wrong before this slice and becomes a permanent hang after task 010 reorders — it must land here, not later. Integration suite.
**Dependencies:** 005 · **Parallelizable:** No

### Task 007: A failure-handler workflow reaches completion on its success path

Failure-handler steps are appended after the terminal and are invisible to the successor scan. The construct **does** have real-host coverage, but it proves only the failure path — the existing fixture's terminal is deliberately never reached — so the success-path terminal-completion case is untested. Extend the existing fixture; do not create it.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** acceptance
**Implements:** DR-1
**Files:** `src/Strategos.Generators.Behavioral.Tests/Workflows/FailureHandlerWorkflow.cs` (**existing, 178 lines — extend**), `src/Strategos.Generators.Behavioral.Tests/Infrastructure/FailureHandlerHostFixture.cs` (existing, registration at `:89`), `src/Strategos.Generators.Behavioral.Tests/FailureHandlerSuccessPathTests.cs` (new)
**Tests:** `Saga_FailureHandlerWorkflow_CompletesOnSuccessPath`, `Saga_FailureHandlerWorkflow_RunsHandlerZeroTimesWhenNothingFails`
**Verification:** on the real host the saga document is deleted and per-step counts are exact — the terminal runs once and the failure handler runs zero times on the success path. The existing `FailureHandlerChainTests` proof stays green; overwriting that file rather than extending it destroys a shipped proof. Fixture nomenclature is concrete domain vocabulary; state and step types are sealed records.
**Dependencies:** 006, 002 · **Parallelizable:** Yes (with 008)

### Task 008: An approval workflow reaches completion on a real host

Approval rejection and escalation steps are appended after the terminal by the same mechanism, and the construct likewise has no real-host coverage. Author the fixture and prove the terminal completes on the approved path.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** acceptance
**Implements:** DR-1
**Files:** `src/Strategos.Generators.Behavioral.Tests/Workflows/ApprovalGatedWorkflow.cs` (new), `src/Strategos.Generators.Behavioral.Tests/Infrastructure/ApprovalHostFixture.cs` (new — a behavioral workflow runs only if a host fixture calls its generated `Add{Pascal}Workflow()`), `src/Strategos.Generators.Behavioral.Tests/ApprovalBehaviorTests.cs` (new)
**Tests:** `Saga_ApprovedPath_CompletesWithoutRunningRejection`, `Saga_ApprovalResume_TargetsMainFlowStep`
**Verification:** on the real host the approved path completes with the rejection and escalation steps running zero times; the resume target after approval is a main-flow step, never a path step.
**Dependencies:** 006, 002 · **Parallelizable:** Yes (with 007)

### Task 009: Linear workflows are byte-unchanged

The classification must be inert for a workflow that has no off-main-flow steps. Pin that with an output-equality guard so a regression here is caught without reading a diff.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-1
**Files:** `src/Strategos.Generators.Tests/Emitters/LinearWorkflowOutputRegressionTests.cs` (new)
**Verification:** non-event-sourced saga output for a linear workflow is byte-identical to the baseline captured in task 030. Authoring the expectation *after* the change would embed post-change output and be green by construction — the failure mode DR-5 exists to prevent — which is why the baseline is captured first and this task only compares against it. `check_test_adequacy`.
**Dependencies:** 006, 030 · **Parallelizable:** Yes

### Task 010: Splice direction belongs to the caller

The two path-collection helpers append into a caller-owned list, which is correct for the forward caller and wrong for the backward one. Have them return their steps in document order and let each caller splice by its own direction, so a future third caller cannot silently inherit the wrong one.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-2
**Files:** `src/Strategos.Generators/Helpers/StepExtractor.cs`, `src/Strategos.Generators.Tests/Helpers/StepExtractorContextTests.cs`
**Tests:** `StepNames_ForkWorkflow_MatchesDocumentOrder`, `StepNames_ForkInsideRepeatUntil_MatchesDocumentOrder`
**Verification:** task 004's two quarantined assertions turn green and its nested-loop assertion **stays** green; an in-place edit to the shared helper instead of a per-caller splice leaves that case red. Integration suite.
**Dependencies:** 006, 004 · **Parallelizable:** No

### Task 011: Dedupe preserves the intended step context

The step list is de-duplicated by taking the first occurrence of each name, so relocating path steps can change which occurrence wins and therefore which context survives for a name that appears both linearly and on a path. Pin the intended outcome.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-2
**Files:** `src/Strategos.Generators/Helpers/StepExtractor.cs`, `src/Strategos.Generators.Tests/Helpers/StepExtractorContextTests.cs`
**Verification:** a name appearing both linearly and on a path retains the context the emitter needs, asserted explicitly rather than incidentally. `check_test_adequacy`.
**Dependencies:** 010 · **Parallelizable:** Yes

### Task 012: The fork twin runs green

Remove the skip from the C#-authored fork twin and prove it equivalent to its imported-JSON sibling on the real host, against the strengthened oracle.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** acceptance
**Implements:** DR-1, DR-2
**Files:** `src/Strategos.Generators.Behavioral.Tests/RoundTripBehavioralTests.cs`
**Tests:** `ForkJoinCSharpTwin_RunsIdentically_ToJsonImport` (existing, un-skipped)
**Verification:** the twin passes with exact per-step counts **and** the ordering assertion from task 003; the skip attribute and its prose are removed together.
**Dependencies:** 010, 003 · **Parallelizable:** No

### Task 013: A build-time diagnostic for unreachable termination

The generator holds both the declared terminal and each computed successor at emission time, so this whole failure class is decidable before anything runs — yet only a real-host test can currently catch it. Add a stable diagnostic that fires when a declared terminal is not the last main-flow step, or when a main-flow step's successor resolves to an off-main-flow step.

The catalog entry and the descriptor that satisfies it land together. Splitting them leaves `AgwfCatalogParityTests` red — it iterates every catalog entry and reports one with no matching descriptor as a mismatch — so a contracts-only half would redden an existing green gate for as long as it stood alone.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-3
**Files:** `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp`, regenerated `src/Strategos.Contracts/Generated/agwf-catalog.json`, `src/Strategos.Contracts/schemas/` and `docs/diagnostics/agwf.md` (the three trees the codegen guard diffs), `src/Strategos.Contracts/Strategos.Contracts.csproj` (`ContractsVersion`), `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs`, `src/Strategos.Generators/WorkflowIncrementalGenerator.cs`, `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs` (new)
**Tests:** `Diagnostic_TerminalNotLastMainFlowStep_Fires`, `Diagnostic_SuccessorResolvesOffMainFlow_Fires`
**Verification:** the diagnostic fires on a fixture for each of the two conditions; the catalog parity test stays green because the descriptor lands with the entry; regeneration is reproducible so the codegen guard's diff over `Generated/`, `schemas/` and `docs/diagnostics` is clean; `ContractsVersion` moves 0.4.0 → 0.5.0 and the consumer upgrade sequences first, because the emitted converter throws on an unknown member. Take the off-main-flow classification as a parameter rather than reading it from context — task 014's counterfactual needs that seam. No hand-authored code literal appears in production C#; a grep gate rejects those.
**Dependencies:** 006, 010 · **Parallelizable:** No

### Task 014: The guard would have caught this bug

A guard is only worth its cost if it fires on the defect it exists for and stays silent otherwise. Prove both.

**Risk Tier:** high · **Test Layer:** integration
**Implements:** DR-3
**Files:** `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs`
**Tests:** `Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug`, `Diagnostic_ExistingCorpus_NeverFires`
**Verification:** the diagnostic takes the off-main-flow classification as a parameter, so the counterfactual is a real test rather than prose: passing an empty classification reproduces the pre-fix state and the diagnostic fires. Without that seam a test cannot revert production code, and the only implementable form collapses into task 013's first case. The corpus sweep runs over the 34 named workflow sources in `src/Strategos.Generators.Tests/Fixtures/SourceTexts.cs`, enumerated by reflection, and the diagnostic fires on none of them.
**Dependencies:** 013 · **Parallelizable:** No

### Task 015: Terminal branch cases can be recognised as terminal

Cases that end the workflow rather than rejoining are never distinguished at emission, and they are excluded from the lookup that is the only live route into the path-end handler — so their last step never reaches it. Widen that lookup to admit terminal cases **and** make the path-end handler decide from the **case-level** terminal flag, falling back to the branch-level rejoin flag. Either change alone is insufficient.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-4
**Files:** `src/Strategos.Generators/Emitters/Saga/BranchHandlerEmitter.cs`, `src/Strategos.Generators/Emitters/Saga/SagaStepHandlersEmitter.cs`, `src/Strategos.Generators/Emitters/Saga/SagaEmissionContext.cs`, `src/Strategos.Generators/Helpers/BranchExtractor.cs`, `src/Strategos.Generators.Tests/Emitters/BranchTerminalCaseTests.cs` (new)
**Tests:** `Branch_TerminalCase_MarksCompletedAtCaseLastStep`, `Branch_RejoiningCase_RoutesToRejoinTarget`
**Verification:** in the mixed shape — one workflow-ending case, one rejoining case, and a declared terminal — the ending case completes at its own last step while the rejoining case still routes to the rejoin target. That shape is the discriminating one: because the branch-level rejoin flag is true there, any fix that reads only that flag routes the ending case to the declared terminal instead. Also delete the unused step-list parameter threaded through the branch extractor, which nothing reads. Integration suite.
**Dependencies:** 006, 010 · **Parallelizable:** No

### Task 016: A branch fixture exists

There is no real-host branch workflow anywhere in the repo. Author one covering a rejoining case and an otherwise case, plus a variant with a workflow-ending case.

**Risk Tier:** high · **Test Layer:** acceptance
**Implements:** DR-4
**Files:** `src/Strategos.Generators.Behavioral.Tests/Workflows/RoundTripBranchWorkflow.cs` (new), `src/Strategos.Generators.Behavioral.Tests/Workflows/TerminalBranchWorkflow.cs` (new), `src/Strategos.Generators.Behavioral.Tests/Infrastructure/RoundTripHostFixture.cs`
**Tests:** `BranchFixture_Registers_OnSharedRoundTripHost`
**Verification:** the fixture compiles and registers on the existing round-trip host rather than standing up a second database container; step names are concrete domain vocabulary in the house style, not abstract graph naming; state and step types are sealed records whose steps return a new state rather than mutating their input. Its shared invocation log means the test class runs non-parallel with a reset hook.
**Dependencies:** 002 · **Parallelizable:** Yes

### Task 017: The branch workflow terminates on a real host

Prove the fix against the defect's actual signature: today the terminal cascades back into a branch path and the workflow cycles forever without the saga document ever being deleted.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** acceptance
**Implements:** DR-4
**Files:** `src/Strategos.Generators.Behavioral.Tests/BranchBehaviorTests.cs` (new)
**Tests:** `Saga_BranchWorkflow_CompletesWithTerminalRunningExactlyOnce`, `Saga_BranchWorkflow_RunsUntakenPathZeroTimes`, `Saga_TerminalBranchCase_CompletesAtCaseEnd`
**Verification:** the saga document is deleted **and** counts are exact — the taken path runs once, the untaken path zero times, and the terminal **exactly once**. The count on the terminal is what distinguishes completion from the current unbounded cycle; a document-absence boolean alone cannot, and would pass against the broken generator once the cycle is externally killed.
**Dependencies:** 015, 016 · **Parallelizable:** No

### Task 018: Published transitions describe the real graph

The generated transition table is built as a flat linear chain over the step list, so a branching workflow publishes sibling exclusive paths chained to one another and a terminal that transitions into a path step. It is public generated API. Make it describe the actual graph.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-7
**Files:** `src/Strategos.Generators/Emitters/TransitionsEmitter.cs`, `src/Strategos.Generators.Tests/Emitters/TransitionsEmitterUnitTests.cs`
**Tests:** `ValidTransitions_ForkWorkflow_HasNoTerminalToPathEdge`, `ValidTransitions_BranchWorkflow_DoesNotChainSiblingCases`
**Verification:** no edge from the terminal to a path step; no edge chaining sibling exclusive paths; each case's last step transitions to its rejoin target, or to completion for a workflow-ending case. This changes emitted public API; task 032 records it in the release note.
**Dependencies:** 010 · **Parallelizable:** Yes (with 019, 020)

### Task 019: The diagram generator survives colliding loop boundaries

The diagram builds lookup tables keyed on each loop's first and last body step, and relocating path steps changes which names those are. A duplicate key throws, so a nested or sibling loop shape whose boundaries collide after the change becomes a generator crash rather than a wrong picture.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-7
**Files:** `src/Strategos.Generators/Emitters/MermaidEmitter.cs`, `src/Strategos.Generators.Tests/Emitters/MermaidEmitterUnitTests.cs`
**Verification:** a nested and a sibling loop shape both emit a diagram instead of throwing; fork paths are rendered as parallel rather than falling through to the linear arm. Note the existing unit fixtures are hand-built and use prefixed case names the branch extractor does not actually produce — align them or the test proves nothing. `check_test_adequacy`.
**Dependencies:** 010 · **Parallelizable:** Yes (with 018, 020)

### Task 020: Reordering the phase enum is not a data migration

The generated phase enum is emitted with a string converter, so reordering its members should not rewrite persisted saga documents. Prove that against stored data rather than resting on a serializer default.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-7
**Files:** `src/Strategos.Generators.Behavioral.Tests/PhaseEnumPersistenceTests.cs` (new), `src/Strategos.Generators.Behavioral.Tests/Infrastructure/RoundTripHostFixture.cs` (registration)
**Tests:** `PersistedSaga_PhaseColumn_StoresEnumName`, `PersistedSaga_WrittenBeforeReorder_LoadsAfterReorder`
**Verification:** the raw persisted document stores the phase as its name, and a saga written before the reorder loads correctly after it.
**Dependencies:** 010, 002 · **Parallelizable:** Yes (with 018, 019)

### Task 021: Re-asserted expectations are read, not accepted

This repo has no golden or snapshot files — the affected expectations are inline string assertions in ordinary test classes. Every one touched by this change is re-asserted by reading the new output and confirming it is correct, never by making the assertion match whatever now comes out.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-7
**Files:** `src/Strategos.Generators.Tests/Emitters/GeneratorIntegrationTests.cs`, `src/Strategos.Generators.Tests/Emitters/TransitionsEmitterUnitTests.cs`
**Verification:** each re-asserted expectation is named in the task's completion note with a one-line statement of what changed and why it is now correct. A re-assertion with no such statement is a review-blocking defect.
**Dependencies:** 010, 018, 019 · **Parallelizable:** No

### Task 022: Close the instance-named fork-path duplicate, then settle the diagnostic

Two diagnostic emission blocks claim intermediate confidence gating does not lower. It does, so both are false positives. The real defect nearby is a **duplicate**, not a dropped gate: one of the two fork-path parse paths strips the instance name while the other keeps it, so the type-named twin is appended alongside the instance-named entry and the workflow gets an extra phase, command and handler — carrying no configured model and therefore no gate, and it is the name the fork dispatch targets. Align the two parse paths so both keep the instance name. Then settle the diagnostic: retarget it only at an inert case that is still reachable from the builder surface, and if none is, retire the id instead of pointing it at a shape the builder cannot express. Whichever way it goes, it lands in the same change as the deletion — deleting the blocks alone leaves a declared diagnostic with no emission path at all.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** integration
**Implements:** DR-6
**Files:** `src/Strategos.Generators/Helpers/StepExtractor.cs`, `src/Strategos.Generators/WorkflowIncrementalGenerator.cs`, `src/Strategos.Generators.Tests/Diagnostics/DeclaredButInertTests.cs`, `src/Strategos.Generators.Tests/Helpers/StepExtractorContextTests.cs`
**Tests:** `StepNames_InstanceNamedForkPathStep_HasNoTypeNamedTwin`, `DeclaredButInert_IntermediateForkPathStep_DoesNotFire`, `DeclaredButInert_IntermediateLoopBodyStep_DoesNotFire`
**Verification:** an instance-named fork-path step yields one entry per authored step, one command and one handler, with the fork dispatch targeting the name the handler is keyed on; the diagnostic no longer fires on the two fixtures it currently false-positives on; and no code path leaves it declared with no trigger. Record which way the diagnostic went — retargeted or retired — and, if retired, that the id is never reused. Integration suite.
**Dependencies:** 010 · **Parallelizable:** Yes

### Task 023: Intermediate confidence gating is proven, not asserted

Two real-host proofs — one intermediate path step, one intermediate loop-body step — establishing that a below-threshold score routes to the declared handler and that the workflow still completes.

**Risk Tier:** high · **Boundary Touching:** true · **Test Layer:** acceptance
**Implements:** DR-6
**Files:** `src/Strategos.Generators.Behavioral.Tests/Workflows/IntermediateConfidenceWorkflow.cs` (new — a fork path and a loop body, each with a confidence-gated intermediate step), `src/Strategos.Generators.Behavioral.Tests/Infrastructure/ConfidenceHostFixture.cs` (**new** — no such fixture exists; the existing confidence tests bind to `WolverineHostFixture`, so either extend that or author this one), `src/Strategos.Generators.Behavioral.Tests/IntermediateConfidenceBehaviorTests.cs` (new)
**Tests:** `Saga_IntermediateForkPathLowConfidence_RoutesToHandler`, `Saga_IntermediateLoopBodyLowConfidence_RoutesToHandler`
**Verification:** both run on the real host and are rejected by the hardened parity guard if skipped. Note the ordering change alters which step is a loop's last body step for a fork inside a loop, and that is the step the loop-body proof asserts against — write the assertion against the post-change boundary.
**Dependencies:** 022, 001, 002 · **Parallelizable:** No

### Task 024: The parity table stops lying

Move the four entries from deferred to lowered against the proofs from task 023, and correct every place the false premise was written down.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-6
**Files:** `src/Strategos.Generators.Tests/Parity/StepConfigParityTests.cs`, `docs/deferred-features.md`, `src/Strategos.Generators/WorkflowIncrementalGenerator.cs` (comment)
**Verification:** the hardened guard accepts all four, each against a proof that runs. A **remaining** deferred entry now also asserts that the diagnostic it cites actually fires — checking only for a positive issue number is how four false entries survived — but the four being flipped here must not be made to depend on AGWF022 firing, since task 022 may retire it. The stale in-source reference to a closed issue is corrected to the one that tracks the work.
**Dependencies:** 023, 001 · **Parallelizable:** No

### Task 025: Auto-triage applies one scope label

Replace the union-of-all-keyword-matches logic with at most one scope label, first match wins, matched on the title only — the decision already recorded in the issue.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-8
**Files:** `.github/workflows/project-automation.yml`
**Verification:** static analysis and workflow lint only — which is not evidence the logic works, and the task says so. Execution is task 026's post-merge step; this task does not claim the behavior is proven.
**Dependencies:** None · **Parallelizable:** Yes

### Task 026: Proven on a live issue after merge, and the backfill is a proposal

GitHub runs issue-triggered workflows from the **default branch**, so an issue filed while this sits on a feature branch would exercise the old logic and read as a false pass. The proof is therefore explicitly post-merge, owned by the maintainer: after merge, file an issue, confirm exactly one scope label lands, close it. Produce the backfill relabel as a reviewable diff.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-8
**Files:** none in-tree; the evidence is a live issue and a proposed diff recorded against `docs/specs/2026-08-22-correctness-core.md`
**Verification:** run **after merge**, the test issue receives one scope label and no type-label noise. Until that runs, DR-8 is not discharged and the task stays open past the PR. The backfill over existing issues is presented for approval and **not** applied as part of this work — it is a mutation of the live board.
**Dependencies:** 025 · **Parallelizable:** No

### Task 027: Org CI runs from an immutable ref

Three jobs consume org reusable workflows from a moving branch. Pin them.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-9
**Files:** `.github/workflows/ci.yml`
**Verification:** the ref is the one the maintainer selects — this is a maintainer call, and the candidate tags are not equivalent: two are byte-identical to the moving branch for all three consumed files, the third differs in two of them, and one commonly-suggested tag is a strictly *older* tree. The parity runbook the maintainer asked for is run, or the maintainer signs off on substituting read-only blob comparison for it; the implementer does not make that substitution unilaterally. All six bespoke gates are confirmed present and outside the bumped workflow, before and after.
**Dependencies:** None · **Parallelizable:** Yes

### Task 030: Capture the pre-change output baseline

Task 009 compares linear-workflow saga output before and after, and there is no "before" in the repo — there are no snapshot or golden files anywhere in the generator test project. Capture one now, while the generator is still unchanged, and commit it as a test resource. Authoring that expectation after the change instead would embed post-change output and be green by construction.

**Risk Tier:** medium · **Test Layer:** unit
**Implements:** DR-1, DR-5
**Files:** `src/Strategos.Generators.Tests/Baselines/LinearWorkflowSaga.baseline.txt` (new), `src/Strategos.Generators.Tests/Emitters/LinearWorkflowOutputRegressionTests.cs` (new, capture half)
**Verification:** the baseline is generated at the pre-change commit and its provenance — the commit it was captured at — is recorded alongside it. A baseline captured after any task in the correctness spine has landed is worthless and must be recaptured.
**Dependencies:** None · **Parallelizable:** Yes

### Task 031: The pin has an update bot

A pinned tag with nothing watching it is a worse steady state than the moving branch it replaces, and #133 asks for this explicitly. The org Renovate preset disables the `github-actions` manager with the note that pins are owned by Dependabot, and this repo has no Dependabot config at all.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-9
**Files:** `.github/dependabot.yml` (new)
**Verification:** a `github-actions` entry scoped to `.github/workflows/`, valid against the Dependabot schema, and not shadowed by the org Renovate preset. Without this task #133 does not close.
**Dependencies:** 027 · **Parallelizable:** No

### Task 028: The protocol pin, and the invariant that states it

Move the protocol pin to the current upstream revision at all three production sites, including the package README that ships to the registry and is the highest-visibility stale pin. Amend the invariant catalog that asserts what "latest" is, including its executable check, whose deny-list omits the revision being superseded and whose path scope excludes the README.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-10
**Files:** `src/Strategos.Ontology.MCP/ToolAnnotations.cs`, `src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs`, `src/Strategos.Agents.Mcp/README.md`, `.claude/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md`, `.claude/skills/strategos-design-invariants/references/deterministic-checks.md`, `.claude/skills/strategos-design-invariants/SKILL.md`
**Verification:** no code change — the annotation record's shape is identical across the two revisions, which is what makes this a docstring move. The catalog amendment goes through the invariant-amendment path rather than a hand edit. Historical records under design, plan and changelog paths are left alone; they were true when written. The two genuine revision gaps stay filed, not folded in.
**Dependencies:** None · **Parallelizable:** Yes

### Task 029: Both package listings, not just the one the issue names

There are two package listing files and the issue names the unpublished one; the site builds only from the other. Correct both, or delete the unpublished duplicate.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-10
**Files:** `docs/packages.md`, `docs/src/content/docs/reference/packages.md`
**Verification:** every packable ontology project appears in the published listing, cross-checked against the actual project list rather than the issue's count. Prose-quality checks scan both source and docs trees, so run them over the whole change.
**Dependencies:** None · **Parallelizable:** Yes

### Task 032: One release note, written once

`CHANGELOG.md` is the only file all four tracks would otherwise touch, and this repo has a recorded failure mode where the merge driver mangles files two branches both edited. One task owns it, at the end.

**Risk Tier:** low · **Test Layer:** unit
**Implements:** DR-7, DR-9
**Files:** `CHANGELOG.md`
**Verification:** the entry records the consumer-visible change to the generated transition table and phase enum ordering, the two fixed termination defects with their issue numbers, and whichever way task 022 settled the diagnostic. Prose checks scan root, source and docs markdown, so run them over the change.
**Dependencies:** 012, 013, 017, 018, 021, 024, 031 · **Parallelizable:** No

### Parallelization

| Group | Tasks | Notes |
|---|---|---|
| Wave 1 | 001, 002, 004, 025, 027, 028, 029, 030 | No dependencies. Oracle hardening, the baseline capture (which must happen before the spine moves), and the whole hygiene lane. |
| Wave 2 | 003, 005, 016 | 003 and 016 need the harness; 005 needs the parse oracle. |
| Wave 3 | 006 → 010 | Serial. The spine: classify, then order. |
| Wave 4 | 007, 008, 009, 018, 019, 020 | Fan out once 010 lands. |
| Wave 5 | 011, 022 → 023, 012, 013, 015 | Serial within the `StepExtractor.cs` group: 011 and 022 both edit it, and 022 must follow 010 before its duplicate oracle is evaluable. 012 closes fork; 013 the guard; 015 branch. |
| Wave 6 | 014, 017, 021, 024, 031 | Closing proofs, the re-assertion pass, and the pin's update bot. |
| Post-merge | 026 | DR-8's only honest proof: issue-triggered workflows run from the default branch. |
| Wave 7 | 032 | The release note, written once, after everything it describes has landed. |

The correctness spine (005 → 006 → 010) is the critical path and cannot be parallelized — those three share the same three files and 010 is unsafe before 006. A second serial group follows it: tasks 011 and 022 both edit `StepExtractor.cs` and `StepExtractorContextTests.cs`, the same pair 004 and 010 touch, so they queue behind 010 rather than running beside it. Everything else fans out around the spine.

### Plan-verification results

| Gate | Result |
|---|---|
| `check_plan_coverage` | **PASS** — 10/10 requirements covered, 0 gaps |
| plan-review (2 voters, fresh-context, standard rung) | **Round 1: refuted**, 5 HIGH. **Round 2: all five closed**, 3 new HIGH — Open Question 5 still carried the inverted Contracts claim, the 013/033 split reddened a gate for four waves, and a uniform off-main-flow skip would have emitted `MarkCompleted()` mid-path. All three fixed in revision 2, along with the loop-exit branch source, the fork join step, task 004's already-green nested assertion, DR-8's execution route, and the wave/matrix staleness. |
| `check_provenance_chain` | **PASS** — 10/10 traced, 0 orphan refs |
| `check_task_decomposition` | **PASS** — 32/32 well-decomposed, valid DAG, no file conflicts between parallel tasks. One advisory breadth challenge on task 013 (6 modules vs a baseline of 4) is **deliberately overridden**: revision 1 did split it along module lines, and round-2 review showed the split leaves `AgwfCatalogParityTests` red from wave 1 to wave 5, because a catalog entry with no matching descriptor is reported as a mismatch. The entry and its descriptor must land together. Breadth here is the cheaper defect. |
| `spec_coverage_check` | **N/A — structurally inapplicable to this repo.** The gate accepts a declared test path only when it ends `.test.<ext>` or `.spec.<ext>` (`TEST_PATH_SUFFIX = /\.(test\|spec)\.[cm]?[jt]sx?$/i`), and its explicit `**Test file:**` form runs through the same check. .NET test files are `*Tests.cs`, so no correct declaration in this repo can pass. Declaring paths as `.test.ts` to make it green would be false evidence. The obligation it stands for is discharged instead by `check_task_decomposition`, which confirmed every task names concrete file targets and every high-tier task names its tests in `Method_Scenario_Outcome` form. |
| `check_coverage_thresholds` | **N/A — same cause.** It reads `coverage/coverage-summary.json`, a JS-tooling artifact this repo does not produce; .NET coverage is enforced by the `coverage-gate` CI job. |

The two N/A gates are the known Node-repo assumption in the exarchos gate suite, not a plan defect. Plan-review should not treat them as gaps.
