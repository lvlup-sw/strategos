---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: git and issue history relevant to compensation lowering, identity, topology, fail-closed import, proof harnesses, and the seven issue-169 product commits
updated: 2026-09-07
skipped: oral intent not preserved in repository/issue records and unbound external-consumer history
lens: history-and-recurrence
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: present corrective program
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: records the action-calculus redesign and dependency order
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: immediately preceding proof/identity implementation and verification dossier
---

# Stage 1 survey — history and recurrence

## Result boundary

This lens uses commit history, the prior #167 dossier, and issue records to identify recurrence classes.
A historical similarity is a risk seed, not proof of a current defect. A class becomes a guard
candidate when the repository shows at least two independent occurrences or when the present diff
crosses the same failure boundary directly.

## Historical compensation and routing chronology

### 2026-06-17 — compensation first reaches generated sagas

- `0f5fa2c` — `feat(generators): lower step resilience into the saga — retry/timeout/compensation/confidence + WithContext (#135) (#137)`.
- `7b3269c` — `fix(generators): wire dead OnFailure worker handler + trigger publish (#140)`.
- `903b155` — `fix(generators): Compensate/OnFailure interop via single ordered trigger (#140)`.

The sequence is material: the first general resilience lowering did not make every configured failure
route live, and the subsequent fix required an ordering rule between compensation and the failure
handler. The current #169 design again changes both compensation and failure routing, so “the central
component looks correct” is not adequate evidence. Every ingress and successor owner needs an
executable guard.

### 2026-06-19 — parity guard follows completeness fixes

- `8b28ab3` — `feat: v2.9.0 close-out — step-resilience completeness + parity guard + schema bootstrap (#144)`.

This establishes a second historical response to incomplete lowering: structural parity and schema
checks were added after feature work. #169 must extend those guards to the inverse identity and all new
generated state/message shapes, not assume the older compensation presence check covers semantics.

### 2026-07-07 — diagnostic fork and fail-closed import

- `abe0459` — `feat(generators): lower the diagnostic-fork edge into the saga (DR-9, #151)`.
- `9b0df25` — `fix(generators): harden JSON import front-end (depth guard, fail-closed monikers, schema-invalid diagnostics, fidelity) [M1/M2/M3/M4/L1/L2]`.

Diagnostic-fork routing is another independent failure producer. The import hardening records the
recurrent boundary where a language-neutral document can pass a schema or parse stage yet lose type
or semantic identity while lowering. #169 adds `inverseAction` and makes compensation-step type
presence important, so malformed/null/blank/ambiguous variants require fail-closed tests at schema,
parser, and bridge layers.

### 2026-08-23 to 2026-08-28 — topology and identity corrections

- `a965f3b` — main-flow termination corrections for fork, branch, and approval chains (#187).
- `25368ca` — Option B identity routing and residue integration (#196).
- `9a2b70d` — review-driven routing, resilience, and upgrade corrections.

These commits repaired cases where the model contained steps but successor scans, roles, or generated
identity did not match executable topology. The issue-167 survey also documented repeated failures in
which a phase name or CLR type was weaker than occurrence identity. #169 persists and later trusts
scope, lane, ordinal, action, execution, and rollback identities; weakening any of them reopens this
class with higher consequence.

## Immediate #167 handoff

The issue-167 verification dossier ended with compensation deliberately unproved and seeded the
following handoff obligation: consume its occurrence-level action identity and closed proof graph
without inventing a parallel CLR-type mapping. The #169 diff retains both pieces intentionally:

- ontology `WorkflowActionReference` remains the proof identity; and
- CLR `CompensationStepType` remains only the executable worker type.

The new `CompensationTopology` and journal do create additional exact representations, but they carry
the existing identity rather than substituting the CLR type for semantic authority. Stage 2 must still
prove the round trip.

The #167 survey also found a false proof caused by an unrooted action catalog and a fork footprint that
omitted a low-confidence handler. #169 moves the hardened catalog into `src/Shared` and expands
topology-closure failures around confidence, approval, fork, and nested scope. These are explicit
recurrence countermeasures, not reasons to skip refutation.

## Current implementation chronology

### `7082184` — core inverse calculus

Adds immutable inverse statuses, obligations, contract/failure analysis, rollback tree variants, exact
semantic equivalence, identity handling, sequence reversal, scopes, parallel noninterference, public
API ledger entries, graph-freeze validation, and AONT216 source analysis/tests.

### `a73fbcc` — typed workflow proof

Adds typed `.Compensate<T>(WorkflowActionReference)`, immutable configuration/IR, TypeSpec 0.12
`inverseAction`, AGWF044/045 catalog entries, source/import extraction, workflow pair proof,
compensability propagation, and public/contract tests.

### `494471f` — durable completed-prefix runtime

Adds `CompensationTopology`, completion journal and authority claims, generated messages/handlers,
nested/fork scope selection, distinct inverse flow, reducer ordering, timeouts, and the real
SagaDocument behavioral fixture.

### `464439c` — public explanation

Adds changelog, migration, action-calculus, workflow API, and diagnostic documentation for the inverse
contract, typed authoring, completed-prefix semantics, persistence restriction, idempotency boundary,
and reconciliation.

### `8f8dd34` — broad hardening

The hardening commit touches 83 files and adds roughly 8.9k lines while removing roughly 0.55k. Its
themes mirror the recurrence classes:

- one shared rooted catalog and decoy-`Define` protection;
- bidirectional equivalence and both write/read noninterference directions;
- nested read footprints and immutable snapshots;
- closed confidence/approval/fork/loop topology and role-aware identities;
- mandatory typed rollback and mixed-order rejection;
- durable dispatch/failure capabilities, high-water/continuity checks, forged/stale/redelivery/timeout
  defenses, and monotonic reconciliation;
- malformed imported compensation rejection; and
- a large adversarial generated-runtime suite.

This commit demonstrates that the first implementation pass did not contain the full correctness
boundary. Later mutation work should target the exact hardening guards rather than only feature code.

### `a37470e` — packed #169 consumer

The earlier package probe exercised #167 but not typed compensation, AONT216/AGWF044/045 semantics.
This commit adds legal typed compensation and a deliberately contradictory inverse expected to fail
exclusively with AGWF044. It closes a concrete false-green harness gap found during verification.

### `40edb5f` — warning-free generated guards and result triage

A fresh packed consumer exposed three generated CS8604 nullable warnings in journal guard paths. The
emitter now snapshots/narrows the journal before dereference; the generator compilation test rejects
CS86xx warnings; and the packed consumer treats nullable warnings as errors. The script also separates
dependency restore failure (`INDETERMINATE`, exit 3) from a legal consumer compile regression (`FAIL`,
exit 2). This is direct evidence that consumer-level proof and verdict triage are recurrent guard
needs, not optional polish.

### `42b4ed7` — nonblank compensation-step moniker authority

The Stage 1 authority/proof inventory found that the hand importer rejected blank
`compensationStepType` values while the TypeSpec and generated schemas required only a string. The
final commit adds `@minLength(1)` and the nonwhitespace pattern to TypeSpec, regenerates the standalone
and bundled schemas, emits read/write `RequireNonWhitespace` validation in generated C#, and adds
schema plus empty/whitespace serialization/deserialization cases. The concrete mismatch is resolved
in the final product subject and becomes another instance of the JSON/schema/import recurrence class.

## Recurrence classes and earliest guards

| Recurrent class | Independent prior occurrences | Present #169 exposure | Candidate earliest effective guard |
|---|---|---|---|
| Accepted configuration is lost or under-scoped during lowering | #135 then #140; #143/#144; #187/#196; #167 confidence-footprint finding | every compensation/failure callback and scope owner | structural occurrence/ingress inventory plus real-generator kill cases per callback family |
| Semantic identity is weakened to phase, CLR type, or partial key | #31 and #189/#190/#191/#196 history summarized by #167 | forward/inverse action, occurrence, scope, lane, execution, rollback IDs | validated names-only values plus adversarial wrong-occurrence/scope/role/execution tests |
| A green harness observes the wrong or incomplete subject | historical parity fixes; #167 rooted catalog; #169 packed probe initially omitted typed compensation; final probe found warnings | analyzer packaging and generated consumer code | isolated exact-byte package consumer with legal and cause-specific negative probes; warnings as errors |
| JSON/schema/import paths disagree or fail open | `9b0df25` import hardening, #167 action identity, and the #169 blank-moniker mismatch resolved by `42b4ed7` | optional inverse object and required nonblank CLR moniker | TypeSpec constraints, generated read/write validation, schema/DTO parity, malformed-kind matrix, resolution failures, round trip |
| Diagnostic vocabulary drifts across generated and live forms | #102/#105/#109 history recorded by #167 | AONT216 plus AGWF044/045 closed tokens/messages/docs | TypeSpec regeneration and enum-entry/catalog/live-descriptor parity; mutation of one representation |
| Proof semantics drift between runtime and analyzers | #168/#167 already required shared finite solver while orchestration remained duplicated | three inverse equivalence implementations | one shared inverse vector corpus interpreted by all front ends or a shared neutral kernel |
| At-least-once delivery reopens completed or terminal work | timeout/retry/compensation fixes since #135 and routing fixes in #196 | dispatch claim, completion journal, inverse result, failure claim, rollback terminal state | capability-chain invariants plus duplicate/stale/forged/crash-window integration cases |
| Generated code compiles in fixture but warns/fails in a real consumer | #187/#196 compile/harness corrections and final `40edb5f` discovery | nullable flow across emitted property guards | full generated-compilation diagnostics plus packed consumer with relevant warnings as errors |
| Persistence/schema upgrade lacks safe reversal | prior generated saga schema/routing changes documented in 2.11 migration | journal schema version 1 and in-flight typed sagas | explicit version guard, drain/version migration, corrupt/unknown-state retention tests |

Each row has at least two independent historical occurrences or a present reproduced harness failure,
so each qualifies as a Stage 3 guard candidate under the verification recurrence rule.

## Deleted defaults and compatibility history

- The base's name/list-style public rollback helpers are replaced by structured plans. They were in
  the unshipped API baseline, so this is not a published-contract removal, but the exact public delta
  still needs API review.
- Duplicate compensation changed from silent last-write-wins to an immediate exception. Existing
  valid call sites remain source-compatible; callers relying on overwrite behavior do not.
- The legacy no-argument overload remains and retains legacy event-sourced behavior, but it cannot
  participate in typed proof. Mixed programs fail closed.
- `requiredOnFailure` still exists for legacy metadata, defaults true, and cannot opt a typed program
  out of rollback.
- The 0.12 inverse member is optional on the wire; the closed diagnostic enum addition is an upgrade
  requirement for consumers even though old documents remain readable.

## Recurrence-to-obligation seeds

1. Build an explicit completeness matrix from every accepted compensation/failure callback shape to
   topology occurrence, dispatch claim, failure claim, and rollback trigger code.
2. Mutation-kill weakening of occurrence/scope/execution/rollback identity at every ingress.
3. Interpret shared semantic inverse vectors through all three proof authorities.
4. Mutation-kill removal of nested read footprints and either write/read conflict direction.
5. Round-trip and malformed-matrix the new wire fields through schema, reader, bridge, IR, and output.
6. Require exact packed-consumer bytes, typed positive/AGWF044 negative, warnings-as-errors, and
   fail/indeterminate separation.
7. Exercise duplicate/stale/forged/timeout/failure sequences against monotonic retained state, plus a
   real supported-host completed-prefix case.
8. Verify the migration guard for unknown/corrupt journal schema and do not promise automated reversal
   for in-flight typed sagas.
