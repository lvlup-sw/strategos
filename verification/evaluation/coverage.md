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

# Evaluation lens 2 — Coverage against the scope set

Fresh-context read of `verification/ledger.md`, `verification/survey.md`,
`verification/stage0.md`, plus the claim-derivation index, existing-proof
survey, mechanism survey, and the `4d060f4...324768f` name-status / deletion
diff. This file judges the **set**, not each obligation’s wording or rung
soundness.

Target kind is **diff**. The codebase stopping-rule check in
`references/scoping.md` does not apply.

Cost setting **high** was stated in stage 0 before any lens ran. The ledger
records `skipped: none`.

---

## Scope-set × Active obligations

Stage 0 ranked surfaces S1–S7. Active slugs assigned below by the surface
their **Claim / Scope** rows actually bind. A slug can appear on more than
one row when it spans a seam.

| Rank | Surface | Survey risk | Active slugs | Count |
|---|---|---|---|---|
| S1 | AGWF035 route arm + shared `PhaseGraph` | highest | `agwf035-underreach-ir-not-emission`, `phasegraph-type-not-instance`, `agwf035-catalog-polarity-lie`, `agwf035-error-still-emits`, `agwf035-json-import-unreached`, `agwf035-all-complete-silent`, `agwf035-overreach-preserved`, `compat-agwf035-breaking`, `compat-validtransitions-nonreversing` | 9 |
| S2 | Contracts 0.7.0 + AGWF037 | highest | `agwf037-reject-not-dedup`, `contracts-0-7-0-pack-incomplete`, `contracts-changelog-contradicts-0-7-0`, `schema-diff-skip-succeeds`, `agwf037-catalog-identity`, `diagnostic-fork-ctor-open` | 6 |
| S3 | MCP `resultType` + `Icons` | high | `mcp-resulttype-and-pin`, `icons-null-when-unset`, `traversal-result-flags-independent` | 3 |
| S4 | `HandAuthoredContract` + AONT205 retarget | high | `handauthoredcontract-unreached`, `descriptor-source-docs-omit-member-2`, `aont205-analyzer-unreached` | 3 |
| S5 | `Requires` obsolete | medium-high | `requires-obsolete-observable`, `compat-publicapi-omits-obsolete` | 2 |
| S6 | Renovate preset path | medium (boundary) | `renovate-resolve-unasserted` | 1 |
| S7 | Docs + CHANGELOG claims | medium | `contracts-changelog-contradicts-0-7-0`, `descriptor-source-docs-omit-member-2`, `claim-clr-free-xor-docs`, `claim-issue-185-tracker` | 4 |

Changed-surface list from stage 0 (15 items) vs the Active set: every named
surface has at least one slug that cites it, **except** the AONT205
replacement scan’s **field-set widening** (see F1) and the shared
`PhaseGraph` **construction rules** (see F4). `OntologyDelta` /
`ActionDescriptor` / `IOntologyBuilder` diffs in this wave are comment-only
or remarks; they do not add a keep-true that the S4/S5/S7 slugs miss.

No ranked member is empty. The faults below are **thin spots, merge-narrowing,
and unrepresented deletions**, not a missing S-row.

---

## Rung distribution

| Rung | Count | Slugs |
|---|---|---|
| 1 Construction and generation | 2 | `phasegraph-type-not-instance`, `agwf035-catalog-polarity-lie` |
| 2 Compiler and type system | 3 | `requires-obsolete-observable`, `diagnostic-fork-ctor-open`, `traversal-result-flags-independent` |
| 3 Deterministic structural analysis | 11 | `agwf035-underreach-ir-not-emission`, `agwf035-error-still-emits`, `agwf035-json-import-unreached`, `contracts-changelog-contradicts-0-7-0`, `schema-diff-skip-succeeds`, `mcp-resulttype-and-pin`, `handauthoredcontract-unreached`, `descriptor-source-docs-omit-member-2`, `aont205-analyzer-unreached`, `compat-publicapi-omits-obsolete`, `agwf037-catalog-identity` |
| 4 Contract and component tests | 6 | `agwf035-all-complete-silent`, `agwf035-overreach-preserved`, `agwf037-reject-not-dedup`, `icons-null-when-unset`, `compat-agwf035-breaking`, `compat-validtransitions-nonreversing` |
| 5 Production-path integration | 2 | `contracts-0-7-0-pack-incomplete`, `renovate-resolve-unasserted` |
| 6 Human judgment | 2 | `claim-clr-free-xor-docs`, `claim-issue-185-tracker` |

The set is **not** parked at rungs 4 and 5. Cheap rungs were used. The
“nobody considered the cheap rungs” pattern does not hold.

---

## Findings

### F1. S4 AONT205 field-set widening has no Active claim

- **Affected:** ledger-wide (S4); evidence files `claim-aont205-ingested-only.md`, `handauthoredcontract-unreached`
- **Concern:** Stage 0 names this surface “`DescriptorSource.HandAuthoredContract` + **AONT205 retarget**.” Mechanism survey finding 20 records a **deletion**: the inline Actions/Events/Lifecycle scan was removed from `OntologyBuilder` and `OntologyGraphBuilder` and replaced by `IngestedIntentInvariant.FindOffendingField`, which keeps the early-return unless `Source == Ingested` and **adds** `InterfaceActionMappings` and `ExternalLinkExtensionPoints` (`IngestedIntentInvariant.cs:22-49`). Same diagnostic id; more ingested shapes now fail the build. That is newer invariant code reading older ingested descriptors.
- **Scope:** The Active slug `handauthoredcontract-unreached` claims member 2 is assigned, survives merge, and is treated as hand-side by **AONT201/203/204**. Its Scope row notes “skip-unless-Ingested **is** reached.” It does not claim (a) Actions/Events/Lifecycle still fail after the extract, or (b) the two new fields are a breaking widen for existing `Ingested` graphs. `aont205-analyzer-unreached` is the Roslyn descriptor, a different root. `claim-aont205-ingested-only.md` exists in `verification/obligations/` and in the claim-derivation index; it did not survive as a canonical Active row.
- **Evidence:** `verification/survey/mechanism.md` findings 20–21; `src/Strategos.Ontology/Internal/IngestedIntentInvariant.cs:18-49`; new tests `AONT205Tests.Build_IngestedInterfaceActionMappings_AONT205Error` and `Build_IngestedExternalLinkExtensionPoints_AONT205Error` (the target added proofs of the widen, which is production code for this lens). Ledger Active table for `handauthoredcontract-unreached` and `aont205-analyzer-unreached`.
- **Suggested action:** Promote a keep-true that the extracted scan is equivalent on the old three fields **and** that the two new fields are an intentional, documented compatibility event for already-ingested graphs. Do not leave that keep-true as a supporting file under the “member 2 has no producer” claim.

### F2. S2 consumer-before-producer / publish path is evidence, not an Active claim

- **Affected:** ledger-wide (S2); `contracts-0-7-0-pack-incomplete`
- **Concern:** Stage 0 ranks S2 highest because Exarchos extracts the nupkg across a process boundary and “emitted converters throw on unknown members, so consumer upgrade must precede producer emission.” That operational path (rollout order, first `contracts-v0.7.0` publish, rollback of a published tag) is written in `compat-agwf037-closed-enum-upgrade-order.md`. The Active claim those files support is “a green 0.7.0 pack test means the nupkg is versioned **and** contains `agwf-catalog.json` plus `AgwfEntryDuplicatePermittedForkTrigger.json`.” Pack membership and upgrade-order are different keep-trues. `catalog_version` staying `"0.2.0"` while `count` moves 30→31 is recorded only in that evidence file.
- **Scope:** Published Contracts wire + Exarchos restore. The ledger’s Rollback rows mention “a published tag does not reverse” but no Active row requires the notice line, the tag, or the order.
- **Evidence:** `verification/stage0.md` S2 / Closure members table; `verification/obligations/compat-agwf037-closed-enum-upgrade-order.md` (Compatibility class: “breaking change presented as additive”); `src/Strategos.Contracts/Generated/agwf-catalog.json` `catalog_version` vs `count`; ledger Evidence line under `contracts-0-7-0-pack-incomplete`.
- **Suggested action:** Either lift upgrade-order / first-publish / consumer-notice into an Active obligation, or record explicitly that pack-membership is the chosen keep-true and the operational path is out of the Active set.

### F3. AGWF035 shared `reported` set can drop a second pair — no obligation

- **Affected:** ledger-wide (S1 seam); not a named slug
- **Concern:** The new under-reach arm reuses the over-reach `Report` helper. Dedup keys `$"{stepName}\u001f{successorStepName}"` (`TerminalReachabilityGuard.cs:420`). Over-reach runs first and passes `(wrongSuccessor, workflow, successor)`. Under-reach passes `(declaredTerminal, workflow, lastStep)`. Two under-reach dispatchers that miss the same terminal collide only when argument 2 also matches; an over-reach pair and an under-reach pair that invert onto the same two names silently drop one. Mechanism survey finding 6 named this. The Active S1 set talks about IR-vs-emission, instance-share, catalog polarity, import, and `hasErrors`. None of them claim “every under-reach dispatcher that should fire is not collapsed by the shared set.”
- **Scope:** One helper, two arms, one `HashSet`. Cross-cutting: only this lens owns the seam between the already-shipped over-reach loop and the new arm.
- **Evidence:** `verification/survey/mechanism.md` finding 6; `TerminalReachabilityGuard.cs:118-128`, `:143-163`, `:412-423`. No Active slug, no evidence file titled for this key.
- **Suggested action:** Add a keep-true (or a kill fixture) that two distinct missing dispatchers, and an over-reach + under-reach pair that share names in opposite slots, both report. Or record that one-report-per-name-pair is accepted.

### F4. S1 construction rules (`claim-phasegraph-edge-kinds`) dropped at synthesis

- **Affected:** ledger-wide (S1); `claim-phasegraph-edge-kinds` (claim-derivation only)
- **Concern:** Claims 103–105 (routed edge replaces linear chaining; additional edge coexists; every target is a step-list entry or a standard terminal) were inventoried as an obligation at rung 4 with “no dedicated test file.” The synthesized Active ledger does not carry that slug. Survey marks S1 highest because a drift between the diagnostic graph and `ValidTransitions` is a published-API lie. All nine S1 Active slugs attack IR-vs-emission, instance-vs-type, polarity, import, gating, or regression of the over-reach half. None lock the **construction algorithm** both consumers now execute from the lifted type.
- **Scope:** `PhaseGraph.Build` / `EdgeBuilder`. Mechanism finding 1 says a body-to-body compare of `EdgeBuilder` against `4d060f4` is identical, and only `ThrowIfNull` / empty-list construction changed. That is a reason the lift is low-delta, not a reason the shared algorithm needs no keep-true at cost **high**.
- **Evidence:** `verification/obligations/_claim-derivation-index.md` table for `claim-phasegraph-edge-kinds`; `verification/survey/mechanism.md` finding 1; `verification/survey/existing-proof.md` P7 (“no rung-4 dedicated PhaseGraph / TransitionsEmitter equivalence test”).
- **Suggested action:** Either restore `claim-phasegraph-edge-kinds` (or an edge-equality lock, already named under `phasegraph-type-not-instance`) as Active, or write on the ledger that construction-rule identity with `4d060f4` is an assumption, not an obligation.

### F5. S7 docs/tracker is heavy relative to the S4/S1 thin spots

- **Affected:** ledger-wide
- **Concern:** Over-investment is a coverage fault because it spends the budget the risky surfaces needed. S7 (medium, no runtime boundary) holds four Active slugs, including a rung-6 “pages state a limit the types already enforce” (`claim-clr-free-xor-docs`) and a rung-6 tracker-state row (`claim-issue-185-tracker`). S4 is high and is missing the retarget/field-widen keep-true (F1). S1 is highest and is missing the report-dedup seam (F3) and the construction-rule lock (F4). The portfolio spent Active rows on CHANGELOG/tracker/guide prose while those seams stayed in supporting files or in the mechanism survey only.
- **Scope:** The Active set as a whole, not the quality of any one S7 row. `contracts-changelog-contradicts-0-7-0` and `descriptor-source-docs-omit-member-2` are still real contradictions; the finding is **count and placement**, not that those two should vanish.
- **Evidence:** Scope-set table above; stage 0 ranking (S1/S2 highest, S7 medium); Active “Supported claims” list keeping `claim-clr-free-xor-docs`.
- **Suggested action:** Keep the two contradiction slugs. Treat the two rung-6 supported-doc/tracker rows as optional at this cost setting if inventory budget is needed for F1–F4.

### F6. INV-3 “gaps remain open” sentence was deleted; no obligation on that deletion

- **Affected:** ledger-wide (S3 / changed surface 10); `mcp-resulttype-and-pin`, `icons-null-when-unset`
- **Concern:** The lens asks for a deletion with no obligation. No source or test **file** was deleted (`git diff --diff-filter=D` is empty). Two in-file deletions carry risk. `Directory.Build.targets` adding `CS0618` to `NoWarn` is already `requires-obsolete-observable`. The other is INV-3: the sentence “Two genuine gaps against the newer revision remain open and are tracked separately” was removed and replaced by text that `resultType` and `Icons` are now in the invariant (`.agents/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md`). That is a removed tracker. Adjacent Active slugs say the new deny-list is grep-not-CI and that the non-null `Icons` path is unreached. They do not claim “deleting the open-gap sentence is allowed only after the protected path is closed.”
- **Scope:** INV-3 catalog + `deterministic-checks.md` 3.4/3.5 (added, not deleted). The deletion is the gap-tracker sentence, not the new checks.
- **Evidence:** `git diff` on `INV-3-mcp-first-class-latest-spec.md`; mechanism/wildcard notes that four tools rely on SDK wrap and `Discover` never sets `Icons`; ledger `mcp-resulttype-and-pin` / `icons-null-when-unset`.
- **Suggested action:** Either add a keep-true that the INV-3 open-gap tracker stays until factory wrap and a consumer `Icons` producer are closed, or accept the deletion as the same claim those two slugs already refute.

### F7. Surrounding import identity sweep was not extended for AGWF037

- **Affected:** `agwf037-reject-not-dedup`; new proof `DuplicatePermittedForkTriggerTests` / `ImportRejectionTests`
- **Concern:** A test in the diff is production code and can be vacuous. Most new tests this wave adds are already named as weak or wrong-subject by existing-proof and then **obligated** (WithoutSuccessor positives, catalog `Contains`, pack version without named AGWF037 files, record-ctor `Icons`, Hosting-test pin). One surrounding proof was **not** extended and has no Active claim: `EachRejectedCase_HasItsOwnDistinctDiagnosticId` still enumerates AGWF027–AGWF034 only (`ImportRejectionTests.cs:470-506`). AGWF037 has its own twin tests, so uniqueness of the new id against the older reject set is unasserted. Existing-proof P14 already recorded the hole; the synthesized ledger did not.
- **Scope:** JSON-import reject identity, S2. Not the C# twin.
- **Evidence:** `verification/survey/existing-proof.md` P14; `src/Strategos.Generators.Tests/Import/ImportRejectionTests.cs:475-507` vs new cases at `:440-467`.
- **Suggested action:** Extend the uniqueness sweep (or add an Active keep-true that AGWF037 is in that set). The existing AGWF037 twins do not close “no case borrows another’s id” for the new code.

### F8. Reverse direction of the MCP pin is only half-stated

- **Affected:** `mcp-resulttype-and-pin`
- **Concern:** The lens asks for the reverse of a compatibility change: newer code that reads **older** persisted / on-the-wire data, not only older consumers that read the new contract. The Active MCP claim and its Rollback row discuss clients that already expect `resultType` seeing an omission after revert, and INV-3 forbids an LCD shim “for older clients.” The reverse — a 2026-07-28 Hosting server emitting `resultType: complete` (including `ErrorResult`) toward clients that still speak a pre-2026-07-28 shape, and newer Hosting reading an older SDK default when the `VersionOverride` is dropped — is an open question on that slug (`ErrorResult` + `complete` protocol-legal; four wrap tools). It is not missing as a surface (S3 has three slugs). It is missing as a **keep-true in the reverse direction**.
- **Scope:** Protocol boundary, S3. `CallToolResult` is not a persisted saga event; “older data” here is older client/SDK composition, not a stored graph.
- **Evidence:** INV-3 `:36` / claim 147 (clients treat an **absent** field as `complete` for older **servers**); factory `ErrorResult` at `OntologyServerToolFactory.cs:410-412`; ledger open questions on `mcp-resulttype-and-pin`.
- **Suggested action:** Add an explicit reverse-compat keep-true or mark the reverse direction out of scope (INV-3’s policy is “do not shim”). Do not leave it only as an open question on the forward pin.

---

## New proofs the target adds (vacuity scan)

Treated as production code. Already represented in the Active set (not re-found here):

| New / extended proof | Vacuity | Already obligated as |
|---|---|---|
| `WithoutSuccessor` under-reach positives | Wrong subject for shipped `Build(model)` | `agwf035-underreach-ir-not-emission` |
| Guard call-site scan checks argument `[1]` only | Unwiring `phaseGraph` stays green | `phasegraph-type-not-instance` |
| Catalog / markdown / enum list appends | Identity / substring | `agwf037-catalog-identity` |
| Pack test 0.6.0 → 0.7.0, named schemas unchanged | Version green without AGWF037 files | `contracts-0-7-0-pack-incomplete` |
| `OntologyToolDescriptor` record-ctor `Icons` | Test-built record | `icons-null-when-unset` |
| Hosting tests’ own `VersionOverride` 2.2.0 | Production csproj pin unasserted | `mcp-resulttype-and-pin` |
| INV-3 / checks 3.4–3.5 greps | Comment-satisfiable; not CI | `mcp-resulttype-and-pin` |
| `HandAuthoredContractMergeTests` `Source == HandAuthored` | Proves collapse; CHANGELOG says survive | `handauthoredcontract-unreached` |
| `IActionBuilderTests` Obsolete reflection | Real for the attribute; suite `NoWarn`s CS0618 | `requires-obsolete-observable` |
| `DescriptorSourceTests` ordinal `= 2` | Restates the enum | absorbed supported claim |

New proofs that are **not** vacuous: AGWF037 C#/JSON twins (subject-bound on this revision’s generator); traverse wire `resultType` serialize helper; AONT205 new-field Build tests (subject-bound for `OntologyGraphBuilder.Build`, not for ingest tagging).

Unrepresented vacuity: F7 (import uniqueness sweep not extended).

NSubstitute `IActionBuilder` tests (existing-proof P41) are **pre-existing**, not added by this diff. Not counted as new proofs.

---

## Deletions

| Deletion | Obligation? |
|---|---|
| No source or test **file** deleted | — |
| Nested `PhaseGraph`/`EdgeBuilder` moved, body identical | F4 (construction not Active); not a dropped check |
| No first-wins-dedup algorithm removed (old path accepted duplicates) | `agwf037-reject-not-dedup` covers the new reject |
| Inline AONT205 scan deleted, replaced and widened | **F1 — no Active keep-true** |
| INV-3 “two genuine gaps remain open” sentence deleted | **F6 — no obligation on the deletion** |
| Packaging asserts retargeted 0.6.0 → 0.7.0 (replaced, not removed) | `contracts-0-7-0-pack-incomplete` |
| `CS0618` warning deleted for every test/benchmark project | `requires-obsolete-observable` |

The common “removed test, no obligation” pattern did not occur. The two
unrepresented deletions are a replaced invariant (F1) and a removed tracker
sentence (F6).

---

## Config, flags, operational, reverse compatibility

**Config / flags.** `Directory.Build.targets` `CS0618` `NoWarn`, Hosting
`VersionOverride` 2.2.0 vs CPM 1.3.0, `renovate.json` `extends`, INV-3
checks 3.4/3.5 not in `ci.yml`, `contracts-schema-diff.yml` skip-and-pass,
and `PublicAPI.Unshipped.txt` (no Obsolete column) all have Active slugs.
No feature-flag surface in this diff was left without a row.

**Operational / rollback.** Generator revert ≠ consumer rebuild:
`compat-validtransitions-nonreversing`, `compat-agwf035-breaking`. Renovate
apply is `renovate-resolve-unasserted`. Schema-diff skip is
`schema-diff-skip-succeeds`. The **publish / first-tag / Exarchos upgrade
order** path is F2.

**Reverse compatibility (newer code, older data).**

| Older artifact | Newer code | Active coverage |
|---|---|---|
| C# workflows that compiled clean | AGWF035 under-reach | `compat-agwf035-breaking` |
| Already-emitted `ValidTransitions` / sagas | Generator revert or rebuild | `compat-validtransitions-nonreversing` |
| Persisted / constructed `DescriptorSource` 0/1 | Additive enum `= 2` | absorbed `claim-handauthoredcontract-additive` |
| Ingested descriptors with the two new intent fields | Widened AONT205 scan | **F1 — none** |
| Exarchos / converter still on 0.6.0 | 0.7.0 catalog + AGWF037 | evidence only (F2); reverse (new code reading old catalog) is additive and unproblematic |
| Pre-2026-07-28 MCP client / SDK 1.3.0 | Hosting 2.2.0 `resultType` | forward pin obligated; reverse is F8 |
| Older `*.workflow.json` without diagnosticForks | AGWF037 import | covered by “distinct / absent stays clean” inside `agwf037-reject-not-dedup`; under-reach on import is `agwf035-json-import-unreached` (new code **not** applied), which is the opposite of reverse-compat |

---

## Claim-inventory leftovers that are not coverage misses

152 claims → 16 derivation slugs, then synthesized to 26 Active rows.
Not-promoted batches (out-of-wave Option B / #147 / #133 / #174 / #156.1 /
#156.3, prior #194 closes, CHANGELOG process, untracked edge docs) match
stage 0 “do not invent.” Unsupported derivation files
(`claim-agwf035-catalog-honest`, `claim-agwf035-emitter-dropped-edge`,
`claim-descriptorsource-docs-three-members`) became Active slugs
(`agwf035-catalog-polarity-lie`, `agwf035-underreach-ir-not-emission`,
`descriptor-source-docs-omit-member-2`). Open derivation files became
`renovate-resolve-unasserted` and `claim-issue-185-tracker`.

Open questions that sit **on** an Active slug (empty AGWF037 trigger names,
dual uniqueness authorities, four SDK-wrapped tools, `RequiresSoft`/`Link`
left current, `contracts-test` required-check) are incomplete proofs, not
missing surfaces. This lens does not promote them.

`claim-agwf-catalog-wire-identity` (31 codes, never renumber, name is wire
identity) did not become Active. Adjacent coverage is
`agwf037-catalog-identity` + `contracts-0-7-0-pack-incomplete`. Mechanism
says AGWF037 was **appended**. Treated as a thin spot next to F2, not a
separate missing S2 member.

---

## Passes

- Every stage-0 ranked member S1–S7 has at least one Active obligation.
- Changed-surface list items 1–11, 13–15 are cited by Active Scope rows;
  item 12’s code diffs are remarks plus the S4 merge/invariant work already
  bound.
- Rung mix uses 1–6. The “entirely 4/5” pattern is absent. Eleven of
  twenty-six sit at rung 3.
- S6 (Renovate) has one obligation and it is the risky one (resolve
  unobserved), not a restatement of the path-token suffix alone.
- S5 (`Requires` obsolete) covers both the attribute/body and the suite-wide
  `NoWarn` deletion.
- File-level deletions: none. The CS0618 suppress and the packaging
  retarget are obligated.
- New tests that are vacuous or wrong-subject were, with the exception of
  F7, already turned into Active keep-trues rather than counted as coverage.
- Config / pin / schema-diff-skip / INV-3-not-in-CI / PublicAPI-omits-Obsolete
  are present.
- Out-of-wave items were not invented. Diff-target stopping-rule check
  correctly does not apply.
- Cross-cutting seams that **are** in the set: AGWF035 vs AGWF037
  emit-or-gate (`agwf035-error-still-emits`), C# vs JSON import
  (`agwf035-json-import-unreached` vs `agwf037-reject-not-dedup`),
  Hosting pin vs CPM vs INV-3 (`mcp-resulttype-and-pin`), merge collapse vs
  unused enum (`handauthoredcontract-unreached`).

---

## Uncertainties

- Whether any out-of-repo TypeSpec/JSON ingest already stamps
  `HandAuthoredContract = 2`. If yes, F1’s “no producer” neighbor is
  in-repo only and the AONT201/203/204 unwidened `==` becomes the live
  reverse-compat path for **new** persisted `2`s, which is inside
  `handauthoredcontract-unreached` and not a new gap.
- Whether `contracts-v0.7.0` is published and whether `contracts-test` is
  a required check. F2’s operational path changes weight if the tag
  already exists.
- Whether Exarchos regenerates `AgwfCode` from each nupkg or pins a
  snapshot. Discriminates F2’s failure signal.
- Whether two empty trigger names on one JSON edge can still reach
  `Create` (`FindDuplicateTriggerNames` skips empty). Open on
  `agwf037-reject-not-dedup`; not settled as a missing slug.
- Whether AGWF035-without-gating is house style. If yes,
  `agwf035-error-still-emits` stays a policy split, not a hole in the set.
- Whether any production `PhaseGraph.Build` of a real consumer workflow
  already disagrees with `4d060f4` edge sets. F4 assumes the lift is
  identity; that was not re-proven in this pass beyond the mechanism
  survey’s body compare.
- `ErrorResult` `{ resultType: complete, isError: true }` protocol
  legality. Blocks how hard F8 should be read.
- No PR exists for `cursor/c801a047`. Tracker-close (`claim-issue-185-tracker`)
  has no merge-intent document in-repo; that is why it is rung 6, not a
  coverage miss of a scope member.
