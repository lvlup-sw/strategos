---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
cost_setting: high
scope_rule: independently mutable authorities and representations for inverse semantics, topology identity, persistence state, diagnostics, wire schema, and delivery artifacts
updated: 2026-09-07
skipped: runtime execution and drift mutation; binding mechanisms are inventoried, not assumed effective
lens: authority-topology
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: intended single semantic contract for derived compensation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream contract/API copy to be governed separately
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream contract/API mirror to be governed separately
---

# Stage 1 survey — authority topology

## Counting rule

A representation unit is one independently editable semantic implementation, declaration/model,
generated artifact, executable validator, test authority, public-API ledger, or hand-maintained prose
surface. Generated files count as shipped representations but not as independent source authorities
when an executable regeneration gate binds them to one source. Repeated uses inside one file do not
inflate the count. Totals are therefore topology indicators, not lines-of-code metrics. There is no
product UI in the #169 diff, so every boundary has **0 UI representations**.

## AT-1 — inverse equivalence has three orchestration authorities above a shared kernel

**High structural risk; no disagreement is asserted by this Stage 1 inventory.**

The intended rule—derive the inverse from the forward effective guarantee/hard requirement and prove
the authored descriptor equivalent—has **three independently mutable decision implementations**:

1. runtime/public `ActionCalculus.AnalyzeInverse` in
   `src/Strategos.Ontology/Descriptors/ActionCalculus.cs`;
2. source-time ontology `OntologyInverseContractAnalyzer` in
   `src/Strategos.Ontology.Generators/Analyzers/OntologyInverseContractAnalyzer.cs`; and
3. workflow occurrence proof in `ProveCompensationContracts` and its helpers in
   `src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs`.

They share important lower layers: `ActionContractProofEngine`, the normalized formula types,
`FiniteDomainSolver`, the Roslyn action-predicate bridge, and the moved
`src/Shared/Analyzers/Proof/OntologyActionCatalog.cs` are source-linked where the target framework
requires a separate compiled copy. Graph freeze is not a fourth semantic implementation: it resolves
the authored action and calls `ActionCalculus.AnalyzeInverse`. The workflow and ontology analyzers do
not call the public runtime method because they operate over Roslyn models in netstandard analyzer
assemblies.

The independently repeated policy includes status/diagnostic precedence, effective rather than raw
guarantees, optional declared action-name agreement, same-subject equality, exact frame equality,
semantic authority equality, two implication directions for each predicate, invalid/opaque handling,
and witness wording. Sharing the finite solver does not bind these orchestration choices. There is no
single shared inverse-vector corpus visibly interpreted by all three front ends; the runtime,
AONT216, and AGWF044 suites author parallel examples. Stage 2 must require explicit cross-front-end
vectors or a neutral source-shared orchestration kernel.

## AT-2 — compensation identity crosses eleven representation units

The executable/proof identity is intentionally two-part: a CLR compensation step type plus an exact
ontology action triple. The changed path contains at least **11 semantic representation units**:

1. `CompensationConfiguration` runtime model/factories;
2. `StepConfigurationBuilder` one-declaration state and overload routing;
3. public fluent interfaces and their API baseline;
4. TypeSpec `CompensationConfiguration` plus referenced `ActionReferenceV1`;
5. generated Contracts C#;
6. standalone generated JSON Schema;
7. bundled workflow schema;
8. hand-written `WireDtos`;
9. `MinimalJsonReader` validation;
10. `WireToModelBridge` CLR/action resolution; and
11. source-generator `CompensationModel`/step extraction.

After proof, the same ontology identities are projected again into `CompensationTopology` and emitted
persisted `CompensationJournalEntry` fields. Those are runtime audit copies, not authoring authorities,
but they are part of the end-to-end identity invariant and must round-trip exactly. Binding mechanisms
include TypeSpec regeneration, schema-conformance tests, projection/import round trips, exact ordinal
value objects, public API baselines, topology tests, and journal source/behavioral tests. No single
test currently appears to feed one valid/invalid identity vector across **every** unit, so the
end-to-end invariant remains a composite obligation.

The first survey pass found one concrete acceptance mismatch inside this topology: TypeSpec/schema
accepted whitespace-only `compensationStepType` while the importer rejected it. Commit `42b4ed7`
resolved it with TypeSpec length/nonwhitespace constraints, both regenerated schema forms, generated
serialization/deserialization validation, and direct schema plus blank read/write tests. This is now
a guarded historical discovery rather than a current mismatch; the full end-to-end identity
obligation remains.

## AT-3 — the topology key is generated once but reinterpreted by many message routes

`CompensationTopology` is the principal static authority for occurrence key, scope ancestry, scope
kind, lane/fork/path/ordinal, and action identity. The workflow analyzer and main compensation emitter
both consume it. The generated saga then contains separate validation/routing helpers for forward
start, forward completion, pre-completion failure, post-completion reducer/routing failure, approval,
confidence, branch, loop, fork dispatch/join, diagnostic fork, timeout, inverse completion, and
inverse failure.

This topology is healthier than independently computing every key in each emitter, but the compiled
switches and helpers are still generated representations that can omit a message family or compare a
weaker subset of fields. The diff adds role-aware failure-handler identities and exact execution IDs
because a phase or CLR type is not sufficient. The authority graph is therefore:

```text
fluent/import IR -> CompensationTopology -> proof
                                  \-------> generated constants/switches
persisted exact claims/journal --------------------^ (validated at every ingress)
```

The earliest guard is a structural inventory requiring every accepted topology occurrence and every
failure ingress to have one generated claim/validation route, backed by real-generator kill fixtures.

## AT-4 — durable rollback state has one generator authority and three storage-facing models

The source authority is primarily `SagaCompensationComponentEmitter` plus the smaller journal and
routing emitters. It generates at least **three persisted record families**:

- `ForwardDispatchClaim`;
- `FailureTriggerClaim`; and
- `CompensationJournalEntry`.

It also emits scalar/list saga properties for schema version, sequence/high-water, active scope,
failed occurrence, pending fork, failure message, outcome unknown, rollback finished, and per-path
quiescence. Commands, events, timeout messages, and worker handlers are additional transport
representations. The closed status values such as `Pending`, `Running`, `RolledBack`, `Failed`, and
`OutcomeUnknown` are emitted strings rather than a shared runtime enum, so source-generation tests and
structural validators are their practical authority.

The storage-facing semantics are split across:

1. generated saga-document fields and handler mutations;
2. Wolverine message dispatch/transaction behavior; and
3. Marten document persistence/redelivery behavior.

Only the first is owned by this repository's generated source. The behavioral Postgres fixture can
exercise the combined boundary, but source tests alone cannot establish that a claim commits before
the external worker observes its command. Durability language must remain conditional on supported
Wolverine/Marten semantics.

## AT-5 — compensation diagnostics have multiple canonical roots

### AONT216

AONT216 has **two executable semantic producers**:

- the ontology Roslyn descriptor/analyzer; and
- runtime graph-freeze `OntologyDiagnostic` construction after calling the public calculus.

Its ID additionally appears in `OntologyDiagnosticIds`, public diagnostic documentation, action-
calculus documentation, and two test families. There is no TypeSpec AONT catalog in this diff. The two
producers intentionally have different outer message formats, but must agree on the semantic reason
and acceptance boundary. Runtime graph-freeze tests and analyzer tests are separate representations;
no literal-message parity is assumed.

### AGWF044 and AGWF045

Each new AGWF code has these representation branches:

- a member in the TypeSpec closed `AgwfCode` enum;
- its own TypeSpec `AgwfEntry...` literal model;
- generated C# enum and constant;
- generated JSON catalog;
- generated Markdown;
- standalone JSON-schema entry plus the closed enum schema;
- bundled schema;
- live `WorkflowDiagnostics` descriptor;
- generator tests; and
- changelog/README/migration/API/diagnostic prose.

The codegen pipeline mechanically binds much of the generated branch to the entry models, and catalog
parity tests bind live descriptor metadata. As found in the issue-167 authority survey, the TypeSpec
enum-member list and per-entry literal models are two authored roots unless a schema test explicitly
compares them. #169 updates the relevant catalog tests, but Stage 2 must inspect their exact assertion
scope rather than count them as proof by name.

## AT-6 — public rollback API is governed by a stronger assembly-level baseline

`Strategos.Ontology` adds the closed inverse status/obligation enums, identities, contract/failure/
analysis records, rollback kinds/leaves/plans, and overloads for analysis and derivation. The ontology
project's public API analyzer is assembly-wide, and `PublicAPI.Unshipped.txt` records the additions.
`Strategos` adds the typed `Compensate` overload and compensation model members; the diff expands its
historically selective API gate and reflection baseline to include the #169 surface. The latter is
still a configured cross-product allowlist rather than assembly-wide authority, so future additions
outside the enumerated surface can escape unless the allowlist and reflection inventory evolve
together.

## AT-7 — package evidence now has separate product and infrastructure outcomes

The package probe has one script authority over exact package selection, local-feed configuration,
consumer source, and expected diagnostics. At final revision it makes dependency restore an explicit
precondition: restore failure exits 3 (`INDETERMINATE`), while failure of the already-restored legal
consumer build exits 2 (`FAIL`). It also treats nullable warnings as errors and includes both a legal
typed inverse and a cause-specific contradictory inverse negative.

This is an important authority distinction. A CI wrapper that collapses exit 3 into “test failed” may
still block conservatively, but a verification report must not convert it to a product refutation or
success. Conversely, any wrapper that treats nonzero 3 as tolerated without preserving an
Indeterminate ledger state would create a false green.

## AT-8 — downstream consumers are coordinated but not bound

Basileus #495 and Exarchos #1895 are two future external representations of the 0.12 wire/API
contract. Their issue bodies name the expected members, diagnostics, legacy omission, and saga
reconciliation behavior, but no downstream commit, package lock, build output, or fixture is part of
tree `2d0f3027...`. Their current adherence is therefore **0 proven implementations out of 2
coordinated consumers** in this Strategos dossier. Stage 4/5 delivery may record issue links; it may
not upgrade them to adoption success.

## Representation and binding summary

| Boundary | Primary authority | Independently mutable semantic decisions | Main binding mechanisms | Stage 2 concern |
|---|---|---:|---|---|
| Inverse equivalence | Intended issue/#168 contract | 3 | shared solver/parser/catalog; parallel tests | orchestration and precedence drift |
| CLR + ontology inverse identity | TypeSpec/public value objects | 11+ authoring/wire/IR units | regeneration, round trip, API, schema tests | end-to-end exactness |
| Compensation topology | `CompensationTopology` | 1 model, many emitted consumers | shared IR, structural/emitter tests | omitted ingress or weakened key |
| Durable authority/journal | saga emitter | 3 record families plus state/messages | generated compile tests, behavioral host | persistence/transaction boundary |
| AONT216 | calculus semantics | 2 producers | analyzer/runtime test families | reason/acceptance parity |
| AGWF044/045 | TypeSpec entries + live descriptors | 2 authored TypeSpec roots plus descriptor | codegen and catalog/schema parity | closed-vocabulary drift |
| Public API | assembly API analyzers/baselines | 2 project policies | compiler API checks + reflection | selective Strategos allowlist |
| External adoption | each consumer repository | 2 external future authorities | adoption issues only today | no implementation evidence |

## Candidate obligations

1. Interpret one shared inverse vector corpus through runtime calculus, AONT216 analyzer, and
   AGWF044 workflow analyzer, including invalid/opaque/refuted precedence and counterexamples.
2. Round-trip CLR compensation type plus exact inverse action triple across fluent IR, TypeSpec,
   generated C#/schema, hand parser/bridge, topology, and persisted journal.
3. Enumerate every topology occurrence and every failure ingress and prove it compares the full
   generated key plus a persisted execution/failure capability.
4. Prove closed diagnostic member parity from TypeSpec enum through entries, generated artifacts,
   live descriptors, docs, and packaged analyzer.
5. Verify the expanded public API scope mechanically and mutation-check removal of a #169 member.
6. Exercise the real persistence boundary; do not use emitter source order as a substitute for
   committed-before-dispatch evidence.
7. Preserve package-probe `PASS`/`FAIL`/`INDETERMINATE` distinction in CI and the final evidence
   ledger.
8. Leave downstream adoption unproven until an exact consumer revision and package build are bound.
