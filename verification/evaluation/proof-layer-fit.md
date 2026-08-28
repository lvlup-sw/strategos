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

# Proof-Layer Fit — evaluation of `verification/ledger.md`

Lens 1 only. Fresh context: this file did not write the ledger. Findings describe
concerns; they are not categorized here.

Method: the five questions from `references/evaluation-lenses.md` §1, using
`references/proof-ladder.md` as the routing function. Inputs: the ledger, the
survey record, Stage 0, and obligation evidence files where the assigned rung,
the **Why not cheaper** column, or the named artifact needed a second look.

Rung names used below: 1 construction/generation, 2 compiler/types, 3
deterministic structural analysis, 4 contract/component tests, 5 production-path
integration, 6 human judgment.

---

## Ledger-wide

### L1. Why-not-cheaper rarely walks each cheaper rung

**Concern.** The ladder requires every obligation above rung 1 to say why *each*
cheaper rung is not enough, and to mark a reason as structural or situational.
Most ledger rows dismiss one familiar cheaper shape (usually “not a type” or
“a test of X cannot see Y”) and stop. That leaves the default gravity toward
rung 4/5 untested on the rungs that were skipped, and it hides situational
generation gaps that the guard discipline is supposed to record.

**Scope.** Ledger-wide. Worst on `contracts-changelog-contradicts-0-7-0`,
`descriptor-source-docs-omit-member-2`, `schema-diff-skip-succeeds`,
`renovate-resolve-unasserted`, `icons-null-when-unset`,
`agwf035-underreach-ir-not-emission`, `compat-agwf035-breaking`,
`contracts-0-7-0-pack-incomplete` (ledger column; the evidence file is better).

**Evidence.** Proof-ladder: “Every obligation above rung 1 must state why each
cheaper rung is not enough.” Ledger `contracts-changelog` Why-not-cheaper is
“Docs are not types.” Evidence `pad-contracts-changelog-contradicts-0-7-0.md`
already notes a generator could lock the lede to `ContractsVersion`; the ledger
column does not. Ledger `icons-null-when-unset` dismisses default-null (rung 2)
and never mentions an assignment-site scan (rung 3). Ledger
`renovate-resolve-unasserted` dismisses types and never mentions a remote-path
existence check (rung 3). No ledger row uses the words “structural” or
“situational.”

**Suggested action.** Rewrite each Why-not-cheaper so it names rungs 1…n−1,
labels each reason structural or situational, and drops authoring-cost language
if any remains (none of the form “a test is easier” was found; the failure mode
here is omission, not that phrase).

### L2. One rung on a bundled claim

**Concern.** Several Active rows join two or three claims that the ladder would
route to different rungs, then pick one rung for the bundle. The cheap half
looks established; the expensive half inherits that look. The skill’s “one
discrete choice” rule says an obligation that cannot choose a rung is not ready:
split it, or write the claim at the generality the single rung can carry.

**Scope.** Ledger-wide. Bundles:

| Slug | Joined claims | Rungs the parts want |
|---|---|---|
| `mcp-resulttype-and-pin` | Hosting pin; per-construction `ResultType`; INV-3 job; SDK wrap on four tools | 3, 3, 3, 5 |
| `icons-null-when-unset` | Null when unset; non-null path reachable from `AddOntologyTools` | 3, 3 or 4 (composition) |
| `handauthoredcontract-unreached` | Production assignment; merge preserves 2; `== HandAuthored` treats 2 as hand-side | 3, 3, 3 (same rung, three artifacts) |
| `requires-obsolete-observable` | `[Obsolete]` is visible; body still writes Preconditions; in-repo compile is not the consumer signal | 2, 4, 2 |
| `agwf037-reject-not-dedup` | Reject-not-dedup; join `hasErrors`; C# and JSON | 4, 3, 4 |
| `contracts-0-7-0-pack-incomplete` | Version 0.7.0; nupkg contains catalog + AGWF037 schema | 5 (pack), 5 (pack) — one rung, two named entries |
| `compat-validtransitions-nonreversing` | Signatures unchanged; successor sets vs `4d060f4`; revert does not roll back consumer trees | 4, 4, 6 |
| `agwf035-catalog-polarity-lie` | Under-reach text describes a missing dispatch **or** the catalog is rewritten | 4/6 (English), 1 (widen) |
| `renovate-resolve-unasserted` | Path token; bot resolves the preset | 3, 5/6 |

**Evidence.** Obligations.md: “If you cannot choose between two rungs, the
obligation is not ready for the ledger. Choose one … or split the obligation
in two.” Ledger `mcp-resulttype-and-pin` Claim names pin + `resultType` on
every constructed result + INV-3 deny-list; Proof artifact lists a csproj pin
assert, a per-construction assignment scan, *and* an INV-3 job; Scope also
names “Four tools rely on SDK wrap.” Those are not one claim.

**Suggested action.** Split each bundle so the assigned rung matches one claim.
Keep the cheap half at the cheap rung. Do not let a pin scan or a null-discovery
test stand in for wrap/reachability.

### L3. Ledger rung and evidence-file rung disagree

**Concern.** Canonical slugs in the ledger do not carry the same rung as the
evidence files they cite. A later reader who opens one file and not the other
will apply the wrong cheapest-sound test.

**Scope.** Ledger-wide, on the merged slugs.

| Slug | Ledger rung | Evidence rungs |
|---|---|---|
| `agwf035-underreach-ir-not-emission` | 3 | `pad-agwf035-underreach-is-ir-not-emission` 4; `agwf035-underreach-injected-graph` 4 |
| `phasegraph-type-not-instance` | 1 | `claim-phasegraph-no-drift` 1; `pad-phasegraph-type-not-instance` 3 |
| `agwf035-catalog-polarity-lie` | 1 | `pad-agwf035-message-lie` 3 |
| `mcp-resulttype-and-pin` | 3 | `pad-hosting-pin-and-resulttype` 4; `int-mcp-hosting-pin-vs-cpm` 3; `int-inv-3-checks-not-in-ci` 3 |
| `contracts-0-7-0-pack-incomplete` | 5 | `pad-contracts-0-7-0` 4 (version identity); `contracts-pack-omits-agwf037` 5 |

**Evidence.** The ledger header says “Canonical slugs below. Duplicate inventory
files remain on disk as contributing evidence.” The merge picked a rung without
recording the dissent or why the ledger won.

**Suggested action.** On each merged slug, state the winning rung and the losing
rung in the evidence file (or in Why-not-cheaper). Delete or retarget the
losing assignment so the disk does not carry two official rungs.

### L4. Named proof artifacts that are not proofs

**Concern.** The lens asks to flag a proof artifact that does not exist and
reads as though it does. Several rows name the *subject* (the current files,
the current tests) as the artifact, or name a check that the same row admits
is missing. That is a missing proof in the shape of a proof.

**Scope.** Ledger-wide. Degrees:

- Honest missing: `renovate-resolve-unasserted` (“None in this repo”).
- Honest-but-easy-to-misread: `agwf035-underreach-ir-not-emission` (“Closure of
  PhaseGraph rejoin edges ↔ emitted Start{target}. Current positives inject
  WithoutSuccessor.”) and `contracts-0-7-0-pack-incomplete` (“Named-entry
  asserts … Version assert already exists.”) — the desired artifact is written
  as if it were the current one, then contradicted in the next sentence.
- Subject-as-artifact: `contracts-changelog-contradicts-0-7-0` (“The three
  texts”), `descriptor-source-docs-omit-member-2` (“The two lists”),
  `claim-issue-185-tracker` (“Issue comment vs Residue subsection”). Those are
  what a human reads, not a check that fails when the claim is false later.
- Wrong-subject-as-artifact: `compat-validtransitions-nonreversing` (“Emitter
  tests”) — those tests do not establish non-reversal of consumer trees;
  `compat-agwf035-breaking` (“Fire/silent fixtures. Current tests inject
  WithoutSuccessor.”) — those fixtures do not establish which production
  shapes newly fail.

**Evidence.** Proof-ladder: “A cheaper rung that does not establish the claim
is not a cheaper proof. It is a missing proof with a reassuring shape.”
`PackagingTests.Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent`
(`PackagingTests.cs:107-137`) asserts version 0.7.0 and three unrelated schema
names; it does not name `agwf-catalog.json` or
`AgwfEntryDuplicatePermittedForkTrigger.json`. The ledger artifact line still
reads as a pack-content lock.

**Suggested action.** Separate “what would prove this” from “what exists today.”
If the artifact is missing, write “none — prescribed: …”. Do not put the
defective document in the Proof-artifact column as if it were the check.

### L5. Identity tests recheck generation

**Concern.** This wave’s catalog/schema/enum/markdown edits append
`"AGWF037"` to hand-authored `GroundTruthCodes` / `Expected` lists and then
read committed generated files. That is the move-down table’s first row: a
test that asserts two representations match, which belongs at generation.
`contracts-codegen-guard` and `AgwfCatalog_HandEdit_FailsGuard` already
regenerate-and-compare. Broad identity tests then recheck, more weakly, what
generation already (or should) guarantee.

**Scope.** Ledger-wide on the Contracts 0.7.0 identity surface; concentrated on
`agwf037-catalog-identity`. Adjacent: INV-3 3.4/3.5 file-level greps absorbed
into `mcp-resulttype-and-pin` (comment-satisfiable mention tests next to a
generation/assignment claim).

**Evidence.** `AgwfMarkdownTests.cs:18-27` hand-lists 31 codes including
`AGWF037`. `:58-60` builds `dataRows` as any line that `Contains` a ground-truth
code. `agwf037-catalog-identity-stale-or-mention.md` kill probe: delete the
extractor `ReportDiagnostic` site and all four catalog tests stay green; change
`.tsp` and leave committed JSON and the emitter test still passes.
`AgwfCatalog_HandEdit_FailsGuard` already regenerates. Proof-ladder: “A broad
test that does the job of a cheaper rung is a finding.”

**Suggested action.** Route catalog identity to rung 1 (generate the ground-truth
list from `AgwfCatalog.tsp`, or treat regen-and-diff as the one lock). Do not
count the four list-appends as the 0.7.0 identity proof.

---

## Obligation-specific

Each row below answers the five lens questions. “Sound” means the assigned rung
*can* establish the claim, not that the artifact already does.

### [agwf035-underreach-ir-not-emission] — rung 3

**Claim (ledger).** Under-reach fires when a rejoin last step does not dispatch
the declared terminal in the **shipped saga**, not only when a test-injected
`PhaseGraph` lacks an IR edge.

1. **Is the rung sound?** A closure that reads emitted `Start{target}` against
   `PhaseGraph` rejoin edges is rung 3 and *can* establish saga-vs-IR. The
   current positives cannot: they call `Report` with
   `PhaseGraph.Build(model).WithoutSuccessor(...)`
   (`TerminalReachabilityDiagnosticTests.cs:456-498`) while production omits
   `phaseGraph` (`WorkflowIncrementalGenerator.cs:1038-1044`). Rung 3 is the
   right *kind* of proof; the named artifact is not present.
2. **Cheaper?** This is a two-representation consistency claim (IR edge vs
   saga start command). Rung 1 owns that class: derive saga dispatch from the
   same `PhaseGraph` the table uses, or derive the graph from emitted handlers.
   Why-not-cheaper never considers a single derivation. It only says two
   `Build` calls agree today and a `Build(model)` test cannot see a forgotten
   handler. That is a reason against a *wrong* rung-4 test, not a reason
   against generation.
3. **Why-not-cheaper real?** Partially. “Types cannot express ‘this IR edge is
   in the saga’” is structural for rung 2. The rest is incomplete (skips rung 1)
   and talks about today’s tests rather than why a cheaper *sound* lock is
   closed.
4. **Broad test rechecking generation/compiler?** The current positives recheck
   the guard function and the `WithoutSuccessor` seam, not generation and not
   the compiler. They do not recheck a cheaper guarantee; they miss the
   production subject.
5. **Cheap rung standing in for behavior?** Yes, in the *current* suite: an IR
   mutation stands in for “emitter forgot `Start{Finally}` while the IR still
   has the construct” (the #184 class). The assigned rung 3 artifact, if built,
   would be the behavior check. The existing tests are the stand-in.

**Concern.** Assigned rung 3 is plausible for a saga-vs-IR closure, but a cheaper
rung-1 single derivation is not ruled out, and the only existing greens are a
cheap IR seam standing in for saga behavior.

**Suggested action.** Either move the claim to “IR under-reach only” and keep
rung 3/4 on production `Build(model)`, or keep the saga claim and add a real
emission closure (or unify derivation at rung 1). Do not leave `WithoutSuccessor`
positives as the proof of shipped-saga dispatch.

### [phasegraph-type-not-instance] — rung 1

1. **Sound?** Yes, for instance-share: one `Build` result passed into `Report`
   and `Emit` is construction. Today `TransitionsEmitter.cs:56` and
   `TerminalReachabilityGuard.cs:127` each call `Build`; the generator does not
   pass a graph. Type-share is already present and is not this rung.
2. **Cheaper?** Nothing is cheaper than 1. The fallback “or an edge-equality
   lock” is *more* expensive (rung 3), situational if instance-share is refused.
3. **Why-not-cheaper real?** The column answers “why isn’t today’s type-share
   enough?” which is the right question at rung 1. “Call-site scan ignores
   argument 6” is about an existing weak rung-3 test, not about a cheaper rung.
4. **Broad test rechecking generation?** The call-site scan
   (`Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline`) is a
   source-text check that does not read `phaseGraph`. It rechecks a mention of
   the guard, not instance-share.
5. **Cheap stand-in?** Type-share standing in for instance-share is exactly the
   “cheaper-looking shape that lets the failure through” case. The ledger
   assigns the right rung and then names a rung-3 fallback in the same cell.

**Concern.** Rung 1 is correct; the artifact cell mixes the cheap lock with a
rung-3 equality lock, so a reader can treat either as done.

**Suggested action.** Keep rung 1. Put edge-equality in Why-not-cheaper as the
situational fallback if instance-share is refused. Do not list both as the
artifact.

### [agwf035-catalog-polarity-lie] — rung 1

1. **Sound?** No, not for “an under-reach report describes a missing dispatch.”
   Rung 1 owns consistency of copies. Generation already copies
   `AgwfCatalog.tsp` into `WorkflowDiagnostics` / `agwf.md` and preserves the
   over-reach sentence (`WorkflowDiagnostics.cs:564`: “chains to” / “runs past
   its declared termination”) while `ReportUnderReach` passes `{0}` = terminal
   and `{2}` = last step. Widening the catalog is the *fix* that creates a new
   sentence; it does not prove that sentence is true of the fault. A later edit
   can write another inverted sentence and generation will copy it.
2. **Cheaper?** There is no cheaper rung. The assigned rung is *too cheap* for
   English polarity. The sound options are: rung 4 (assert the rendered
   under-reach message is the under-reach story), rung 6 (review the sentence),
   or a split catalog *id* so under-reach cannot reuse the over-reach member
   (rung 1/3 for *wiring*, still not for English).
3. **Why-not-cheaper real?** “Three string copies can match and still invert
   polarity. Substring tests cannot fail the lie.” That is a real reason *against
   treating identity tests as the proof*. It is not a reason that rung 1 *is*
   the proof. Evidence `pad-agwf035-message-lie.md` assigned rung 3 and said
   generation preserves a false sentence — the opposite of the ledger’s rung 1.
4. **Broad test rechecking generation?** Catalog identity tests lock the old
   sentence. They recheck generation of a lie.
5. **Cheap stand-in?** Yes. Construction/generation standing in for “the
   diagnostic text is true of the arm.”

**Concern.** Rung 1 cannot carry English polarity. The OR in the claim (“describes
a missing dispatch, *or* the catalog sentence is rewritten”) lets a catalog bump
count as proof of truth.

**Suggested action.** Split: (a) under-reach uses a catalog member whose
template is about missing dispatch (rung 1/3 wiring); (b) the rendered message
on an under-reach fixture states that story (rung 4) or a human accepts the
wording (rung 6). Do not assign (b) to generation.

### [agwf035-error-still-emits] — rung 3

1. **Sound?** Yes. `hasErrors` (`WorkflowIncrementalGenerator.cs:933-941`) is a
   hand-maintained list; AGWF037 is on it; AGWF035 is reported after the gate
   (`:1038-1045`) and the model stays non-null. Membership of that list is a
   structural fact.
2. **Cheaper?** Rung 2 cannot: `DiagnosticSeverity.Error` does not null the
   model. Rung 1 could generate `hasErrors` from a catalog “gates-emission”
   column (situational; no such column).
3. **Why-not-cheaper real?** Yes for rung 2. Situational rung 1 is unstated.
4. **Broad test rechecking generation/compiler?** No existing test is claimed as
   the proof. A future “Error plus generated files” test would be rung 4 and
   more expensive than the list check.
5. **Cheap stand-in?** No. The list *is* the emit-or-gate policy.

**Concern.** Minor: Why-not-cheaper should mark “no gates-emission catalog
column” as situational rung 1.

**Suggested action.** Keep rung 3. Add the situational note. If house style is
“Error still emits,” the claim changes and this row is no longer a missing
gate — that is identity of the claim, not the rung.

### [agwf035-json-import-unreached] — rung 3

1. **Sound?** Yes. Reachability of `TerminalReachabilityGuard.Report` from
   `EmitWorkflowSources` / `BridgeImportFile` is a call-graph property. One
   production `Report` site, on the C# transform only.
2. **Cheaper?** Missing call is representable (rung 2 closed). No generator
   weaves the guard onto every emit path (rung 1 situational).
3. **Why-not-cheaper real?** Yes. “C# `RunGenerator` does not close import” is
   the right dismissal of a cheaper-looking rung-4 suite.
4. **Broad test rechecking generation?** The existing call-site scan looks at
   an identifier and would stay green if import never calls `Report`. That is
   a weak rung-3 test, not a generation recheck.
5. **Cheap stand-in?** No. The hole *is* a missing call.

**Passes** this lens. Keep rung 3. Tighten the call-site scan so every
`EmitWorkflowSources` path is in the graph.

### [agwf035-all-complete-silent] — rung 4

1. **Sound?** Yes. Silence on one authored shape is module semantics.
2. **Cheaper?** No. Types cannot say “this branch is all-terminal.” A structural
   scan that is not the guard’s own IR walk cannot tell legitimate zero-dispatch
   from a dropped rejoin.
3. **Why-not-cheaper real?** Yes. Structural, not authoring-cost.
4. **Broad test rechecking generation/compiler?** The fixture calls `Report` on
   production `Build` (no `WithoutSuccessor`) *and* `RunGenerator` on the same
   source. That is the right subject, not a compiler recheck.
5. **Cheap stand-in?** No. Negative tests can be vacuous; this one drives a
   concrete all-Complete + Finally shape.

**Passes** this lens.

### [agwf035-overreach-preserved] — rung 4

1. **Sound?** Yes for “still fires on not-last / construct-owned successor.”
   Position and successor rules are not a type invariant.
2. **Cheaper?** A call-site scan (rung 3) can lock that the over-reach arm still
   exists; it cannot decide the two conditions. Why-not-cheaper in the evidence
   file states this; the ledger column is shorter (“Position/successor rules
   are not a type invariant”) and skips rung 1/3.
3. **Why-not-cheaper real?** Real for rung 2; incomplete for 1 and 3.
4. **Broad test rechecking generation/compiler?** `Diagnostic_ExistingCorpus_NeverFires`
   is a broad silence suite. It is a complement, not a substitute, for the
   existing over-reach fire fixtures. It does not recheck the compiler. Empty-
   classification counterfactuals are the wrong subject (ledger/evidence already
   say so).
5. **Cheap stand-in?** Corpus-never-fires standing in for “over-reach still
   holds” would be the stand-in; the row also names the existing fire fixtures.

**Concern.** Small: name the over-reach fire fixtures as the artifact; keep
corpus-never-fires as supporting silence, not as the proof.

**Suggested action.** Keep rung 4. Complete Why-not-cheaper for rungs 1 and 3.

### [agwf037-reject-not-dedup] — rung 4

1. **Sound?** Yes for reject-versus-dedup on named triggers. Policy
   (report AGWF037, drop the model, both surfaces) is composition, not a type.
   CS0152 is a different C# mechanism and does not exist on JSON import.
2. **Cheaper?** `hasErrors` membership for AGWF037 is already true in source
   (`WorkflowIncrementalGenerator.cs:930-938`) and is a rung-3 lock for the
   *gating* half. Structural analysis can see a uniqueness check and cannot
   prove it rejects rather than dedups (evidence `claim-agwf037-reject-not-dedup.md`).
   That split is L2: gating could drop to 3; reject-not-dedup stays at 4.
3. **Why-not-cheaper real?** Yes for the reject-not-dedup half. The ledger
   sentence “Reject-and-gate-emission is composition, not a type” is real and
   slightly over-assigns the gating half.
4. **Broad test rechecking generation/compiler?** The twins do not recheck
   CS0152; they correctly treat it as a different mechanism. Catalog identity
   tests are a different slug.
5. **Cheap stand-in?** Empty-name skip (`FindDuplicateTriggerNames`) is a hole
   in “each trigger at most once,” not a cheap proof of the named-trigger claim.

**Concern.** Gating is cheaper than the assigned rung; reject-not-dedup is not.

**Suggested action.** Keep rung 4 for reject-not-dedup. Lift `hasErrors`
membership to a sibling rung-3 obligation or a Why-not-cheaper note that gating
is already a list check.

### [contracts-0-7-0-pack-incomplete] — rung 5

1. **Sound?** Yes. The claim is what Exarchos extracts from the **nupkg**.
   Packaged-artifact membership is rung 5. `PackagingTests.cs:107-137` already
   packs and asserts version + three family representatives; it does not name
   the two AGWF037 paths. The assigned rung matches the *prescribed* artifact,
   not the current test.
2. **Cheaper?** A csproj `Content` read (rung 3) lists intended items and can
   miss a glob that fails at pack time — evidence
   `contracts-pack-omits-agwf037.md` says this; the ledger Why-not-cheaper only
   says “Source files ≠ packed files. Compiler does not see NuGet content.”
   Rung 4 without `dotnet pack` cannot see the archive. No cheaper rung is
   sound for pack membership.
3. **Why-not-cheaper real?** Real for rung 2; incomplete for 3 and 4 in the
   ledger column (complete in the evidence file).
4. **Broad test rechecking generation/compiler?** The current pack test
   rechecks version (already a project property) and three unrelated schemas.
   That is a broad pack test doing the job of a version pin, and *not* doing
   the job of the new catalog member. The version half is a cheaper claim
   (csproj `ContractsVersion` / `PackageVersion` at
   `Strategos.Contracts.csproj:37-40`) dressed as a pack test.
5. **Cheap stand-in?** Version-and-family-representative standing in for
   “0.7.0 contains AGWF037 artifacts.”

**Concern.** Rung 5 is right for nupkg content. The existing pack test is a
cheaper version lock plus a wrong-subject content lock. Why-not-cheaper in the
ledger skips rung 3.

**Suggested action.** Keep rung 5. Add named-entry asserts. Say why a csproj
item scan is not enough (glob vs pack). Treat the version assert as already
owned by the project property (rung 1-adjacent), not as this obligation’s proof.

### [contracts-changelog-contradicts-0-7-0] — rung 3

1. **Sound?** A check that the 2.11.0 lede, Residue, and packaged
   `Strategos.Contracts/CHANGELOG.md` agree with `ContractsVersion` and name
   AGWF037 *would* be rung 3. The named artifact is “The three texts” — the
   documents themselves. That is a human read (rung 6) of a current
   contradiction, not a structural lock that will fail the next time they drift.
2. **Cheaper?** Situational rung 1: generate the lede / package changelog line
   from `ContractsVersion`. Evidence already says “A generator could lock them
   to `ContractsVersion`; none does.” The ledger column omits that.
3. **Why-not-cheaper real?** “Docs are not types” is true and incomplete.
4. **Broad test rechecking generation?** No test exists. A future substring
   test on CHANGELOG would be the weak mention shape.
5. **Cheap stand-in?** The current files standing in for a check.

**Concern.** Assigned rung 3 is the right *kind* of lasting proof; the artifact
is a rung-6 reading. Cheaper generation is situational and unstated.

**Suggested action.** Either generate the version sentence (rung 1) or add a
check that the three texts contain 0.7.0 / AGWF037 (rung 3). Do not list the
texts as the proof.

### [schema-diff-skip-succeeds] — rung 3

1. **Sound?** Yes for the YAML shape: `have_prev=false` at
   `contracts-schema-diff.yml:41-55` and a compare step gated on
   `have_prev == 'true'` (`:57-62`) is a structural skip-as-success. The
   prescribed “self-test that a checkout with no `v*` tags fails the job” is
   rung 5 (CI execution), mixed into a rung-3 row.
2. **Cheaper?** Job conclusion is not a type. No generator emits this workflow
   from a fail-closed template (situational rung 1).
3. **Why-not-cheaper real?** “`JsonSchemaDiff` unit tests do not run when the
   `node` step is skipped” is a real reason against rung 4. Rungs 1 and 2 are
   unstated.
4. **Broad test rechecking generation?** The unit tests of the differ are the
   cheaper-looking suite that the Why-not-cheaper correctly rejects.
5. **Cheap stand-in?** `JsonSchemaDiff` tests standing in for “the job compared”
   — the ledger already refuses that stand-in.

**Concern.** Rung 3 is sound for the workflow graph. The self-test in the
artifact cell is a second, more expensive proof. The workflow file is unchanged
this wave; that is scope for another lens.

**Suggested action.** Keep rung 3 for `have_prev=false ⇒ non-success` and
`contracts-v*` matching. Move the no-tags CI self-test to a sibling rung-5
row if it is required, or mark it situational.

### [mcp-resulttype-and-pin] — rung 3

1. **Sound?** For the pin and for “each factory `new CallToolResult` assigns
   `ResultType`,” yes: those are a csproj graph and an assignment-site scan.
   For “every constructed `CallToolResult` emits `resultType: complete`”
   including the four SDK-wrapped tools, no. Those tools never assign
   `ResultType` in this repo (`int-mcp-hosting-pin-vs-cpm.md`: Explore / Query /
   Action / Validate return domain objects; wrap is SDK 2.2.0). A Hosting
   assignment scan does not observe wrap. Hosting tests also
   `VersionOverride="2.2.0"`, so a transport test can stay green after the
   production pin is removed.
2. **Cheaper?** Types require the property, not the assignment (rung 2 closed
   for “must assign”). File-level INV-3 grep is cheaper-looking and
   comment-satisfiable (`inv3-resulttype-icons-grep-substring.md`). Generation
   does not emit these constructions. Wrap-on-the-wire is *more* expensive
   (rung 5), not cheaper.
3. **Why-not-cheaper real?** Yes for the assignment half. It does not explain
   why rung 3 is enough for wrap, because it is not.
4. **Broad test rechecking generation/compiler?** INV-3 3.4 (`grep -L ResultType`
   on files that mention `CallToolResult`) rechecks a mention. Hosting pack
   tests assert a `ModelContextProtocol` *name*, not version 2.2.0.
   `AssertResultTypeComplete` / `ProviderBoundDispatchTests` recheck wrap under
   the *test* pin.
5. **Cheap stand-in?** Yes: pin + two factory assignments + a substring grep
   standing in for “every tool on the protected path emits `resultType`.”

**Concern.** Rung 3 is sound for pin and factory assignment and for “INV-3 is
not in `ci.yml`.” It is not sound for SDK-wrap emission. The bundle is L2.

**Suggested action.** Split: (a) Hosting `VersionOverride` locked, read from
the production csproj (rung 3); (b) every Hosting `CallToolResult` construction
assigns `ResultType` (rung 3, syntax-aware, not `grep -L`); (c) wrap path for
the four discovered tools observed on a composition that uses the production
pin (rung 5); (d) INV-3 3.4/3.5 registered fail-closed (rung 3 workflow graph).
Do not let (a)/(b) stand in for (c).

### [icons-null-when-unset] — rung 4

1. **Sound?** The discovery-null test is a rung-4 sample of one path. It does
   not establish “Discover never assigns a placeholder” for every future
   assignment site, and it does not establish “non-null `Icons` → `Tool.icons`
   is reachable from `AddOntologyTools`.” Evidence
   `int-mcp-icons-non-null-unreached.md` already assigned the reachability half
   to rung 3 (composition graph) and showed `CreateServerTools` takes only
   `OntologyGraph`, `Discover` never sets `Icons`, and the mapping test calls
   internal `CreateServerTool`.
2. **Cheaper?** Yes for null-when-unset: an assignment-site scan over `src/`
   (rung 3) is cheaper and stronger than one discovery assert. Why-not-cheaper
   dismisses default-null (rung 2) and skips rung 3. Evidence
   `pad-icons-null-when-unset.md` already ran `rg 'Icons\s*='` and found no
   production descriptor assignment — that scan *is* the cheaper proof.
3. **Why-not-cheaper real?** Real for rung 2; false as a complete walk (rung 3
   can carry the null-when-unset half).
4. **Broad test rechecking generation/compiler?**
   `CreateServerTools_PreservesOutputSchemaAndAnnotations` asserting null is
   close to rechecking the type default. INV-3 3.5 (`grep -L Icons`) rechecks
   a mention, not null-when-unset.
5. **Cheap stand-in?** Yes: discovery-null standing in for “no placeholder
   anywhere,” and the internal mapping test standing in for public-factory
   reachability.

**Concern.** Null-when-unset sits one rung too high. Non-null reachability is a
second claim that the current rung-4 artifact cannot carry.

**Suggested action.** Move null-when-unset to rung 3 (assignment-site scan;
forbid a placeholder literal). Split reachability to its own composition-graph
row (rung 3) or drop it from this slug. Do not treat the internal
`CreateServerTool` test as the public-root proof.

### [handauthoredcontract-unreached] — rung 3

1. **Sound?** Yes for “no production assignment,” “`MergeTwo` restamps
   `HandAuthored`,” and “unwidened `== HandAuthored` skips 2.” Those are
   assignment-site and comparison-site facts. Unused enum members compile
   (rung 2 closed).
2. **Cheaper?** No for reachability of a provenance value. Rung 1 would be a
   generated ingest that stamps 2; none exists (that is the hole).
3. **Why-not-cheaper real?** Yes. “Merge test asserts the collapse” correctly
   refuses to treat a rung-4 test of the *defect* as the proof of the *claim*.
4. **Broad test rechecking generation/compiler?** Ordinal tests that lock
   `= 2` (absorbed `claim-handauthoredcontract-additive`) recheck a compiler-
   visible enum member. They do not recheck assignment.
5. **Cheap stand-in?** The additive member compiling standing in for “a shipped
   authoring surface assigns 2” — the ledger refuses that stand-in.

**Passes** this lens on rung choice. The three sub-claims share rung 3 but need
three artifacts (assignment scan, `MergeTwo.cs:67`, `OntologyGraphBuilder`
`:330/:409/:566`). List them as three artifacts, not one “production
assignment-site scan.”

### [descriptor-source-docs-omit-member-2] — rung 3

Same shape as the changelog row. A grep/lock that the two edited lists name
all three `DescriptorSource` members would be rung 3. Artifact “The two lists”
is a human read. Situational rung 1 (generate the list from the enum) is
unstated. Why-not-cheaper “Docs are not types” is incomplete.

**Concern.** Rung 3 is the right kind of lasting proof; the artifact is the
subject. Cheaper generation from the enum is available in principle.

**Suggested action.** Generate the member list, or add a check that
`source.md` / `ontology-sources.md` name `HandAuthoredContract`. Do not list
the pages as the proof.

### [requires-obsolete-observable] — rung 2

1. **Sound?** For “`[Obsolete]` is the consumer signal,” yes: obsolete is a
   compiler feature. For “still writes Preconditions,” no: that is method-body
   semantics (`ActionBuilderOfT.cs:77-90`) and needs rung 4 or a structural
   read of the body, not “the violating program does not compile.” For “a
   clean in-repo test compile is not evidence consumers see CS0618,” the
   assigned artifact “Compile of a `NoWarn`-free subject that fails CS0618”
   is the right rung-2 proof and **does not exist**:
   `Directory.Build.targets:3-5` adds `CS0618` to `NoWarn` for every test and
   benchmark project.
2. **Cheaper?** Nothing cheaper for the warning. Rung 1 is not involved.
3. **Why-not-cheaper real?** “Obsolete is a compiler feature. This wave removed
   it from in-repo callers.” Real for why the rung is 2; it does not say why
   the body-stability half is not rung 4.
4. **Broad test rechecking generation/compiler?** In-repo tests that still call
   `Requires` under `NoWarn` recheck that the method *compiles*, which is the
   “still compiles” claim, and they **cannot** recheck CS0618. PublicAPI
   RS0016/RS0017 recheck add/remove, not Obsolete (`compat-publicapi-omits-obsolete`).
5. **Cheap stand-in?** `[Obsolete]` plus a green in-repo suite standing in for
   “consumers see CS0618.” The ledger *states* that stand-in is invalid, then
   names a NoWarn-free compile that is not in the tree.

**Concern.** Rung 2 is correct for the attribute. The body-stability half is
the wrong rung. The consumer-visible CS0618 artifact is prescribed and missing.
In-repo suppression makes the cheap compiler rung unable to speak in this
repository.

**Suggested action.** Split: attribute presence (rung 2, already true);
Preconditions lowering (rung 4, existing reflection/lowering tests if they
still assert the body); consumer-visible CS0618 (rung 2 on a NoWarn-free
subject — add that subject, or record the proof as missing). Do not treat the
test-suite compile as the consumer signal.

### [renovate-resolve-unasserted] — rung 5

1. **Sound?** “Renovate resolves the organisation’s dotnet preset” is an
   external-process claim. Rung 5 is the right *kind* of proof. The artifact is
   “None in this repo.” Even a local integration test cannot run the GitHub
   App. The cheapest *sound* proof in practice may be rung 6 (a recorded
   Renovate dry-run / logs) or an out-of-repo observation. Assigning rung 5
   with no artifact is honest about the hole and still reads as if a
   production-path test is the planned lock.
2. **Cheaper?** The path-token suffix
   `tools/renovate-config/presets/dotnet.json` is a cheaper claim and the
   ledger says it holds. A rung-3 check that the remote file exists at that
   path on `lvlup-claude` (or the renamed repo) is cheaper than “the bot
   resolved” and is not the same claim. Why-not-cheaper only dismisses types.
3. **Why-not-cheaper real?** Incomplete. “Types cannot resolve a GitHub
   `local>` preset” is true and skips rungs 3 and 4.
4. **Broad test rechecking generation/compiler?** No test exists.
5. **Cheap stand-in?** Path-token edit standing in for “resolves” — the ledger
   names this and still parks both claims on one rung-5 row (supported-claims
   section absorbs `claim-renovate-path-token` into this slug).

**Concern.** Rung 5 is theoretically right and practically unobservable in-repo.
The cheaper path-token claim is established and should not live at rung 5.

**Suggested action.** Split: path token (rung 3, holds). Resolve (rung 5 or 6,
artifact none — record indeterminate, or require a recorded bot run). Do not
let the path edit inherit a production-path rung.

### [aont205-analyzer-unreached] — rung 3

1. **Sound?** Yes. A `DiagnosticDescriptor` with no `Diagnostic.Create` site is
   a call-graph fact. Runtime AONT205 is a different root (evidence correctly
   separates them).
2. **Cheaper?** Unused `static readonly` field compiles (rung 2 closed). No
   generator ties descriptors to report sites (situational rung 1).
3. **Why-not-cheaper real?** Yes.
4. **Broad test rechecking generation/compiler?** Runtime `AONT205Tests` prove
   the builder exception, not the analyzer. The ledger does not treat them as
   this proof.
5. **Cheap stand-in?** No. The descriptor compiling is refused as the proof.

**Passes** this lens.

### [compat-agwf035-breaking] — rung 4

1. **Sound?** “Which authored shapes newly fail AGWF035” is semantic and wants
   rung 4. The current fire fixtures inject `WithoutSuccessor`, so they do not
   establish which *production* shapes newly fail (open question on the row).
   “This arm is new code on an existing error id” is cheaper: a structural
   diff of the guard versus `4d060f4` (rung 3).
2. **Cheaper?** Call-site scan cannot prove which shapes fail — real for that
   half. A new-arm-exists check is cheaper for the compatibility *classification*.
3. **Why-not-cheaper real?** Real for “which shapes”; incomplete for “this is
   a breaking diagnostic” as a code-existence fact.
4. **Broad test rechecking generation/compiler?** Injected-graph fires recheck
   the seam, not the compiler, and not the production graph.
5. **Cheap stand-in?** Injected-graph fixtures standing in for “consumer
   workflows that compiled on `4d060f4` now fail.”

**Concern.** Rung 4 is right for production shapes and the artifact is the
wrong subject (same seam as `agwf035-underreach-ir-not-emission`). Breaking-
ness-as-new-arm is cheaper.

**Suggested action.** Split or narrow: new arm on an existing id (rung 3, diff
vs merge-base). Production shapes that fail (rung 4 on `Build(model)`, not
`WithoutSuccessor`). Do not use the seam tests as the compatibility proof.

### [compat-validtransitions-nonreversing] — rung 4

1. **Sound?** No, not for the ledger claim. “A generator revert is not a revert
   of already-emitted consumer `ValidTransitions` tables” is a fact about
   generated source in other repositories. Emitter tests in *this* repo
   (`TransitionGraphLoweringTests`, `TransitionsEmitterUnitTests`) establish
   current signatures and some successor sets. They cannot establish
   non-reversal of consumer trees. That claim is rung 6 (or not an in-repo
   proof). Successor-set equality versus the pre-lift nested type at
   `4d060f4` is a different claim; Why-not-cheaper discusses that claim
   instead of the ledger claim.
2. **Cheaper?** Signatures-unchanged can be a structural compare of emitted
   member lists (rung 3) or is already implied by an unchanged emitter API.
   Non-reversal has no cheaper in-repo rung that is sound.
3. **Why-not-cheaper real?** It answers a different question (“did the lift
   preserve sets?”) than the Claim column.
4. **Broad test rechecking generation/compiler?** Emitter tests recheck that
   generation still emits a dictionary of the same shape — a generation
   property — and are then asked to prove a compatibility non-reversal they
   cannot see.
5. **Cheap stand-in?** Yes: in-repo emitter tests standing in for
   “consumer tables do not roll back.”

**Concern.** Assigned rung 4 and the named tests cannot carry the claim as
written. Why-not-cheaper is about a third claim (set preservation vs base).

**Suggested action.** Rewrite the claim to what emitter tests can prove
(signatures / successor sets at this revision), or move non-reversal to rung 6
as a documented property of generated consumer source. Add a vs-`4d060f4`
equality lock if set preservation is the real obligation.

### [compat-publicapi-omits-obsolete] — rung 3

1. **Sound?** Yes. PublicAPI files are not generated from attributes.
   RS0016/RS0017 have no Obsolete column. That is a metadata/graph fact.
2. **Cheaper?** Situational rung 1: generate Unshipped from the public surface
   including attributes. Why-not-cheaper says the files are not generated —
   that *is* the situational reason, not labeled as such.
3. **Why-not-cheaper real?** Yes, incomplete labeling only.
4. **Broad test rechecking generation/compiler?** RS0016 on add/remove is the
   cheap analyzer that cannot see this change — correctly refused as the proof
   that `Requires` is obsolete.
5. **Cheap stand-in?** Unshipped diffs standing in for “Obsolete is tracked” —
   the ledger’s point is that they cannot.

**Passes** this lens. Label Why-not-cheaper as situational for rung 1.

### [diagnostic-fork-ctor-open] — rung 2

1. **Sound?** Yes. Hide the primary constructor; `Create` is the only writer.
   Invalid states (`MaxForks < 1`, duplicate/empty triggers, empty anchors)
   remain representable today (`DiagnosticForkModel` public-to-assembly
   primary constructor).
2. **Cheaper?** No. A throw in `Create` is one factory, not a type.
3. **Why-not-cheaper real?** Textbook structural reason.
4. **Broad test rechecking generation/compiler?**
   `Create_WithDuplicateTrigger_ThrowsArgumentException` rechecks the factory
   and does not constrain `new DiagnosticForkModel(...)`.
5. **Cheap stand-in?** The factory throw standing in for an unrepresentable
   record — the ledger refuses that stand-in.

**Passes** this lens.

### [traversal-result-flags-independent] — rung 2

1. **Sound?** Yes. A discriminated `Success | Error` makes
   `IsError: false` + `Error` present unrepresentable.
   `TraversalResult` is independent `init` properties today.
2. **Cheaper?** No. The named `{ passed, error }` shape is the problem.
3. **Why-not-cheaper real?** Yes.
4. **Broad test rechecking generation/compiler?** Factory tests that go through
   `OntologyTraverseTool.Error` never construct the mixed state.
5. **Cheap stand-in?** `ResultType = complete` on both factory arms standing in
   for a closed core result — evidence already separates that pair as
   protocol-legal and not this obligation.

**Passes** this lens.

### [agwf037-catalog-identity] — rung 3

1. **Sound?** Regenerate-then-compare is a sound *check* that two artifacts
   match. The ladder’s owner for “repeated representations of one thing” is
   rung 1. The four tests this wave edited do not regenerate; they append to
   hand-authored lists and read committed files
   (`AgwfCatalogEmitterTests`, `AgwfMarkdownTests.cs:58-60` `Contains`).
2. **Cheaper?** Yes. Generate `GroundTruthCodes` from `AgwfCatalog.tsp`, or
   treat the existing `AgwfCatalog_HandEdit_FailsGuard` /
   `contracts-codegen-guard.yml` regen-and-diff as the one lock. Why-not-cheaper
   “Lists are hand-authored” is situational (the cheaper rung exists in
   principle and is already used by a sibling test this wave did not extend).
3. **Why-not-cheaper real?** Situational, written as if structural. Authoring
   cost of generating the list is the smallest term.
4. **Broad test rechecking generation/compiler?** This is the textbook case.
   Four identity tests recheck, weakly, what codegen-guard already does when
   it runs. Markdown `Contains` accepts a mention. Unwiring the diagnostic
   leaves them green.
5. **Cheap stand-in?** Hand-authored list membership standing in for “freshly
   compiled catalog contains the id” and for “the generator still reports
   AGWF037.”

**Concern.** Assigned rung 3 is one step too expensive and the current tests
are one step too weak. The claim belongs at generation.

**Suggested action.** Move to rung 1 (derive the ground-truth list, or make
regen-and-compare the sole identity proof). Parse markdown table id cells if
a doc lock is still wanted. Do not treat the four list-appends as the proof.

### [claim-clr-free-xor-docs] — rung 6

1. **Sound?** Yes. The obligation is that guide pages *state* a limit the types
   already enforce. Page accuracy is human judgment. The type limit itself is
   pre-existing rung 2 and is not this wave’s new mechanism (evidence says so).
2. **Cheaper?** Pages are not generated from the type system. A substring test
   that the quote appears can pass on a comment — Why-not-cheaper correctly
   refuses that cheaper-looking test.
3. **Why-not-cheaper real?** Yes. Structural for “not generated”; situational
   if someone later generates the guide from the rationale corpus.
4. **Broad test rechecking generation/compiler?** `RationaleCorpusParityTests`
   proves the bound, not that the new prose is accurate. The ledger does not
   treat that test as this proof.
5. **Cheap stand-in?** No. The row does not let the type system stand in for
   the docs, or the docs stand in for the type system.

**Passes** this lens.

### [claim-issue-185-tracker] — rung 6

1. **Sound?** If the claim is “tracker comment and Residue must agree,” a human
   comparison is the only sound rung. Evidence
   `claim-issue-185-tracker-close.md` ends: “This file is not an obligation.”
   The ledger still assigns a rung and an artifact. A proof rung on a
   non-obligation is decoration.
2. **Cheaper?** Tracker state is not a type. No generator updates GitHub issue
   comments.
3. **Why-not-cheaper real?** “Tracker state is not a type” is true and
   incomplete (skips 3–5: no CI binds merge to issue state).
4. **Broad test rechecking generation/compiler?** No.
5. **Cheap stand-in?** CHANGELOG Residue standing in for issue-tracker state —
   the row’s point is that they disagree.

**Concern.** Rung 6 is correct *if* this stays an obligation. The cited evidence
file withdraws the obligation. That is an identity problem that makes the rung
assignment moot until synthesis decides whether the row is real.

**Suggested action.** Decide whether this is an obligation. If yes, keep rung 6
and treat the artifact as a human checklist (update the comment after merge;
do not title the PR “Close”). If no, remove the rung rather than leave a
human-judgment proof on a withdrawn claim.

---

## Passes

What this lens read and found correctly routed, in brief.

- **Rung distribution is not stuck at 4 and 5.** The Active set uses all six
  rungs (1×2, 2×3, 3×11, 4×6, 5×2, 6×2). Inventory considered cheap rungs.
  That is the opposite of the evaluation-lens warning that a ledger sitting
  entirely at 4/5 means nobody considered construction, types, or structure.
- **`agwf035-error-still-emits`, `agwf035-json-import-unreached`,
  `aont205-analyzer-unreached`.** Missing-call / missing-membership / unused
  descriptor are rung-3 graph properties. Why-not-cheaper correctly refuses
  “Error severity,” “C# `RunGenerator`,” and “the field compiles.”
- **`agwf035-all-complete-silent`.** Silence on one authored shape is rung 4.
  The fixture uses production `Build` and the generator. Why-not-cheaper is
  structural.
- **`agwf037-reject-not-dedup` (reject half).** Reject-versus-dedup is
  composition. CS0152 is correctly treated as a different mechanism.
- **`contracts-0-7-0-pack-incomplete` (rung choice).** Nupkg membership is rung 5.
  Evidence file explains why a csproj scan is not enough.
- **`handauthoredcontract-unreached` (rung choice).** Unused enum members
  compile; reachability is a graph property.
- **`compat-publicapi-omits-obsolete`.** Analyzer scope is a structural fact.
  RS0016 is not asked to prove Obsolete.
- **`diagnostic-fork-ctor-open`, `traversal-result-flags-independent`.**
  Invalid states belong at the type. Factory throws and factory tests are
  correctly refused as the cheap stand-in.
- **`claim-clr-free-xor-docs`.** Doc accuracy is rung 6. Substring tests and
  the pre-existing type limit are not used as substitutes for each other.
- **No Why-not-cheaper says “a test is easier.”** The authoring-cost anti-pattern
  the ladder names does not appear as a phrase. The failures are incomplete
  walks and wrong-rung assignments, not that sentence.

---

## Uncertainties

- **Is AGWF035-without-gating house style?** If Error-still-emits is
  intentional, `agwf035-error-still-emits` is a documentation/policy claim
  (possibly rung 6) rather than a missing `hasErrors` lock. The rung stays 3
  only while the claim is “fail-closed Errors share one emit-or-gate policy.”
  Not settled; listed run-wide in the ledger.
- **Does `contracts-codegen-guard` plus `AgwfCatalog_HandEdit_FailsGuard`
  already make the four AGWF037 list-appends redundant?** If both jobs are
  required on this change, `agwf037-catalog-identity` is a weaker duplicate of
  an existing rung-1/3 lock, not a missing one. Survey left `contracts-test`
  required-check open. Same stake.
- **Can a generator-driven under-reach fixture exist without `WithoutSuccessor`?**
  If yes, `agwf035-underreach-ir-not-emission` and `compat-agwf035-breaking`
  collapse to “use that fixture” at rung 4 and the saga-closure / rung-1
  unification questions change shape. Open on both evidence files.
- **Is instance-share of `PhaseGraph` required, or is `Build` purity accepted?**
  If purity is the accepted T1 deliverable, `phasegraph-type-not-instance`
  becomes an equality-lock (rung 3) or a withdrawn instance-share claim.
  Ledger open questions say none on instance vs type; Stage 0 / CHANGELOG
  still say “share one PhaseGraph so they cannot drift.”
- **Can Renovate resolve be observed in-repo?** If not, rung 5 is a
  non-existent proof and the obligation is indeterminate until a human records
  a bot run (rung 6). The `lvlup-claude` → `exarchos` rename is still an open
  question.
- **Does Exarchos require the AGWF037 *entry schema* file, or only
  `agwf-catalog.json`?** That changes which named pack asserts are load-bearing
  (`contracts-pack-omits-agwf037.md` open question). It does not change the
  rung (still 5 for nupkg content).
- **Is `claim-issue-185-tracker` an obligation?** Evidence file says no. Ledger
  assigns rung 6. This lens cannot settle identity; it can only say the rung
  is moot until that is decided.
- **Out-of-repo producers of `HandAuthoredContract = 2`.** If one exists, the
  assignment-scan half of `handauthoredcontract-unreached` narrows and the
  merge-collapse / `== HandAuthored` halves remain. Does not change the rung.
- **This lens did not re-read every contributing inventory file for slugs
  whose ledger row, Why-not-cheaper, and cited pad/claim files already
  agreed.** Those rows are in Passes. A contributing file that disagrees with
  both the ledger and the pad file would have been missed.
