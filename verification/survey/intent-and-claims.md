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

# Lens 2 — Intent And Claims (diff form)

Every item below is a **claim to check**, not a fact. Quotes are word for word. Identical wording is one claim with every source that states it. Near-duplicates stay separate.

**PR description:** none. `gh pr list --repo lvlup-sw/strategos --head cursor/c801a047 --state all` returned `[]`.

---

## Stated purpose

From the recorded scope answer (`verification/stage0.md`):

> Knock out remaining #185 residue that is implementable without maintainer portal work or Option B: AGWF035 route-analysis, AGWF037 duplicate PermitTrigger, renovate path (#181), MCP resultType+Icons (#176/#177), DescriptorSource.HandAuthoredContract (#163), and Requires obsolete + rationale docs (#115).

From the dispatch plan overview (`/home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md`):

> Knock out the remaining #185 residue that is implementable without maintainer portal work or the deferred Option B identity redesign: the AGWF035 route-analysis arm plus five independent tracks that do not share files.

---

## Numbered claim inventory (Stage 2 seed set)

### A. Scope answer and out-of-wave caveats

1. **Claim (purpose).** "Knock out remaining #185 residue that is implementable without maintainer portal work or Option B: AGWF035 route-analysis, AGWF037 duplicate PermitTrigger, renovate path (#181), MCP resultType+Icons (#176/#177), DescriptorSource.HandAuthoredContract (#163), and Requires obsolete + rationale docs (#115)." — source: scope answer / `verification/stage0.md`.

2. **Claim (caveat).** "Option B (carry construct identity through exclusive paths) — deferred; AGWF036 stays the ship" — source: scope answer / `verification/stage0.md` (also plan "Out of this wave").

3. **Claim (caveat).** "#147 Trusted Publishing (nuget.org portal)" is out of wave. — source: scope answer / plan.

4. **Claim (caveat).** "#133 parity-gate confirm and #174 live-issue proof" are out of wave. — source: scope answer / plan.

5. **Claim (caveat).** "#156.1 `sampleSize` semantic call and #156.3 positional `DiagnosticForkCount_{i}` re-key" are out of wave. — source: scope answer / plan.

6. **Claim (inclusion).** "#156.2 (duplicate PermitTrigger / AGWF037) **is** in this wave." — source: `verification/stage0.md`.

7. **Claim (caveat).** Untracked `docs/{designs,plans,research}/2026-06-16-edge-*` files are out of scope (plan T6: do not reopen junction-table / traversal leftover docs). — source: `verification/stage0.md` / plan T6.

### B. Dispatch-plan acceptance criteria

8. **Claim (plan overview).** "Knock out the remaining #185 residue that is implementable without maintainer portal work or the deferred Option B identity redesign: the AGWF035 route-analysis arm plus five independent tracks that do not share files." — source: plan.

9. **Claim (prior close).** "#194 (`4d060f4`) already closed the live defects (#189/#190/#191, #182/#186, #179, #184, #180, #178, #183)." — source: plan.

10. **Claim (what remains).** "What is left is the class-closing guard, plus the own-cycle items that do not need nuget.org or a live-issue proof." — source: plan.

11. **Claim (T1 purpose).** "Today the guard only decides **over-reach** (terminal has a successor, or a main-flow step chains into a construct-owned step). It is blind to a terminal that is last but that nothing dispatches." — source: plan T1.

12. **Claim (T1 purpose).** "[#184](https://github.com/lvlup-sw/strategos/issues/184) was the motivating instance and is already fixed in the emitter; this arm is the compile-time lock so the next dropped edge does not need Postgres." — source: plan T1.

13. **Claim (T1 mechanism / guarantee).** "Lift that private nested type to a shared internal type so the diagnostic and the emitted table cannot drift." — source: plan T1. Restated in commit `46fb93a` body: "The termination-reachability guard and the emitted ValidTransitions table must resolve successors from one graph so they cannot drift."

14. **Claim (T1 false-positive trap).** "a `Branch` whose cases all `.Complete()` plus a `.Finally<T>()` legitimately dispatches the terminal **zero** times. Naive "something must list the terminal as next step" fails that shape." — source: plan T1.

15. **Claim (T1 fire rule).** "Fire only when a construct is marked **rejoin** (fork join, rejoining branch/loop-exit case, approval resume, linear predecessor) and that last step's successors do not include the declared terminal. All-terminal exclusive paths stay silent." — source: plan T1.

16. **Claim (T1 catalog).** "Keep **one code**. Do not add AGWF037 here. Prefer the existing message template: `{0}` = declared terminal, `{2}` = the last step that should have dispatched it. Only widen catalog remediation if that sentence becomes a lie." — source: plan T1.

17. **Claim (T1 AC).** "Rejoining loop-exit / branch case with the Finally edge stripped → AGWF035" — source: plan T1 tests.

18. **Claim (T1 AC).** "All-`.Complete()` branch + Finally → silent" — source: plan T1 tests.

19. **Claim (T1 AC).** "Shipped fork/branch corpus + existing `Diagnostic_ExistingCorpus_NeverFires` stay silent" — source: plan T1 tests.

20. **Claim (T1 AC).** "Guard still reached from [WorkflowIncrementalGenerator.cs](src/Strategos.Generators/WorkflowIncrementalGenerator.cs) with `MainFlowClassification`" — source: plan T1 tests.

21. **Claim (T1 no Option B).** "T1: Extract PhaseGraph; AGWF035 under-reach arm with all-Complete() negative; no Option B" — source: plan todo `t1-agwf035-route`.

22. **Claim (T2).** "Two `PermitTrigger(ForkTrigger.X, …)` on one edge already fail closed as CS0152. Add a dedicated error on **both** the C# extractor and the JSON-import bridge. Reject, do not first-wins-dedup — two same-trigger declarations can carry different evidence schemas." — source: plan T2.

23. **Claim (T2 AC).** "New tests: C# twin fires AGWF037; JSON-import twin fires the same id; distinct triggers stay clean." — source: plan T2.

24. **Claim (T2 packaging).** "Contracts `0.6.0 → 0.7.0`" / "Regen the catalog (do not hand-edit `Generated/` or `docs/diagnostics/agwf.md`)." — source: plan T2.

25. **Claim (T3).** "Second `extends` entry 404s (`renovate-config/presets/dotnet.json`). The file lives at `tools/renovate-config/presets/dotnet.json`. One-line path fix." — source: plan T3.

26. **Claim (T3 caveat).** "Do not migrate Renovate → Dependabot here (#147)." — source: plan T3.

27. **Claim (T4 / #176).** "`CallToolResult` constructions (`MapTraversalResult`, `ErrorResult`, and any sibling mappers) must set `resultType` with 2026-07-28 semantics." — source: plan T4.

28. **Claim (T4 / #176).** "Confirm the property on the installed MCP SDK before inventing a parallel field. Round-trip at the MCP boundary." — source: plan T4.

29. **Claim (T4 / #176).** "Extend the INV-3 deny-list so no pre-2026-07-28 response shape is emitted." — source: plan T4.

30. **Claim (T4 / #177).** "add optional `Icons` on `OntologyToolDescriptor` (INV-3: do not document the omission). Null when the source supplies none — do not invent a placeholder icon." — source: plan T4.

31. **Claim (T4 / #177).** "Stop the INV-3 checklist from flagging the gap." — source: plan T4.

32. **Claim (T5).** "Keep `HandAuthored = 0` compiling. Add `HandAuthoredContract` **additively** (`= 2`) so `Ingested = 1` does not move." — source: plan T5.

33. **Claim (T5).** "Retarget AONT205 to `Ingested` only." — source: plan T5.

34. **Claim (T5).** "A contract-authored action survives graph merge; a mechanically ingested action on Actions/Events/Lifecycle/InterfaceActionMappings/ExternalLinkExtensionPoints still fails AONT205." — source: plan T5.

35. **Claim (T5).** "Document which authoring surface maps to which value." — source: plan T5.

36. **Claim (T6).** "Document the CLR-free ⊕ polymorphic limit, citing [RationaleCorpusParityTests](src/Strategos.Ontology.Npgsql.Tests/Parity/RationaleCorpusParityTests.cs) (\"a SymbolKey-ONLY interface fan-out is NOT expressible\")." — source: plan T6.

37. **Claim (T6).** "State that `ObjectTypeFromDescriptor` / `ApplyDelta` is the first-class CLR-free path; the fluent `Object<T>` / `Interface<T>` surface stays CLR-generic." — source: plan T6.

38. **Claim (T6 / no-behavior-change).** "`[Obsolete]` on `.Requires(...)` pointing at `ActionDescriptor.Preconditions` (the existing descriptor-first field). Do not invent a new fluent successor." — source: plan T6.

39. **Claim (T6).** "This is a tracked public-API change (RS0016/RS0017)." — source: plan T6.

40. **Claim (CHANGELOG process).** "this repo's `weave` merge driver mangles dual edits. No track edits CHANGELOG.md. A short integration pass writes one 2.11.0 subsection after the tracks merge." — source: plan.

### C. CHANGELOG Residue (#185) — claims the diff makes about itself

`CHANGELOG.md:170-199`

41. **Claim.** "`AGWF035` now decides route under-reach." — `CHANGELOG.md:172`.

42. **Claim.** "The guard already reported a declared `Finally<T>` that was not last on the main flow, or a main-flow step whose successor was construct-owned." — `CHANGELOG.md:172-173`.

43. **Claim.** "It was blind to a rejoin construct whose last step never dispatched the terminal." — `CHANGELOG.md:173-174`.

44. **Claim.** "The under-reach arm fires on that shape, and stays silent when every exclusive path already `Complete()`s alongside a `Finally<T>` — those routes terminate without ever starting the terminal." — `CHANGELOG.md:174-176`.

45. **Claim (guarantee).** "The guard and the emitted `ValidTransitions` table now share one `PhaseGraph` so they cannot drift." — `CHANGELOG.md:176-177`.

46. **Claim.** "`AGWF037` — duplicate permitted fork trigger (error)." — `CHANGELOG.md:179`.

47. **Claim.** "Two `PermitTrigger` declarations on one diagnostic-fork edge that name the same closed trigger fail closed before `CS0152`." — `CHANGELOG.md:179-180`.

48. **Claim.** "First-wins dedup would silently drop one evidence schema." — `CHANGELOG.md:180-181`.

49. **Claim.** "The same gate runs on C# `AllowDiagnosticFork` and on JSON import." — `CHANGELOG.md:181-182`.

50. **Claim.** "`Strategos.Contracts` bumps **0.6.0 → 0.7.0**." — `CHANGELOG.md:182`.

51. **Claim.** "Renovate resolves the organisation's dotnet preset (#181)." — `CHANGELOG.md:184`.

52. **Claim.** "The second `extends` entry 404'd because the preset lives under `tools/`, not at the repo-root path Renovate was resolving." — `CHANGELOG.md:184-185`.

53. **Claim.** "The 1.3.0 MCP SDK has no `ResultType`; Hosting pins 2.2.0 so every constructed `CallToolResult` can set the 2026-07-28 complete discriminator." — `CHANGELOG.md:187-189`. Restated in commit `887eb9a` body and `OntologyServerToolFactory.cs:51-55`.

54. **Claim (no-behavior-change).** "`OntologyToolDescriptor.Icons` stays null when unset." — `CHANGELOG.md:189`.

55. **Claim.** "INV-3 now denies the pre-2026-07-28 response shape instead of flagging the icon gap." — `CHANGELOG.md:189-190`.

56. **Claim (no-behavior-change / additive).** "`DescriptorSource.HandAuthoredContract`. Appended as `2` without moving `HandAuthored = 0` or `Ingested = 1`." — `CHANGELOG.md:192-193`.

57. **Claim.** "AONT205 retargets to mechanical ingestion, so TypeSpec / JSON contract-authored actions survive graph merge." — `CHANGELOG.md:193-194`.

58. **Claim (obsolete / no-behavior-change).** "`IActionBuilder<T>.Requires` is obsolete (#115). Prefer `ActionDescriptor.Preconditions`. The method stays so existing `Object<T>` authoring still compiles; there is no fluent successor." — `CHANGELOG.md:196-197`.

59. **Claim.** "Docs name `ObjectTypeFromDescriptor` / `ApplyDelta` as the CLR-free authoring seam and record that a SymbolKey-only interface fan-out is not expressible." — `CHANGELOG.md:197-199`.

### D. Commit messages (`git log 4d060f4..HEAD`)

60. **Claim (integration).** "docs(changelog): record the #185 residue tracks in 2.11.0" — commit `324768f`.

61. **Claim.** "Fire AGWF035 when a rejoin last step does not dispatch the terminal." — commit `5e94af4` subject.

62. **Claim.** "Under-reach is now decidable from the shared PhaseGraph." — commit `5e94af4` body.

63. **Claim.** "All-Complete() exclusive paths plus Finally stay silent, which is the false-positive trap." — commit `5e94af4` body.

64. **Claim.** "Lift TransitionsEmitter.PhaseGraph to a shared internal type." — commit `46fb93a` subject.

65. **Claim (guarantee).** "The termination-reachability guard and the emitted ValidTransitions table must resolve successors from one graph so they cannot drift." — commit `46fb93a` body.

66. **Claim.** "feat(ontology-mcp): emit CallToolResult.resultType and optional Icons (#176, #177)" — commit `887eb9a` subject.

67. **Claim.** "feat(generators): reject duplicate PermitTrigger with AGWF037" — commit `97f52cd` subject.

68. **Claim.** "Two same-trigger declarations can carry different evidence schemas, so the C# extractor and JSON-import bridge reject the edge rather than first-wins dedup." — commit `97f52cd` body.

69. **Claim.** "feat(contracts): add AGWF037 and bump Contracts 0.6.0 to 0.7.0" — commit `12098da` subject.

70. **Claim.** "A dedicated duplicate-permitted-fork-trigger code is required so C# and JSON import can reject two PermitTrigger(ForkTrigger.X) declarations on one edge instead of failing closed as CS0152." — commit `12098da` body.

71. **Claim.** "Mark IActionBuilder<T>.Requires obsolete in favor of ActionDescriptor.Preconditions." — commit `d01a78f` subject.

72. **Claim (no-behavior-change).** "Keep the method compiling so existing Object<T> authoring still works; do not add a fluent successor." — commit `d01a78f` body.

73. **Claim (no-behavior-change).** "Test projects suppress CS0618 so existing Requires call sites still exercise the Preconditions lowering." — commit `d01a78f` body.

74. **Claim.** "Document the CLR-free XOR polymorphic limit and first-class descriptor path." — commit `c366147` subject.

75. **Claim.** "ObjectTypeFromDescriptor/ApplyDelta is the CLR-free authoring seam; fluent Object<T>/Interface<T> stays CLR-generic. Cite RationaleCorpusParityTests: a SymbolKey-only interface fan-out is not expressible." — commit `c366147` body.

76. **Claim.** "feat(ontology): add HandAuthoredContract without moving Ingested." — commit `662f0d1` subject.

77. **Claim.** "Keep HandAuthored = 0 and Ingested = 1, append HandAuthoredContract = 2, and retarget AONT205 to mechanical ingestion so contract-authored actions survive graph merge." — commit `662f0d1` body.

78. **Claim.** "fix(ci): point Renovate at the existing lvlup-claude dotnet preset (#181)" — commit `334f64c` subject.

79. **Claim.** "The second extends entry 404s because the preset lives under tools/, not at the repo-root path Renovate was resolving." — commit `334f64c` body.

### E. Issue 185 (residue tracker) — referenced requirements and open items

80. **Claim (issue title).** "v2.11.0 slice delta: the correctness core closes six and surfaces four — and the termination class is sampled, not closed" — issue 185 title. State: OPEN.

81. **Claim (issue body).** "The guard decides one half of the class: a terminal that is *over*-reachable. It is blind to a terminal that is last but that nothing dispatches, which is exactly #184." — issue 185 body.

82. **Claim (issue body).** "`AGWF035` currently decides position — is the declared terminal last on the main flow, and does any main-flow step resolve into a construct-owned step. It does not decide **route** — does anything actually dispatch the terminal." — issue 185 body.

83. **Claim (issue body / thesis).** "a defect the compiler can see should not need Postgres to surface." — issue 185 body. Restated in `TerminalReachabilityGuard.cs:27-28`.

84. **Claim (issue body).** "Adding that arm makes #184 compile-time decidable" — issue 185 body.

85. **Claim (issue body / trap).** "\"the terminal must appear as some handler's next step\" false-positives on a `Branch` whose cases all `.Complete()` alongside a `.Finally<T>()`, which legitimately dispatches the terminal zero times." — issue 185 body.

86. **Claim (issue body).** "The check has to separate *every route legitimately terminated* from *a construct dropped the edge*." — issue 185 body.

87. **Claim (issue comment 2 / still open).** "this is the residue tracker, not a completed work item." — issue 185 comment (rsalus, 2026-08-28).

88. **Claim (issue comment 2 / landed prior).** "Landed in #194 (`4d060f4`) and closed: #189, #190, #191 (identity / AGWF003 + AGWF036), #186, #182 (approvals), #179 (bool CS8510), #184 (loop-exit Finally), #180 (fork-twin host isolation), #178 (invariant catalog), #183 (Newtonsoft phase-ordinal docs)." — issue 185 comment 2.

89. **Claim (issue comment 2 / still open by design).** "AGWF035 under-reach / route-analysis arm (over-reach only today)" — issue 185 comment 2.

90. **Claim (issue comment 2 / still open by design).** "Option B (carry construct identity through exclusive paths)" — issue 185 comment 2.

91. **Claim (issue comment 2 / still open by design).** "Paved road / ontology, own cycles: #147, #181, #163, #115, #156, #176, #177" — issue 185 comment 2.

92. **Claim (issue comment 2 / still open by design).** "Maintainer-owned: #133, #174" — issue 185 comment 2.

93. **Claim (issue body / next-slice scope, historical).** "Scope: #179, #182, #184, #186, #180 — plus the route-reachability arm on `AGWF035`." — issue 185 body (written before #194 closed those defects).

### F. Spec — AGWF035 design (`docs/specs/2026-08-22-correctness-core.md`)

These are the spec's claims about the original over-reach guard. This wave claims to add the complementary under-reach arm; the spec itself does not describe that arm.

94. **Claim (spec DR-3).** "A new stable `AGWF` diagnostic fires at generation time when a workflow's declared terminal step is not the last main-flow step, or when any main-flow step's computed successor is an off-main-flow step." — spec DR-3, `docs/specs/2026-08-22-correctness-core.md:105`.

95. **Claim (spec DR-3 AC).** "The diagnostic has a catalog entry with a stable id, and fires on a fixture reproducing each of the two conditions." — spec `:108`.

96. **Claim (spec DR-3 AC).** "Reverting DR-1 with DR-2 in place produces the diagnostic — i.e. the guard would have caught this bug before it shipped." — spec `:109`.

97. **Claim (spec DR-3 AC / no-false-positive).** "The diagnostic does **not** fire on any existing fixture in `Strategos.Generators.Tests` or `Behavioral.Tests` (no false positives on the shipped corpus)." — spec `:110`.

98. **Claim (spec).** "The generator knows at emission time whether the declared terminal is the last main-flow entry. Nothing checks it." — spec `:60` (historical; CHANGELOG 2.11.0 Added already claimed the over-reach half).

99. **Claim (spec / INV-5).** "A new `AGWF` diagnostic closes the class at the earliest tier that can catch it, per INV-5 — and is the only thing that stops the sixth appending block someone adds next year from re-opening this bug silently." — spec `:60`.

100. **Claim (spec).** "The failure is compile-time decidable — the generator holds both the declared terminal and the computed successor at emission — and today the only thing that catches it is a Testcontainers real-host run that most contributors cannot execute." — spec `:213`.

101. **Claim (catalog / AGWF035 remediation, unchanged this wave).** "Step '{0}' in workflow '{1}' chains to '{2}', which is not on the workflow's main flow. A step reached only through its own construct — a fork path, a branch case, a failure or approval handler, or a low-confidence handler chain — is never a main-flow successor, so the saga runs past its declared termination instead of completing." — `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:344` / `docs/diagnostics/agwf.md:43`. Plan T1 claims this template can name the dropped-edge source as `{2}` without becoming a lie.

### G. Code comments and catalog text added in the diff

102. **Claim (guarantee).** "Shared by the transition table emitter and the termination-reachability guard so the diagnostic and the emitted `ValidTransitions` table cannot drift." — `src/Strategos.Generators/Models/PhaseGraph.cs:16-17`.

103. **Claim.** "A routed edge replaces linear chaining: a step that has one never also falls through to the next list entry." — `PhaseGraph.cs:21-23`.

104. **Claim.** "An *additional* edge coexists with linear chaining. A confidence-gated step still proceeds along the main flow when the score clears the threshold, so its edge to the low-confidence handler is recorded alongside, not instead of, its main-flow successor." — `PhaseGraph.cs:26-28`.

105. **Claim.** "Every target is either an entry of the workflow's step-name list or one of the two standard terminals, so a target is always a member of the emitted phase enum without this type having to restate the enum emitter's membership rules." — `PhaseGraph.cs:31-33`.

106. **Claim.** "Decides, at emission time, whether a workflow's main flow actually ends at the termination its author declared — and reports it when it does not." — `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs:15-16`.

107. **Claim.** "The complementary fault is a terminal that is last but that a rejoin construct's last step does not dispatch." — `TerminalReachabilityGuard.cs:22-23`.

108. **Claim.** "a branch whose cases all complete legitimately dispatches its declared terminal zero times." — `TerminalReachabilityGuard.cs:25`.

109. **Claim.** "Until this guard existed the only thing that caught either arm was a container-backed saga run, which most contributors cannot execute; a defect the compiler can see should not need Postgres to surface." — `TerminalReachabilityGuard.cs:26-28`.

110. **Claim.** "Fired only for constructs marked rejoin — fork join, a rejoining branch or loop-exit case, an approval resume, a linear predecessor. A branch whose cases all `Complete()` plus a `Finally` legitimately dispatches the terminal zero times and stays silent." — `TerminalReachabilityGuard.cs:136-139`.

111. **Claim.** "Argument 0 is the declared terminal; argument 2 is the last step that should have dispatched it." — `TerminalReachabilityGuard.cs:139-140`.

112. **Claim.** "Two conditions, one code." — `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs:26`.

113. **Claim.** "The under-reach arm is the complementary fault: a rejoin construct's last step does not dispatch the declared terminal. A branch whose cases all `Complete()` plus a `Finally` legitimately dispatches that terminal zero times and must stay silent." — `TerminalReachabilityDiagnosticTests.cs:31-33`.

114. **Claim.** "A branch whose cases all `Complete()`, plus a declared terminal. Nothing dispatches the terminal; that is legitimate and must stay silent." — `TerminalReachabilityDiagnosticTests.cs:692-693`.

115. **Claim.** "A diagnostic fork must declare each trigger at most once." — `src/Strategos.Generators/Models/DiagnosticForkModel.cs:129`.

116. **Claim.** "Used by the C# extractor and the JSON-import bridge so a duplicate `PermitTrigger` is rejected rather than first-wins-deduped (#156.2)." — `DiagnosticForkModel.cs:143-144`.

117. **Claim.** "Two `PermitTrigger(ForkTrigger.X)` calls on one edge are rejected — no model, and AGWF037 is reported. The twins carry different evidence schemas so first-wins dedup would silently drop one schema." — `src/Strategos.Generators.Tests/Helpers/DiagnosticForkExtractorTests.cs:180-182`.

118. **Claim.** "C# twin: two `PermitTrigger` calls for the same closed trigger (different evidence schemas) fire AGWF037 and emit no saga." — `src/Strategos.Generators.Tests/Diagnostics/DuplicatePermittedForkTriggerTests.cs` (xml summary from diff).

119. **Claim.** "Distinct triggers on one edge stay clean: no AGWF037, and a saga still lowers." — same test file (xml summary from diff).

120. **Claim (catalog AGWF037).** "Workflow '{0}' declares permitted trigger '{2}' more than once on one diagnostic-fork edge at {1}. Two same-trigger declarations can carry different evidence schemas; declare each trigger at most once." — `AgwfCatalog.tsp:364` / `docs/diagnostics/agwf.md:45`.

121. **Claim (catalog / INV-5).** "Ground truth = exactly **31 defined codes** with gaps preserved as gaps (INV-5: never renumber)." — `AgwfCatalog.tsp:14-15`.

122. **Claim (catalog / wire identity).** "Member NAMES are the wire identity: the enum round-trips by name and never by ordinal, so a member may be added but never renamed or reordered without a major bump." — `AgwfCatalog.tsp:15-17`.

123. **Claim (packaging).** "the package must version at exactly 0.7.0" / "0.7.0 adds the duplicate-permitted-fork-trigger diagnostic id over 0.6.0's path-end type-collision id" — `src/Strategos.Contracts.Tests/PackagingTests.cs` (comments from diff).

124. **Claim.** "Null Icons stay unset — do not invent a placeholder icon (INV-3 / #177)." — `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs:248`.

125. **Claim.** "Optional MCP `Tool.icons` (2026-07-28). Null when the descriptor source supplies none — never a placeholder icon." — `src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs:40-41`.

126. **Claim.** "MCP 2026-07-28 `resultType` for a finished `tools/call`. The installed 1.3.0 SDK has no `CallToolResult.ResultType`; Hosting pins 2.2.0+ which exposes the protocol field. Always `complete` here (final content, including `isError: true`); `input_required` is MRTR and is not emitted." — `OntologyServerToolFactory.cs:51-55`.

127. **Claim.** "2026-07-28: servers MUST emit resultType. Absent field is the pre-2026-07-28 shape. Round-trip through the SDK serializer so the wire form (not just the in-memory default) carries \"complete\"." — `src/Strategos.Ontology.MCP.Hosting.Tests/TraversalToolHostingTests.cs:100-102`.

128. **Claim.** "Numeric values are part of the public contract. New members are appended so existing values never move" — `src/Strategos.Ontology/Descriptors/DescriptorSource.cs:9-10`.

129. **Claim.** "AONT205 rejects intent-only fields only when `Ingested`." — `DescriptorSource.cs:37`.

130. **Claim.** "Contract-authored intent is first-class and survives graph merge; AONT205 does not apply." — `DescriptorSource.cs:60-61`.

131. **Claim.** "Ingested … Must not carry intent-only fields (`Actions`, `Events`, `Lifecycle`, `InterfaceActionMappings`, `ExternalLinkExtensionPoints`)." — `DescriptorSource.cs:50-52`.

132. **Claim.** "Mechanical ingestion (`DescriptorSource.Ingested`) may not contribute intent-only collections; `HandAuthored` and `HandAuthoredContract` pass through." — `src/Strategos.Ontology/Internal/IngestedIntentInvariant.cs:6-9`.

133. **Claim.** "a contract-authored `HandAuthoredContract` action survives graph merge; ingested intent on the same type still fails AONT205." — `src/Strategos.Ontology.Tests/Merge/HandAuthoredContractMergeTests.cs:12-14` (xml summary).

134. **Claim.** "Prefer `ActionDescriptor.Preconditions`. There is no fluent successor; this method remains only so existing CLR-generic `Object<T>` authoring still compiles." — `src/Strategos.Ontology/Builder/IActionBuilderOfT.cs:35-37`.

135. **Claim (Obsolete message).** "Use ActionDescriptor.Preconditions to declare action preconditions. There is no fluent successor." — `IActionBuilderOfT.cs:39`.

### H. Docs and INV-3 text added in the diff

136. **Claim.** "There is no fluent successor — do not invent a replacement builder method. `.Requires(...)` still compiles so existing `Object<T>` authoring keeps working, and it is the only way the CLR-generic fluent surface writes preconditions today." — `docs/src/content/docs/guide/ontology/index.md:66`.

137. **Claim.** "`ObjectTypeFromDescriptor` and `ApplyDelta` are the **first-class CLR-free path**." — `docs/src/content/docs/guide/ontology/polyglot-descriptors.md:127`.

138. **Claim.** "The fluent `Object<T>` / `Interface<T>` surface stays **CLR-generic**. Those overloads take a type parameter and populate `ClrType` from it. They are not a CLR-free authoring path, and there is no `Object(symbolKey)` fluent twin that also declares a polymorphic interface." — `polyglot-descriptors.md:129`.

139. **Claim.** "This is the CLR-free ⊕ polymorphic limit: you can have a CLR-free (SymbolKey-only) graph, or a polymorphic (interface-typed) fan-out, but not both on the same link." — `polyglot-descriptors.md:133`.

140. **Claim (cited bound).** "a SymbolKey-ONLY interface fan-out is NOT expressible" — `polyglot-descriptors.md:135` (quoted from `RationaleCorpusParityTests`).

141. **Claim.** "An `InterfaceDescriptor` carries a CLR `Type`. A CLR-free descriptor has `ClrType == null`, so it cannot also be a polymorphic interface target." — `polyglot-descriptors.md:137`.

142. **Claim.** "A `SymbolKey`-only polymorphic interface is not a missing API to invent on the fluent surface — it is a type-system limit." — `polyglot-descriptors.md:144`.

143. **Claim.** "Action preconditions are declared on `ActionDescriptor.Preconditions`; fluent `.Requires(...)` is obsolete and has no fluent successor." — `docs/src/content/docs/reference/ontology/index.md:9`.

144. **Claim (INV-3).** "Strategos's MCP layer (`Strategos.Ontology.MCP`) targets the *current* MCP protocol spec and leverages its modern features: structured tool descriptors with `OutputSchema`, optional `Icons`, `_meta` envelopes on every response, `resultType` on every `CallToolResult`, `ToolAnnotations`, capability hints. It does not write to a lowest-common-denominator subset that older clients would also accept." — `.agents/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md:3`.

145. **Claim (INV-3 AC).** "Does every `CallToolResult` construction set `ResultType` (`\"complete\"` unless the call is an MRTR `input_required`)?" — INV-3 `:11`.

146. **Claim (INV-3 AC).** "Does `OntologyToolDescriptor` expose optional `Icons`, null when the source supplies none (no placeholder icon)?" — INV-3 `:12`.

147. **Claim (INV-3).** "`CallToolResult.resultType` is required in 2026-07-28 (`\"complete\"` | `\"input_required\"` | extension values). Servers MUST include it; clients treat an absent field as `\"complete\"` for older servers. Strategos always emits `\"complete\"` on constructed results (no MRTR `input_required` path)." — INV-3 `:36`.

148. **Claim (INV-3).** "`OntologyToolDescriptor.Icons` is null when the source supplies none — an always-null placeholder icon is worse than an absent one, so discovery leaves it unset." — INV-3 `:37`.

149. **Claim (INV-3 HIGH).** "Emitting `CallToolResult` without `resultType` is this failure mode." — INV-3 `:41`.

150. **Claim (deterministic-checks 3.3).** "Hosting is included so a pre-2026-07-28 response shape cannot be reintroduced on the SDK-bound `CallToolResult` bridge." — `.agents/skills/strategos-design-invariants/references/deterministic-checks.md:109-110`.

151. **Claim (deterministic-checks 3.4).** "Absent `resultType` is the pre-2026-07-28 response shape. Every `new CallToolResult` (or `new()` inferred as one) in Hosting must assign `ResultType`." — `deterministic-checks.md:114-116`.

152. **Claim (deterministic-checks 3.5 / no-behavior-change).** "The property is optional and must stay null when unset — do not flag a missing placeholder icon as a gap." — `deterministic-checks.md:132-133`.

---

## What else I read

- `/home/reedsalus/.claude/skills/verify-code/references/survey-lenses.md` §2 Diff form
- `/home/reedsalus/.claude/skills/verify-code/references/validating-claims.md`
- `/home/reedsalus/.claude/skills/verify-code/references/workspace.md` (frontmatter)
- `verification/stage0.md` in full
- `git log 4d060f4..HEAD` (14 commits; 6 are merge-only with empty bodies)
- `CHANGELOG.md` Residue (#185) and the surrounding 2.11.0 Correctness-core prose (already on the base; not claimed as this wave's delivery)
- Plan `/home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md` in full
- Issue 185 body + 2 comments via `gh issue view 185 --repo lvlup-sw/strategos`
- `gh pr list --head cursor/c801a047` (empty)
- Spec `docs/specs/2026-08-22-correctness-core.md` — AGWF035 / DR-3 and the problem statement; not every already-shipped DR-1..DR-10 AC is inventoried as a claim of *this* diff
- `git diff 4d060f4...HEAD` comment-bearing additions (PhaseGraph, TerminalReachabilityGuard, DiagnosticForkModel, Hosting factory, DescriptorSource, IActionBuilder, INV-3, docs, tests)
- GitHub MCP `issue_read` / `list_pull_requests` failed with org PAT lifetime 403; `gh` succeeded

---

## Assumptions and unsettled questions

- **No PR exists** for `cursor/c801a047`. There is no PR description to inventory. Absence of a PR description is recorded; it is not treated as absence of intent (the plan, CHANGELOG Residue, and commits state intent).
- **Issue 185 is still OPEN** and comment 2 still lists AGWF035 under-reach, #181, #163, #115, #156, #176, #177 as "still open by design." Those sentences describe tracker state at comment time, not a claim that this branch left them unimplemented. Whether merging this branch is supposed to close any of those numbers is unset in-repo (no PR).
- **Spec DR-3 describes only the over-reach half.** The under-reach / route arm is specified in issue 185 + the plan, not in the 2026-08-22 spec. Stage 2 should not treat spec DR-3 ACs as a complete statement of what this wave claims.
- **AGWF035 catalog remediation was not widened.** Plan T1 says widen only if the existing `{0}/{1}/{2}` sentence becomes a lie when `{2}` is a missing dispatcher rather than a bad successor. That is an open consistency claim (C16 / C101 vs C41).
- **"Renovate resolves the organisation's dotnet preset"** is a claim about an external process. This inventory records it; it does not observe a Renovate run.
- **"Hosting pins 2.2.0"** / "1.3.0 SDK has no ResultType" are package-identity claims, not observed here.
- **"Contracts 0.7.0"** is claimed as a source bump. Stage 0 notes a published `contracts-v0.7.0` tag is *not* created by this branch — a claim to check later, not inventoried as a guarantee this diff makes.
- Merge commits (`ba7997c` … `bc13438`) carry no additional intent text.
- Untracked `docs/2026-06-16-edge-*` files were not read (out of scope).
