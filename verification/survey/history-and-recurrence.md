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
lens: 6. History And Recurrence
seed_for: Stage 2 lens 7 (Recurrence To Guard)
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/185
    why: residue tracker; names the inert-control class and the AGWF035 half-close
  - path: /home/reedsalus/.cursor/plans/issue_185_remainder_125df8c7.plan.md
    why: dispatch plan this follow-up is verifying
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/docs/specs/2026-08-22-correctness-core.md
    why: AGWF035 / termination-class design
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/CHANGELOG.md
    why: 2.11.0 claims plus Residue (#185) subsection
  - path: https://github.com/lvlup-sw/strategos/issues/184
    why: AGWF035 under-reach motivating instance (loop-exit Finally drop)
  - path: https://github.com/lvlup-sw/strategos/issues/181
    why: Renovate 404 preset — inert CI/config path
  - path: https://github.com/lvlup-sw/strategos/issues/176
    why: MCP resultType pin lag
  - path: https://github.com/lvlup-sw/strategos/issues/177
    why: MCP Icons gap (pre-existing, not a revision delta)
  - path: https://github.com/lvlup-sw/strategos/issues/163
    why: DescriptorSource two-way enum / AONT205 over-reach
  - path: https://github.com/lvlup-sw/strategos/issues/115
    why: Obsolete Requires without fluent successor
  - path: https://github.com/lvlup-sw/strategos/issues/156
    why: #156.2 duplicate PermitTrigger / first-wins-dedup
---

# Lens 6 — History And Recurrence

This file is the **Stage 2 recurrence-list seed set**. Each class below is a
candidate for lens 7 (Recurrence To Guard). Counts are prior instances found
in tickets, commits, and fixtures — not a claim that the class is closed.

**Read:** `verification/stage0.md`; #185 body + 2 comments; #184, #181, #176,
#177, #163, #115, #156, #155, #175, #145 (partial), #180, #182, #186, #179,
#178, #183, #147, #133, #174 (partial), #189, #190, #191; PRs #187, #188, #194;
plan `issue_185_remainder_125df8c7.plan.md`; `docs/specs/2026-08-22-correctness-core.md`;
`CHANGELOG.md` 2.11.0 + Residue; `git log --follow` on key files; existing
kill fixtures under `src/Strategos.Generators.Tests` and Hosting tests.

**Not read as facts:** GitHub MCP `issue_read` (403 — org forbids PAT lifetime
>366d). Issue bodies came from `gh issue view`. No live Renovate dashboard
was fetched.

**Author diversity on the changed path is one person.** `git shortlog` on
`TerminalReachabilityGuard.cs`, `TransitionsEmitter.cs`, `AgwfCatalog.tsp`,
`DiagnosticForkExtractor.cs`, `renovate.json`, `DescriptorSource.cs`,
`OntologyServerToolFactory.cs` is Reed / Reed Salus only. Recurrence here is
same-author re-entry, not multi-author drift.

---

## Recurrence list (seed set)

Legend for **this diff**: `adds-guard` | `extends-guard` | `instance-fix-only` |
`docs-only` | `none`.

### R1. Termination under-reach vs over-reach (AGWF035 half-closed)

| Field | Evidence |
|---|---|
| **Class** | A declared `Finally<T>` is not actually the saga's termination: either a main-flow step chains *past* it into a construct-owned step (over-reach), or a rejoin construct's last step never *starts* it (under-reach). |
| **Prior count** | **5 filed instances** of the termination family, plus the half-closed guard itself. |
| **Prior instances** | **Over-reach:** #155 (`38854c0` era, closed by #187 `a965f3b`) Fork→Join→Finally terminal chains into a fork-path step; stall. **#175** (closed #187) Branch→Finally terminal cascades into a branch-path step; unbounded cycle, `grep -c MarkCompleted` = 0. **Under-reach:** **#184** (closed #194 `4d060f4`) loop-exit `BranchOnExit` never publishes `Start{Finally}`; AGWF035 as shipped is blind *by construction* (#184 §"Why the new build-time guard does not catch it"). **Adjacent dispatch-drop (same family, different construct):** **#182** (closed #194) approval-before-Fork resumes onto the join; hang. **#186** (closed #194) last-on-flow approval rejection sets phase and publishes nothing; park forever. |
| **Prior fixes** | #187: off-main-flow classification + `AGWF035` **over-reach arm only** (`TerminalReachabilityGuard` created `a965f3b`). #194: emitter fixes for #184/#182/#186; **explicitly left the route arm deferred** (#185 comment 2: "AGWF035 under-reach / route-analysis arm (over-reach only today)"). |
| **Guard exists?** | **Half.** `AGWF035` since 2.11.0 (`AgwfCatalog.tsp`, Contracts 0.5.0). Catalog remediation still describes the over-reach sentence (`docs/diagnostics/agwf.md` AGWF035: "chains to '{2}', which is not on the workflow's main flow"). |
| **This diff** | **`extends-guard`.** `5e94af4` adds the under-reach arm; `46fb93a` lifts `TransitionsEmitter.PhaseGraph` so the diagnostic and `ValidTransitions` share one graph. One code, not AGWF037. |
| **Kill fixtures** | **Over-reach (pre-existing, #187):** `Diagnostic_TerminalNotLastMainFlowStep_Fires`; `Diagnostic_SuccessorResolvesOffMainFlow_Fires`; `Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug` (empty classification → names `AssessDamage` — the shipped #155 successor). **Under-reach (this diff):** `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires` (strip `PayClaim→CloseClaim`); `Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires`. **Negative (false-positive trap, this diff):** `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire`. **Corpus:** `Diagnostic_ExistingCorpus_NeverFires`; `Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline`. |
| **Guard candidate?** | Already a guard. Recurrence trigger is that the **first** AGWF035 ship left the class open while documenting the missing arm (#185, #184, correctness-core DR-3). A class found twice with a half-guard is still a class. |
| **Proof-system note** | The under-reach arm was specified in the same issue that recorded the over-reach guard. Shipping a diagnostic that cannot see the motivating #184 shape is the inert-control class (R3) applied to INV-5. |

### R2. Duplicate trigger / first-wins-dedup (silent collision)

| Field | Evidence |
|---|---|
| **Class** | Two declarations that share a key (trigger, step name, path-end type) are resolved by first-wins / last-write-win instead of reject. Different payloads (evidence schemas, successors, terminality) are silently dropped. |
| **Prior count** | **4 production instances** + one adjacent ontology reject + one intentional first-match policy. |
| **Prior instances** | **#156.2** (still OPEN as part of #156): two `PermitTrigger(ForkTrigger.X)` on one edge → generated `cmd.Trigger switch` hits **CS0152**. Fail-closed, but no dedicated AGWF; CodeRabbit on PR #154, deferred `7962f8a`. Explicit: "rejection (not first-wins dedup) … two same-trigger declarations may carry *different* evidence schemas". **#189** (closed #194): `successorWithinPath[stepName] = …` last-writer-wins; Branch cases sharing a non-last name silently mis-route. #189 text: first-writer-wins "just restores `main`'s arbitrary winner … while *looking* like collision handling." **#191** (closed #194): `BuildBranchPathInfo` last-writer-wins on `LastStepName`. **#190** (closed #194): two fork paths, same step type, distinct instance names → CS0111; v2.11.0 halved overload count (3→2) without closing. **Ontology (existing reject):** `OntologyGraphBuilder.cs:221,1392` + tests `:322,:688` — must not first-wins-bind `ClrType`. **#174** auto-triage "first match wins, title only" is an *intentional* first-wins policy, not a defect, but the same operator. |
| **Prior fixes** | #194: AGWF003 retargeted to `EffectiveName` including `BranchPath` (#189); AGWF036 path-end type collision (#190/#191), C# + JSON import. Ontology already rejected first-wins. #156.2 left as CS0152. |
| **Guard exists?** | **Per-instance, not one policy.** AGWF003 (duplicate EffectiveName), AGWF036 (exclusive-path type collision). No shared "collision ⇒ reject" mechanism. PermitTrigger had only the C# compiler. |
| **This diff** | **`adds-guard`.** AGWF037 (`12098da` catalog 0.6.0→0.7.0; `97f52cd` extractor + import). Reject on C# `AllowDiagnosticFork` and JSON import. Model `Create` already threw (`DiagnosticForkModelTests.Create_WithDuplicateTrigger_ThrowsArgumentException`); the generator/import path did not report a catalog id. |
| **Kill fixtures** | `DuplicatePermittedForkTriggerTests.CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga` (same trigger, different evidence fields `stampId` / `otherStampId`); `DiagnosticForkExtractorTests.Extract_DuplicatePermitTrigger_RejectsEdgeAndReportsAgwf037`; `ImportRejectionTests.ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga`; `DiagnosticForkModelTests.Create_WithDuplicateTrigger_ThrowsArgumentException`; `FindDuplicateTriggerNames_ReportsOnlyRepeatedNames`. Negatives: `CsharpTwin_DistinctPermitTriggers_DoesNotFireAgwf037`; `Extract_DistinctPermitTriggers_YieldsModelWithoutAgwf037`; `ForkDistinctTriggers_IsNotRejected_AndLowersSaga`. |
| **Guard candidate?** | Class found **four** times. Three diagnostics (003/036/037) after three incidents. Lens 7 should ask whether a single collision-reject policy is owed, or whether per-key AGWF ids are the local idiom (they are — INV-5 monotonic catalog). |

### R3. Inert-looking control (present and does nothing)

| Field | Evidence |
|---|---|
| **Class** | A control that *looks* present (config, gate, diagnostic, catalog, publish scaffold, test oracle) is unreachable or blind, so a green result or a checked-in file is not evidence it works. Issue #185 names this as "the slice's most portable result": six controls that looked present and were inert. |
| **Prior count** | **≥6 named by #185**, plus two paved-road siblings still open. **This is the meta-class.** |
| **Prior instances (the six #185 names)** | (1) INV-3 grep gate with a **stale deny-list** — omitted the superseded revision it should reject (#178, correctness-core DR-10). (2) Declared-vs-lowered **parity guard matched by substring** — skipped / commented / `<see cref>` all passed; four false `Deferred` AGWF022 entries stood (DR-5, DR-6, #145). (3) **`renovate.json` extends a 404 path** (#181) — Renovate never opened a PR; `gh pr list --author app/renovate` empty; `auto-merge-renovate` is dead automation that reads as working. (4) **Test harness reported completion from document absence** — never-created ≡ completed (DR-5, #180). (5) **Invariant catalog untracked** — `.claude/` gitignored, INV-1..8 unreviewable (#178, closed #194 by moving to `.agents/`). (6) **Diagnostic aimed at an unauthorable shape** — AGWF022 false-positive blocks; retargeting at instance-named+configured fork-path would have been a control with no DSL trigger (DR-6; see R7). |
| **Siblings not in the six** | **#147** OIDC `id-token: write` present, unused; publish still `--api-key` (out of this wave). **#133** org preset disables `github-actions` *because* Dependabot owns them — Dependabot config was absent, so the new `@v1` pins had no update path until #188. **#145** branch-case last-step `RequireConfidence` compiled and did nothing. **#163** (issue 185: "ships inert without a producer") — AONT205 locks contract-authored actions; enum had no value that meant "hand-authored, not C#". **#180** host leftover inbox made a green-in-isolation twin fail in-class. |
| **Prior fixes** | #187: parity guard parses a real test; AGWF022 retargeted at approval-preceding confidence (authorable). #188/#194: INV-3 catalog tracked; deny-list widened to `2025-11-25` + Agents.Mcp + `*.md`; Dependabot added. #194: host recycle (#180); completion oracle requires invocation delta. |
| **Guard exists?** | **Partial, and the class still has unguarded members.** INV-3 checks 3.1–3.3 exist as *documented greps* in `deterministic-checks.md` — not shown as a CI job in `.github/workflows` (`rg` there hits only `auto-merge-renovate`). Parity guard is a real test. Renovate-extends-resolve: **no guard**. #147 unused OIDC: **no guard**. A config that parses is exactly how #181 went unnoticed (acceptance text). |
| **This diff** | **Mixed.** T3 `334f64c` = **`instance-fix-only`** (path token `renovate-config/…` → `tools/renovate-config/…`). T4 `887eb9a` = **`extends-guard`** for INV-3: Check 3.4 (`CallToolResult` files must mention `ResultType`) + Check 3.5 (`OntologyToolDescriptor.cs` must mention `Icons`); deny-list still human-run grep. No check that `extends` URLs 200. No live Renovate proof (Dependency Dashboard / first PR) — #181 remains OPEN. |
| **Kill fixtures** | INV-3: documented greps, not checked-in failing subjects. Renovate: **no kill fixture** of a 404 `extends` (the old path is gone; not preserved). Parity: the `[Skip]`-ed #155 twin was the negative (DR-5). AGWF022: `DeclaredButInert_ApprovalPrecedingStepConfidence_ReportsAgwf022` + `_LowersNoConfidenceGate`. MCP: `TraversalToolHostingTests.AssertResultTypeComplete` (JSON contains `"resultType":"complete"` + round-trip); `OntologyToolDescriptor_WithoutIcons_DoesNotInventPlaceholder`. |
| **Guard candidate?** | **Yes — owed.** Class found **three or more times with no structural guard** on the CI/config axis (Renovate 404, stale deny-list, unused OIDC, dead auto-merge job). That is itself a finding about the proof system. A resolve-check on `renovate.json` `extends` (and a "scaffolding permission unused" check for #147) is the natural next guard. Do not treat the path edit as closing the class. |

### R4. MCP protocol pin lag

| Field | Evidence |
|---|---|
| **Class** | INV-3 says "latest non-draft MCP revision, never the LCD subset." The pin, the deny-list, and the emitted shape drift independently. |
| **Prior count** | **3 filed gaps** on one audit, plus the stale pin that produced them. |
| **Prior instances** | **#166** (closed #188): docs re-pin 2026-07-28; annotation shape unchanged — *documentation-only*, and that is how #176/#177 were missed. **#176** (OPEN): `CallToolResult.resultType` new at 2026-07-28; Strategos omitted it. **#177** (OPEN): `Tool.icons` existed in **both** 2025-11-25 and 2026-07-28 — not a revision delta; INV-3 checklist kept flagging. **#178:** INV-3 named a superseded revision; deny-list omitted it; 12 sites across 3 files. **#171** (adjacent, not this wave): `Idempotent` → `IdempotentHint`. |
| **Prior fixes** | #188/#194: catalog re-pin + deny-list widen. Code shape unchanged until this diff. |
| **Guard exists?** | INV-3 Check 3.3 (revision-string grep). It **failed as a guard** once (stale deny-list → zero hits on a stale tree). Checks 3.1–3.2 (`_meta`, `OutputSchema`) exist. No CI job found that runs these greps. |
| **This diff** | **`extends-guard` + instance fix.** Hosting pin 2.2.0 (1.3.0 SDK has no `ResultType`). Every constructed `CallToolResult` sets `resultType`. Optional `Icons`, null when unset. INV-3 Check 3.4 / 3.5 added. Checklist stops flagging the icon gap. |
| **Kill fixtures** | `AssertResultTypeComplete` on MapTraversal / Error / sibling mappers (`TraversalToolHostingTests`, `ProviderBoundDispatchTests`). `CreateServerTool_WithIcons_MapsOntoProtocolTool`; null-when-unset tests. Check 3.4 is a `grep -L ResultType` over files that construct `CallToolResult` — substring presence, not assignment on every construction. |
| **Guard candidate?** | Class found three times. The deny-list grep is the existing idiom; it already decayed once. Lens 7 should treat "stale deny-list" as a **self-test** obligation (a fixture that contains a forbidden revision must fail the check). Check 3.4 as `grep -L` can pass if `ResultType` appears in a comment. |

### R5. Enum ordinal / additive split

| Field | Evidence |
|---|---|
| **Class** | A published enum's numeric values are a persistence / wire contract. Inserting or reordering members silently remaps stored data. |
| **Prior count** | **2 shipped incidents** + 1 deferred positional sibling. |
| **Prior instances** | **#183** (closed #194, docs-only): generated `{Workflow}Phase` reorder from document-order `StepNames` is a **data migration under Newtonsoft** (`EnumStorage` ordinal). STJ + `JsonStringEnumConverter` stores names; the non-migration proof was scoped only to this repo's Marten host. Guard = `docs/src/content/docs/reference/phase-persistence.md` + CHANGELOG warning. **#163** (OPEN): original proposal set `HandAuthoredContract = 1`, `Ingested = 2` — would have **moved** `Ingested`. Plan T5 and this diff refuse that. **#156.3** (out of wave): `DiagnosticForkCount_{i}` positional keys; v2.10.0 live on NuGet; re-key is a Marten migration. Same class, different surface. |
| **Prior fixes** | #194: document the Newtonsoft condition. No machine guard that Phase member order is frozen, or that public enum values only append. |
| **Guard exists?** | **Convention + docs.** `DescriptorSource` remarks now say "numeric values are part of the public contract; new members are appended." PublicAPI tracks the enum. No test that `Ingested == 1` forever. |
| **This diff** | **`instance-fix-only` (avoids the class).** `662f0d1`: `HandAuthoredContract = 2`; `HandAuthored = 0` and `Ingested = 1` do not move. AONT205 retargeted via `IngestedIntentInvariant` to `Ingested` only. Tests: `AONT205Tests.Build_HandAuthoredContractWithActions_DoesNotFireAONT205`; `HandAuthoredContractMergeTests.Merge_ContractAuthoredAction_SurvivesIngestedStructuralContribution`; `Merge_IngestedActions_StillFailAONT205`. |
| **Kill fixtures** | Merge/AONT205 tests above. **No fixture that fails if someone writes `Ingested = 2`.** The #163 body *is* that kill fixture, living only in the ticket. |
| **Guard candidate?** | Class found twice (#183, the avoided #163 remap) plus #156.3. A PublicAPI / compile-time assert that published enum numeric values are append-only is a guard candidate. Three times with docs-only on #183 is a proof-system finding if #156.3 is counted; this wave is instructed not to invent #156.3 obligations. |

### R6. Obsolete without successor

| Field | Evidence |
|---|---|
| **Class** | A public API is marked done (or "deprecated") without a reachable replacement on the same authoring surface, so the attribute is a comment. |
| **Prior count** | **1 filed instance**, with a documented near-miss. |
| **Prior instances** | **#115** (OPEN): remaining mechanical task "Must be **mechanical** (`[Obsolete]` with a named successor), not documentation-only." #185: "#115's only mechanical task needs an `[Obsolete]` whose successor **#168 has not yet defined**." v2.9.0 already delivered the CLR-free rationale path; this was the leftover. |
| **Prior fixes** | None on `Requires` before this diff. `IActionBuilderOfT.Requires` shipped since ontology v2 (`cdfa048` / `b8657dc`). |
| **Guard exists?** | RS0016/RS0017 PublicAPI tracking. No guard that every `[Obsolete]` names a type that exists and is reachable from the same fluent surface. |
| **This diff** | **`instance-fix-only`.** `d01a78f`: `[Obsolete("Use ActionDescriptor.Preconditions … There is no fluent successor.")]`. Points at an existing descriptor-first field. Plan T6: do not invent a fluent successor. Docs (`c366147`) name `ObjectTypeFromDescriptor` / `ApplyDelta` as the CLR-free seam. |
| **Kill fixtures** | None that fail if Obsolete text names a missing type. PublicAPI.Unshipped records the attribute. |
| **Guard candidate?** | **Not yet** under the "found twice" rule. One instance. Related to R3 (#163 "inert without a producer"; R7 unauthorable diagnostic) but not the same class. Record as a **first instance**. |

### R7. Diagnostic aimed at an unauthorable (or already-false) shape

| Field | Evidence |
|---|---|
| **Class** | A diagnostic or gate is declared, but no compiling authoring path can trigger it — or it fires on a shape that is not in fact inert. A present diagnostic is then an INV-5 violation (control with no trigger / false positive). |
| **Prior count** | **2 concrete AGWF022 incidents** + the AGWF035 half-close (R1) as the same operator. |
| **Prior instances** | **AGWF022 false positives** (correctness-core §Problem + DR-6): fork-path and loop-body confidence *already lowered*; four `Deferred` parity entries and two emission blocks were wrong. **AGWF022 near-miss retarget:** instance-named + configure-lambda fork-path is **unauthorable** — `IForkPathBuilder` has no overload that takes both. Spec: retargeting there "would leave a declared control with no trigger, the same INV-5 violation." **#187 actually retargeted** at approval-preceding-step confidence, which *is* authorable (`DeclaredButInertTests`). **AGWF035 over-reach-only** (#184/#185): diagnostic present, cannot see the filed under-reach shape. **#179 fixture:** `SourceTexts.WorkflowWithTerminalBranch` *is* the bool-discriminator CS8510 shape but was only parse/Mermaid-consumed — a fixture that looks like coverage and is not (R3). |
| **Prior fixes** | #187: delete false-positive blocks; retarget AGWF022; harden parity to require a running test *and* that a deferred entry's cited diagnostic actually fires. |
| **Guard exists?** | `DeclaredButInertTests` pins the live target. Parity guard requires cited diagnostic to fire for deferred entries. No general "diagnostic has at least one authorable kill fixture" catalog check. |
| **This diff** | **Does not add this class.** Completes AGWF035 (R1). Does not change AGWF022. |
| **Kill fixtures** | `DeclaredButInert_ApprovalPrecedingStepConfidence_ReportsAgwf022`; `_LowersNoConfidenceGate` (proves the claim). AGWF035 under-reach fixtures (R1) are the kill for the half-closed diagnostic. |
| **Guard candidate?** | Found twice (false AGWF022 + half-closed AGWF035). A catalog-level rule — every AGWF/AONT error id has a kill fixture that compiles from the public DSL — is the class-level guard. `AgwfCatalogParityTests` / `AgwfSingleSourceTests` exist but check identity, not reachability of the trigger. |

### R8. Published table / saga / diagnostic drift (shared graph)

| Field | Evidence |
|---|---|
| **Class** | `ValidTransitions` / Mermaid / saga handlers / AGWF035 are independent constructions of the same route graph and disagree. |
| **Prior count** | **3 filed disagreements.** |
| **Prior instances** | **#175:** `TransitionsEmitter` flat linear chain; sibling exclusive paths chained; terminal → path step. Emitted public API. **#184:** `TransitionsEmitter` and `MermaidEmitter` handle `BranchOnExit`; the saga did not — table declared an edge the saga cannot take. **#189:** transitions table `{ Normalize, [PayFull, PayPartial, Failed] }` vs saga that can only emit one successor. |
| **Prior fixes** | #187: construct-aware `PhaseGraph` *inside* `TransitionsEmitter` (private nested). #194: saga emission for loop-exit; AGWF003/036. Diagnostic still built its own successor scan. |
| **Guard exists?** | No equality proof that saga edges ⊆ `ValidTransitions` ⊆ diagnostic graph. |
| **This diff** | **`extends-guard` (narrow).** Shared internal `PhaseGraph` (`46fb93a`) binds AGWF035 under-reach to `ValidTransitions`. **Does not bind the saga.** A later emitter edit can still lie relative to the table. |
| **Kill fixtures** | Under-reach tests pass a graph with an edge stripped (`PhaseGraph.WithoutSuccessor`) — that is a diagnostic kill, not a saga/table equality kill. |
| **Guard candidate?** | Found three times. Sharing PhaseGraph is the right local idiom for diagnostic↔table. Saga↔table remains open. |

---

## High-churn files and code rewritten more than once

Commit counts since 2026-01-01 on the generator/contracts path (not unique authors):

| File | Commits | Rewrites |
|---|---|---|
| `WorkflowIncrementalGenerator.cs` | 10 | Classification, AGWF003/022/035/036/037 call sites. Highest churn in scope. |
| `WorkflowDiagnostics.cs` | 10 | Catalog descriptors track every new AGWF. |
| `AgwfCatalog.tsp` | 7 | 0.2 → 0.7.0: AGWF022, 023–034, 035, 036, 037. Touched `a965f3b`, `4d060f4`, `12098da`. |
| `StepExtractor.cs` | 7 | Ordering vs classification; instance-name duplicate (#145). |
| `TransitionsEmitter.cs` | 4 | **Rewritten twice:** linear `StepNames` chain (init/`57cf4ed`) → construct graph (`a965f3b`) → `PhaseGraph` extracted (`46fb93a`). |
| `TerminalReachabilityGuard.cs` | 2 | Created over-reach-only (`a965f3b`); under-reach arm (`5e94af4`). |
| `DiagnosticForkExtractor.cs` | 2 | Introduced `38854c0` (v2.10.0); AGWF037 `97f52cd`. |
| `renovate.json` | 3 | Init `57cf4ed`; CodeRabbit+Renovate `#4` `1926a7f` (the 404 path lands here); path fix `334f64c`. **Broken for ~7 months** (2026-01-09 → 2026-08-27). |
| `DescriptorSource.cs` | 2 | Two-value enum `acd62f2` (2.5.0); additive `= 2` `662f0d1`. |
| `OntologyServerToolFactory.cs` | 4 | Bridge `5784566`; contracts `38854c0`; `resultType`/`Icons` `887eb9a`. |
| `INV-3-*-spec.md` / `deterministic-checks.md` | 2 on this branch | Tracked in #194; deny-list + 3.4/3.5 this diff. |

`PhaseGraph` is new (`46fb93a` only) but is the third representation of a graph that already lived as a private nested type and, before that, as a linear scan. That is the rewrite signal.

---

## Review comments that repeat

| Theme | Where it repeats | Implication |
|---|---|---|
| Collision / last-write-win / first-wins | CodeRabbit on **#187** surfaced #189/#190/#191 in one review. #156.2 (CodeRabbit on #154) is the same operator on triggers. #189 body warns that first-writer-wins *looks* like a fix. Ontology comments repeat "do not first-wins-bind." | Review keeps finding the class. Guards arrived one incident later each time (R2). |
| Control looks present, is inert | #185 body (the six). #184 "why AGWF035 does not catch it." #181 "a config that parses is not evidence." #178 deny-list returns zero on a stale tree. DR-5 substring parity. CodeRabbit on #194: test still named `Diagnostic_DuplicateInBranchPaths_NoDiagnostic` after it started requiring AGWF003 — the **name** is an inert control. | This is the review refrain of the milestone. R3. |
| Half-closed AGWF035 | #184 acceptance: "either the route arm lands, or the guard's documented scope stays narrowed." #185 comment 2 after #194: still open by design. Plan T1. CHANGELOG Residue. | Three written reminders before `5e94af4`. |
| Oracle duplication | CodeRabbit on #187: `RunWorkflowWithOutcomeAsync` copies `SagaCompletionProbe` line-for-line; only one path is pinned. | Adjacent to R3 (oracle that can pass on the wrong subject). |
| Live proof vs lint | #133/#174 left OPEN: parity-gate confirm and live-issue proof are maintainer-owned. #181 acceptance: Dependency Dashboard or first PR, not a valid JSON file. | Recurs whenever the control lives in another process (GitHub App, org workflow). |

---

## Prior incidents on this path (timeline)

```
2026-01-09  1926a7f  renovate.json second extends 404 path introduced (#4)
2026-05-15  acd62f2  DescriptorSource { HandAuthored=0, Ingested=1 }; AONT205 overloads both
2026-07-08  38854c0  v2.10.0; #155/#156 filed; PermitTrigger CS0152-only; fork C# twin skipped
2026-08-07  73cfcb4  v2.10.0 on NuGet — #155/#175 live in every published version
2026-08-22  correctness-core spec; #185 filed; AGWF035 specified as position (over-reach)
2026-08-23  a965f3b  #187: classification + AGWF035 over-reach; #184 filed the same day
2026-08-24  7978e8b  #188 hygiene; CodeRabbit review of #187 files #189/#190/#191
2026-08-27  4d060f4  #194 residue without shutting #185; AGWF035 route arm still deferred
2026-08-27  324768f  this branch: T1–T6 + CHANGELOG Residue
```

#181's inert Renovate config is the **oldest** open instance of R3 on a file this diff touches (~7 months).

GitHub issue state at survey time (not a merge claim): #185, #181, #176, #177, #163, #115, #156, #147, #133, #174 **OPEN**. #184/#155/#175/#180/#182/#186/#179/#178/#183/#189/#190/#191 **CLOSED** via #187/#194. This branch implements several still-OPEN tickets; closing them is out of band.

---

## Existing kill-fixture register (what each one already guards)

| Fixture | Guards class |
|---|---|
| `Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug` | R1 over-reach (shipped #155) |
| `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires` | R1 under-reach (#184 shape) |
| `Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires` | R1 under-reach (branch rejoin) |
| `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` | R1 false-positive trap |
| `Diagnostic_ExistingCorpus_NeverFires` | R1 / R7 corpus silence |
| `CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga` | R2 PermitTrigger C# |
| `ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga` | R2 PermitTrigger JSON import |
| `Create_WithDuplicateTrigger_ThrowsArgumentException` | R2 model-level reject |
| `DeclaredButInert_ApprovalPrecedingStepConfidence_*` | R7 live AGWF022 target |
| `AssertResultTypeComplete` + JSON substring | R4 resultType emit |
| `OntologyToolDescriptor_WithoutIcons_DoesNotInventPlaceholder` | R4 null-when-unset |
| `Build_HandAuthoredContractWithActions_DoesNotFireAONT205` | R5 / #163 retarget |
| `Merge_IngestedActions_StillFailAONT205` | R5 Ingested still prohibited |
| OntologyGraphBuilder first-wins tests | R2 (ontology ClrType) |
| StepConfigParityTests (hardened) | R3 substring-parity |
| *(missing)* renovate 404 `extends` | R3 CI/config |
| *(missing)* `Ingested == 1` ordinal freeze | R5 |
| *(missing)* saga edges ≡ PhaseGraph | R8 |

---

## Proof-system findings (class ×3 with no / half guard)

1. **R3 inert-looking control — three or more times, CI/config axis still unguarded.** #185 named six. Parity and catalog-tracking got guards. Renovate-extends-resolve, unused OIDC (#147), and "grep deny-list is current" did not. A third instance with no guard **is** a finding about the proof system.
2. **R2 silent collision — four times, three after-the-fact diagnostics.** AGWF003 → AGWF036 → AGWF037. Each incident minted a new id. That is the local INV-5 idiom; it is also evidence that collision-reject is not a standing policy.
3. **R1 AGWF035 shipped half-closed.** The second half was written down in #184/#185 the day the first half merged. A diagnostic that cannot see its motivating instance is R3 applied to INV-5.

---

## Open questions

1. Does the new AGWF035 under-reach arm fire on the #182 / #186 approval-dispatch shapes if those emitter fixes regress, or only on rejoin-last-step? The plan scopes the arm to "construct marked rejoin." Unsettled — do not assume coverage of the approval family.
2. Does `gh api` on `lvlup-sw/lvlup-claude` contents still 200 at `tools/renovate-config/presets/dotnet.json` *from this environment*? #181 body showed it did on 2026-08-23. Not re-verified here. Even a 200 is not the #181 acceptance (live Renovate run).
3. Are INV-3 deterministic greps executed in CI, or only as a human checklist? `.github/workflows` has no hit. If they are checklist-only, Check 3.4/3.5 inherit the stale-deny-list decay.
4. Is there a consumer already persisting `DescriptorSource` as an integer? Additive `= 2` is safe only if nobody stored a closed two-value assumption. Unsettled.
5. GitHub issues #181/#176/#177/#163/#115 remain OPEN. Whether that is tracker lag or withheld live proof (#181) is unsettled.
6. Option B / AGWF036-as-the-ship: #189/#190/#191 closed by reject-the-shape, not construct identity. Recurrence of "same type, two instance names" after AGWF036 would be a new instance of R2, not of R1.

## Assumptions

- External references are leads. Issue bodies and commit messages are primary; CHANGELOG Residue is a claim (lens 2), cited here only as a pointer to which tracks landed.
- `#156` counts as three items; only **#156.2** is in this wave. Prior-count for R2 includes #156.2 as one instance.
- "Guard" means a mechanism that fails when the class is present, not a test that the current instance is fixed.
- Untracked `docs/2026-06-16-edge-*` files were not mined (stage0 out of scope).
