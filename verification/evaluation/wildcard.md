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
  - path: /home/reedsalus/.claude/skills/verify-code/references/evaluation-lenses.md
    why: Stage 3 lens 4 (Wildcard) instructions
---

# Evaluation — Wildcard

Lens 4 only. The other evaluators accepted the ledger. This pass questions
the survey’s framing, looks for a perspective those three lenses do not
hold, crosses cheap-rung fit with set-fitness and with kill-attempts, and
records what reads as wrong without a home in those views.

Did not read `verification/evaluation/proof-layer-fit.md`,
`coverage.md`, or `refutation.md`. Other-lens content used here is the
orchestrator’s one line per lens.

## Framing

The run is held together by three different answers to “what matters,”
and they do not agree.

1. **Scope answer** (stage 0, word for word): knock out the #185 leftover
   list — AGWF035 route-analysis, AGWF037, renovate path, MCP
   resultType+Icons, `HandAuthoredContract`, `Requires` obsolete. That is
   a residue-completion wave. Survey wildcard W5 already noted that this
   matches the later leftover comment, not issue 185’s original “next
   slice” (close the termination class).
2. **Cost setting**: high, because Contracts 0.7.0 is a published
   generated contract, because MCP / Renovate / Exarchos cross a
   process boundary, and because AGWF035/037 are controls others rely
   on. That ranking puts S2 and S3 next to S1.
3. **Strongest survey mechanism**: under-reach compares two IR walks;
   the #184 class (emitter forgets `Start{Finally}` while the model
   still describes the rejoin) stays silent; `ValidTransitions` is
   unread at runtime (`CHANGELOG.md:127-128`).

Inventory accepted all three. The ledger then treats leftover-list
tracks, boundary-crossing packaging, and the #184 hole as peer Active
members. That is the frame this lens does not accept as given.

A perspective none of the other three lenses holds: **advertised
strength versus scoped strength, on a consumer clock this branch has
not started.** Cheap-rung fit asks whether a proof can carry a claim.
Coverage asks whether the set matches the scope set. Refutation asks
whether each obligation is real. None of them asks whether the wave
sold a stronger property than it was allowed to ship, or whether the
load-bearing moment is a tag / rebuild / bot run that does not exist
yet.

## Findings

### Ledger-wide — three “what matters” functions share one Active list

- **Affected:** ledger-wide
- **Concern:** A reader who ranks by the leftover list will spend the
  budget across T1–T6. A reader who ranks by the cost-setting triggers
  will treat Exarchos converters and MCP pin lag as the subject. A
  reader who ranks by consequence of a hung saga will treat
  `agwf035-underreach-ir-not-emission` as the only load-bearing
  member and the rest as packaging. The ledger does not say which
  function evaluation is supposed to use, so the other lenses will
  disagree about over-investment without noticing they are scoring
  different exams.
- **Scope:** Stage 0 scope answer vs cost-control bullets vs survey
  structural backbone items 1–3.
- **Evidence:** Stage 0 ranks S1 and S2 both “highest.” CHANGELOG
  Residue (`:170-199`) is six file-disjoint tracks. The 2.11.0 lede
  (`:10-17`) is still a single correctness-core story and a single
  contracts bump `0.4.0 → 0.6.0`.
- **Suggested action:** Synthesis should name the scoring function
  before it ranks findings. If the leftover list is the exam, S1’s
  #184 hole is out of wave (Option B forbidden). If the termination
  class is the exam, S3–S6 are the over-investment.

### Ledger-wide — neighborhood holes and this-wave lies share standing

- **Affected:** ledger-wide
- **Concern:** Reverse-dependency closure at high cost pulled
  surrounding gates and pre-existing representable-invalid states into
  Active. That is a legitimate high-tier move. The odd result is that
  a reader cannot tell “this wave shipped a false sentence” from “this
  wave walked past a hole that predated it.” Both look like the same
  kind of member.
- **Scope:** Active mix of residue claims and surrounding /
  pre-existing surfaces.
- **Evidence:** `schema-diff-skip-succeeds` records that
  `contracts-schema-diff.yml` is unchanged on `324768f`.
  `aont205-analyzer-unreached` is a descriptor with no
  `ReportDiagnostic` site; the wave retargeted the runtime invariant,
  not the analyzer. `diagnostic-fork-ctor-open` and
  `traversal-result-flags-independent` are type-shape holes the wave
  touched at the edge. Contrast `agwf035-catalog-polarity-lie` and
  `contracts-changelog-contradicts-0-7-0`, which are sentences this
  revision published.
- **Suggested action:** Mark neighborhood members as surrounding in
  the ledger so a later pass does not treat them as residue-track
  failures.

### Ledger-wide — “supported claims” sit in Active as if they were defects

- **Affected:** ledger-wide;
  `agwf035-all-complete-silent`; `agwf035-overreach-preserved`;
  `agwf037-reject-not-dedup`; `icons-null-when-unset`;
  `claim-clr-free-xor-docs`
- **Concern:** The ledger’s own “Supported claims” section says these
  survived inventory as “the code supports the claim” and remain
  Active so evaluation can refute them. That handoff is honest. The
  portfolio effect is not: a kill-attempt that succeeds on a
  supported claim looks like a removed risk, and a cheap-rung pass
  on the same claim looks like a well-placed proof of a problem.
  They are regression pins and vacuous holds, not the same object as
  a CHANGELOG overclaim.
- **Scope:** Active vs “Supported claims (kept as obligations with
  existing proof).”
- **Evidence:** Ledger `:524-534`. Four of the five named slugs are
  also full Active rows with Consequence written in the defect voice
  (`agwf035-all-complete-silent`: “Legal exclusive-complete
  workflows fail the build” — that is the *failure of the proof*,
  not a shipped defect).
- **Suggested action:** Evaluate supported claims as a separate
  cohort. Do not let a refutation of “the green test is real” erase
  the scoped-out risk the silence is standing in for.

### Ledger-wide — the highest-consequence finding is ornamental by instruction

- **Affected:** ledger-wide; `agwf035-underreach-ir-not-emission`
- **Concern:** Stage 0 forbids inventing obligations that demand
  Option B. The #184 class is exactly the hole under-reach cannot
  see: IR has the rejoin, the emitter forgets `Start{Finally}`. The
  ledger is allowed to name that hole and forbidden to close it. The
  obligation then reads as the most severe S1 member while the wave
  definition makes it permanently advisory. That is not missing
  coverage and not a wrong rung. It is a scoped-out risk wearing a
  first-class Active badge.
- **Scope:** Stage 0 “Out of wave” vs CHANGELOG Residue `:172-177`
  and plan T1 (“compile-time lock so the next dropped edge does not
  need Postgres”).
- **Evidence:** `pad-agwf035-underreach-is-ir-not-emission.md`
  discriminating detail: production `Report` rebuilds
  `PhaseGraph.Build(model)`; positives inject `WithoutSuccessor`.
  Survey W1: a handler that forgets a construct the IR still
  describes keeps the graph edge.
- **Suggested action:** Keep the name. Do not let Active standing
  imply this wave owes a saga-emission lock. Record Option B as the
  form of the remaining risk, not as a fix this ledger can assign.

### Ledger-wide — Exarchos is the cost-setting premise and an unobserved consumer

- **Affected:** ledger-wide; `contracts-0-7-0-pack-incomplete`;
  `schema-diff-skip-succeeds`; `agwf035-catalog-polarity-lie`;
  `agwf037-catalog-identity`
- **Concern:** High cost is justified by “Exarchos extracts
  `agwf-catalog.json` and JSON Schema from the NuGet package” and
  “emitted converters throw on unknown members.” No obligation
  observes Exarchos. Several S2 members inherit their consequence
  from that unvalidated lead. Cheap-rung fit can place the pack
  assert well. Coverage can say S2 is represented. Refutation can
  kill a claim that this repo cannot see Exarchos. The missing
  object is the *reason S2 is highest-cost*.
- **Scope:** Stage 0 S2 boundary; run-wide open question “Is
  `contracts-v0.7.0` published?”
- **Evidence:** Stage 0 `:91-93`, `:143`. Open questions repeat
  “published tag / required check” and never resolve them. Pack and
  schema-diff obligations name Exarchos in Consequence and cite no
  Exarchos source.
- **Suggested action:** Treat Exarchos behavior as a lead until a
  consumer artifact is read. Do not let pack/schema obligations
  inherit “converter throw” as if it were established.

### Ledger-wide — `ValidTransitions` is unread at runtime; S1 still treats table-agreement as the lock

- **Affected:** ledger-wide; `phasegraph-type-not-instance`;
  `compat-validtransitions-nonreversing`;
  `agwf035-underreach-ir-not-emission`
- **Concern:** The 2.11.0 body already says nothing in the generated
  saga consults `ValidTransitions` at runtime. The residue sentence
  then sells “share one `PhaseGraph` so they cannot drift” as if
  diagnostic↔table agreement were the termination lock. Two
  unread-at-runtime artifacts agreeing is a published-API
  consistency property. It is not the walk that starts `Finally`.
  Coverage will keep S1 represented. Cheap-rung fit will accept
  construction/generation for instance-share. The missing
  perspective is which S1 obligation is *load-bearing for a hung
  saga* versus *load-bearing for a tool that reads the table*.
- **Scope:** `CHANGELOG.md:127-128` vs Residue `:176-177`;
  `PhaseGraph.cs:16-17`.
- **Evidence:** Survey W1. Two `Build` sites
  (`TransitionsEmitter.cs:56`, `TerminalReachabilityGuard.cs:127`).
  Generator `Report` omits the graph (`WorkflowIncrementalGenerator.cs:1038-1043`).
- **Suggested action:** Split S1 into “saga dispatch” and “published
  table.” Do not let instance-share stand in for the dispatch lock.

### Ledger-wide — one Unreleased release, two contract minors, three stories

- **Affected:** ledger-wide; `contracts-changelog-contradicts-0-7-0`;
  `contracts-0-7-0-pack-incomplete`
- **Concern:** 2.11.0 is still Unreleased. The lede narrates one
  correctness-core bump `0.4.0 → 0.6.0`. Residue, 150 lines later,
  adds `0.6.0 → 0.7.0`. The package CHANGELOG still ends at AGWF036.
  A consumer upgrading 2.10.0 → 2.11.0 will see one product version
  with two contract minors and a lede that names only the first.
  That is not a docs nit next to pack-content asserts. It is the
  only consumer-facing account of the second bump.
- **Scope:** `CHANGELOG.md:8-17`, `:182`;
  `src/Strategos.Contracts/CHANGELOG.md`;
  `Strategos.Contracts.csproj` `ContractsVersion` `0.7.0`.
- **Evidence:** Survey W5; `pad-contracts-changelog-contradicts-0-7-0.md`.
- **Suggested action:** Weight the lede/package contradiction as the
  consumer-clock event for S2, not as a sibling of the nupkg path
  assert.

---

### `phasegraph-type-not-instance` × `agwf035-underreach-ir-not-emission` — one sold sentence, two locks

- **Affected:** `phasegraph-type-not-instance`;
  `agwf035-underreach-ir-not-emission`
- **Concern:** Cheap-rung fit will place both well: type-share is
  already present, the claims are instance-share and IR-vs-emission,
  and cheaper rungs cannot express either. Coverage may call them
  redundant because they cite the same CHANGELOG sentence and the
  same two `Build` sites. They are not redundant. Passing one
  `Build` result into `Report` and `Emit` still compares two IR
  derivations. Walking emitted `Start{target}` still allows the two
  graphs to diverge. The product sold them as one lock (“share one
  `PhaseGraph` so they cannot drift” / “#184 compile-time
  decidable”).
- **Scope:** `TransitionsEmitter.cs:56`;
  `TerminalReachabilityGuard.cs:127`;
  `WorkflowIncrementalGenerator.cs:1038-1043`.
- **Evidence:** `pad-phasegraph-type-not-instance.md`;
  `pad-agwf035-underreach-is-ir-not-emission.md`. Open question on
  the under-reach row already asks whether a saga-emission lock is
  in-scope or IR-vs-graph is the accepted T1 deliverable.
- **Suggested action:** Keep both. Pair them. Do not merge. Name
  instance-share as table↔diagnostic and IR-vs-emission as the
  #184-shaped remainder.

### `agwf035-error-still-emits` × `agwf037-reject-not-dedup` — fail-closed is not one policy

- **Affected:** `agwf035-error-still-emits`;
  `agwf037-reject-not-dedup`; `agwf035-json-import-unreached`
- **Concern:** Same wave, two new (or newly armed) Error codes,
  opposite generation consequences. AGWF037 joins `hasErrors` and
  runs on C# extract and JSON import. AGWF035 reports after the
  gate, on a live model, C# only, and still emits. Cheap-rung fit
  can place each structural check well. Coverage can say S1 and S2
  are represented. Refutation can kill “AGWF errors share one
  emit-or-gate policy” as a claim nobody validated — the code never
  promised a single policy in one table. The underlying risk stays
  real in a different form: **suppress-AGWF035-and-ship-the-saga**
  is the composition, and INV-5 / `WorkflowDiagnostics.cs:556-558`
  still say the unreachable workflow does not run.
- **Scope:** `WorkflowIncrementalGenerator.cs:930-941` vs `:1038-1045`
  and `:84-87`.
- **Evidence:** `pad-agwf035-error-still-emits.md`; survey W3.
  AGWF037 remarks state the gate and implement it.
- **Suggested action:** Treat the house-style split as the object,
  not two independent omissions. If emit-anyway is intentional,
  the CHANGELOG/remarks sentence is the remaining false claim. If
  it is not, AGWF035’s absence from `hasErrors` is the defect.

### `agwf035-catalog-polarity-lie` — the sibling bump is used as permission

- **Affected:** `agwf035-catalog-polarity-lie`;
  `contracts-0-7-0-pack-incomplete`
- **Concern:** T1 kept the over-reach sentence to avoid a catalog
  bump. T2 in the same wave already paid `0.6.0 → 0.7.0` for
  AGWF037. Cheap-rung fit will say widening `messageFormat` sits at
  construction/generation and “why not cheaper” is real (substring
  tests cannot fail a polarity lie). Coverage will keep it as S1/S2.
  The odd remainder: the version bump is treated as *license* to
  widen AGWF035, not as a *subject* whose 0.7.0 contents should
  have included the widen. Paying for a bump does not decide what
  the bump must contain.
- **Scope:** `AgwfCatalog.tsp`; `WorkflowDiagnostics.cs:564`;
  `ReportUnderReach` arg order `{0}`=terminal, `{2}`=dispatcher.
- **Evidence:** Survey W1; ledger Why-not-cheaper on this row;
  plan T1 “only widen if that sentence becomes a lie” — already
  met, already ignored.
- **Suggested action:** Judge the polarity lie on the sentence, not
  on whether 0.7.0 has already been spent. If 0.7.0 ships without
  the widen, the bump is the event that published the lie.

### `agwf035-all-complete-silent` and `agwf035-overreach-preserved` — kill the obligation, keep the hole

- **Affected:** `agwf035-all-complete-silent`;
  `agwf035-overreach-preserved`
- **Concern:** A kill-attempt should succeed on both as *defect
  obligations*: the code does what the CHANGELOG says, and existing
  fixtures already pin it. The underlying risks are real in other
  forms. All-Complete + Finally silence is Option B deferred — the
  accepted hole, not a property that needs a new proof.
  Over-reach-preserved is a regression pin for the already-shipped
  half while this wave adds a second arm on the same id. Coverage
  that treats them as S1 representation will over-count. Cheap-rung
  fit that praises the component tests will treat a green pin as a
  well-placed proof of a problem.
- **Scope:** Under-reach fire rule; spec DR-3; generator negatives.
- **Evidence:** Ledger “Supported claims”;
  `pad-all-complete-finally-silent.md`;
  `claim-agwf035-overreach-preserved.md`. Stage 0 out-of-wave:
  Option B.
- **Suggested action:** Keep them as regression pins. Do not count
  them toward S1 risk coverage. Name Option B as the form behind
  the silence.

### `compat-agwf035-breaking` — a break that may not exist on any authored workflow

- **Affected:** `compat-agwf035-breaking`;
  `agwf035-underreach-ir-not-emission`;
  `agwf035-catalog-polarity-lie`
- **Concern:** Cheap-rung fit places a breaking-diagnostic claim at
  component tests. Coverage wants S1’s consumer-compile consequence
  represented. Refutation has a clean kill: production `Build` on a
  well-formed model does not omit the rejoin edge, and the only red
  fixtures inject `WithoutSuccessor`. The obligation then describes
  a source-breaking upgrade that this revision may never inflict on
  a real `[Workflow]`. The underlying risk is real in a different
  form: **when the arm does fire, the catalog sentence is the
  over-reach polarity.** Authors who hit it will remediate the
  wrong fault. That is a message-lie, not an upgrade-break.
- **Scope:** Existing error id, new arm, C# extract only.
- **Evidence:** `compat-agwf035-underreach-breaking-diagnostic.md`
  open question: “If the only red fixtures use `WithoutSuccessor`,
  the breaking surface may be narrower than the CHANGELOG reads.”
  Survey backbone item 3.
- **Suggested action:** Do not let “breaking diagnostic” stand as
  the consumer-facing fact unless a production-`Build` fire shape
  exists. Pair the remaining risk with the polarity lie.

### `icons-null-when-unset` — vacuous hold; the live hole is the missing producer

- **Affected:** `icons-null-when-unset`
- **Concern:** Cheap-rung fit places the discovery-null test well.
  Coverage can mark S3 represented. A kill-attempt on “placeholder
  icons must not appear” should succeed: `Discover` never assigns,
  so null-when-unset holds because nothing writes. The underlying
  risk is real as **hosts cannot supply icons through
  `AddOntologyTools`.** That is a missing producer, not a
  null-when-unset invariant. The ledger already says this in
  Consequence and still titles the row as the CHANGELOG claim.
- **Scope:** `OntologyToolDescriptor.Icons`; `ApplyIcons`;
  `Discover` never sets.
- **Evidence:** `pad-icons-null-when-unset.md`; ledger open
  question “Is consumer `Icons` a future factory overload?”
- **Suggested action:** Split the vacuous CHANGELOG hold from the
  unreached non-null path. Do not treat a green null assert as
  coverage of #177.

### `handauthoredcontract-unreached` — missing producer is the wrong costume

- **Affected:** `handauthoredcontract-unreached`;
  `descriptor-source-docs-omit-member-2`;
  `aont205-analyzer-unreached`
- **Concern:** A kill-attempt can take the additive-enum half:
  unused members compile, AONT205 skip-unless-Ingested is reached,
  issue 185 already called #163 inert without a producer. The
  underlying risk is real in a different form: **ordinal `2` is a
  published public API; `MergeTwo` restamps `HandAuthored`;
  unwidened `== HandAuthored` branches skip value 2.** The first
  out-of-repo ingest that stamps `2` loses provenance at merge and
  misses AONT201/203. That is a compatibility time-bomb, not a
  “tests forgot to assign production.” Docs that still list two
  members make the time-bomb discoverable as the wrong mapping.
- **Scope:** `DescriptorSource.cs:63`; `MergeTwo.cs:67`;
  `OntologyGraphBuilder.cs:330/:409/:566`.
- **Evidence:** Survey W4; `pad-handauthoredcontract-unreached.md`.
  Merge test asserts the collapse.
- **Suggested action:** Evaluate member 2 as a shipping ordinal
  plus merge lattice, not as an unreached assignment hunt.

### `renovate-resolve-unasserted` — instance claim, class risk

- **Affected:** `renovate-resolve-unasserted`
- **Concern:** Cheap-rung fit will accept production-path
  integration for “Renovate resolves the preset” and note that the
  artifact is “None in this repo.” Coverage will keep S6. A
  kill-attempt on the strong claim should succeed: this repository
  cannot observe the GitHub App. The path-token suffix is the
  weaker claim that holds. The underlying risk is real as **R3
  inert-looking control** — the same class #181 named, now with a
  new path string. The next token edit will be unobservable the
  same way.
- **Scope:** `renovate.json` second `extends` token.
- **Evidence:** Survey W7; ledger G-R3 “instance-fix only; stays
  open.” Recurrence proof-system finding: CI/config axis still
  unguarded after 3+ hits.
- **Suggested action:** Keep the weak path-token claim. Move the
  strong resolve claim to the R3 class, not to this one-line diff.

### `claim-issue-185-tracker` — the evidence file denies the ledger row

- **Affected:** `claim-issue-185-tracker`
- **Concern:** The Active row claims a tracker/CHANGELOG
  disagreement that can auto-close 185. The evidence file ends
  “This file is not an obligation.” Cheap-rung fit has nowhere to
  sit (human judgment, no artifact in-repo). Coverage can keep S7
  as claim inventory. Refutation can kill it as a project-management
  disagreement. The odd: inventory promoted a file that refuses
  promotion. The underlying risk is real as **process control** —
  a “Close #185” title has already auto-closed the tracker once —
  not as a property of revision `324768f`.
- **Scope:** GitHub issue 185 comment 2 vs CHANGELOG Residue. No PR
  for `cursor/c801a047` at inventory time.
- **Evidence:** `claim-issue-185-tracker-close.md` last line;
  ledger `:504-520`.
- **Suggested action:** Remove Active standing or refile as a
  merge-time checklist item. Do not score it as a code obligation.

### `mcp-resulttype-and-pin` — two proof systems, neither closes the class

- **Affected:** `mcp-resulttype-and-pin`
- **Concern:** Factory assignments and a Hosting `VersionOverride`
  2.2.0 are real. CPM stays on 1.3.0. INV-3 3.4/3.5 are substring
  greps and are not in `ci.yml`. Cheap-rung fit will say types
  cannot carry assignment and greps are comment-satisfiable —
  correct. Coverage will mark S3 represented. The missing
  perspective is the **proof-system split**: the invariant catalog
  is asked to do CI’s job, and CI does not run the catalog. Four
  tools still rely on SDK wrap. `ErrorResult` sets
  `resultType: complete` on an error payload; INV-3 asserts that is
  legal. That protocol question is a different object from the pin.
- **Scope:** `MapTraversalResult` / `ErrorResult`; Hosting pin;
  INV-3 3.4/3.5.
- **Evidence:** Survey backbone item 7; W7; ledger open question on
  `ErrorResult` + `complete`.
- **Suggested action:** Separate “Hosting constructs set
  `resultType`” from “INV-3 is a CI control” from “error channel vs
  complete discriminator.” Do not let one row carry all three.

### `schema-diff-skip-succeeds` — a witness that may not have testified

- **Affected:** `schema-diff-skip-succeeds`;
  `contracts-0-7-0-pack-incomplete`
- **Concern:** This wave adds a schema. The job that would classify
  it as additive can skip and succeed. Cheap-rung fit places the
  YAML shape well. Coverage can keep S2’s surrounding gate.
  Refutation can kill “this PR’s schema-diff is already
  false-green” if this clone’s `v2.10.0` satisfies `have_prev`.
  The underlying risk is real as **the 0.7.0 bump may have no
  observed schema-diff.** The version bump is the event; the
  unchanged workflow is a witness. A green job that did not
  compare and a missing required-check are the same merge signal
  if the job is not required — and that required-check question is
  still open.
- **Scope:** `.github/workflows/contracts-schema-diff.yml`,
  unchanged on `324768f`.
- **Evidence:** `schema-diff-skip-succeeds.md` open questions.
- **Suggested action:** Attach this row to the bump event, not to
  a workflow this wave did not edit. Settle required-check before
  treating skip-success as a merge defect.

### `requires-obsolete-observable` — the suite is the silencer

- **Affected:** `requires-obsolete-observable`;
  `compat-publicapi-omits-obsolete`
- **Concern:** `[Obsolete]` is real and the method still writes
  Preconditions. Cheap-rung fit will say the compiler already
  carries Obsolete and the missing proof is a `NoWarn`-free
  subject. Coverage will mark S5. The odd pairing: this wave
  removed in-repo callers *and* added `CS0618` to `NoWarn` for
  every test/benchmark project, *and* PublicAPI Unshipped has no
  Obsolete column. The in-repo suite cannot see the warning this
  wave claims consumers will see. Soft/Link variants stay current
  on the same interface (survey W6). The live authoring path is
  not “Requires is obsolete”; it is “one method warns, three
  siblings do not, and the suite will not tell you.”
- **Scope:** `IActionBuilder<T>.Requires`;
  `Directory.Build.targets`; packaged README still demos
  `.Requires`.
- **Evidence:** Survey W6; ledger G-R6 “appeared once; no guard
  owed.”
- **Suggested action:** Evaluate CS0618 suppression as the
  in-repo proof hole. Do not treat PublicAPI add/remove as
  evidence of Obsolete.

### `agwf037-catalog-identity` × `contracts-0-7-0-pack-incomplete` — identity vs payload

- **Affected:** `agwf037-catalog-identity`;
  `contracts-0-7-0-pack-incomplete`
- **Concern:** Cheap-rung fit places catalog identity at
  structural analysis and pack content at integration — both
  sound. Coverage may call them S2 over-investment. They are
  not the same hole. Identity tests can pass on a stale committed
  JSON or a markdown mention while the nupkg is fine. The pack
  test can pass on version `0.7.0` while AGWF037’s catalog/schema
  never entered the nupkg. The shared premise is the unobserved
  Exarchos extract. Without that premise, identity is an in-repo
  false-green and pack-content is an in-repo packaging gap.
- **Scope:** `GroundTruthCodes` / markdown `Contains`;
  `PackagingTests`; NuGet Content items.
- **Evidence:** Ledger rows; survey existing-proof bullets on
  identity vs pack.
- **Suggested action:** Keep both. Do not merge. Do not fund
  either with “Exarchos will throw” until that lead is read.

## The odd

The ledger is a residue-tracker dump that evaluation is being asked
to score as a correctness-core portfolio.

It fits no single other-lens category because the fault is not “a
scope-set member has no obligation,” not “a rung is wrong,” and not
“these obligations are fake.” The fault is that **the organizing
unit is issue 185’s leftover list, while the sentences the product
published are a termination lock, a second contracts minor, and a
fail-closed house style — and the wave was forbidden to close the
lock it advertised.**

The single sharpest oddity inside that frame: `claim-issue-185-tracker`
is Active while its evidence file says it is not an obligation, and
`agwf035-underreach-ir-not-emission` is Active at full standing while
stage 0 forbids the only fix that would make the CHANGELOG sentence
true.

## Passes

- The survey’s structural backbone (type-share not instance-share;
  polarity inversion; IR-not-emission; AGWF035 vs AGWF037 policy
  split; `HandAuthoredContract` unassigned; Hosting pin vs CPM;
  Renovate path-without-resolve) is the right backbone. Inventory
  did not lose those facts.
- Canonical slugs plus contributing evidence files are traceable.
  Duplicate inventory files on disk are labeled as contributing,
  not as a second ledger.
- Out-of-wave exclusions (Option B, #147, #133/#174, #156.1/#156.3,
  untracked `docs/2026-06-16-edge-*`) are recorded and were not
  silently reopened as product-fix obligations.
- Recurrence classes G-R1–G-R8 staying open is consistent with the
  instance-fix pattern the survey named. The ledger does not pretend
  a residue wave closed those classes.
- `agwf037-reject-not-dedup` as a supported composition (C# + JSON,
  gates emission) is the one residue track whose delivery matches
  its CHANGELOG sentence. It is the contrast that makes the AGWF035
  policy split visible.
- Run-wide open questions on the ledger match the ones the survey
  could not settle. They were not silently promoted to facts.

## Uncertainties

- Whether any out-of-repo producer already stamps
  `DescriptorSource.HandAuthoredContract = 2`. If yes, merge
  collapse and unwidened `== HandAuthored` are live now. If no,
  member 2 is a shipping ordinal with no current caller.
- Whether Renovate still resolves the `lvlup-claude` slug after the
  `exarchos` rename. Not fetched. The strong resolve claim stays
  unobservable.
- Whether `contracts-v0.7.0` exists and whether `contracts-test` /
  `contracts-schema-diff` are required checks. Those answers change
  which S2 rows are merge defects versus latent YAML shape.
- Whether `ErrorResult` with `resultType: complete` is 2026-07-28
  legal. INV-3 asserts yes. This lens did not read the protocol
  spec as a fact.
- Whether AGWF035-without-gating is intentional house style. That
  single human answer flips `agwf035-error-still-emits` from
  “remarks lie” to “omission.”
- Whether Exarchos parses AGWF035 `{0}/{2}` as
  chain-to-successor. If it does, the polarity lie is an
  automated wrong remediation, not only a human-readable one.
- Whether inventory agents read a dirty worktree. Stage 0 names
  HEAD `324768f`. This worktree also carries uncommitted docs,
  contracts, and test edits. Citations above were re-read from
  the worktree; a later reader should not assume they match a
  clean `324768f` tree without checking.
- Whether any in-repo sample `[Workflow]` fails under-reach on
  production `Build`. If none do, `compat-agwf035-breaking` is
  describing a CHANGELOG-shaped break that authors will not meet.
- This lens did not settle cheap-rung or kill verdicts. Those
  belong to the other three evaluators. Cross-lens remarks here
  are hypotheses about how those lenses will meet, not reports of
  their files.
