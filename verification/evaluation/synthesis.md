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

# Stage 3 synthesis

High-tier refutation used three independent angles (reachability, premise origin, named proof). An obligation is **refuted** when a majority of attempts refute it. Evidence files: `proof-layer-fit.md`, `coverage.md`, `refutation-reachability.md`, `refutation-premise.md`, `refutation-proof.md`, `wildcard.md`.

## Verdict counts (pre-fix ledger)

| Outcome | Count | Slugs |
|---|---|---|
| **Pass** (supported; existing proof holds) | 5 | `agwf035-all-complete-silent`, `agwf035-overreach-preserved`, `agwf037-reject-not-dedup`, `icons-null-when-unset` (null path), `claim-clr-free-xor-docs` |
| **Fail** (survived; claim violated or proof missing) | 6 | `agwf035-catalog-polarity-lie`, `contracts-0-7-0-pack-incomplete`, `descriptor-source-docs-omit-member-2`, `contracts-changelog-contradicts-0-7-0` (lede/package honesty), `requires-obsolete-observable` (in-repo CS0618 silence), `mcp-resulttype-and-pin` (wrap/CI proof gap — not a factory-site miss) |
| **Indeterminate** | 2 | `renovate-resolve-unasserted` (slug after rename), `handauthoredcontract-unreached` (out-of-repo producer) |
| **Refuted** | 13 | listed under `## Refuted` in the ledger (12 slugs + framing-kill of live Renovate apply) |

`agwf035-underreach-ir-not-emission` is a **refinement**, not a fail of T1: the delivered lock is IR-vs-`PhaseGraph`. The #184 saga-emission lock is Option B / out of wave. Recorded as open class G-R1 / G-R8.

## Findings by category

### Refuted

Removed from the active ledger. Discriminating evidence in the refutation files.

| Slug | Killer | Evidence |
|---|---|---|
| `phasegraph-type-not-instance` | reachability (majority) | `PhaseGraph.Build` is a pure function of the unchanged model; two calls cannot drift |
| `agwf035-json-import-unreached` | reachability + premise | Import sets `Loops`/`Branches` null and has no `Finally`; CHANGELOG scopes AGWF035 to C# |
| `agwf035-error-still-emits` | premise | “All Error AGWFs gate emission” was never claimed; gating language is reserved for AGWF037 |
| `schema-diff-skip-succeeds` | reachability + premise | `fetch-depth: 0`; this repo has `v*` tags; workflow unchanged |
| `aont205-analyzer-unreached` | all three | This wave did not claim compile-time AONT205; runtime `ApplyDelta`/`Build` is the control |
| `compat-agwf035-breaking` | reachability + premise | Production `Build` still has rejoin edges; only `WithoutSuccessor` fixtures fail |
| `compat-validtransitions-nonreversing` | reachability + premise | Standing generator contract, not a claim this target made |
| `compat-publicapi-omits-obsolete` | reachability + premise | Duplicate of `requires-obsolete-observable`; RS0016 has no Obsolete column |
| `diagnostic-fork-ctor-open` | majority | Both fronts reject or call `Create`; no production `new` |
| `traversal-result-flags-independent` | majority | Only TraverseTool constructs; flags set together |
| `agwf037-catalog-identity` | majority | `contracts-codegen-guard` already regen-and-diffs this wave’s paths |
| `claim-issue-185-tracker` | all three | Tracker state is not a product path; inventory file already disclaimed it |
| `renovate-resolve-unasserted` (as “bot applies”) | reachability + premise | Path token matches the existing file; live resolve is an unvalidated external premise |

### Refinement

| Slug | Change |
|---|---|
| `agwf035-underreach-ir-not-emission` | Narrow Claim to IR-vs-`PhaseGraph`. Saga-emission lock is out of wave (Option B / third walk). |
| `phasegraph-type-not-instance` | Soften CHANGELOG: shared `Build` algorithm, not one instance. |
| `icons-null-when-unset` | Keep null-when-unset. Drop invented non-null `AddOntologyTools` producer. |
| `handauthoredcontract-unreached` | “Survive merge” is `Actions = hand.Actions`, not `Source = 2`. Member 2 stays additive and inert without a producer (plan-accepted). |
| `mcp-resulttype-and-pin` | Split: factory sites + Hosting 2.2.0 pin hold; four-tool wrap and INV-3-in-CI are proof gaps, not factory misses. |
| `requires-obsolete-observable` | Method + `[Obsolete]` hold. In-repo `CS0618` NoWarn is a silencer, not proof consumers see the warning. |

### Gap

| Text (inventory focus) | Disposition |
|---|---|
| AONT205 field-set widening (two new fields vs old three) | Recorded; no new Active slug this pass (coverage F1). Not a cheap fix. |
| AGWF035 report-dedup shared key | Recorded; accepted one-report-per-name-pair unless a kill fixture appears. |
| `claim-phasegraph-edge-kinds` dropped at synthesis | Identity with `4d060f4` `EdgeBuilder` is an assumption (mechanism: body unchanged). |
| Consumer-before-producer 0.7.0 upgrade-order | Standing DR-18; not a code defect on this branch. |

No second evaluation pass: gaps did not produce a new class of obligations.

### Bias

Give to the author; not settled here.

1. **Leftover-list vs correctness-core.** The wave is #185 leftovers scored as a termination-class audit. T1 can check off while the #184 *emission* class stays open. Stage 0 forbids Option B, which is the only close of that class.
2. **Exarchos is the cost-setting premise and is unobserved.** Pack/catalog obligations inherit that exposure.
3. **Suite-wide CS0618 NoWarn** hides every future obsolete, not only `Requires`.

## Recommended actions (fix vs structural)

| Slug | Label | Action |
|---|---|---|
| `agwf035-catalog-polarity-lie` | **fix** | Widen AGWF035 remediation so under-reach is not the over-reach sentence. Catalog already at 0.7.0. |
| `contracts-0-7-0-pack-incomplete` | **fix** | Assert `agwf-catalog.json` and `AgwfEntryDuplicatePermittedForkTrigger.json` in `PackagingTests`. |
| `descriptor-source-docs-omit-member-2` | **fix** | Add `HandAuthoredContract = 2` to the two lists this wave edited. |
| `contracts-changelog-contradicts-0-7-0` | **fix** | Product lede 0.4.0→0.7.0; Contracts package CHANGELOG names AGWF037. Soften PhaseGraph “cannot drift.” |
| `requires-obsolete-observable` | structural (class open) | Do not un-NoWarn the suite. Class G-R6 is a first instance; no guard owed. |
| `mcp-resulttype-and-pin` | structural (class open) | Factory sites hold. INV-3-in-CI / CPM pin are G-R3/G-R4; not implemented this pass. |
| `agwf035-underreach-ir-not-emission` | structural (class open) | G-R1 / G-R8. Do not implement Option B or a saga-emission lock. |

## Classes left open

See `verification/guards.md`. R1, R2, R3, R4, R5, R7, R8 stay open. R6 appeared once.

## Evidence-file paths

- `verification/evaluation/proof-layer-fit.md`
- `verification/evaluation/coverage.md`
- `verification/evaluation/refutation-reachability.md`
- `verification/evaluation/refutation-premise.md`
- `verification/evaluation/refutation-proof.md`
- `verification/evaluation/wildcard.md`
