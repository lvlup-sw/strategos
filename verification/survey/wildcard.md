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

# Survey lens 7 — Wildcard

Covered ground (orchestrator one-liners; not re-done here): mechanism of the
diff; word-for-word claims; authority counts; production-path reachability;
existing-proof rungs; history/recurrence. This file starts after those.

Empty would have been useful. It is not empty. The oddities are interactions
and frame mismatches, not missing types or missing registrations.

## Findings

### W1. AGWF035 under-reach answers a different question than the motivating defect

CHANGELOG Residue (`CHANGELOG.md:172-177`) and the guard remarks
(`TerminalReachabilityGuard.cs:20-28`, `:132-140`) claim the new arm is the
compile-time lock for “a terminal that is last but that a rejoin construct’s
last step does not dispatch,” so the next dropped edge does not need Postgres.
Issue 185 named #184 as that instance: loop-exit rejoin cases never published
`Start{Finally}` because they were absent from the dedicated branch-handler
loop — the **model already knew** the rejoin.

The arm does not inspect saga emission. It fires when
`EnumerateRejoinDispatchersOf` (`TerminalReachabilityGuard.cs:174-231`) lists a
last step whose `PhaseGraph.SuccessorsOf` does not contain the terminal
(`:150-164`). `PhaseGraph.AddLoopEdges` / `AddBranch` (`PhaseGraph.cs:209-227`,
`:456-462`) add that rejoin edge from the same model fields the #184 handler
loop ignored. A handler that forgets a construct the IR still describes keeps
the graph edge, so under-reach stays silent and `ValidTransitions` still
advertises the missing dispatch.

The shared-graph claim (`PhaseGraph.cs:16-17`, `CHANGELOG.md:176-177`) binds
the diagnostic to the emitted table, not to the handler that actually
publishes `Start{Finally}`. The 2.11.0 body already says nothing in the
generated saga consults that table at runtime (`CHANGELOG.md:128`). Two
unread-at-runtime artifacts now agree; the dispatch walk is a third.

Positive tests never compile a naturally dropped-edge workflow. They inject
`PhaseGraph.WithoutSuccessor` (`TerminalReachabilityDiagnosticTests.cs:459`,
`:484`). Production never constructs that graph: the generator call site
omits `phaseGraph` (`WorkflowIncrementalGenerator.cs:1038-1043`), so the
guard rebuilds `PhaseGraph.Build(model)` (`TerminalReachabilityGuard.cs:127`).
On any well-formed model the two walks agree and the new arm does not fire.

The plan’s own widen-if-lying rule (`issue_185_remainder_125df8c7.plan.md`
T1) is met and ignored. Under-reach reports `{0}` = terminal, `{2}` = last
step that should have dispatched it (`TerminalReachabilityGuard.cs:157-163`).
The catalog sentence still says `{0}` **chains to** `{2}` and “the saga runs
**past** its declared termination” (`AgwfCatalog.tsp:344`,
`WorkflowDiagnostics.cs:564`, `docs/diagnostics/agwf.md:43`). That is the
opposite polarity. T1 kept the template to avoid a catalog bump; T2 in the
same wave already paid `0.6.0 → 0.7.0` for AGWF037. The constraint that
justified the lie was removed by a sibling track.

Tests assert only `Contains("CloseClaim")` / `Contains("PayClaim")`
(`TerminalReachabilityDiagnosticTests.cs:470-473`). Substring presence cannot
fail when the sentence names the right steps and describes the wrong fault.

### W2. Import lowering shares emitters, not the new termination lock

JSON import bridges into the same `EmitWorkflowSources`
(`WorkflowIncrementalGenerator.cs:93-112`, `:117-122`) and therefore the
same `TransitionsEmitter` / `PhaseGraph`. `TerminalReachabilityGuard.Report`
has one production call, on the C# `[Workflow]` transform (`:1038`). The
import pipeline reports bridge diagnostics and emits; it never calls the
guard. AGWF037 is on both extractors (`DiagnosticForkExtractor`,
`WireToModelBridge`). AGWF035 under-reach is C#-only. A JSON twin of the
#184 shape can still emit a table that claims the Finally edge and a saga
that does not dispatch it, with no AGWF035.

### W3. “Fail closed” is not one policy in this wave

AGWF037 is in the `hasErrors` list that returns a null model
(`WorkflowIncrementalGenerator.cs:930-942`) — no saga. AGWF036 already gated
that way via `pathEndTypeCollisions`. AGWF035 is reported after that gate,
on a live model (`:1033-1045`). The C# output path then emits if
`result.Model is not null` (`:82-86`). An AGWF035 **error** still generates
the saga. Same wave, two “the class cannot silently reopen” codes, opposite
generation consequences. Suppress AGWF035 and the broken saga is the
shipped composition.

### W4. #163 is still the inert enum issue 185 said it was

Issue 185 (body, section E): “#163 ships inert without a producer.” This
wave adds `DescriptorSource.HandAuthoredContract = 2`
(`DescriptorSource.cs:63`) and retargets AONT205 to `Ingested` only. No
production assignment of `HandAuthoredContract` exists; every
`Source = DescriptorSource.HandAuthoredContract` is a test fixture
(`AONT205Tests.cs:213`, `HandAuthoredContractMergeTests.cs:36`,
`IOntologyBuilderInvariantTests.cs:204`).

`MergeTwo` then erases the new value: `Source = DescriptorSource.HandAuthored`
(`MergeTwo.cs:19`, `:67`). The merge test **asserts** that collapse
(`HandAuthoredContractMergeTests.cs:87`). CHANGELOG Residue (`:192-194`) and
the enum remarks (`DescriptorSource.cs:60-61`) say contract-authored actions
“survive graph merge.” Actions do (`MergeTwo.cs:78`); provenance does not.
A later reader of `Source` cannot tell fluent hand from contract hand. The
three-way split is a compile-time constant plus a test-only constructor
argument.

### W5. Residue packaging vs the issue’s own next-slice thesis

Issue 185’s original “next slice” was close the termination class (#179,
#182, #184, #186, #180, plus the AGWF035 route arm). It said #176/#177 have
“no urgency” and are “explicitly not in the next slice.” The later
“still open by design” comment widened the leftover list. This wave matches
the leftover list (T1–T6), not the original thesis.

The 2.11.0 **lede** still narrates a single correctness-core story and a
single contracts bump: `0.4.0 → 0.6.0` (`CHANGELOG.md:17`). Residue, 150
lines later, adds a second bump `0.6.0 → 0.7.0` (`:182`).
`src/Strategos.Contracts/CHANGELOG.md` Unreleased Added still ends at
AGWF036 / `0.5.0 → 0.6.0` and never names AGWF037 or `0.7.0`. Three
published stories of the same package version, two of them stale relative
to `Strategos.Contracts.csproj` `ContractsVersion` `0.7.0`. A consumer who
reads the release lede or the package changelog is told the wrong number.

The plan called the tracks file-disjoint. They are not story-disjoint:
Contracts version, PublicAPI.Unshipped, INV-3, and the 2.11.0 narrative
are shared. Parallel dispatch plus a late integration CHANGELOG pass
produced a lede that predates the residue it now contains.

### W6. #115 obsolete is a one-method warning, not an authoring path

Issue 185: “#115’s only mechanical task needs an `[Obsolete]` whose
successor #168 has not yet defined.” The wave obsoletes
`IActionBuilder<T>.Requires` and points at `ActionDescriptor.Preconditions`
with “no fluent successor” (`IActionBuilderOfT.cs:35-40`,
`ActionBuilderOfT.cs:77-78`). `RequiresSoft` / `RequiresLink` /
`RequiresLinkSoft` stay current on the same interface (`:42-46`).
`Directory.Build.targets:4-5` globally suppresses CS0618 for every test
project so the obsolete method can keep being exercised. Combined with W4
(no contract producer) and T6’s “descriptor-first is the CLR-free path,”
fluent authors lose one method and gain no replacement surface that this
diff actually wires.

### W7. Hosting 2.2.0 / repo 1.3.0 split, and Renovate’s same proof shape

MCP `resultType` exists only because Hosting `VersionOverride`s
`ModelContextProtocol` to 2.2.0 (`Strategos.Ontology.MCP.Hosting.csproj:18-20`)
while the rest of the repo stays on the 1.3.0 CPM pin. INV-3 now requires
`resultType` on every `CallToolResult`. The composition that can set the
field is one package. `ErrorResult` sets `ResultType = "complete"` on an
`IsError` payload (`OntologyServerToolFactory.cs:410-412`). Spec-legal if
`resultType` is elicitation, not success — but the deny-list and the pin
are now coupled: revert the pin and INV-3’s new deny becomes unsatisfiable
on 1.3.0.

Renovate (`renovate.json:3-6`) changes one `local>` path token. #181’s
class, as issue 185 stated it, is a control that looks present and is
inert. The replacement is another path string. Nothing in this repository
fetches `lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`
or proves the GitHub App resolves it. Same shape as the defect it claims
to close.

## What else was read

- `verification/stage0.md` (complete)
- `survey-lenses.md` section 7 only; `validating-claims.md`; `workspace.md`
- Issue 185 via `gh` (body + two comments). GitHub MCP 403 on org token policy.
- Plan T1–T6; spec problem statement + DR-3 (leads, not facts)
- `CHANGELOG.md` 2.11.0 lede + Residue; `src/Strategos.Contracts/CHANGELOG.md`
- `TerminalReachabilityGuard.cs`, `PhaseGraph.cs`, call site and tests
- `AgwfCatalog.tsp` AGWF035/037, `WorkflowDiagnostics.cs` messages
- `WorkflowIncrementalGenerator` import vs C# pipelines, `hasErrors`
- `DescriptorSource`, `MergeTwo`, merge tests, AONT205 sites
- `IActionBuilderOfT` / `ActionBuilderOfT`, `Directory.Build.targets`
- `OntologyServerToolFactory`, Hosting csproj pin, INV-3 catalog (lead)
- `renovate.json`

Did not read other survey-lens output files. `rg` listed
`verification/survey/mechanism.md` as a path; that file was not opened.

## Assumptions and open questions

- Assumed HEAD `324768f` is the analyzed revision; citations are from the
  worktree at that commit (CHANGELOG Residue is in that commit).
- Assumed #184’s mechanism is still the one CHANGELOG 2.11.0 Fixed describes
  (missing loop-exit in the branch-handler loop). Not re-run against pre-#194
  generator source in this lens.
- Open: does any out-of-repo TypeSpec/JSON ingest already stamp
  `HandAuthoredContract`, so W4 is only in-repo-inert?
- Open: does Exarchos, or any consumer, parse AGWF035 `{0}/{2}` as
  “step chains to successor,” and would under-reach therefore be actioned
  backwards?
- Open: does the JSON import path have a second AGWF035 call this lens
  missed (no `rg` hit under `Import/`)?
- Open: does the new Renovate path 404 the same way the old one did?
  Not fetched. Not a fact.
- Open: is `ErrorResult`’s `resultType: complete` required by 2026-07-28,
  or an accidental collapse of error into the success discriminator?
- Did not settle whether AGWF035-without-gating is intentional (errors
  should still emit so the consumer sees the saga) or an omission next to
  the new AGWF037 gate.
