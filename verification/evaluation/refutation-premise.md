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

# Refutation — premise origin

Angle: **PREMISE ORIGIN**. Kill an obligation when its “must keep true”
sentence came from a claim nobody validated, duplicates another active
slug, or rests on a misreading of the code. Default to refuted when
uncertain. A real obligation with poor wording is a **refinement**
(survived the kill; wording must change), not a refutation.

Read: `validating-claims.md`. Claimed guarantees become obligations.
Unsupported or open claims become findings / open questions — they do
not re-enter the active ledger as a stronger “must.” Descriptions that
state intent while the code implements something narrower do not
promote the overstated half.

Revision `324768f` vs `4d060f4`. Code was re-read; inventory evidence
files were used as leads, not as facts.

## Ledger-wide

Inventory promoted several **unsupported** or **open** claim-derivation
files into active slugs (`claim-agwf035-emitter-dropped-edge`,
`claim-renovate-resolves-preset`, `claim-issue-185-tracker-close`).
`validating-claims.md` forbids that promotion. Representable-invalid
and integration lenses also invented uniformity rules the target never
stated (every Error gates emission; every `DiagnosticDescriptor`
reports; every authoring front runs AGWF035; PublicAPI tracks
`[Obsolete]`). Those invented rules are the premises that die below.

## Verdicts

### [agwf035-underreach-ir-not-emission] — survived (refinement)

The Claim field demands a **shipped-saga** dispatch lock. That sentence
is claims 12 / 84 (plan T1 / issue 185 “#184 compile-time decidable”),
already marked **unsupported** in
`claim-agwf035-emitter-dropped-edge.md`. The code’s own remarks define
a different subject: `PhaseGraph.WithoutSuccessor` “simulate[s] the next
dropped rejoin edge without forking the builder”
(`PhaseGraph.cs:116-118`); `ReportUnderReach` compares
`EnumerateRejoinDispatchersOf` to `graph.SuccessorsOf`
(`TerminalReachabilityGuard.cs:150-164`); production rebuilds
`PhaseGraph.Build(model)` (`:127`) because the generator call
(`WorkflowIncrementalGenerator.cs:1038-1043`) does not pass a graph.

“Dropped edge” in the plan is the IR/graph edge the test seam already
names, not a missing `Start{Finally}` in saga emission. Promoting the
unsupported #184 / Postgres / saga-emission half as the Claim is a
misread.

**Keep:** under-reach must fire when a rejoin last step is absent from
`PhaseGraph.SuccessorsOf` on a production `Build(model)` (the narrower
`claim-agwf035-route-underreach`). **Drop** the saga-emission / #184
wording. That is wording, not a fake obligation.

### [phasegraph-type-not-instance] — survived (refinement)

CHANGELOG “share one `PhaseGraph` so they cannot drift”
(`CHANGELOG.md:176-177`) was read as **instance** identity. The type
remarks say the consumers share **`Build`** (`PhaseGraph.cs:16-17`,
`:116-118`). The optional `phaseGraph` parameter is documented as a
test seam (`TerminalReachabilityGuard.cs:56-58`), so the generator
omitting argument 6 is intentional, not a missed wiring.

`Build` is a pure function of the model (ledger Assumptions). Two calls
on the same `WorkflowModel` cannot drift unless someone later forks the
algorithm. “Cannot drift” is a real claimed guarantee. “One instance”
is a misread of “one `PhaseGraph`.”

**Keep:** no-drift via a single derivation (`Build`). **Drop:** object
identity as the lock.

### [agwf035-catalog-polarity-lie] — survived

Plan T1: keep the existing template unless the sentence becomes a lie
when `{2}` is the missing dispatcher. Under-reach passes
`declaredTerminalStepName` as `{0}` and `lastStep` as `{2}`
(`TerminalReachabilityGuard.cs:139-140`, `:157-163`). The shipped
`messageFormat` still says `Step '{0}' … chains to '{2}'` and “the saga
runs past its declared termination”
(`WorkflowDiagnostics.cs:564`). Remarks at `:550-553` already document
the inverted argument meaning. The two sentences cannot both be true
of under-reach. Premise is a stated plan constraint; the contradiction
is in source.

### [agwf035-error-still-emits] — refuted

Premise: “AGWF errors sold as fail-closed share one emit-or-gate
policy.” Nobody sold that policy. The run-wide open question already
asks whether emit-anyway is house style.

When this generator means “no saga,” it says so and implements it:
`PathEndTypeCollision` remarks “An error that blocks generation”
(`WorkflowDiagnostics.cs:583`) and that id is in `hasErrors`; AGWF037
remarks “Reject the whole workflow (no saga)” (`:606`) and
`hasDuplicatePermittedForkTrigger` joins `hasErrors`
(`WorkflowIncrementalGenerator.cs:930-941`). AGWF035 remarks say “a
workflow that cannot reach its termination does not run” (`:556-558`)
— the authored machine, matching the diagnostic title, not
`hasErrors`. Resilience Errors in the same method are explicitly
“do not gate code generation” (`:920-926`). Report is after the gate
(`:1038-1045`) on purpose, same as those advisories.

The “must not pair with a generated saga” sentence converts an
unvalidated house-style question into a must. Refuted.

### [agwf035-json-import-unreached] — refuted

Premise: “Every authoring front that emits `ValidTransitions` also
runs `Report`.” CHANGELOG Residue scopes AGWF035 to the C# guard arm
(`CHANGELOG.md:172-177`). The AGWF037 paragraph, by contrast, names
both surfaces: “The same gate runs on C# `AllowDiagnosticFork` and on
JSON import” (`:181-182`). Over-reach was already C#-only; this wave
added under-reach at the existing C# call site
(`WorkflowIncrementalGenerator.cs:1038`). `rg TerminalReachabilityGuard`
in `src/` hits that site, the guard type, and tests — not
`BridgeImportFile`.

The uniformity rule was invented from AGWF037’s *explicit* dual-path
claim. No inventory claim said import would grow AGWF035. Refuted.

### [agwf035-all-complete-silent] — survived

CHANGELOG: stays silent when every exclusive path already
`Complete()`s alongside `Finally<T>` (`CHANGELOG.md:175-176`).
`AddBranchRejoinDispatchers` skips `IsTerminal` cases
(`TerminalReachabilityGuard.cs:254-256`); `CollectConstructDispatchers`
keeps the branch predecessor out of the linear scan (`:366-371`,
`:375-409`).
`Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` drives both
direct `Report` (production `Build`) and `RunGenerator`. Claimed
guarantee; mechanism and fixture exhibit it.

### [agwf035-overreach-preserved] — survived

This wave extends an existing error id. Spec DR-3 / CHANGELOG
(`:172-174`) still require the over-reach arm (terminal not last, or
construct-owned successor). That arm is still the first half of
`Report` (`TerminalReachabilityGuard.cs:85-117`). A real regression
obligation for a claimed already-shipped half. Not a duplicate of
under-reach.

### [agwf037-reject-not-dedup] — survived

CHANGELOG Residue states reject-not-dedup on C# and JSON, fail-closed
before CS0152 (`CHANGELOG.md:179-182`). Extractor reports and
`return false`; `hasDuplicatePermittedForkTrigger` joins `hasErrors`;
`WireToModelBridge` runs the same scan. Tests exist on all three
fronts. Claimed guarantee; code exhibits it.

### [contracts-0-7-0-pack-incomplete] — survived

CHANGELOG and the pack-test comment say 0.7.0 adds the
duplicate-permitted-fork-trigger id
(`PackagingTests.cs:72-80`, `:84`). The test this wave retargeted
asserts nupkg / nuspec `0.7.0` and three unrelated family
representatives (`SdlcEventEnvelope`, `WorkflowDefinitionV1`,
`InvariantEntry` at `:126-137`). It does not name
`AgwfEntryDuplicatePermittedForkTrigger.json` or
`diagnostics/agwf-catalog.json`. Deleting those Content items leaves
the test green. The comment is the claim; the asserts are the
discriminating miss. Not a duplicate of codegen-guard (that job
locks the *source tree*, not the nupkg Exarchos extracts).

### [contracts-changelog-contradicts-0-7-0] — refuted

Premise: the 2.11.0 **lede** must restate 0.6.0→0.7.0. The lede
(`CHANGELOG.md:17`) is the original 2.11.0 story (0.4.0→0.6.0,
AGWF035 then AGWF036). Residue (`:182`) is this wave’s addendum and
already states 0.6.0→0.7.0. That is sequential structure, not a
contradiction. Claim 60 was already **not-promoted** as “CHANGELOG
weave process, not product.”

The packaged `Strategos.Contracts/CHANGELOG.md` Unreleased still
names AGWF036 / 0.5.0→0.6.0 and omits AGWF037. Plan T2 required
catalog regen, not a package-CHANGELOG rewrite. Nobody validated the
package CHANGELOG as a version authority. Docs hygiene is not this
slug’s “lede contradicts” premise. Refuted.

### [schema-diff-skip-succeeds] — refuted

The workflow file is **unchanged** on `4d060f4...324768f`. It is
designed for product `v*` tags (`contracts-schema-diff.yml:9`, `:42`),
and this clone’s `v2.10.0` contains `src/Strategos.Contracts/schemas`.
The second clause (“must match `contracts-v*` not `v*`”) is an
unvalidated rewrite of a job that names “previous release tag.”
`contracts-v*` exists only through `contracts-v0.4.0`; there is no
`contracts-v0.6.0` to be the intended baseline.

This wave’s schema add is additive. Skip-success and compare-success
are the same merge conclusion for an additive-only change. Whether
the job is a required check is open. Surrounding-gate skip-success
is a real CI shape and not a validated obligation of *this* residue
wave. Refuted.

### [mcp-resulttype-and-pin] — survived

CHANGELOG: Hosting pins 2.2.0 so every constructed `CallToolResult`
can set the 2026-07-28 complete discriminator
(`CHANGELOG.md:187-189`). Hosting `VersionOverride` 2.2.0 is in the
csproj; `MapTraversalResult` / `ErrorResult` assign `ResultType`.
INV-3’s “deny the pre-2026-07-28 shape” is a claimed control this
wave extended. The wrap/CI holes are proof-layer gaps on a real
claim, not a fake premise. Do not split them into a refutation.

### [icons-null-when-unset] — survived (refinement)

CHANGELOG claims only “`OntologyToolDescriptor.Icons` stays null when
unset” (`CHANGELOG.md:189`). `Discover` never assigns; `ApplyIcons`
returns on null; factory tests assert both sides null. That half is
real and exhibited.

The second Claim sentence — non-null `Icons` reachable from
`AddOntologyTools` if a consumer supplies icons — is **not** in
CHANGELOG, INV-3, or the plan. `CreateServerTools` takes only
`OntologyGraph` and always `Discover()`s. Integration-completeness
invented a producer path. **Drop** sentence 2; **keep**
null-when-unset.

### [handauthoredcontract-unreached] — survived (refinement)

CHANGELOG: “AONT205 retargets to mechanical ingestion, so TypeSpec /
JSON contract-authored actions survive graph merge”
(`CHANGELOG.md:192-194`). In-repo, `HandAuthoredContract =` appears
only on the enum; `IngestedIntentInvariant` skips unless
`Source == Ingested` (`IngestedIntentInvariant.cs:22-24`);
`MergeTwo.Merge` writes `Source = HandAuthored` (`MergeTwo.cs:67`)
while taking `Actions = hand.Actions` (`:78`).

“Survive graph merge” was read as **Source identity 2 persists**.
The discriminating line is `:78`: actions *payload* is what merge
keeps. Collapse to `HandAuthored` is the documented lattice
(“hand wins”). That half is a misread.

**Keep:** no shipped producer stamps `2`, so ingest that still
stamps `Ingested` still fails AONT205 — the class the “so” clause
said this member closes. **Drop:** “must survive merge as ordinal 2”
and the AONT201/203/204 `== HandAuthored` widening as if they were
the same claim.

### [descriptor-source-docs-omit-member-2] — survived

Claim 35 / plan T5: document which authoring surface maps to which
`DescriptorSource` value. Pages this wave’s cluster still publishes
list two members (`source.md:65-66`: `HandAuthored`, `Ingested` only).
`DescriptorSource.cs` remarks name all three; the pages do not.
Failed claimed docs delivery. Not a duplicate of
`handauthoredcontract-unreached` (enum reachability ≠ provenance
list). The two-member list is the discriminating exhibit.

### [requires-obsolete-observable] — survived

CHANGELOG: `Requires` is obsolete, stays so existing `Object<T>`
authoring still compiles (`CHANGELOG.md:196-197`). Interface and
impl carry `[Obsolete]`; the body still appends Preconditions.
`Directory.Build.targets:3-5` adds `CS0618` to `NoWarn` for every
test/benchmark project, so a green in-repo compile does not exhibit
consumer CS0618. Claimed guarantee plus a real false-green of the
observability proof. Not invented.

### [renovate-resolve-unasserted] — refuted

CHANGELOG “Renovate resolves the organisation’s dotnet preset”
(`CHANGELOG.md:184`) is rhetoric for a path-token edit. Competing
explanation in `pad-renovate-resolve-unasserted.md`: the defect was
the 404 path; pointing `extends` at
`tools/renovate-config/presets/dotnet.json` *is* the fix.
`claim-renovate-resolves-preset.md` is **open** (no in-repo exhibit).
`validating-claims.md`: an unsettled claim stays an open question
and does not become an obligation premise. The path-suffix claim
already holds and was absorbed. Promoting the open resolve question
is the forbidden promotion. Refuted.

### [aont205-analyzer-unreached] — refuted

Premise: “a shipped analyzer `DiagnosticDescriptor` is reported, or
it is not a compile-time control.” This wave’s AONT205 work is
**runtime**: `git diff 4d060f4..324768f` on the analyzer /
`OntologyDiagnostics` / `OntologyDiagnosticIds` is empty; the only
hit is new `IngestedIntentInvariant.cs`. CHANGELOG claims the
runtime retarget, not a Roslyn report site. The unused descriptor
is pre-existing. Package text still says `AONT001-AONT035`.
Compile-time AONT205 was never a claimed delivery of this residue.
The uniformity rule is invented. Refuted.

### [compat-agwf035-breaking] — refuted

Premise: previously succeeding `[Workflow]` compilations now fail
AGWF035. Fire fixtures inject `PhaseGraph.WithoutSuccessor`
(`TerminalReachabilityDiagnosticTests.cs:459`, `:484`). Production
`Report` rebuilds `PhaseGraph.Build(model)`. For a fluent-authored
model, `EnumerateRejoinDispatchersOf` and `AddForkEdges` /
`AddBranch` / `AddLoopEdges` walk the same construct fields
(last step → join / rejoin / continuation). A well-formed IR that
compiled yesterday still has those edges today. The obligation’s
own open question asks whether any real workflow fails on
production `Build`. Default to refuted: the breaking-surface
premise was never exhibited outside the test seam. Not the same
as “the IR fire-rule is real” (`agwf035-underreach-ir-not-emission`
refinement).

### [compat-validtransitions-nonreversing] — refuted

Claim: “a generator revert is not a revert of already-emitted
consumer `ValidTransitions` tables.” That is true of every source
generator and was not a claim this target made. Signatures are
unchanged (`TransitionsEmitter.cs:68-109`); no inventory claim
said revert reverses consumer trees. Generic generated-code
persistence is not an obligation of this lift. If successor-set
equality vs `4d060f4` matters, that is a different (missing)
obligation, not this tautology. Refuted.

### [compat-publicapi-omits-obsolete] — refuted

Duplicate of the observability half of
`requires-obsolete-observable`, plus a restatement of how
PublicApiAnalyzers work (RS0016/RS0017 track add/remove; they have
no Obsolete column; Hosting csproj `:27-30` says so). Nobody claimed
Unshipped would lock `[Obsolete]`. Empty `Shipped.txt` is a repo
convention question, not a product invariant. Refuted.

### [diagnostic-fork-ctor-open] — refuted

Premise: invalid `DiagnosticForkModel` states must be
unrepresentable except via `Create`. This wave’s claimed control is
AGWF037 on extract/import, which **avoids** `Create` on duplicate
triggers. `rg 'new DiagnosticForkModel'` in production `src/` hits
only `Create` itself (`DiagnosticForkModel.cs:134`). Consequence
cites deferred `#151` lowering (out of wave). Representable-invalid
invented a type-closure guarantee the target did not state. Latent
`internal` record ctor with no production `new` is not a reachable
fail of *this* reject. Refuted.

### [traversal-result-flags-independent] — refuted

Premise: `IsError` and `Error` must not be independently
representable. This wave claimed `resultType: complete` on
constructed `CallToolResult`s, not a `TraversalResult` discriminated
union. Production constructions are `OntologyTraverseTool` success
returns (`:173-181`) and `Error` (`:184-185`), which set the flags
together. Mixed state is a theoretical `init` hole. Not a claimed
guarantee of #176/#177. Refuted.

### [agwf037-catalog-identity] — refuted

Something else already guarantees freshness.
`AgwfCatalog_HandEdit_FailsGuard` regenerates then compares.
`contracts-codegen-guard.yml` path-filters `src/Strategos.Contracts/**`
and `docs/diagnostics/**` (this wave touches both), regenerates, and
`git diff --exit-code` on `Generated/`, `schemas/`, `docs/diagnostics`
(`:56-60`). The four tests this wave extended are hand-authored
identity lists. Treating them as *the* 0.7.0 freshness lock is a
misread of which job owns regen. The obligation’s own open question
already says: if those jobs run, the emitter stale-read is
redundant. They run on this path set. Refuted.

### [claim-clr-free-xor-docs] — survived

CHANGELOG: docs name `ObjectTypeFromDescriptor` / `ApplyDelta` as
the CLR-free seam and record that a SymbolKey-only interface fan-out
is not expressible (`CHANGELOG.md:197-199`).
`polyglot-descriptors.md:125-144` states both sentences, including
the quoted parity-test bound. Claimed docs delivery; pages exhibit
it. Human-judgment rung is appropriate. Not a type-system
obligation (types already enforce the limit); it is the prose this
wave said it would add.

### [claim-issue-185-tracker] — refuted

`claim-issue-185-tracker-close.md` disposition is **open** and ends
“This file is not an obligation.” Ledger Assumptions already say
issue 185 “still open by design” is comment-time tracker state, not
proof this branch left the work undone. No PR binds merge to issue
transitions. Tracker hygiene is not a code correctness invariant.
Promoting the open file into Active violates `validating-claims.md`.
Refuted.

## Counts

| Verdict | Slugs |
|---|---|
| Refuted | 13 |
| Survived (refinement) | 4 |
| Survived | 9 |

## Passes

Re-read `TerminalReachabilityGuard`, `PhaseGraph`,
`WorkflowIncrementalGenerator` `hasErrors` / `Report` placement,
`WorkflowDiagnostics` AGWF035/037 remarks, Residue CHANGELOG,
`PackagingTests` 0.7.0 asserts, `contracts-schema-diff.yml`,
`contracts-codegen-guard.yml`, `MergeTwo` / `IngestedIntentInvariant`,
`source.md` provenance list, `polyglot-descriptors.md` XOR section,
`Directory.Build.targets` CS0618, DiagnosticFork / TraversalResult
construction sites, and the AONT205 analyzer diff (empty).

## Uncertainties

- Out-of-repo TypeSpec/JSON ingest stamping `HandAuthoredContract`
  (does not restore the “Source = 2 survives merge” wording).
- Whether `contracts-codegen-guard` / `contracts-test` /
  `schema-diff` are **required** GitHub checks. For catalog-identity
  the job exists and matches this wave’s paths; for schema-diff
  required-check status was already an open question and was not
  used to keep the slug.
- Whether any out-of-repo `[Workflow]` has an IR the two walks
  disagree on. In-repo fire fixtures do not show that. Defaulted to
  refuted for `compat-agwf035-breaking`.
