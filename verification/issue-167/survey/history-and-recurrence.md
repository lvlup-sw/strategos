---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, and documentation surface against merge-base 45c86a63a9437abd920240b4dc95b235c0f72d37
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
  - path: https://github.com/lvlup-sw/strategos/issues/143
    why: prior defect-class record for declarative workflow features lost before runtime lowering
  - path: https://github.com/lvlup-sw/strategos/pull/144
    why: merged parity guard and fixes that exposed further extraction defects
  - path: https://github.com/lvlup-sw/strategos/pull/187
    why: merged main-flow correction and primary evidence for false-green proof controls
  - path: https://github.com/lvlup-sw/strategos/issues/185
    why: maintainer recurrence ledger for generator routing, identity, and under-scoped controls
  - path: https://github.com/lvlup-sw/strategos/pull/194
    why: first merged repair wave for exclusive-path identity collisions
  - path: https://github.com/lvlup-sw/strategos/pull/196
    why: merged identity-carrying routing and review fixes for controls scoped to one arm
  - path: https://github.com/lvlup-sw/strategos/issues/31
    why: prior end-to-end identity-loss defect where a descriptor name was validated then discarded
  - path: https://github.com/lvlup-sw/strategos/pull/49
    why: prior graph-hash review that found incomplete canonical ordering and missing sensitivity coverage
  - path: https://github.com/lvlup-sw/strategos/pull/102
    why: introduction of the TypeSpec-owned AGWF diagnostic identity catalog
  - path: https://github.com/lvlup-sw/strategos/issues/105
    why: follow-up that documented live-descriptor metadata drift outside the first AGWF guard
  - path: https://github.com/lvlup-sw/strategos/pull/109
    why: merged metadata-parity guard for AGWF catalog entries
---

# Stage 1 survey — history and recurrence

## Reading rule

This lens asks whether the classes touched by #167 have failed before. Issue and PR prose was
treated as a lead. A recurrence is recorded only where the repository history contains a
discriminating code change, regression fixture, or review fix consistent with the report. At the
time of this lens, the working tree was uncommitted, so its line anchors identified only that
historical analyzed content.

This is a historical recurrence survey. Its issue/PR facts and defect-class chronology are preserved;
current proof status is governed by `../ledger.md`, not by present-tense wording in the historical
examples. The analyzed content was later captured at immutable commit `4110b512`; subsequent repairs
were committed at `595a949`, `6851fe7` serialized the pack script, and current final subject
`98fabb4` aligns workflow-name nonblank validation. The exact-current local portfolio and pending
protected CI/review are recorded in `../final-evidence.md`.

The change is in an unusually active seam. On the `main` ancestry since 2026-06-01,
`WorkflowIncrementalGenerator.cs` changed in 8 commits, `StepExtractor.cs` in 6,
`WireToModelBridge.cs` in 5, and `AgwfCatalog.tsp` in 7. The retained topic branches contain still
more intermediate repair commits (30, 16, 17, and 20 respectively). The history is concentrated
under one human author identity (two spellings, `Reed Salus` / `Reed`), not distributed across many
authors. Churn, rather than author count, is the recurrence signal.

## H1. The declared/accepted graph becoming smaller before execution or proof is a repeated class

**Risk: high. Scope: explicit #167 behavior, because the issue promises a build-time check for a
bound workflow, not merely the subset one syntax walk happened to retain. Guard candidate: yes.**

This is not a new hypothetical:

- Issue #143 records five DSL features that were accepted, exported, and documented but dropped
  before code generation. PR #144 (`8b28ab3`) added a declared-versus-lowered parity guard; while
  backfilling it, the change found that top-level/loop `ValidateState` was silently discarded and
  that a first repair let a parent step absorb a nested handler's validation.
- PR #187 (`a965f3b`) found five constructs appending off-main-flow entries while three successor
  scans used inconsistent private classifications. The resulting fork/branch/approval workflows
  could compile but never terminate. Its resolution introduced `MainFlowClassification` and routed
  all successor scans through that authority.
- PR #196 (`25368ca`, review fix `9a2b70d`) found the next form: controls were present and live, but
  applied only to the first of several handler chains or omitted sibling branch routes. Issue #185
  explicitly records this as a seventh instance of the broader inert/under-scoped-control family.

#167 creates another multi-hop declarative path:

`Performs` authoring -> runtime `StepDefinition.Action` -> C# extraction or JSON import ->
`WorkflowModel`/`PhaseGraph` -> workflow/action proof.

The current diff recognizes the history: `TopologyClosureInspector` says its purpose is to prevent
proof of a smaller graph at
`src/Strategos.Generators/Helpers/TopologyClosureInspector.cs:13-20`, manually classifies the
step-bearing constructs at `:61-105`, and its result blocks binding proof at
`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:190-199`. The new negative corpus
also covers method groups, custom extensions, partial fork paths, delegate steps, failure handlers,
confidence handlers, and approval callbacks in
`src/Strategos.Generators.Tests/Proof/TopologyClosureProofTests.cs:19-440`.

The remaining recurrence risk is structural, not another observed missing switch arm. The old
reflection forcing function expressly excludes `Performs` from its enumerated surface and relies on
prose saying separate tests guard it
(`src/Strategos.Generators.Tests/Parity/StepConfigParityTests.cs:35-38,61-65,696-719`). Nothing in
that guard forces a future step-bearing overload or authoring context to enter the action-reference
extraction/projection/import/proof corpus. The new inspector is itself a manually maintained method
switch, the same shape that repeatedly became under-scoped in #187/#196.

**Stage-2 seed:** require one forcing-function inventory of every public API that can create or
configure an executable occurrence. Each signature must be classified as (a) action-aware and
covered through both front ends, (b) deliberately actionless and rejected for bound proof, or (c)
explicitly deferred with a diagnostic. Its kill fixture should remove one supported topology arm or
introduce one unclassified synthetic signature and prove the guard fails, rather than merely
asserting that today’s test names exist.

## H2. Bare-name identity has repeatedly survived validation and then failed at a downstream map

**Risk: medium/high. Scope: inferred from the reverse dependency closure of #167's typed,
occurrence-level identity; the issue does not explicitly require reuse of the generator's
path-routing key. Guard candidate: yes.**

The history contains two independent validated forms:

- Issue #31 traced `GetObjectSet<T>(string objectType)` validating the supplied descriptor name and
  then discarding it, leaving storage dispatch keyed only by CLR type. Merged commit `75cb1ff`
  carried descriptor identity end to end. This is the same failure shape #167 is intended to eliminate at
  the ontology/workflow join: a parameter can be typed and validated yet still cease to participate
  in the later decision.
- PRs #194/#196 repaired three routing failures caused by maps keyed by bare step/type names. The
  resulting `PathRoutingKey` explicitly states that fork paths may share a phase name and that
  emitters must use `(construct, constructId, pathId, phaseName)` rather than invent a second key
  (`src/Strategos.Generators/Models/PathRoutingKey.cs:28-50`).

The #167 path correctly keys ontology actions by the full ordinal
`(DomainName,ObjectTypeName,ActionName)` triple
(`src/Strategos.Generators/Proof/OntologyActionCatalog.cs:268-281,1003-1037`). However, action
occurrences are still collapsed by `PhaseName` in extraction
(`src/Strategos.Generators/Helpers/StepExtractor.cs:233-254`) and again in proof
(`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:544-628`). Conflicting action
identities are converted to `DynamicOrInvalid`, so this is fail-closed today; this lens found no
silent false acceptance. It is nevertheless a recurrence boundary because the runtime routing
authority deliberately permits same-phase fork occurrences while the proof authority represents
one semantic action per phase. The public model calls `Action` occurrence-owned and says one CLR
implementation can represent different ontology actions
(`src/Strategos/Definitions/StepDefinition.cs:74-81`). Distinct instance names provide the current
escape hatch, but that limitation is not derived from `PathRoutingKey` or guarded as a shared
invariant.

**Stage-2 seed:** settle whether “occurrence” means phase occurrence or path-qualified syntactic
occurrence. If path-qualified, reuse one key type in extraction, wire projection, and proof and add
a two-fork-path/same-phase/different-action kill fixture. If phase-scoped is intentional, document
the required instance-name disambiguation and add a deterministic rejection fixture. Do not leave
the two identity authorities to agree by convention.

## H3. Verification controls on this seam have repeatedly been green for the wrong subject

**Risk: high for the verification run, not by itself a new product defect. Scope: inferred from
#167's “machine-checked” enforcement class and the user's explicit verify-code request. Guard
candidate: yes; exact present-day test gaps belong to the Existing Proof Inventory lens.**

PR #187 contains three discriminating examples:

1. the declared/lowered parity guard accepted a skipped test, commented-out test, helper, or doc
   reference because it searched for a method-name substring;
2. the behavioral harness treated an absent saga document as completion, so a saga that never
   started passed;
3. count-only fork assertions accepted terminal-before-join ordering because every step still ran
   once.

PR #196 then found a fourth shape: a real control attached only to `qualified[0]` passed for one
message type and silently did nothing for its siblings. These are distinct mechanisms but one
recurrent class: the check runs and reports green without binding itself to every subject it claims
to cover.

Current code carries useful historical guards: `BehavioralProofInspector` requires an unsuppressed
test declaration, and the #167 topology tests generate concrete compilations. The historical
lesson is still load-bearing: success must include exact compilation health, exact occurrence/edge
coverage, and a kill fixture for every structural guard. This lens intentionally does not duplicate
the current assertion-level gaps catalogued by the Existing Proof Inventory lens.

**Stage-2 seed:** make “proof harness observes the intended compilation and every expected
occurrence” an obligation separate from “the proof algorithm is correct.” A guard self-test must
demonstrate that suppression, an unrelated compile error, a dropped occurrence, and one-of-N
coverage cannot report success.

## H4. Hash and diagnostic drift are recurrent classes, but the present change has standing guards

**Risk: residual low. Scope: explicit for hash compatibility; inferred for diagnostic artifact
parity. No independent blocker from this lens.**

PR #49's CodeRabbit review found incomplete sort tie-breakers in the first graph hasher: two
structurally different entries sharing a prefix key could make insertion order affect the hash. The
repair added total ordering and a permutation-invariance fixture. #167 writes the typed
`BoundWorkflow.WorkflowId` into the legacy routing slot at
`src/Strategos.Ontology/Internal/OntologyGraphHasher.cs:238-258`; the current kill fixtures assert
that rebinding changes the hash and that the typed representation retains the pinned old hash at
`src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs:290-333`. That directly addresses the
issue's explicit unchanged-hash acceptance criterion.

The AGWF catalog followed a similar sequence: PR #102 (`0923338`) single-sourced diagnostic IDs,
then issue #105/PR #109 (`3db46ff`) found that severity/title/message remained independently
authored and added a reflection-driven parity check. That guard enumerates every catalog entry and
compares all three live descriptor fields at
`src/Strategos.Generators.Tests/Diagnostics/AgwfCatalogParityTests.cs:14-77`, so the four new #167
diagnostics are within an existing class-level guard.

These controls should stay in the final verification set, but history supplied no evidence that
#167 bypasses either one after the current fixes. They are not promoted as blockers here.

## Recurrence list for Stage 2

| Class | Prior occurrences | Present exposure | Scope | Disposition |
|---|---:|---|---|---|
| Accepted declarative surface omitted or under-scoped downstream | at least 7 documented across #135/#143/#144/#187/#196 | New action metadata traverses every topology and two front ends; closure inventory remains manual | Explicit | Open guard candidate; high |
| Semantic identity validated then reduced to a weaker key | #31 plus #189/#190/#191 and #196 | Full action triple is preserved, but path routing and proof use different occurrence keys | Inferred | Open design/guard question; medium/high |
| Green control bound to the wrong or incomplete subject | at least 4 mechanisms in #187/#196 | Applies to every #167 proof/harness claim | Inferred | Open verification obligation; high |
| Canonical hash omits/ties structural fields | PR #49 review sequence | Typed workflow ID occupies old hash slot with a pinned compatibility vector | Explicit | Guarded; residual low |
| Generated diagnostic catalog drifts from live descriptors | #102 -> #105/#109 | Four new AGWF entries | Inferred | Guarded; residual low |

## What else I read and what I deliberately left to other lenses

- Local history for the generator entry point, step extractor, import bridge, AGWF catalog, graph
  hasher, action binding, and builder API surfaces; retained topic-branch repair commits; issue
  bodies/comments and merged PR descriptions/reviews named in the frontmatter.
- The current `TopologyClosureInspector`, action catalog, binding proof occurrence map,
  `PathRoutingKey`, step action-reference tests, graph-hash tests, parity guard, and AGWF metadata
  parity gate.
- There is no separate production-incident archive in the supplied corpus. The strongest incident
  evidence is the merged defect PRs, executable regression fixtures, and maintainer recurrence
  ledger in #185. Ticket headlines alone were not treated as proof.
- I did not report the compilation-local catalog reachability question (Production Path lens), the
  count and ownership of diagnostic/API representations (Authority Topology lens), or assertion
  omissions in individual #167 tests (Existing Proof Inventory lens). Those surfaces were read only
  far enough to avoid duplicating their assigned findings.
- The public builder API gate's historical exact-seven scope was also not promoted here; the
  Authority Topology lens owns whether #167's continuation-interface additions fall outside it.

No product file was edited and no test command was run for this survey lens.

## Assumptions and unsettled questions

- The historical issue/PR mechanisms describe real defects because the corresponding commits add
  code paths and regression fixtures that discriminate the reported behavior from configuration or
  environment failure. Counts quoted above come from local Git history; retained branch counts are
  explicitly not mainline commit counts.
- It remains unsettled whether two same-phase fork occurrences are contractually allowed to perform
  different ontology actions without instance names. Current proof rejects the shape, while runtime
  routing has a path-qualified identity capable of distinguishing it.
- It remains unsettled what class-level forcing function should own future occurrence-authoring
  overloads. A list of today’s tests is insufficient given the documented false-green history; the
  guard needs an executable kill fixture and a fail-closed self-test.
