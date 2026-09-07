---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
cost_setting: high
scope_rule: independent cross-lens review of temporal evidence, hostile wire structure, future DTO evolution, and consumer rollout
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: intended build-time closure claim
  - path: https://github.com/lvlup-sw/strategos/issues/153
    why: external consumer coordination requirement
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: next semantic consumer of occurrence identity
---

# Evaluation — wildcard

## Findings

### W-1 — Exact-current local evidence is temporally atomic

The complete local portfolio in `../final-evidence.md` binds to current final subject
`98fabb410e4432cc39fd71ec92651e6ceecfcc7c`: solution build/tests, Contracts/codegen, live schema,
docs, standalone gates, fresh digest-bound packages and consumer, hardened pack script, and Basileus
smoke all passed on that subject.

The protected portfolio is not yet recorded. Even correct local command/result pairs cannot
authorize a protected subject or future release artifact set.

The portfolio must therefore complete protected execution and review. The future Contracts tag must
bind its own release nupkg hashes. This finding changes evidence status, not product semantics.

**Category suggestion:** gap in evidence binding; recorded across the ledger instead of inventing a
new product test.

### W-2 — A future wire DTO field could have reopened the repaired echo bug

The first complete-field repair could have used a manual recursive list. That would be correct today
and silently incomplete after the next generated DTO property. The chosen implementation instead
reflects every public property in deterministic ordinal order, encodes the runtime DTO type, and
throws on unsupported scalar kinds. `WireStepFingerprintCoverageTests` independently discovers the
reachable DTO graph and mutates every declared property.

The guard was reviewed for the angles not owned by ordinary identity tests:

- the generator targets `netstandard2.0`; the used reflection and invariant formatting APIs are
  available there and the Release project build succeeds;
- null and derived DTO types are encoded explicitly;
- parsed JSON creates an acyclic object graph, so reflection recursion cannot encounter a cycle on
  the production route;
- `MinimalJsonReader` caps object/array recursion at depth 128 before fingerprinting; and
- traversal is deterministic and linear in the finite parsed DTO graph.

**Category suggestion:** addressed gap and guard, not a residual defect.

### W-3 — Duplicate JSON member names are a separate policy boundary

The dependency-free reader follows a last-member-wins object-member policy. Generated
`System.Text.Json` models and JSON Schema do not expose a distinct duplicate-key rejection contract.
That ambiguity is broader than #167 and does not allow two *separate occurrence records* with one
stable step ID to bypass the new semantic echo check. Promoting it as an action-identity blocker would
conflate two mechanisms.

**Category suggestion:** no new #167 obligation; retain as a documented residual wire-policy risk.

### W-4 — Authoring ergonomics constrain some same-type occurrence combinations

Several builder positions offer an instance-name overload or a configuration callback but not both.
Those positions cannot simultaneously give a repeated CLR type a distinct phase name and call
`.Performs`. The runtime and docs disclose the current limitation, and the proof rejects ambiguous
same-phase action identity rather than guessing. This is conservative loss of expressiveness, not an
unsound acceptance.

**Category suggestion:** no correctness gap for #167; potential follow-up API ergonomics work.

### W-5 — Adoption issues are coordination, not implementation evidence

`basileus-adoption.md` and `exarchos-adoption.md` describe the right work, including optional action
identity, closed enum growth, API mirrors, and pinned consumer fixtures. They are now linked to
Basileus #493 and Exarchos #1893, establishing coordination. They do not establish that either
consumer has upgraded, implemented the enum/API changes, or passed its pinned fixtures. The same
distinction applies to an unexecuted release workflow: code that could verify a package is not package
evidence.

**Category suggestion:** existing downstream obligation remains unproven.

### W-6 — Transparent syntax is not a semantic boundary

Parentheses around a fluent receiver do not change the C# program, but direct
`MemberAccess.Expression is InvocationExpressionSyntax` checks can treat them as the end of a chain.
The defect reached both occurrence extraction and full branch semantics. Central transparent-syntax
stripping plus helper and end-to-end tests now make the equivalence explicit.

**Category suggestion:** concrete extraction gap repaired and covered by the accepted-surface guard.

### W-7 — Display identifiers are not structural ancestry

Flattened loop prefixes are deliberately human-readable and use `_` as a separator, while authored
loop and step names may themselves contain underscores. They therefore cannot encode unique direct
membership or ancestry: an underscore-bearing body-step name can erase a back-edge. The same
ambiguity survives a prefix-plus-depth heuristic: sibling `A_B` can begin with nested `C` and look
like a descendant of `A` at the relevant depth. All three constructions reached the supported C#
path and initially produced false-green proofs.

The repaired model carries an immutable outer-to-inner path of syntax-call identities. It uses
`RepeatUntil.ArgumentList.SpanStart`, not `InvocationExpression.SpanStart`, because chained fluent
invocations share the latter with their receivers. This structural path is local to the immutable
syntax tree, deterministic, and never serialized as public identity.

**Category suggestion:** concrete topology gap repaired and promoted to a permanent guard.

### W-8 — Root failure is concurrent with fork work, not just later control flow

A terminal root failure handler is authored outside a fork path, which makes it easy to omit from a
per-path footprint walk. At runtime, however, a path worker can trigger it while other path workers
are still active. The conflict is therefore part of fork noninterference. A deliberately overlapping
root-handler/sibling write set reproduced the false green and now produces AGWF041.

**Category suggestion:** concrete fork-footprint gap repaired and added to the recurrence guard.

### W-9 — Error precedence must follow authority precedence

An occurrence action tuple must resolve uniquely before it can contribute an edge to recursive
workflow-binding discovery. Otherwise an ambiguous tuple that includes the bound action can be
reported as an AGWF042 cycle, hiding the authoritative AGWF040 identity failure. The dedicated
ambiguous-self-reference regression now pins one exclusive AGWF040.

**Category suggestion:** addressed occurrence-identity/diagnostic-partition gap.

## Cross-lens reconciliation

- Proof-layer fit places wire semantic equality at rung 4, while the property-discovery mechanism is
  a cheaper rung-3 guard. Both are needed and neither duplicates the other.
- Coverage adds AGWF043 to workflow identity rather than treating a generated-name collision as an
  action-refinement counterexample.
- Refutation kills the empty-workflow obligation but preserves front-end rejection under topology
  completeness.
- The authority-topology workflow-name whitespace mismatch is repaired at `98fabb4` with exact-current
  Contracts/codegen evidence; it is no longer a current product finding.
- The historically violated proof-harness obligation is locally supported after the remaining raw
  import/approval routes were migrated and the complete exact-current `98fabb4` generator portfolio
  passed. It remains `Indeterminate` until protected execution and review bind the guard.

## Passes

- No additional unsupported scalar, cycle, depth, determinism, or asymptotic flaw was found in the
  reflection fingerprint on its production input path.
- No path was found where an exact action tuple but conflicting occurrence configuration can still be
  accepted as a projection echo.
- No remaining loop-ownership decision depends on underscore parsing in the proof/extraction path;
  display-prefix helpers in emitters remain outside this structural ancestry decision.
- No new product obligation was invented from the general duplicate-key or authoring-ergonomics
  boundaries.

## Uncertainties

- The revision/fingerprint and complete local evidence portfolio bind final subject
  `98fabb4`/`f7271c9`.
- Basileus #493 and Exarchos #1893 establish issue creation only. Package consumption and downstream
  behavior cannot be inferred from those issue links or local markdown bodies.
- Protected PR checks and the future Contracts-tag release evidence remain pending.
