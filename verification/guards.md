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
    why: residue tracker names the inert-control class
  - path: /home/reedsalus/.cursor/worktrees/strategos/891j/verification/guards-draft.md
    why: full per-class specification this register condenses
---

# Guard register

A class that appeared more than once owes a guard. This wave **specified** the
guards. It did **not** implement them except where a cheap instance fix also
closed a lie (AGWF035 catalog polarity). Every recurring class **stays open**.

Full specifications: `verification/guards-draft.md`. Ranking by findings removed:
R3 → R2 → R1 → R8 → R4 → R7 → R5. R6 appeared once; no guard owed.

## G-R1 — Termination under/over-reach

```text
class: R1 termination under/over-reach
first instance: #155 / #175 (over-reach, closed #187)
second instance: #184 (under-reach; AGWF035 was blind)
earliest sound layer: 3 (PhaseGraph); 1 if one instance also drives saga (G-R8)
policy data location: AgwfCatalog.tsp AGWF035 + required_call_sites list (to add)
mechanism: TerminalReachabilityGuard on C# emit; under-reach is IR-vs-PhaseGraph
kill fixture: Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug; WithoutSuccessor twins (function only)
guard self-test: none yet — call-site scan ignores phaseGraph; no import site; AGWF035 not in hasErrors
protected paths: Strategos.Generators.Tests via ci.yml build-test
pass signal: over-reach and all-Complete negatives on this revision
fail signal: AGWF035 on over-reach / injected under-reach
indeterminate signal: generator sources unreadable
resource limits: in-process generator; no Postgres
temporary exceptions: none
owner: workflow-generator / INV-5
expiry: none
```

**Class stays open.** This wave extends the guard. It does not bind saga dispatch (Option B / out of wave). Human control: review of emitter loops that publish `Start{Finally}`.

## G-R2 — First-wins / silent collision

```text
class: R2 first-wins / silent collision
first instance: #156.2 PermitTrigger (CS0152 only)
second instance: #189 successorWithinPath last-writer
earliest sound layer: 3
policy data location: collision-keys.json (to add)
mechanism: per-id AGWF003 / 036 / 037 — not one policy
kill fixture: AGWF037 C#/import twins
guard self-test: AGWF037 twins fail if reject is unwired
protected paths: C# extract, JSON import
pass signal: duplicate PermitTrigger → AGWF037, no saga
fail signal: first-wins keeps one evidence schema
indeterminate signal: none
resource limits: compile-time
temporary exceptions: empty trigger names (skipped today)
owner: INV-5 catalog
expiry: none
```

**Class stays open.** AGWF037 is an instance guard. The next keyed IR map is uncovered.

## G-R3 — Inert-looking control

```text
class: R3 inert-looking control (CI/config)
first instance: #181 renovate 404 extends
second instance: #178 stale INV-3 deny-list
earliest sound layer: 3 (resolve + job); live bot apply is layer 8
policy data location: scripts/policies/control-resolve.yaml (to add)
mechanism: none on quality-gates
kill fixture: old 404 extends token was deleted, not preserved
guard self-test: none
protected paths: none in CI
pass signal: n/a
fail signal: n/a
indeterminate signal: GitHub API / Renovate apply unobserved
resource limits: n/a
temporary exceptions: #147 / #133 / #174 out of wave
owner: platform / #181
expiry: live-apply proof remains human-owned
```

**Class stays open.** T3 is a path-token instance fix. Human control: maintainer confirms Renovate Dependency Dashboard after merge.

## G-R4 — MCP pin / deny-list / shape

```text
class: R4 MCP protocol pin lag
first instance: #166 docs-only re-pin
second instance: #176 resultType omitted
earliest sound layer: 3 executed (situational rung 1 absent)
policy data location: deterministic-checks.md 3.4/3.5 (prose)
mechanism: Hosting VersionOverride 2.2.0 + factory ResultType; INV-3 greps not in CI
kill fixture: AssertResultTypeComplete
guard self-test: none for Check 3.4/3.5
protected paths: Hosting tests only
pass signal: traverse wire contains resultType complete
fail signal: omitted ResultType on factory constructions
indeterminate signal: four-tool SDK wrap unasserted
resource limits: in-process
temporary exceptions: ErrorResult + complete (INV-3 asserts legal)
owner: INV-3
expiry: none
```

**Class stays open.** Factory sites hold. Checklist extension is not a guard.

## G-R5 — Published enum ordinals only append

```text
class: R5 published enum ordinals only append
first instance: #183 Phase Newtonsoft ordinal
second instance: avoided #163 remap of Ingested
earliest sound layer: 2
policy data location: none (convention + DescriptorSourceTests)
mechanism: DescriptorSourceTests freeze 0/1/2 for one enum
kill fixture: insert-at-1 fragment lives only in the ticket
guard self-test: snapshot, not a kill
protected paths: Ontology.Tests
pass signal: Ingested == 1, HandAuthoredContract == 2
fail signal: ordinal move
indeterminate signal: none
resource limits: unit
temporary exceptions: none
owner: ontology public API
expiry: none
```

**Class stays open.** Avoiding the remap is a fix with no class guard.

## G-R7 — Diagnostic with no authorable trigger

```text
class: R7 diagnostic on unauthorable / false shape
first instance: AGWF022 false positives
second instance: AGWF035 half-close
earliest sound layer: 3 (catalog closure)
policy data location: none
mechanism: DeclaredButInertTests pins AGWF022 only
kill fixture: AGWF022 approval-preceding; AGWF035 WithoutSuccessor is not a public-path kill
guard self-test: none catalog-wide
protected paths: Generators.Tests
pass signal: AGWF022 fires on an authorable workflow
fail signal: Error id with no compiling trigger
indeterminate signal: none
resource limits: generator driver
temporary exceptions: none
owner: INV-5
expiry: none
```

**Class stays open.** Completing AGWF035’s IR arm does not add the catalog rule.

## G-R8 — Table / saga / diagnostic drift

```text
class: R8 table / saga / diagnostic drift
first instance: #175 flat ValidTransitions
second instance: #184 table advertised an edge the saga did not take
earliest sound layer: 1 (one graph instance) — situationally absent
policy data location: none
mechanism: shared PhaseGraph type; two Build() calls; saga unbound
kill fixture: historical #175/#184/#189; missing saga-omit-StartFinally with intact IR
guard self-test: none
protected paths: none
pass signal: n/a
fail signal: n/a
indeterminate signal: none
resource limits: n/a
temporary exceptions: Option B out of wave
owner: workflow-generator
expiry: none
```

**Class stays open.** Type-share is not instance-share and is not saga-share. Human control: emitter review of `Start{Finally}` loops.

## R6 — first instance only

`[Obsolete]` without a fluent successor (#115). No guard owed. If a second Obsolete names a missing type, convert then.
