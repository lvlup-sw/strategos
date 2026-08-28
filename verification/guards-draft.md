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
lens: 7. Recurrence To Guard
status: draft
external_references:
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/verification/survey/history-and-recurrence.md
    why: Stage 1 recurrence seed (R1–R8)
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/verification/survey.md
    why: Stage 1 synthesis; protected-path gaps this lens converts
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/verification/survey/existing-proof.md
    why: what surrounding checks assert and what they let through
  - path: /home/reedsalus/.claude/skills/verify-code/references/guards.md
    why: specification and earliest-sound-layer rules
---

# Guards draft — Recurrence To Guard

Draft for the orchestrator. Do not treat this file as `verification/guards.md`.
No guard in this file is implemented. No product fix is implemented.

**Rule applied.** A class that appears more than once owes a guard. A fix with no
guard leaves the class open. R6 appeared once and is recorded as a first
instance, not a candidate.

**Reuse before addition.** Prefer the local idioms already in the tree:
INV-5 monotonic AGWF ids, `quality-gates` bash scripts under `scripts/`,
`AgwfCatalog.tsp` + catalog tests, `PhaseGraph` as the route-graph type,
`DescriptorSourceTests` ordinal asserts, `DeclaredButInertTests` as the
reachability pin.

## Ranking (findings removed)

| Rank | Class | Prior hits | Why this rank |
|---|---|---|---|
| 1 | R3 inert-looking control | ≥6 named by #185 | Meta-class. CI/config axis still unguarded. One resolve + INV-3-in-CI job closes the proof-system finding. |
| 2 | R2 silent collision | 4 | Three after-the-fact AGWF ids. Next new key will mint another. |
| 3 | R1 termination under/over-reach | 5 | AGWF035 exists and this diff extends it; it still misses import, emission gating, and saga-true drops. |
| 4 | R8 table / saga / diagnostic drift | 3 | One shared graph instance is the cheaper close for what R1 cannot see. |
| 5 | R4 MCP pin lag | 3 | Deny-list already decayed once. Same job as R3 can carry the policy. |
| 6 | R7 diagnostic on unauthorable / false shape | 2 + R1 operator | Catalog identity tests exist; trigger reachability does not. |
| 7 | R5 enum ordinal / additive split | 2 | This diff avoids the remap. Docs-only on #183. Per-enum tests are not a class guard. |

---

## Per-class conversion

Legend: **earliest rung** is a proof-ladder rung (1–6). **Earliest sound layer**
is the guards.md layer (1–8).

### R1. Termination under-reach vs over-reach

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. Five filed instances (#155, #175, #184, #182, #186) plus the half-closed guard. |
| **Earliest rung** | **3** — structural analysis of the route graph. Rung 1 is available if one `PhaseGraph` *instance* drives diagnostic, `ValidTransitions`, and saga dispatch. Rung 2 cannot express “this step is last on the main flow.” Today’s under-reach positives sit at **4** on a test-injected graph. |
| **Prior fix?** | Yes. #187 shipped the over-reach arm only. #194 fixed the #184/#182/#186 *emitters* and left the route arm deferred. This diff adds the under-reach arm (`5e94af4`) and lifts `PhaseGraph` (`46fb93a`). |
| **Why the prior fix did not prevent the repeat** | The first AGWF035 ship documented the missing arm the same day (#184, #185). A diagnostic that cannot see its motivating shape is R3 applied to INV-5. Emitter fixes without the route arm left the class open by design. |
| **Existing guard on the protected path?** | **Half, and not on every protected path.** `TerminalReachabilityGuard.Report` is called from `WorkflowIncrementalGenerator.cs:1038` with `MainFlowClassification` and **no** `phaseGraph` (rebuilds via `PhaseGraph.Build(model)`). It is **not** called from JSON import. AGWF035 does **not** join `hasErrors` at `:933–938`, so Error still emits a saga. Call-site scan (`Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline`) checks only argument `[1]` (`MainFlowClassification`); omitting `phaseGraph` still passes. |
| **Kill fixture to reuse?** | **Yes, for the function, not the composition.** Over-reach: `Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug` (#155 `AssessDamage`). Under-reach: `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires`, `Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires` — both inject `PhaseGraph.WithoutSuccessor`. Negative: `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire`. Missing: a generator-path under-reach twin that does *not* inject a graph; a JSON-import twin; an approval-dispatch (#182/#186) twin. |
| **Class open after this diff?** | **Yes.** Extending the guard is not closing it. See `recur-termination-route-complete`. |

### R2. Duplicate trigger / first-wins-dedup

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. Four production instances (#156.2, #189, #190, #191) plus ontology’s existing ClrType reject. |
| **Earliest rung** | **3** — reject on a keyed declaration. Rung 2 (CS0152 / CS0111) caught some shapes and missed last-write-win dictionaries. Rung 1 if uniqueness is derived from one declaration list. |
| **Prior fix?** | Yes, per incident. #194 retargeted AGWF003 (`EffectiveName` / BranchPath) and added AGWF036 (path-end type). This diff adds AGWF037 for `PermitTrigger`. Ontology already rejected first-wins on `ClrType`. #156.2 was left as CS0152 until this diff. |
| **Why the prior fix did not prevent the repeat** | Each fix closed one key. Collision-reject is not a standing policy. Review (CodeRabbit on #187, then #154) kept finding the same operator on the next map. First-writer-wins “looks like collision handling” (#189). |
| **Existing guard on the protected path?** | **Per-id, not one policy.** AGWF003 / 036 / 037 run on their keys. AGWF037 runs on C# extract **and** JSON import and **does** suppress saga emission (`hasDuplicatePermittedForkTrigger` at `WorkflowIncrementalGenerator.cs:930–938`). No shared “any colliding key ⇒ reject” mechanism. The next `dict[name] =` site is uncovered. |
| **Kill fixture to reuse?** | **Yes.** `CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga`; `Extract_DuplicatePermitTrigger_RejectsEdgeAndReportsAgwf037`; `ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga`; `DiagnosticForkModelTests.Create_WithDuplicateTrigger_ThrowsArgumentException`; ontology first-wins tests. #189 last-writer on `successorWithinPath` is the historical fixture for a *different* key. |
| **Class open after this diff?** | **Yes.** AGWF037 is an instance guard. The class is first-wins on any keyed declaration. See `recur-collision-reject`. |

### R3. Inert-looking control

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. The meta-class. Six named by #185, plus #147, #133, #145, #163, #180. |
| **Earliest rung** | **3** — a check that the control is reachable / resolves. Types cannot express “this URL 200s” or “this job ran.” Live Renovate apply is **rung 6 / layer 8** (external process). |
| **Prior fix?** | Partial. #187 hardened substring-parity into a real test parse. #188/#194 tracked the INV-3 catalog and widened the deny-list. #194 recycled the host leftover inbox. This diff’s T3 (`334f64c`) is **instance-fix-only** (path token). T4 extends INV-3 Checks 3.4 / 3.5 as **documented greps**, not a CI job. |
| **Why the prior fix did not prevent the repeat** | A config that parses is how #181 went unnoticed. Grep deny-list returning zero on a stale tree is how #178 went unnoticed. The decay rule applies: another correct path token is not the fix. The old 404 `extends` was **not** kept as a fixture. |
| **Existing guard on the protected path?** | **Parity and catalog-tracking: yes. CI/config axis: no.** `quality-gates` (`ci.yml:169–186`) runs AGAG / catch / prose only. `rg` of `.github/workflows` for INV-3 / renovate-resolve: no job. #147 unused OIDC: **out of wave** — do not invent a Trusted-Publishing obligation. |
| **Kill fixture to reuse?** | **Partial.** Parity: the `[Skip]`-ed #155 twin (DR-5). MCP: `AssertResultTypeComplete`, `OntologyToolDescriptor_WithoutIcons_DoesNotInventPlaceholder`. Renovate 404 `extends`: **missing** (old path deleted). INV-3: documented greps, no checked-in failing subject. |
| **Class open after this diff?** | **Yes.** Path edit without a resolve-check leaves the class open. See `recur-inert-control-resolves`. |

### R4. MCP protocol pin lag

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. #166 docs-only re-pin; #176 `resultType`; #177 `icons` (not a revision delta); #178 stale deny-list (12 sites). |
| **Earliest rung** | **1** in principle (pin, deny-list, and emitted shape derived from one revision document). **Situational: no such generator.** Cheapest sound here is **3** — executed deny-list + assignment scan. Check 3.4 as `grep -L ResultType` is **not** sound (a comment satisfies). |
| **Prior fix?** | #188/#194 re-pinned the catalog and widened the deny-list. Code shape unchanged until this diff (Hosting 2.2.0 + `ResultType` + optional `Icons`). Checks 3.4 / 3.5 added to `deterministic-checks.md`. |
| **Why the prior fix did not prevent the repeat** | Documentation-only re-pin (#166) is how #176/#177 were missed. Check 3.3 failed as a guard once (stale deny-list → zero hits). The greps still do not run in CI. |
| **Existing guard on the protected path?** | Check 3.3 exists as a human recipe. It does **not** run on `quality-gates`. Hosting tests cover traverse wire `resultType` and factory icons-null. No test asserts the production csproj pin is 2.2.0 (tests carry their own `VersionOverride`). |
| **Kill fixture to reuse?** | `AssertResultTypeComplete` (MapTraversal / Error / sibling mappers). `CreateServerTool_WithIcons_MapsOntoProtocolTool`. **Missing:** a fixture file that contains a denied revision and must fail Check 3.3; a `new CallToolResult` that omits `ResultType` in a file that already mentions the identifier. |
| **Class open after this diff?** | **Yes.** Checklist extension is not a guard. See `recur-mcp-pin-bound`. |

### R5. Enum ordinal / additive split

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. #183 (generated `{Workflow}Phase` reorder / Newtonsoft ordinal) and the avoided #163 remap (`HandAuthoredContract = 1` would have moved `Ingested`). #156.3 is the same class and **out of wave**. |
| **Earliest rung** | **2** — published numeric values unrepresentable except as append. `DescriptorSourceTests` (`Ingested == 1`, `HandAuthoredContract == 2`) is rung 4 restating a rung 2 fact for **one** enum. |
| **Prior fix?** | #194: document the Newtonsoft condition (`phase-persistence.md`). No machine guard that Phase member order is frozen. This diff appends `= 2` and does not move `Ingested`. |
| **Why the prior fix did not prevent the repeat** | Docs-only on #183. The #163 original proposal would have remapped under the same convention. Remarks on `DescriptorSource` (“new members are appended”) are an unenforced comment. |
| **Existing guard on the protected path?** | **Convention + one enum’s tests.** PublicAPI tracks member *names*, not ordinals. No append-only check for published enums as a class. Phase enum still docs-only. |
| **Kill fixture to reuse?** | `DescriptorSourceTests` is a **snapshot**, not a kill. The #163 body (insert-at-1) is the kill, living only in the ticket. AONT205 retarget tests prove the *new* value’s semantics, not ordinal freeze. |
| **Class open after this diff?** | **Yes.** Avoiding the remap is a fix with no class guard. See `recur-enum-ordinal-frozen`. |

### R6. Obsolete without successor — first instance, no guard owed

| Question | Answer |
|---|---|
| **Appeared more than once?** | **No.** One filed instance (#115). Related to R3 / R7 (inert without a producer) but not the same class. |
| **This diff** | Instance-fix-only. `[Obsolete("Use ActionDescriptor.Preconditions … There is no fluent successor.")]`. |
| **Guard candidate?** | Not under the “found twice” rule. Record as a first instance. A later second Obsolete-that-names-a-missing-type becomes R6 and then owes a guard (every `[Obsolete]` names a reachable successor or an explicit “no successor” token). |

### R7. Diagnostic aimed at an unauthorable or already-false shape

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. AGWF022 false positives (already-lowered confidence) and the unauthorable instance-named+configure-lambda retarget; AGWF035 over-reach-only as the same operator. |
| **Earliest rung** | **3** — catalog closure: every Error id has at least one compiling public-DSL (or import) trigger. Rung 1 if `kill_fixture` is a required catalog field. `AgwfCatalogParityTests` / `AgwfSingleSourceTests` check **identity**, not reachability. |
| **Prior fix?** | #187 deleted false-positive blocks, retargeted AGWF022 at approval-preceding confidence (authorable), hardened parity to require a running test *and* that a deferred entry’s cited diagnostic actually fires. |
| **Why the prior fix did not prevent the repeat** | The fix pinned **one** live target (`DeclaredButInertTests`). It did not require every catalog Error to have an authorable kill. AGWF035 then shipped as a present diagnostic that could not see #184. |
| **Existing guard on the protected path?** | `DeclaredButInertTests` pins AGWF022. Parity guard requires cited diagnostic to fire for *deferred* entries. No catalog-wide “Error ⇒ compiling kill fixture” check. |
| **Kill fixture to reuse?** | `DeclaredButInert_ApprovalPrecedingStepConfidence_ReportsAgwf022` + `_LowersNoConfidenceGate`. AGWF035 under-reach fixtures (R1) are the kill for the half-closed diagnostic — they do **not** compile a public-DSL source that the *generator* rejects (they inject `WithoutSuccessor`). |
| **Class open after this diff?** | **Yes.** Completing AGWF035 does not add the catalog rule. See `recur-diagnostic-trigger-reachable`. |

### R8. Published table / saga / diagnostic drift

| Question | Answer |
|---|---|
| **Appeared more than once?** | Yes. #175 (flat linear chain / sibling exclusive paths), #184 (table declared `BranchOnExit` the saga could not take), #189 (table listed three successors; saga can emit one). |
| **Earliest rung** | **1** — one graph generates table, diagnostic, and saga dispatch. Type-share (`46fb93a`) is rung 2 for the *type*, not the *instance*. Survey backbone §1: production builds twice (`TransitionsEmitter.cs:56`, `TerminalReachabilityGuard.cs:127`). Generator `Report` at `:1038` does not pass a graph. |
| **Prior fix?** | #187: construct-aware `PhaseGraph` *inside* `TransitionsEmitter`. #194: saga emission for loop-exit. This diff extracts the type and binds AGWF035 under-reach to that type. **Does not bind the saga.** |
| **Why the prior fix did not prevent the repeat** | Each emitter kept its own construction. Sharing a type lets a later edit rebuild a different graph. The #184 class (IR correct, saga forgets `Start{Finally}`) stays silent because under-reach compares IR rejoin dispatchers to `PhaseGraph`, not saga emission. |
| **Existing guard on the protected path?** | No equality proof that saga edges ⊆ `ValidTransitions` ⊆ diagnostic graph. |
| **Kill fixture to reuse?** | `WithoutSuccessor` is a **diagnostic** kill, not a saga/table equality kill. Historical #175 / #184 / #189 shapes are the fixtures to keep. Missing: a twin whose IR has `PayClaim→CloseClaim` and whose emitted saga omits `StartCloseClaim`. |
| **Class open after this diff?** | **Yes.** Type-share without instance-share leaves the class open. See `recur-graph-representations-agree`. |

---

## Specified guard candidates

### G-R1 — Complete AGWF035 on every emission path

| Field | Specification |
|---|---|
| **Class** | A declared `Finally<T>` is not the saga’s termination: a main-flow step chains past it (over-reach), or a rejoin last step never starts it (under-reach). A present AGWF035 that cannot see one of those arms, or that reports Error and still emits a saga, is the same class. |
| **Policy** | Data file (extend `AgwfCatalog.tsp` entry AGWF035 or a sibling policy JSON the catalog test reads): `arms: [over-reach, under-reach]`; `required_call_sites: [WorkflowIncrementalGenerator, JSON-import emit]`; `required_graph_arg: PhaseGraph`; `gates_emission: true`; `silent_when: all-terminal-exclusive-paths`. Policy is data, not a sentence inside `TerminalReachabilityDiagnosticTests`. |
| **Mechanism** | Keep `TerminalReachabilityGuard` (earliest sound layer: **3**, state-machine restriction on the route graph). Pass one `PhaseGraph.Build(model)` instance into both `Report` and `TransitionsEmitter`. Call `Report` from the JSON import emit path. Include AGWF035 in `hasErrors` so Error suppresses `Saga.g.cs` the way AGWF037 already does. Extend `Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline` to require a `phaseGraph` argument and an import call site. |
| **Kill fixture** | Reuse `Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug` (over-reach / #155). Add a **generator-path** twin of `Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires` that does not inject `WithoutSuccessor` — a compiling public-DSL (or import JSON) source whose IR lacks `PayClaim→CloseClaim`. Keep `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` as the false-positive trap. Do not delete the injected-graph tests; they remain the unit kill of the function. |
| **Self-test** | Deleting the `TerminalReachabilityGuard.Report` call, omitting `phaseGraph`, or removing AGWF035 from `hasErrors` turns the call-site scan and the no-saga import/C# twins red. A skip, crash, or missing generator tree is fail (not pass). If the walk of `src/Strategos.Generators` finds zero files, throw (already fail-closed at tests `:703–704`). |
| **Protected path** | `ci.yml` `build-test` → `Strategos.Generators.Tests`. Blocks merge of a `[Workflow]` or `*.workflow.json` that violates the policy. Blocks saga emission when AGWF035 fires. |
| **Exceptions** | None for product workflows. A dated allowlist entry (`owner`, `expiry`) in the policy file is required to silence one arm. Approval-dispatch drops (#182/#186) are **not** exceptions — they are R8 if the IR still has the edge. |

```text
class: R1 termination under/over-reach
first instance: #155 / #175 (over-reach, closed #187)
second instance: #184 (under-reach; AGWF035 blind by construction)
earliest sound layer: 3 (state-machine on PhaseGraph); move to 1 when one instance drives saga too (G-R8)
policy data location: AgwfCatalog.tsp AGWF035 + required_call_sites list
mechanism: TerminalReachabilityGuard on C# and JSON emit; hasErrors gates emission
kill fixture: Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug; generator-path rejoin twin (to add)
guard self-test: call-site scan requires phaseGraph + import site; no-saga on AGWF035
protected paths: Strategos.Generators.Tests via ci.yml build-test
pass signal: guard ran on this revision’s generator and import; no AGWF035 or all-Complete negative
fail signal: AGWF035 on a rejoin/over-reach subject, or saga emitted despite AGWF035
indeterminate signal: generator sources unreadable; import fixture missing; walk throws
resource limits: in-process generator driver; no Postgres
temporary exceptions: none without owner+expiry
owner: workflow-generator / INV-5
expiry: none (standing)
```

### G-R2 — Collision rejects; first-wins is unwritable

| Field | Specification |
|---|---|
| **Class** | Two declarations that share a key (trigger, step name, path-end type, or the next IR map key) resolve by first-wins / last-write-win. Different payloads are dropped in silence. CS0152 / CS0111 after emit is not this guard. |
| **Policy** | Policy JSON (new, next to the catalog or under `src/Strategos.Generators/Diagnostics/`): one row per collision key `{ id, key, scope, authoring: [csharp, json] }`. Seed rows: AGWF003 `EffectiveName` / workflow; AGWF036 `pathEndType` / exclusive-path; AGWF037 `PermitTrigger` / diagnostic-fork-edge. Adding a new keyed IR map requires a new row **before** the map ships. INV-5 monotonic ids stay; the policy is the standing rule, the id is the consumer-facing handle. |
| **Mechanism** | One reject helper used by every keyed IR write (the ontology `ClrType` reject is the local precedent). A rung-3 architecture test forbids `dict[k] =` / last-writer assignment on IR maps that are not listed in the policy as `intentional_first_match` (the #174 triage operator is the only seeded intentional). Per-id AGWF diagnostics remain the emission. Do not invent a fourth id for a key that already has one. |
| **Kill fixture** | Reuse the AGWF037 twins (`CsharpTwin_DuplicatePermitTrigger_FiresAgwf037AndEmitsNoSaga` — same trigger, different evidence `stampId` / `otherStampId`; `ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga`; `Create_WithDuplicateTrigger_ThrowsArgumentException`). Reuse ontology first-wins tests. Historical #189 `successorWithinPath[stepName] =` is the fixture that the *policy scan* must still reject if that assignment returns. |
| **Self-test** | A fixture IR map that last-write-wins a listed key, and a fixture that last-write-wins an *unlisted* key, both fail the architecture test. Deleting the helper or the policy file is fail, not skip-and-pass. Empty trigger names remain skipped (current extractor behavior) and must be listed as a policy exception, not silently dropped. |
| **Protected path** | Generator + JSON import (already true for AGWF037) + ontology graph builder. `build-test` and ontology test projects. |
| **Exceptions** | `#174` title-only first-match: `owner` = issue-triage, `expiry` = standing-intentional, `reason` = “first match wins, title only.” Empty trigger names: owner = generator, expiry = until a dedicated empty-name diagnostic exists. |

```text
class: R2 first-wins / silent collision
first instance: #156.2 PermitTrigger (CS0152 only)
second instance: #189 successorWithinPath last-writer
earliest sound layer: 3 (forbidden last-write on keyed IR); compiler CS0152 is not dedicated
policy data location: collision-keys.json (to add)
mechanism: shared reject helper + architecture test over IR map writes
kill fixture: AGWF037 C#/import twins; ontology ClrType tests; #189 assignment if restored
guard self-test: unlisted last-write-win fails the scan; missing policy file fails
protected paths: C# extract, JSON import, OntologyGraphBuilder
pass signal: listed keys reject with their AGWF/AONT id; saga suppressed
fail signal: two declarations share a listed key and one payload is kept
indeterminate signal: policy file unreadable; scan crashed
resource limits: compile-time / unit
temporary exceptions: #174 intentional first-match; empty trigger names (dated)
owner: INV-5 catalog
expiry: none (standing)
```

### G-R3 — A control that looks present must resolve

| Field | Specification |
|---|---|
| **Class** | A control that looks present (config `extends`, gate, diagnostic, catalog, publish scaffold, test oracle) is unreachable or blind. A green CI row or a checked-in file is then not evidence it works. |
| **Policy** | Policy YAML consumed by a new `scripts/check-control-resolve.sh` (same idiom as `scripts/check-agag-hygiene.sh`): `renovate.extends[]` must resolve; `inv3.checks[]` must be invoked by `quality-gates`; `deny_list_fixture` path must exist and must fail Check 3.3 when present. Do not encode the rule as prose in `deterministic-checks.md` only. |
| **Mechanism** | Rung **3**, run on `quality-gates` (`ci.yml:169`). For each `renovate.json` `extends` token: resolve via GitHub contents API (or documented equivalent) and require HTTP 200. Job failure on non-200 or on request error (indeterminate ≠ pass). Invoke INV-3 Checks 3.1–3.5 from the same job (shared with G-R4). Require the deny-list self-test fixture (G-R4) so a stale deny-list cannot return zero and look clean. |
| **Kill fixture** | Reconstruct the deleted #181 subject: `verification/fixtures/renovate.404-extends.json` with `local>lvlup-sw/lvlup-claude:renovate-config/presets/dotnet.json` (the 2026-01-09 token). The resolve script must reject that file. Parity’s `[Skip]`-ed #155 twin remains the substring-oracle kill (already in-tree). **Do not** treat the current `renovate.json` path edit as the kill — the class is already “fixed” on HEAD and the old subject was deleted. |
| **Self-test** | A unit/script mode `--self-test` runs the 404 fixture and expects non-zero. If that mode is deleted, or if `quality-gates` drops the step, a YAML-presence test (reuse `AgwfCodegenGuardTests` shape **but** parse the job steps, do not `Contains` a comment) goes red. A contents-API outage is **indeterminate** (job fails, distinct message), not pass. |
| **Protected path** | `ci.yml` `quality-gates`, every PR and push to main. Blocks merge of a 404 `extends` or a skipped INV-3 step. |
| **Exceptions** | Live Renovate *apply* (Dependency Dashboard / first PR) is **layer 8**. Name the owner: repository maintainer. That proof cannot be encoded in this repo; #181 remains open until a human records it. #147 unused OIDC / Trusted Publishing is **out of wave** — list it as a remaining class member, do not implement a portal guard here. #133 / #174 live-issue proof: same, out of wave. |

```text
class: R3 inert-looking control (CI/config axis)
first instance: #181 renovate 404 extends (~7 months)
second instance: #178 stale INV-3 deny-list (zero hits on a stale tree)
earliest sound layer: 3 (resolve + job invocation); live bot apply is layer 8
policy data location: scripts/policies/control-resolve.yaml (to add)
mechanism: check-control-resolve.sh on quality-gates; INV-3 checks invoked not documented
kill fixture: reconstructed 404 extends token; parity Skip-twin
guard self-test: --self-test on 404 fixture must fail; missing job step fails
protected paths: ci.yml quality-gates
pass signal: every extends token 200; INV-3 steps ran; deny-list fixture failed Check 3.3
fail signal: 404 or stale deny-list or INV-3 step absent
indeterminate signal: GitHub API error / timeout (must not become pass)
resource limits: one contents GET per extends token; fail on timeout
temporary exceptions: none for 404 extends
owner: platform / #181
expiry: live-apply proof remains human-owned until recorded
```

### G-R4 — MCP pin, deny-list, and emitted shape stay one revision

| Field | Specification |
|---|---|
| **Class** | INV-3 “latest non-draft MCP revision, never the LCD subset.” The pin, the deny-list, and the emitted `CallToolResult` / tool shape drift independently. A docs-only re-pin or a file-level substring grep is this class. |
| **Policy** | Lift Checks 3.1–3.5 from `deterministic-checks.md` into `scripts/policies/inv3-mcp.yaml`: `current_revision: 2026-07-28`; `denied_revisions: [2024-11-05, 2025-03-26, 2025-06-18, 2025-11-25]`; `call_tool_result_requires: ResultType` **per construction**, not per file; `descriptor_requires: Icons` (optional, null when unset); `hosting_package_pin: 2.2.0`. |
| **Mechanism** | Rung **3**, same `quality-gates` job as G-R3 (reuse). Assignment scan: every `new CallToolResult` / `new()` inferred as one must have `ResultType =` in that initializer. Check 3.4 as `grep -L ResultType` is **rejected** as the mechanism (comment / unused identifier satisfies). A test asserts Hosting **production** csproj `VersionOverride` is the policy pin (tests’ own override is the wrong subject). |
| **Kill fixture** | `AssertResultTypeComplete` (reuse). Add `verification/fixtures/inv3-denied-revision.md` containing `2025-11-25` under a path Check 3.3 scans — Check 3.3 must fail on that fixture. Add a construction-site fixture that mentions `ResultType` in a comment and omits the assignment — the assignment scan must fail. |
| **Self-test** | The denied-revision fixture is committed and is **expected to fail** Check 3.3 when pointed at. A wrapper asserts that pointing the check at the fixture exits non-zero; pointing it at production sources exits zero. If the wrapper or the fixture is deleted, the job fails. |
| **Protected path** | `quality-gates` + `Strategos.Ontology.MCP.Hosting.Tests` (wire `resultType` on traverse; extend to every constructed `CallToolResult` path the factory owns). |
| **Exceptions** | `ErrorResult` + `resultType: complete` legality is an **open protocol question** (survey L7). Do not exempt it in silence; if exempted, owner = MCP hosting, expiry dated, reason cites the spec clause. |

```text
class: R4 MCP protocol pin lag
first instance: #166 docs-only re-pin
second instance: #176 resultType omitted; #177 icons gap; #178 stale deny-list
earliest sound layer: 1 if generated from one revision doc (situational gap); 3 executed here
policy data location: scripts/policies/inv3-mcp.yaml (to add)
mechanism: assignment scan + deny-list grep + production pin assert, on quality-gates
kill fixture: AssertResultTypeComplete; denied-revision fixture; comment-only ResultType fixture
guard self-test: Check 3.3 against denied-revision fixture must fail
protected paths: quality-gates; Hosting tests
pass signal: no denied revision in scoped sources; every construction assigns ResultType; pin matches policy
fail signal: stale pin, omitted ResultType assignment, or deny-list miss
indeterminate signal: policy file missing; grep crashed
resource limits: repo grep
temporary exceptions: ErrorResult complete-discriminator (needs spec cite + expiry)
owner: INV-3
expiry: none (standing)
```

### G-R5 — Published enum numerics only append

| Field | Specification |
|---|---|
| **Class** | A published enum’s numeric values are a persistence / wire contract. Inserting or reordering members remaps stored data. Docs that say “append only” are not this guard. |
| **Policy** | Frozen ordinal map as data: `{ type, member, value }[]`. Seed: `DescriptorSource.HandAuthored=0`, `Ingested=1`, `HandAuthoredContract=2`. PublicAPI continues to track names. Do **not** add `#156.3` `DiagnosticForkCount_{i}` rows in this wave. `{Workflow}Phase` document-order freeze is a second policy file owned by the generator (the #183 instance) — include it; it is already in-wave history. |
| **Mechanism** | Rung **2** / **3**. Keep `DescriptorSourceTests` as the snapshot for that enum. Add a generator/architecture test: for each seeded type, `(int)member` equals the policy value; a new member must be appended (max existing + 1) or the policy file updated in the same change. For generated `{Workflow}Phase`, lock that `StepNames` document-order is not the emit order, or that emit order is snapshotted — whichever matches `phase-persistence.md` once encoded as data. |
| **Kill fixture** | Construct the #163 proposal as a fixture enum (or a source fragment) `{ HandAuthored = 0, HandAuthoredContract = 1, Ingested = 2 }` — the guard must reject the moved `Ingested`. Do not delete `DescriptorSourceTests`; they are the current snapshot, not the kill. |
| **Self-test** | Running the guard against the insert-at-1 fixture fails. Deleting the policy file or the test fails the suite. PublicAPI-only membership (name without ordinal) is **not** a pass signal. |
| **Protected path** | `Strategos.Ontology.Tests` + generator tests for Phase emit. `builder-api-stability` does **not** cover this surface today; do not pretend it does. Either extend that job’s scope or keep the test in `build-test`. |
| **Exceptions** | None for `DescriptorSource`. Phase Newtonsoft-vs-STJ host difference: owner = persistence docs, expiry standing, reason = “Marten host uses STJ names; Newtonsoft ordinal consumers are warned.” That exception does not waive the freeze. |

```text
class: R5 enum ordinal / additive split
first instance: #183 Phase reorder (docs-only close)
second instance: avoided #163 Ingested remap
earliest sound layer: 2 (frozen ordinals) / 3 (policy snapshot)
policy data location: frozen-enum-ordinals.json (to add)
mechanism: snapshot test + append-only check; keep DescriptorSourceTests
kill fixture: insert-at-1 DescriptorSource fragment from #163
guard self-test: insert-at-1 fixture fails; missing policy fails
protected paths: Ontology.Tests; generator Phase emit tests
pass signal: seeded (member, value) pairs hold; new member is max+1
fail signal: any seeded value moved
indeterminate signal: policy unreadable
resource limits: compile-time
temporary exceptions: none that move Ingested
owner: ontology public API / generator Phase
expiry: none
```

### G-R7 — Every catalog Error has an authorable kill fixture

| Field | Specification |
|---|---|
| **Class** | A diagnostic or gate is declared, but no compiling authoring path can trigger it — or it fires on a shape that is not inert. A present diagnostic is then an INV-5 violation. |
| **Policy** | Catalog field `kill_fixture` (path + expected id) on every AGWF/AONT **Error**. Warning/info may omit. Policy is the catalog, not a list inside `AgwfCatalogParityTests`. |
| **Mechanism** | Rung **3** (catalog closure). Extend `AgwfCatalogParityTests` / a sibling: for each Error id, the named fixture compiles from the public DSL or JSON import **through `GeneratorTestHelper.RunGenerator` or import emit**, and reports that id. Identity parity stays. A fixture that only calls `Report(..., WithoutSuccessor)` does **not** satisfy this field for that id (it may remain as a unit test). |
| **Kill fixture** | Reuse `DeclaredButInert_ApprovalPrecedingStepConfidence_ReportsAgwf022`. AGWF035 needs a **generator-path** public-DSL kill (same gap as G-R1). A catalog entry with `kill_fixture` pointing at a missing file is itself a fail. |
| **Self-test** | A committed catalog-shadow fixture that adds `AGWF999` Error with no `kill_fixture` (or a missing path) must fail the closure test. Deleting the closure test while Errors remain is a fail (pair with a codegen/catalog count assert already in-tree). |
| **Protected path** | `Strategos.Generators.Tests` + `Strategos.Contracts.Tests` catalog identity. Runs on `build-test` / `contracts-test`. |
| **Exceptions** | Deferred catalog entries already require the cited diagnostic to fire (existing parity). A new Error may ship with a dated exception (`owner`, `expiry` ≤ 14 days) only if the issue that specified it is linked. AGWF035 over-reach-only was that exception and expired the day #184 filed. |

```text
class: R7 diagnostic on unauthorable / already-false shape
first instance: AGWF022 false positives + unauthorable retarget
second instance: AGWF035 over-reach-only (#184)
earliest sound layer: 3 (catalog closure); 1 if kill_fixture is a required TypeSpec field
policy data location: AgwfCatalog.tsp kill_fixture (to add)
mechanism: catalog Error ⇒ RunGenerator/import fixture fires that id
kill fixture: DeclaredButInert AGWF022; generator-path AGWF035 (to add)
guard self-test: AGWF999-without-fixture shadow fails
protected paths: Generators.Tests; Contracts.Tests
pass signal: every Error id has a compiling public-path kill that reports it
fail signal: Error id with no reachable trigger, or trigger is a test double only
indeterminate signal: catalog unreadable; TypeSpec compile failed
resource limits: in-process generator
temporary exceptions: dated ≤14 days, issue-linked
owner: INV-5
expiry: none
```

### G-R8 — One route graph, three consumers

| Field | Specification |
|---|---|
| **Class** | `ValidTransitions`, Mermaid, saga handlers, and AGWF035 are independent constructions of the same route graph and disagree. Type-share without instance-share is this class. |
| **Policy** | `representations: [ValidTransitions, AGWF035, SagaDispatch]` must be derived from one `PhaseGraph` instance per emit. Equality: `saga_edges ⊆ table_edges` and `table_edges == diagnostic_edges`. Mermaid is a fourth consumer if it still builds separately — include it in the policy list once confirmed. |
| **Mechanism** | Earliest sound layer: **1** (one graph generates all three). Until the saga emitter consumes the same instance, a rung-3/4 generated conformance fixture compares extracted saga `Start{X}` / complete edges to `PhaseGraph.SuccessorsOf` and to the emitted `ValidTransitions` source. Build the graph once in the generator and pass it in (closes the survey backbone §1 lie that “share one PhaseGraph” is instance-share). |
| **Kill fixture** | Historical #184: table declared `BranchOnExit`; saga did not publish `Start{Finally}`. Reconstruct as a twin: IR/graph has `PayClaim→CloseClaim`; strip only the saga emit (not `WithoutSuccessor`). #175 sibling-exclusive-path chain is the table-over-generation kill (already what over-reach AGWF035 covers if table and diagnostic share an instance). #189 three-successor table vs one-successor saga is the third fixture. |
| **Self-test** | Mutating `SagaEmitter` to drop `StartCloseClaim` while `PhaseGraph` still has the edge fails the conformance fixture. Unwiring `phaseGraph` from `Report` fails G-R1’s call-site scan **and** this equality (diagnostic rebuilds, saga does not). |
| **Protected path** | Generator emit path for every `[Workflow]` in `Strategos.Generators.Tests`. Not JSON-import-only; not `Report` unit-only. |
| **Exceptions** | None. A construct that legitimately has zero Finally edges (all-`Complete()` exclusive paths) is a **policy case**, not an exception — the graph already encodes it; G-R1’s negative fixture is the lock. |

```text
class: R8 table / saga / diagnostic drift
first instance: #175 transitions table chained exclusive siblings
second instance: #184 table declared an edge the saga cannot take
earliest sound layer: 1 (one PhaseGraph instance); 3/4 equality until saga consumes it
policy data location: generator emit contract (representations list)
mechanism: build once, pass in; conformance: saga ⊆ table == diagnostic
kill fixture: reconstructed #184 saga-omit-StartFinally; #175/#189 historical
guard self-test: saga emit drop with intact IR fails
protected paths: WorkflowIncrementalGenerator emit
pass signal: three consumers read one instance; equality holds
fail signal: any extra or missing edge in one consumer
indeterminate signal: emit failed before comparison
resource limits: in-process generator
temporary exceptions: none
owner: generator / INV-1
expiry: none
```

---

## Classes that stay open

Every recurring class stays **open**. This diff’s instance work does not close a class.

| Class | Why it stays open | Obligation |
|---|---|---|
| **R1** | Guard extended, not completed. No `phaseGraph` at the production call site; no JSON import; Error does not join `hasErrors`; under-reach kills inject `WithoutSuccessor`; #182/#186 not covered if IR is correct. | `recur-termination-route-complete` |
| **R2** | AGWF037 is a third per-key id. Collision-reject is still not a standing policy. | `recur-collision-reject` |
| **R3** | T3 is a path token. No resolve-check. INV-3 greps are not a CI job. Old 404 fixture was deleted. Live Renovate apply is human-owned. | `recur-inert-control-resolves` |
| **R4** | Checks 3.4/3.5 are human recipes and file-level substrings. Deny-list already decayed. No production-pin assert. | `recur-mcp-pin-bound` |
| **R5** | `= 2` avoids the remap. `DescriptorSourceTests` freeze one enum. Phase (#183) still docs-only. No append-only policy. | `recur-enum-ordinal-frozen` |
| **R6** | First instance. No guard owed. The Obsolete text is a fix. | *(none — not a recurring class)* |
| **R7** | `DeclaredButInertTests` pins AGWF022. Catalog identity ≠ trigger reachability. | `recur-diagnostic-trigger-reachable` |
| **R8** | `PhaseGraph` is type-share. Saga unbound. Diagnostic rebuilds. | `recur-graph-representations-agree` |

**Human control that replaces a layer-8 remainder.** Live Renovate apply (#181 acceptance) and unused-OIDC / Trusted Publishing (#147, out of wave): repository maintainer. The in-repo guard (G-R3) can only prove the preset **resolves**, not that the GitHub App **applied** it.

---

## Open-class obligations (ledger format)

Evidence files: `verification/obligations/{slug}.md`.

### [recur-termination-route-complete] — AGWF035 closes both arms on the shipped emit path

| | |
|---|---|
| **Claim** | A declared `Finally<T>` is the saga’s termination on every authoring path the generator ships, or AGWF035 fires and suppresses emission. |
| **Scope** | `TerminalReachabilityGuard`, `WorkflowIncrementalGenerator` (`:1038`, `:933–938`), JSON import emit, `PhaseGraph`. |
| **Consequence** | A consumer compiles a saga that runs past termination or never starts it. Observed at runtime as stall / unbounded cycle (`grep -c MarkCompleted = 0` on #175) or a parked instance (#184/#182/#186). |
| **Proof rung** | Deterministic structural analysis. |
| **Proof artifact** | G-R1: shared `PhaseGraph` instance, import call site, `hasErrors` membership, generator-path kill fixtures. |
| **Why not cheaper** | Generation (rung 1) is not available until saga consumes the same instance (G-R8). The compiler cannot express main-flow lastness. |
| **Failure signal** | Nothing in production. AGWF035 is compile-time. A missed arm is a green build. |
| **Rollback** | Revert the generator commits. Does not reverse already-emitted consumer sagas until rebuild. |
| **Lenses** | 7 Recurrence To Guard; survey 1, 5, 6. |

**Open questions:**

- Does the under-reach arm fire on a regression of #182 / #186 if those emitters drop a start command the IR still has? Survey scopes the arm to constructs marked rejoin. `(partial: production Report rebuilds PhaseGraph from the model, so an IR-correct drop stays silent)`

### [recur-collision-reject] — Keyed declarations reject collisions

| | |
|---|---|
| **Claim** | Two declarations that share a collision key are rejected with a catalog id. First-wins and last-write-win are unwritable on listed keys. |
| **Scope** | Generator IR maps, JSON import, `DiagnosticForkExtractor`, `OntologyGraphBuilder` ClrType bind. |
| **Consequence** | The kept payload is arbitrary. The dropped evidence schema, successor, or terminality is silent. Consumer sees CS0152/CS0111 or a wrong route. |
| **Proof rung** | Deterministic structural analysis. |
| **Proof artifact** | G-R2 policy JSON + shared reject helper. Existing AGWF003/036/037 are instance proofs, not the class proof. |
| **Why not cheaper** | Compiler diagnostics are incidental and miss dictionary last-write-win. No generator currently derives uniqueness for every IR map. |
| **Failure signal** | Sometimes a C# compiler error after emit. Often nothing (last-write-win). |
| **Rollback** | Revert the per-id diagnostic. A published AGWF id does not reverse. |
| **Lenses** | 7 Recurrence To Guard; survey 6. |

**Open questions:**

- None that block specifying the standing policy. Whether the next collision key already exists outside this wave was not inventoried.

### [recur-inert-control-resolves] — Present CI/config controls resolve

| | |
|---|---|
| **Claim** | Every declared `renovate.json` `extends` token resolves, and every INV-3 deterministic check that the skill documents is invoked by `quality-gates`. |
| **Scope** | `renovate.json`; `.github/workflows/ci.yml` `quality-gates`; `deterministic-checks.md` Checks 3.1–3.5. |
| **Consequence** | A 404 preset or a stale deny-list produces a green tree for months (#181 ~7 months). Dependabot/Renovate never open the PR the file implies. |
| **Proof rung** | Deterministic structural analysis. |
| **Proof artifact** | G-R3 resolve script + job invocation. Live bot apply is not this artifact. |
| **Why not cheaper** | Types cannot express URL resolution. Generation does not apply. |
| **Failure signal** | Nothing. Renovate silence is indistinguishable from “no updates.” |
| **Rollback** | Revert `renovate.json` and the job step. A Renovate run that already applied a preset does not reverse. |
| **Lenses** | 7 Recurrence To Guard; survey 3, 4, 6. |

**Open questions:**

- Does `gh api` on `lvlup-sw/lvlup-claude` contents still 200 at `tools/renovate-config/presets/dotnet.json` after the `exarchos` rename? Even a 200 is not the #181 acceptance. `(partial: in-repo path token points at that slug; live resolve not re-verified this run)`

### [recur-mcp-pin-bound] — MCP pin and emitted shape cannot drift

| | |
|---|---|
| **Claim** | The current MCP revision, the deny-list, every `CallToolResult` construction, and the Hosting package pin stay one revision. |
| **Scope** | INV-3 policy; `OntologyServerToolFactory`; Hosting csproj pin; Checks 3.1–3.5. |
| **Consequence** | Clients on 2026-07-28 omit `resultType` or see a docs pin that the binary does not emit. A stale deny-list returns zero and looks clean. |
| **Proof rung** | Deterministic structural analysis. |
| **Proof artifact** | G-R4 executed policy. Hosting traverse tests are component proofs of one path. |
| **Why not cheaper** | Rung 1 (derive from one revision document) is situationally absent. File-level `grep -L ResultType` is cheaper and unsound. |
| **Failure signal** | Protocol clients may reject or ignore the old shape. In-repo, nothing unless a test hits that tool. |
| **Rollback** | Revert factory assignments and the Hosting pin. Clients that already require `resultType` then see the omission. |
| **Lenses** | 7 Recurrence To Guard; survey 2, 5, 6. |

**Open questions:**

- Is `ErrorResult` with `resultType: complete` protocol-legal? A wrong yes would bake a lie into G-R4. `(needs human input)` only after spec read — **not tagged yet**; this run did not re-read the MCP revision text.

### [recur-enum-ordinal-frozen] — Published enum numerics only append

| | |
|---|---|
| **Claim** | Numeric values of published enums in this wave’s public surface do not move. New members append. |
| **Scope** | `DescriptorSource`; generated `{Workflow}Phase` (#183). Not `#156.3`. |
| **Consequence** | Stored integers remap. Newtonsoft `EnumStorage` ordinal consumers read the wrong phase. AONT205 would have treated the moved value as ingested. |
| **Proof rung** | Compiler and type system (frozen values), enforced by a snapshot because the language does not lock ordinals. |
| **Proof artifact** | G-R5 policy snapshot. `DescriptorSourceTests` is the current one-enum snapshot, not the class proof. |
| **Why not cheaper** | Generation of enum members from a snapshot is available in principle and not present. Situational. |
| **Failure signal** | Nothing until a stored document is read back wrong. |
| **Rollback** | Revert the member. A published `= 2` is a compatibility event. |
| **Lenses** | 7 Recurrence To Guard; survey 6. |

**Open questions:**

- Is there a consumer already persisting `DescriptorSource` as an integer? Additive `= 2` is safe only if nobody stored a closed two-value assumption.

### [recur-diagnostic-trigger-reachable] — Catalog Errors have an authorable trigger

| | |
|---|---|
| **Claim** | Every AGWF/AONT Error id can be triggered from a compiling public DSL or JSON import path, and does not fire on a shape that is not the defect. |
| **Scope** | `AgwfCatalog.tsp`; `WorkflowDiagnostics`; `DeclaredButInertTests`; generator/import kill fixtures. |
| **Consequence** | A present diagnostic is an INV-5 violation (control with no trigger) or a false-positive block (AGWF022). Contributors trust a catalog that cannot see the filed shape (AGWF035 half-close). |
| **Proof rung** | Deterministic structural analysis. |
| **Proof artifact** | G-R7 `kill_fixture` catalog field + `RunGenerator` closure. |
| **Why not cheaper** | Types cannot express “this id is reachable.” Catalog identity generation (rung 1) does not imply a trigger. |
| **Failure signal** | Nothing. The diagnostic is simply never reported, or is reported wrongly. |
| **Rollback** | Retarget or delete the id. Retired ids stay retired (INV-5). |
| **Lenses** | 7 Recurrence To Guard; survey 5, 6. |

**Open questions:**

- None for the catalog rule. Which current Error ids lack a public-path kill was not fully inventoried this pass.

### [recur-graph-representations-agree] — Saga, table, and diagnostic read one graph

| | |
|---|---|
| **Claim** | Saga dispatch edges are a subset of `ValidTransitions`, and `ValidTransitions` equals the graph AGWF035 consults, because they are one `PhaseGraph` instance. |
| **Scope** | `PhaseGraph.Build`; `TransitionsEmitter.cs:56`; `TerminalReachabilityGuard` default build; `WorkflowIncrementalGenerator.cs:1038`; saga emitter. |
| **Consequence** | Published API lies (`IsValidTransition` allows an edge the saga cannot take, or the reverse). CHANGELOG “share one PhaseGraph so they cannot drift” is type-share. |
| **Proof rung** | Construction and generation. |
| **Proof artifact** | G-R8: build once, pass in; conformance until saga consumes the instance. |
| **Why not cheaper** | This *is* the cheapest sound rung. Type-share (rung 2) does not establish instance identity. |
| **Failure signal** | Consumer-visible wrong `ValidTransitions`. Runtime hang when the table and the saga disagree. |
| **Rollback** | Revert `PhaseGraph` extraction. Does not reverse already-emitted tables. |
| **Lenses** | 7 Recurrence To Guard; survey 1, 6, 7. |

**Open questions:**

- Does Mermaid still build a separate graph at this revision? If yes, it is a fourth consumer and belongs in G-R8’s policy list.

---

## Assumptions

- Recurrence seed counts are prior instances, not a claim that a class is closed.
- `#156.3`, `#147`, `#133`, `#174`, Option B are out of wave. They appear only as class members or exceptions, not as new product work.
- `#174` first-match is an intentional operator, not a defect.
- External issue bodies are leads; call sites cited above were read at `324768f`.
- Author diversity on the changed path is one person (survey). Recurrence is same-author re-entry.

## What this lens did not do

- Did not implement any guard or product fix.
- Did not inventory every AGWF Error for a missing public-path kill (G-R7 open question).
- Did not re-resolve the live Renovate preset or re-read the MCP 2026-07-28 spec text.
