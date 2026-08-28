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

# Stage 1 survey record

Cost control **high** was stated in `verification/stage0.md` before these lenses ran. Full per-lens evidence:

- [survey/mechanism.md](survey/mechanism.md) — lens 1
- [survey/intent-and-claims.md](survey/intent-and-claims.md) — lens 2 (**claim inventory seed**, 152 claims)
- [survey/authority-topology.md](survey/authority-topology.md) — lens 3
- [survey/production-path.md](survey/production-path.md) — lens 4
- [survey/existing-proof.md](survey/existing-proof.md) — lens 5
- [survey/history-and-recurrence.md](survey/history-and-recurrence.md) — lens 6 (**recurrence seed**)
- [survey/wildcard.md](survey/wildcard.md) — lens 7

## Structural backbone (lenses 1, 3, 4)

Merged. Contradictions settled from code, not by preferring a lens.

1. **PhaseGraph is a shared type and algorithm, not one instance.** Production builds twice: `TransitionsEmitter.cs:56` and `TerminalReachabilityGuard.cs:127`. The generator `Report` at `WorkflowIncrementalGenerator.cs:1038` does not pass a graph. `WithoutSuccessor` is a test seam. CHANGELOG “share one PhaseGraph so they cannot drift” is type-share, not instance-share. (L1, L3, L7)

2. **AGWF035 under-reach reuses the over-reach catalog sentence with inverted args.** `{0}` = terminal, `{2}` = last step. `WorkflowDiagnostics.cs:564` still says `{0}` chains to `{2}` and the saga runs *past* termination. T2 already paid Contracts 0.7.0, so the “do not widen the catalog” constraint that justified the lie is gone. (L1, L7)

3. **Under-reach compares IR rejoin dispatchers to PhaseGraph, not saga emission.** A handler that forgets a construct the model still describes keeps the graph edge. The #184 class (missing `Start{Finally}` while IR is correct) stays silent. Positive tests inject `WithoutSuccessor`; production rebuilds `PhaseGraph.Build(model)`. (L1, L5, L7)

4. **AGWF035 is C#-only and does not join `hasErrors`.** JSON import (`BridgeImportFile` → `EmitWorkflowSources`) never calls the guard. AGWF037 *does* run on both C# extract and JSON import and *does* suppress saga emission. AGWF035 Error still emits. (L1, L4, L7)

5. **AGWF037 is a new reject, not a deleted first-wins-dedup.** Extractor returns false; `Create` throws. Empty trigger names are skipped. Dual uniqueness authorities: runtime `HashSet<ForkTrigger>` vs generator string set. (L1, L3, L4)

6. **`HandAuthoredContract = 2` has no production assignment.** AONT205 skip-unless-Ingested is reached. `MergeTwo` still writes `Source = HandAuthored`. Unwidened `== HandAuthored` branches remain at `OntologyGraphBuilder.cs:330/:409/:566`. Compile-time AONT205 descriptor has no `ReportDiagnostic` site found. (L1, L4, L5, L7)

7. **MCP `resultType` is factory-set on traverse; four tools rely on SDK 2.2.0 wrap.** Hosting `VersionOverride` 2.2.0; CPM still lists 1.3.0. `Icons` null path is reached; non-null path is unreached (`Discover` never sets). (L1, L4, L3)

8. **`Requires` is `[Obsolete]` only.** Still callable. `Directory.Build.targets` adds `CS0618` to `NoWarn` for all tests/benchmarks. Packaged ontology README still demos `.Requires` with no note. (L1, L3, L4)

9. **Renovate path token points at a file that exists** (`tools/renovate-config/presets/dotnet.json` on `lvlup-claude` → `exarchos` rename). No binder that the remote preset resolves. Bot apply is unobserved. (L3, L4, L6)

10. **DescriptorSource docs on pages this diff edited still list two members** (`source.md:63-66`, `ontology-sources.md:40-43`). (L3)

11. **CHANGELOG 2.11.0 lede still says Contracts 0.4.0 → 0.6.0**; Residue and csproj say 0.7.0. Package CHANGELOG never names AGWF037. (L7)

## Claim inventory (lens 2 — do not promote to behavior)

152 claims. Full numbered inventory: `survey/intent-and-claims.md`. Highest-stakes leads:

- "`AGWF035` now decides route under-reach."
- "The guard and the emitted `ValidTransitions` table now share one `PhaseGraph` so they cannot drift."
- "Fire only when a construct is marked **rejoin** … All-terminal exclusive paths stay silent."
- "Reject, do not first-wins-dedup."
- "`Strategos.Contracts` bumps **0.6.0 → 0.7.0**."
- "Hosting pins 2.2.0 so every constructed `CallToolResult` can set the 2026-07-28 complete discriminator."
- "`OntologyToolDescriptor.Icons` stays null when unset."
- "AONT205 retargets to mechanical ingestion."
- "The method stays so existing `Object<T>` authoring still compiles."
- "Renovate resolves the organisation's dotnet preset (#181)."
- "Prefer the existing message template … Only widen catalog remediation if that sentence becomes a lie."

Issue 185 still lists several of these items as open-by-design. Spec DR-3 is over-reach only.

## Existing proofs (lens 5)

Most new tests are rung 4. Weaknesses that later inventory must treat as obligations, not as coverage:

- AGWF035 under-reach kill fixtures inject `WithoutSuccessor` / empty classification — not the generator’s production `Build`.
- Guard call-site scan is source-text; unwiring `phaseGraph` still passes.
- No equality lock between diagnostic graph and `ValidTransitions`.
- Catalog/schema/enum tests are identity, not behavior.
- Markdown / INV-3 greps are substring; a comment can satisfy.
- Schema-diff CI **skips and succeeds** if no previous tag.
- Contracts pack does not require the AGWF037 schema/catalog to be inside the nupkg.
- `contracts-test` vs skip `*Contracts.Tests*` — required-check status open.
- Icons factory tests ≠ discovery wiring.
- Hosting pack tests do not assert the 2.2.0 pin.
- AONT205 merge tests assert `Source == HandAuthored` (collapse).
- `IActionBuilder` NSubstitute test can pass by construction.
- Renovate: **no proof**.
- INV-3 / deterministic-checks / CHANGELOG / plan: **prose (rung 6), not proofs**.

## Recurrence list (lens 6 — Stage 2 seed)

| Class | Prior | Guard? | This diff |
|---|---|---|---|
| R1 Termination under/over-reach | 5 | Half (over-reach only) | extends-guard |
| R2 First-wins / silent collision | 4 | Per-id AGWF003/036 | adds AGWF037 |
| R3 Inert-looking control | ≥6 | Partial; Renovate-resolve none | T3 instance-fix only |
| R4 MCP pin lag | 3 | INV-3 Check 3.3 (decayed once) | extends 3.4/3.5 |
| R5 Enum ordinal / additive split | 2 | Docs/convention | instance-fix `= 2` |
| R6 Obsolete without successor | 1 | No | instance-fix |
| R7 Diagnostic on unauthorable/false shape | 2 | DeclaredButInertTests | no change |
| R8 Table/saga/diagnostic drift | 3 | No equality proof | PhaseGraph binds 035↔table, not saga |

**Proof-system finding:** R3 CI/config axis still unguarded after 3+ hits.

## Single-lens findings kept

- L1: empty-name AGWF037 hole; AGWF035 does not join `hasErrors`.
- L3: three identical `GroundTruthCodes` test lists; PublicAPI does not track Obsolete.
- L4: compile-time AONT205 descriptor unreached (no `ReportDiagnostic`).
- L5: IActionBuilder NSubstitute wrong-subject; packaging omits AGWF037 schema requirement.
- L7: CHANGELOG lede vs Residue version mismatch; ErrorResult `resultType: complete` spec-legal question.

## Open questions (run-wide)

- Does under-reach cover a regression of #182/#186 if those emitters drop a start command the IR still has? (Likely no — W1.)
- Is `HandAuthoredContract` assigned by any in-repo or out-of-repo producer?
- Does Renovate resolve the `lvlup-claude` slug after the `exarchos` rename?
- Is `contracts-v0.7.0` published / is `contracts-test` a required check?
- Is AGWF035-without-gating (still emits saga) intentional?
- Is `ErrorResult` with `resultType: complete` protocol-legal?
