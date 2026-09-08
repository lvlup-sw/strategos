---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
evidence_binding: current final subject is product commit 98fabb410e4432cc39fd71ec92651e6ceecfcc7c; the serial solution build, complete tests, Contracts/codegen, live schema comparison, docs, standalone policy gates, fresh package hashes and consumer, hardened pack script, and Basileus smoke are exact-current; protected PR CI and review remain unbound
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, wire, package, workflow-policy, and documentation surface against the base revision
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: action-calculus semantics consumed by the workflow proof
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: deferred compensation consumer of the occurrence-level identity
  - path: https://github.com/lvlup-sw/strategos/issues/204
    why: explicit follow-up for portable cross-assembly proof catalogs
  - path: https://github.com/lvlup-sw/strategos/issues/153
    why: contract-consumer adoption rule for Basileus and Exarchos
---

# Issue #167 verification ledger

## Evidence-binding status

The current immutable product subject is commit `98fabb410e4432cc39fd71ec92651e6ceecfcc7c`
with tree `f7271c9cb517e30c658b6d9245a739a9dc8d365d`. `final-evidence.md` records
the exact-current serial solution build, complete 5,517-success/5,533-total portfolio,
Contracts/codegen, live schema comparison, docs, standalone policy gates, fresh package hashes and
consumer, hardened pack script, and Basileus smoke. Protected PR CI and completed-diff review remain
pending.
Under verify-code's evidence-binding rule, no active obligation is `Verified` yet.

| Verdict | Count | Meaning in this ledger |
|---|---:|---|
| Indeterminate | 21 | Exact-commit local proofs exist, but protected PR evidence does not; Contracts publication separately requires its future tag run and release-package digest. |
| Violated | 0 | The proof-harness, nonvacuity, and completed-event naming fixes passed in the exact-commit local portfolio; protected binding remains pending. |
| Unproven | 2 | Downstream consumer implementation and #169 remain intentionally outside this slice. |
| Verified | 0 | No assigned proof has yet run on a protected path bound to `98fabb4`. |

The earlier promise-inventory files use provisional language from Stage 2. This ledger is the
authoritative Stage 4 verdict and does not promote those observations.

## Active obligations

### workflow-resolution-and-emission-identity — Workflow identities resolve and emit without collision

| | |
|---|---|
| **Claim** | Every source-visible bound workflow identifier resolves ordinally to exactly one merged C#/JSON workflow; zero/many matches fail with AGWF039, dynamic bindings fail with AGWF042, and distinct ordinal identifiers that collide after generated-name normalization fail with AGWF043 before source emission collides. |
| **Scope** | `WorkflowIncrementalGenerator`, `WorkflowBindingProofAnalyzer`, generated-name normalization, C#/JSON workflow catalogs, and AGWF039/042/043. |
| **Consequence** | A build can prove the wrong workflow, skip proof, or crash/overwrite generated output for a different workflow identity. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | Exact ordinal catalog grouping and generated-name collision analysis, exercised by C#/C#, C#/JSON, and JSON/JSON collision fixtures with exclusive diagnostics. |
| **Why not cheaper** | Generation cannot derive one identity authority from both authored front ends, and the type system cannot prove compilation-wide uniqueness or generated-name injectivity. |
| **Failure signal** | AGWF039/042/043 compiler errors; a generator crash is indeterminate rather than a valid failure signal. |
| **Rollback** | Remove typed binding/emission support or rename colliding workflows and regenerate. Reordering catalog input is not a rollback. |
| **Lenses** | Promise against delivery, integration completeness, authority topology, refutation. |

**Verdict:** Indeterminate. The 1,761-test generator suite and exclusive-diagnostic
collision/resolution fixtures passed locally at exact-current `98fabb4`; protected execution remains
pending.

**Open questions:**

- None.

Evidence: `obligations/inventory-promise-01-workflow-resolution.md` and
`obligations/merged-frontends-share-binding-proof.md`.

### workflow-contract-refinement — A bound workflow is a behavioral subtype of its action

| | |
|---|---|
| **Claim** | For every closed compilation-local binding, the bound requirement implies every entry requirement; every successful exit implies the bound declared guarantee; subjects match; the workflow frame is contained; and its authority join is no stronger. Definite refutations are AGWF041 and invalid/opaque inputs are AGWF042. |
| **Scope** | `WorkflowBindingProofAnalyzer`, source-linked predicate/kernel code, frame and authority analysis, and AGWF041/042. |
| **Consequence** | A consumer compiles a workflow that cannot safely implement the ontology action it is advertised to implement. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Shared refinement vectors plus real-generator entry, exit, subject, frame, authority, invalid, opaque, and counterexample fixtures. |
| **Why not cheaper** | Generated/type/structural checks can preserve shapes and graph closure but cannot establish Boolean implication, semantic forgetting, or authority/frame behavior. |
| **Failure signal** | The compiler emits AGWF041/042; after a false acceptance, no runtime refinement backstop exists. |
| **Rollback** | Remove the binding or revert the proof-bearing workflow feature. Disabling only a diagnostic would not reverse the unsafe route. |
| **Lenses** | Claim derivation, promise against delivery, proof inventory, refutation. |

**Verdict:** Indeterminate. Shared refinement vectors and real-generator fixtures passed in the
5,517-success/5,533-total local exact-current `98fabb4` portfolio; protected execution remains
pending.

**Open questions:**

- None within the explicitly compilation-local closed fragment.

Evidence: `obligations/inventory-promise-02-binding-refinement.md`.

### closed-topology-completeness — Proof uses the complete supported workflow graph

| | |
|---|---|
| **Claim** | Every executable occurrence and transition in the supported closed grammar enters the proof graph; analyzer-invisible or unsupported branch, loop, fork, approval, confidence, failure, delegate, recursive, or compensation shapes fail AGWF042 rather than certifying a smaller graph. Loop ancestry is structural and cannot be inferred from underscore-delimited display/phase names. |
| **Scope** | C# extraction, `TopologyClosureInspector`, `PhaseGraph`, JSON import rejection, and emitted transition topology. |
| **Consequence** | The analyzer proves a fiction that omits a runtime route, so all later seam and exit results are unsound. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Real-parser and real-generator topology closure/semantic matrices, including legal and refuted paths and explicit conservative exclusions. Parenthesized fluent receivers must preserve earlier invocations. Three loop kills cover an underscore-bearing body-step back-edge and the seam from loop `A` into sibling `A_B`, including when `A_B` begins in nested loop `C`; ownership is keyed by immutable outer-to-inner `RepeatUntil` `ArgumentList.SpanStart` paths. |
| **Why not cheaper** | The type system admits callbacks and collections whose values require user-code execution; structural enumeration alone does not prove semantic edge obligations. |
| **Failure signal** | AGWF042 for a recognized unclosed shape; a silently omitted route has no production signal. |
| **Rollback** | Reject the affected topology for bound workflows or remove the binding until its runtime route is represented. |
| **Lenses** | History/recurrence, production path, coverage, refutation. |

**Verdict:** Indeterminate. The exact-current `98fabb4` generator run passed 1,761/1,761,
including the topology-closure/semantics vectors and the hardened common harness/import/approval
paths. Protected execution remains pending.

**Open questions:**

- None for the documented v2.13 subset; #169 owns compensation semantics.

Evidence: `obligations/inventory-promise-03-seams-and-closure.md` and
`obligations/configured-occurrence-extraction-complete.md`.

### occurrence-action-identity — Action identity stays occurrence-scoped across every front end

| | |
|---|---|
| **Claim** | Every public configured class-step occurrence can preserve exactly one immutable ordinal `(domain, object type, action)` tuple through runtime builders, C# extraction, projection, TypeSpec JSON, import, and proof; missing, malformed/dynamic, duplicate, and distinct same-CLR-type occurrences remain distinguishable. Identity resolution precedes recursive-binding traversal, so an ambiguous occurrence that happens to name a workflow-bound action remains AGWF040 rather than being misclassified as an AGWF042 cycle. |
| **Scope** | `WorkflowActionReference`, `StepDefinition.Action`, the 12 callback surfaces, `StepExtractor`, projection, wire DTOs, import, and proof occurrence maps. |
| **Consequence** | A runtime step is proved as the wrong ontology action, silently omitted, or falsely rejected because identity collapsed to a CLR type or phase alias. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Exact public-surface inventory plus builder, extractor, projection, round-trip, named-reuse, malformed, collision, and ambiguous-self-reference diagnostic-precedence vectors. |
| **Why not cheaper** | Immutable value objects make malformed tuples unrepresentable at runtime, but cannot prove Roslyn syntax extraction or cross-representation transport. |
| **Failure signal** | AGWF040 for missing/dynamic/malformed/ambiguous occurrence identity; AGWF042 only after a uniquely resolved identity enters an opaque, invalid, or recursive proof; silent transport loss otherwise has no direct signal. |
| **Rollback** | Remove `.Performs` from the affected authoring surface or reject that shape for bound proof. |
| **Lenses** | Authority topology, exposure/compatibility, recurrence, wire refutation. |

**Verdict:** Indeterminate. The exact surface inventory, extraction matrices, projection/import
round trips, and precedence fixture passed locally in the exact-current `98fabb4` portfolio;
protected execution remains pending.

**Open questions:**

- A future callback still needs a mechanically coupled extraction vector; the present exact
  reflection inventory and behavior matrix are separate authorities.

Evidence: `obligations/inventory-promise-04-occurrence-identity.md` and
`obligations/configured-occurrence-extraction-complete.md`.

### wire-echo-semantic-equivalence — A duplicated wire occurrence is only a lossless projection echo

| | |
|---|---|
| **Claim** | A stable step ID repeated between a top-level wire list and fork path is accepted only for exactly one occurrence in each location whose complete parsed DTO graphs are field-equivalent; any action/configuration/runtime/gate/terminal/instance difference fails AGWF042. |
| **Scope** | `WireToModelBridge` identity diagnostics, all `StepDefinition` arms, nested action/configuration/retry/compensation/confidence/validation DTOs, and imported saga emission. |
| **Consequence** | List order chooses one of two contradictory occurrences, allowing compensation, retry, timeout, gate, terminal, instance, confidence, or action semantics to bypass proof. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Collision/echo fixtures plus a reflection-driven mutation test that requires every public property on every reachable wire DTO to change the structural fingerprint. |
| **Why not cheaper** | DTO generation preserves declared fields but cannot decide whether two independently serialized object graphs are equal; the compiler cannot constrain external JSON. A structural helper plus component-level import tests is the earliest sound combination. |
| **Failure signal** | AGWF042 and no saga; parser failure or unsupported fingerprint type is indeterminate and must not be accepted. |
| **Rollback** | Reject all duplicate stable IDs, including projection echoes, until semantic equivalence can be established. |
| **Lenses** | Refutation, wildcard, representable invalid states, recurrence-to-guard. |

**Verdict:** Indeterminate. The identity, recursive fingerprint, malformed-JSON, and semantic-echo
cases passed locally in the exact-current `98fabb4` portfolio; protected execution remains pending.

**Open questions:**

- None for the closed DTO graph. Duplicate JSON member names remain a general parser policy outside
  this occurrence-echo claim.

Evidence: `obligations/wire-echo-semantic-equivalence.md` and
`evaluation/refutation-occurrence-wire.md`.

### fork-noninterference-and-join — Fork footprints and joint guarantees are complete

| | |
|---|---|
| **Claim** | Every occurrence executable before a fork join, including transitive confidence handlers and a root failure handler that can run while sibling workers remain active, contributes its reads/writes; sibling paths have no write/write or write/read conflict; and the conjunction of every path tail guarantee implies the join requirement. |
| **Scope** | Fork occurrence traversal, `BuildForkFootprint`, `ProveForkJoins`, resource identity, and AGWF041. |
| **Consequence** | Concurrent paths race or the join dispatches outside its requirement despite a green build. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Legal/disjoint and refuted write/write, both write/read, transitive-confidence-handler, concurrent-root-failure-handler, and weakened-tail fixtures, with mutation kills for omitted traversal/tails/diversions. |
| **Why not cheaper** | Types and structural edge counts cannot establish semantic read/write intersections or implication of the join requirement. |
| **Failure signal** | AGWF041 when detected; missed interference may surface only as nondeterministic state corruption. |
| **Rollback** | Reject bound forks or serialize the paths. Adding an out-of-band lock is not proof of the declared contract. |
| **Lenses** | Wildcard survey, false-green shapes, refutation, coverage. |

**Verdict:** Indeterminate. Legal/refuted fork vectors and the omitted-tail, transitive-diversion,
root-failure-handler, and completed-event/`NotFound` kills passed locally in the exact-current
`98fabb4` portfolio; protected execution remains pending.

**Open questions:**

- None for the currently accepted pre-join diversion grammar.

Evidence: `obligations/inventory-promise-05-fork-noninterference.md`,
`obligations/fork-handler-footprint-complete.md`, and
`obligations/fork-join-conjunction-proof-nonvacuous.md`.

### legacy-binding-hash-stable — The typed wrapper preserves legacy binding bytes

| | |
|---|---|
| **Claim** | `.BoundToWorkflow("id")` remains source compatible, the typed reference writes the same ordinal ID in the same canonical slot, and the wrapper-only change preserves the base hash while a different ID changes it. |
| **Scope** | Both action builders, `WorkflowBindingReference`, `OntologyGraphHasher`, graph caches/freezes, and migration guidance. |
| **Consequence** | An unchanged binding produces unexplained graph-version churn or a changed route fails to invalidate the version. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | A fixed base-revision hash oracle with exact fixture provenance plus changed-ID sensitivity. |
| **Why not cheaper** | Compilation proves the overload exists but not canonical byte position or whole-fixture hash compatibility. |
| **Failure signal** | Graph version/cache churn; no diagnostic attributes it to binding serialization. |
| **Rollback** | Restore the old token bytes/slot or version the migration. Blessing a new constant is not rollback. |
| **Lenses** | Compatibility, history, proof inventory, refutation. |

**Verdict:** Indeterminate. The base-derived golden and changed-ID sensitivity passed locally in the
exact-current `98fabb4` portfolio; protected execution remains pending.

**Open questions:**

- None.

Evidence: `obligations/inventory-promise-06-string-and-hash-compatibility.md` and
`obligations/legacy-binding-hash-stable.md`.

### runtime-generator-refinement-parity — Runtime and generator classify the same contracts alike

| | |
|---|---|
| **Claim** | The public runtime refinement API and build-time workflow proof agree on status and primary obligation for the shared requirement, guarantee, subject, frame, authority, opaque, and invalid vectors. |
| **Scope** | `ActionCalculus.AnalyzeRefinement`, linked formula/solver/parser sources, workflow proof orchestration, and the neutral vector corpus. |
| **Consequence** | A contract is accepted in one environment and rejected in another, making diagnostics and reusable contracts nonportable. |
| **Proof rung** | Rung 4 — shared contract tests. |
| **Proof artifact** | One neutral corpus lowered independently through both production entry points, with exact status/obligation expectations and mutation sensitivity. |
| **Why not cheaper** | Source-linking prevents kernel-copy drift but does not unify orchestration, precedence, authority, or frame decisions. |
| **Failure signal** | Test disagreement; production has no reconciliation channel. |
| **Rollback** | Route both callers through one semantic result or revert the divergent orchestration. |
| **Lenses** | Authority topology, promise against delivery, proof-layer fit. |

**Verdict:** Indeterminate. The neutral runtime/generator vector corpus passed locally in the
exact-current `98fabb4` portfolio. The generator still exposes diagnostic IDs plus stable message
fragments rather than a structured obligation token, and protected execution remains pending.

**Open questions:**

- Whether to expose a structured generator failure category is a durability improvement, not a
  prerequisite for the current finite vector set.

Evidence: `obligations/inventory-promise-07-runtime-generator-parity.md` and
`obligations/runtime-generator-refinement-parity.md`.

### contracts-wire-contract — Contracts 0.11 preserves optional occurrence identity correctly

| | |
|---|---|
| **Claim** | Contracts 0.11.0 defines one optional `action` on all five step arms; when present all three action identity strings are required and nonblank; the workflow-name lookup identity is also nonblank; read/write and projection/import preserve them; action omission retains the legacy JSON shape; and the closed diagnostic vocabulary includes AGWF039–AGWF043. |
| **Scope** | TypeSpec, generated C#, standalone/bundled JSON Schema, serializer options, projection/import, package content, and diagnostic enum/catalog. |
| **Consequence** | Producer and consumer disagree about validity or silently lose/rewrite an action identity. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | TypeSpec compile/schema checks, generated-model read/write tests, projection/import round trips, omission tests, DTO/schema conformance, and package-content assertions. |
| **Why not cheaper** | Regeneration proves representations agree with the emitter, not that serializer callbacks, omission, or both adapters behave correctly. |
| **Failure signal** | Schema/deserialization failure downstream; without the tests, missing optional data can remain silent. |
| **Rollback** | Stop emitting `action`, revert to Contracts 0.10, and suppress new diagnostic tokens until consumers upgrade. |
| **Lenses** | Exposure/compatibility, authority topology, coverage, wire refutation. |

**Verdict:** Indeterminate. At exact-current `98fabb4`, TypeSpec/C# regeneration was stable, Contracts
passed 185/185, the full portfolio exercised wire round trips, and the fresh Contracts nupkg digest is
`9fa47ba6a373d3565d3ef9328beb41f277a348cd2d0517f741cf7557f0d724b5`. Protected execution remains
pending.

**Open questions:**

- None.

Evidence: `obligations/inventory-promise-08-contract-wire-shape.md`.

### generated-validation-semantics — Generated nonblank checks match their schema

| | |
|---|---|
| **Claim** | The exact non-whitespace schema pattern produces generated read/write validation for required and optional strings without affecting unrelated regex patterns or scalar aliases. |
| **Scope** | `RecordEmitter`, `ContractJsonValidation`, `ActionReferenceV1`, and every current schema matching the exact pattern. |
| **Consequence** | Generated models accept schema-invalid JSON, reject valid legacy payloads, or broaden a generic emitter change unexpectedly. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Emitter boundary vectors plus serializer read/write cases over current affected generated records; regeneration is a prerequisite, not the behavioral oracle. |
| **Why not cheaper** | A clean generated diff is circular evidence for emitter behavior, and compilation cannot execute JSON callbacks. |
| **Failure signal** | Downstream `JsonException` or external schema-validator disagreement; no centralized runtime alert. |
| **Rollback** | Restrict validation to the new contract or revert the generic emitter rule and regenerate. |
| **Lenses** | False-green shapes, authority topology, proof-layer fit. |

**Verdict:** Indeterminate. At exact-current `98fabb4`, ActionReference and workflow-name serializer
vectors passed, regeneration was stable, and the required/optional/unrelated-pattern/scalar-alias
emitter-boundary kill passed in the complete portfolio. Protected execution remains pending.

**Open questions:**

- None after `98fabb4` aligned both `ActionReferenceV1` and `WorkflowDefinitionV1.Name` on the exact
  non-whitespace pattern; the action link-path scalar does not produce a record callback.

Evidence: `obligations/generated-nonblank-validation-semantic.md`.

### merged-frontends-share-proof — C# and JSON enter one proof path

| | |
|---|---|
| **Claim** | Authored and imported workflows enter one ordinal catalog and one binding analyzer, with equivalent legal/refuted results and stable duplicate precedence. |
| **Scope** | `AdditionalText` parsing, `WireToModelBridge`, aggregate generator wiring, workflow catalog merge, proof, and source emission ordering. |
| **Consequence** | JSON bypasses proof, mixed catalogs crash emission, or equivalent contracts differ by authoring format. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Real-generator legal/illegal imports and all three front-end duplicate pairings, with exact exclusive errors and generated-output compilation. |
| **Why not cheaper** | Schema/type checks stop before parse/bridge/catalog/proof/emission integration. |
| **Failure signal** | Compiler error or generator crash; missing proof has no runtime signal. |
| **Rollback** | Disable imported bindings or reject mixed catalogs until the aggregate route is sound. |
| **Lenses** | Integration completeness, proof inventory, refutation. |

**Verdict:** Indeterminate. C# and imported workflows enter the same catalog/proof path, and their
generated outputs passed the repaired common harness in the exact-current `98fabb4` portfolio.
Protected execution remains pending.

**Open questions:**

- None.

Evidence: `obligations/merged-frontends-share-binding-proof.md`.

### rooted-descriptor-catalog — Only executable rooted descriptor actions satisfy proof lookup

| | |
|---|---|
| **Claim** | Direct `ActionDescriptor` syntax participates only when rooted in `DomainOntology.Define -> ObjectTypeFromDescriptor -> Actions`; unrelated, conditional, escaped, or subject-mismatched descriptors cannot satisfy workflow occurrences. |
| **Scope** | `OntologyActionCatalog.InventoryDirectDescriptorActions`, ontology ownership and SymbolKey-only descriptor paths. |
| **Consequence** | Dead syntax proves an action the runtime graph never registers, or supported rooted descriptors silently disappear from proof. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Real-generator rooted legal/illegal and unrooted controls whose diagnostic expectations require descriptor inventory to execute. |
| **Why not cheaper** | Syntax ancestry is structural, but the full claim also requires parsed action contracts to resolve into a workflow proof. |
| **Failure signal** | AGWF040/042 for a rejection; a vanished bound-action inventory can otherwise make the analyzer skip with no signal. |
| **Rollback** | Require fluent ontology authoring or reject direct descriptors until ownership is provable. |
| **Lenses** | Production path, false-green shapes, refutation. |

**Verdict:** Indeterminate. The rooted legal/ownership controls and the bound-action/missing-occurrence
AGWF040 kill passed in the exact-current `98fabb4` generator portfolio, making wholesale or selective
inventory loss observable. Protected execution remains pending.

**Open questions:**

- None.

Evidence: `obligations/rooted-descriptor-catalog-proof-nonvacuous.md`.

### proof-fixture-subject-binding — Proof fixtures compile and diagnose the intended subject

| | |
|---|---|
| **Claim** | Every #167 parser/generator proof rejects invalid authored input, unexpected generator errors, and unexpected updated-compilation errors; negative cases distinguish the intended AGWF result from unrelated failure. |
| **Scope** | `GeneratorTestHelper`, `ParserTestHelper`, local import runners, and all #167 proof suites. |
| **Consequence** | A broken fixture or broken generated tree coexists with the expected substring/diagnostic and CI reports a false green. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | One shared fail-closed harness plus kill fixtures for input compilation, generator-driver diagnostics, updated compilation, and unrelated expected-error lookalikes. |
| **Why not cheaper** | Roslyn types make diagnostics available but do not cause the test to fail unless the harness partitions and asserts them; static inventory cannot establish a generator execution. |
| **Failure signal** | Consumer compile failure; the shared harness separately rejects invalid authored input, unexpected driver/generator errors, and unexpected updated-compilation errors. |
| **Rollback** | Disable any proof assertion that cannot bind its compilation subject until the shared harness is authoritative. |
| **Lenses** | False-green shapes, proof inventory, evidence binding, refutation. |

**Verdict:** Indeterminate. At exact-current `98fabb4` the shared helper validates authored compilation,
driver and generator diagnostics, and updated compilation; all raw #167 import/approval routes use
the validated path, and the complete 1,761-test generator suite passed. The narrow intentionally
unlowerable topology-rejection exception remains explicit. Protected execution remains pending.

**Open questions:**

- None.

Evidence: `obligations/binding-proof-fixtures-compile-intended-subject.md`.

### public-api-authority — The complete #167 public surface is build-baselined

| | |
|---|---|
| **Claim** | The ten reviewed builder interfaces, `WorkflowActionReference`, and the full `StepDefinition` carrier type are exactly in PublicApiAnalyzer scope and baseline; removing or reshaping them without a declared API change fails the gate. |
| **Scope** | `.editorconfig`, `PublicApi.globalconfig`, shipped/unshipped ledgers, reflection closure tests, and `check-builder-api-stability.sh`. |
| **Consequence** | A cross-product consumer breaks while Strategos CI remains green. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | Exact file/member allowlist closure, PublicApiAnalyzer warnings-as-errors build, and a real baseline mutation self-test. |
| **Why not cheaper** | Ordinary compilation accepts deliberate source-shape changes; the obligation is policy over source plus a reviewed ledger. |
| **Failure signal** | RS0016/RS0017-family errors and a nonzero gate result; infrastructure failure is a distinct nonzero indeterminate result. |
| **Rollback** | Restore compatibility or explicitly baseline/document the break. Updating the ledger without review is not rollback. |
| **Lenses** | Authority topology, recurrence, coverage, proof-layer fit. |

**Verdict:** Indeterminate. The exact-current solution build/tests exercise the API closure on
`98fabb4`; the standalone analyzer/mutation gate also passed on that subject as recorded in
`final-evidence.md`. Protected execution and review remain pending.

**Open questions:**

- None. The existing mutation kills the shared analyzer/gate mechanism; it need not duplicate one
  mutation per baseline line.

Evidence: `obligations/public-api-gate-covers-binding-surface.md`.

### schema-compatibility-fails-closed — Contracts schema comparison uses the published subject

| | |
|---|---|
| **Claim** | The complete candidate schema tree is compared with the latest stable published Contracts package, missing/unreadable baseline is indeterminate and blocking, nested/ref/discriminator/union/item/constraint narrowing is detected, and required version progression is enforced. |
| **Scope** | `contracts-schema-diff.yml`, `JsonSchemaDiff`, the Node classifier, and their mutation/cross-implementation tests. |
| **Consequence** | A nominally additive Contracts release breaks existing producers/consumers behind a green or skipped compatibility check. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | Package-bound baseline extraction with digest plus complete recursive comparator vectors, implementation parity, and empty/unreadable baseline kills. |
| **Why not cheaper** | Regeneration proves current self-consistency, not compatibility with immutable published bytes; compilation cannot classify JSON Schema evolution. |
| **Failure signal** | Nonzero CI result distinguishing breaking change from baseline/tool indeterminacy. |
| **Rollback** | Restore an additive schema or issue the required version bump before publication. |
| **Lenses** | False-green shapes, exposure/compatibility, proof inventory, coverage. |

**Verdict:** Indeterminate. The recursive C#/Node vectors and live production command passed locally
at exact-current `98fabb4`, comparing against published Contracts 0.4.0 nupkg
`503f565462a48518367fd4366bec6698c092c91d2a4dbde2d4f4925c1139ee0b`; four breaking changes were
permitted by the pre-1.0 minor bump. Baseline/network failure remains a distinct blocking outcome by
design, and protected execution remains pending.

**Open questions:**

- Network/package unavailability remains indeterminate by design and blocks the gate.

Evidence: `obligations/contracts-schema-diff-fails-closed.md`.

### generated-diagnostic-authority — Diagnostic IDs and metadata have one generated authority

| | |
|---|---|
| **Claim** | AGWF039–AGWF043 IDs/order and entry metadata cannot diverge between TypeSpec enum, entry schemas, generated C#/catalog/docs, and live Roslyn descriptors. |
| **Scope** | `AgwfCatalog.tsp`, `AgwfCatalogEmitter`, generated contracts/catalog/docs, descriptor parity, and codegen drift gates. |
| **Consequence** | The analyzer emits a token a generated consumer rejects or publishes conflicting title/severity/message metadata. |
| **Proof rung** | Rung 1 — construction and generation. |
| **Proof artifact** | Code generation rejects enum/entry-set disagreement; clean regeneration and descriptor parity bind all derived representations. |
| **Why not cheaper** | This is the cheapest rung: repeated representations are derived and regeneration must be clean. |
| **Failure signal** | Codegen/test/CI diff failure; if regeneration does not run, the result is indeterminate, never pass. |
| **Rollback** | Revert the new diagnostic entries or regenerate all outputs from the restored TypeSpec source. |
| **Lenses** | Authority topology, history/recurrence, proof-layer fit. |

**Verdict:** Indeterminate. Clean regeneration, generated diagnostic authority, and catalog/live-
descriptor parity passed at exact-current `98fabb4`; protected execution remains pending.

**Open questions:**

- None.

Evidence: `obligations/inventory-promise-10-authority-and-api-governance.md`.

### contracts-release-revalidates-artifacts — The Contracts tag re-establishes package evidence

| | |
|---|---|
| **Claim** | A `contracts-v0.11.0` job checks out the tag commit, regenerates TypeSpec/C#, runs Contracts tests, requires a clean generated tree, compares against the published package, packs the validated bytes, and records the candidate digest before publication. |
| **Scope** | `publish-contracts.yml`, TypeSpec toolchain, Contracts tests, schema diff, pack, and NuGet publish. |
| **Consequence** | Immutable NuGet bytes differ from the reviewed/validated contract and cannot be repaired in place. |
| **Proof rung** | Rung 5 — production-path integration test. |
| **Proof artifact** | The actual tag workflow log and nupkg digest, bound to the tag commit and tool versions. |
| **Why not cheaper** | Generation, compilation, and project tests do not prove the publication composition or package bytes selected by NuGet. |
| **Failure signal** | Nonzero tag job before publish; downstream breakage is the late fallback signal. |
| **Rollback** | Publish a corrected higher version and have consumers pin/upgrade; published NuGet bytes are immutable. |
| **Lenses** | Evidence binding, production path, compatibility, coverage. |

**Verdict:** Indeterminate. The current workflow contains the required revalidation steps, but no
tag run or tag-produced candidate digest exists yet.

**Open questions:**

- None before release; the actual tag run supplies the evidence.

Evidence: `obligations/contracts-release-revalidates-artifacts.md`.

### packed-consumer-binding — The shipped package loads and enforces #167

| | |
|---|---|
| **Claim** | An isolated consumer restores the exact locally packed Strategos packages, compiles a legal typed binding, and fails an illegal binding exclusively because one AGWF041 seam is refuted, with package IDs/versions/digests recorded. |
| **Scope** | CI `pack-verify`, local-feed/NuGet configuration, package selection, analyzer loading, and legal/illegal consumer builds. |
| **Consequence** | In-repo tests pass while the shipped analyzer is absent, a remote/stale package is selected, or an unrelated compile error satisfies the negative probe. |
| **Proof rung** | Rung 5 — production-path integration test. |
| **Proof artifact** | Hermetic local-only restore, manifest/digest checks, zero-error legal build, exact exclusive AGWF041 negative build, and wrong-package/unrelated-error kills. |
| **Why not cheaper** | Project references and nupkg file inspection cannot prove NuGet selection, analyzer loading, or consumer compilation. |
| **Failure signal** | Nonzero probe with classified cause; restore/tool failure is indeterminate, not a product pass. |
| **Rollback** | Pin the prior package set or withdraw the unpublished candidate. |
| **Lenses** | False-green shapes, packaging, production path, refutation. |

**Verdict:** Indeterminate. The exact-current `98fabb4` fresh external package probe compiled the
legal consumer with 0 warnings/errors and rejected the illegal consumer with AGWF041; all six
consumed package hashes are recorded in `final-evidence.md`. The exact hardened pack script and
Basileus smoke also passed on that subject. Protected `pack-verify` execution and review remain
pending.

**Open questions:**

- None.

Evidence: `obligations/inventory-promise-09-packed-consumer.md` and
`obligations/packed-probe-binds-local-artifacts-and-cause.md`.

### ci-proof-policy-version-pinned — Required CI executes an immutable policy revision

| | |
|---|---|
| **Claim** | Required reusable build/test, coverage, and baseline workflows run from reviewed immutable SHAs so a green result identifies the policy that selected the #167 proof suites. |
| **Scope** | Required reusable workflow invocations in `.github/workflows/ci.yml`. |
| **Consequence** | A moved tag silently changes or omits proof while the same product commit remains green. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | SHA-only `uses` policy with a structural check rejecting movable refs on required jobs. |
| **Why not cheaper** | Generation and C# compilation cannot constrain GitHub Actions workflow references. |
| **Failure signal** | Structural/CI failure; a moved tag historically produced no product-visible signal. |
| **Rollback** | Restore the last reviewed workflow SHA or vendor the proof commands. |
| **Lenses** | False-green shapes, evidence binding, coverage. |

**Verdict:** Indeterminate. All three required reusable invocations are pinned to reviewed commit
`ffd6fb4979fba8a4317cca6fd7ad9ec4090acff9`; actionlint and shellcheck passed locally at
exact-current `98fabb4`. No protected run is bound to that subject.

**Open questions:**

- None for the required reusable jobs in scope.

Evidence: `obligations/ci-proof-policy-version-pinned.md`.

### docs-build-before-merge — Changed documentation compiles on the PR subject

| | |
|---|---|
| **Claim** | The same PR/merge subject that changes #167 documentation runs `npm ci` and the Starlight production build before merge. |
| **Scope** | `docs/**`, the CI `docs-build` job, package lock, and Astro/Starlight build. |
| **Consequence** | Code merges with broken or stale migration/reference documentation and failure appears only after main deployment. |
| **Proof rung** | Rung 5 — production-path integration test. |
| **Proof artifact** | PR CI documentation build bound to the final revision. |
| **Why not cheaper** | Markdown/path checks and isolated content tests do not execute the deployable site composition. |
| **Failure signal** | Nonzero PR docs job; absent/skipped job is indeterminate. |
| **Rollback** | Revert or repair the documentation and rebuild before merge. |
| **Lenses** | Coverage, evidence binding, false-green shapes. |

**Verdict:** Indeterminate. The PR workflow contains the unconditional docs job, and the
exact-current `98fabb4` locked install/Starlight build emitted and Pagefind-indexed 80 pages with no
tracked changes. The PR job itself has not run on the protected path.

**Open questions:**

- None.

Evidence: `obligations/docs-build-before-merge.md`.

### compilation-local-proof-boundary — Unsupported catalogs never masquerade as proved

| | |
|---|---|
| **Claim** | #167 explicitly proves only ontology actions and workflows visible to the same Roslyn compilation plus JSON `AdditionalFiles`; referenced assemblies and runtime `IOntologySource` catalogs are not represented as a successful proof and remain tracked by #204. |
| **Scope** | `OntologyActionCatalog`, generator packaging/docs, unrooted descriptors, and #204. |
| **Consequence** | Users rely on a cross-assembly guarantee the analyzer cannot observe and receive no compile-time or runtime enforcement. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | Source-only catalog implementation, fail-closed unrooted cases, and consistent public documentation linking #204. |
| **Why not cheaper** | Types cannot make metadata catalogs appear; this is a reachability/ownership boundary across compilation inputs. |
| **Failure signal** | No proof is emitted for out-of-scope metadata; documentation and explicit follow-up prevent treating that absence as success. |
| **Rollback** | Co-locate sources/AdditionalFiles or remove the binding until portable catalogs land. |
| **Lenses** | Production path, claim derivation, scope reconciliation. |

**Verdict:** Indeterminate. Source-only catalog ownership controls, unrooted rejection fixtures, and
the explicit #204 documentation boundary passed in the exact-current `98fabb4` local portfolio.
Protected execution and review remain pending.

**Open questions:**

- None; portable catalogs are explicitly #204, not an implicit #167 success path.

Evidence: `obligations/inventory-promise-11-compilation-local-boundary.md`.

### downstream-contract-adoption — Basileus and Exarchos adopt the published boundary

| | |
|---|---|
| **Claim** | Dedicated Basileus and Exarchos issues identify Contracts 0.11.0, AGWF039–AGWF043, optional action identity, and the expanded builder/API mirror work before #167 lands. |
| **Scope** | External contract consumers and #153's adoption rule. |
| **Consequence** | Producers emit fields/tokens or source breaks before closed-enum and API-mirror consumers can accept them. |
| **Proof rung** | Rung 5 — production-path integration/consumer evidence. |
| **Proof artifact** | Issue URLs plus downstream package/round-trip/API-mirror fixtures and linked adoption PRs. |
| **Why not cheaper** | Producer generation/compilation cannot establish independently maintained consumer compatibility. |
| **Failure signal** | Downstream build/deserialization failure; no producer-side check substitutes for adoption. |
| **Rollback** | Delay emission/release or pin consumers to the previous producer/package. |
| **Lenses** | Exposure/compatibility, coverage, wildcard. |

**Verdict:** Unproven for completed consumer adoption, but #153's pre-landing coordination rule is
satisfied: Basileus issue [#493](https://github.com/lvlup-sw/basileus/issues/493) and Exarchos issue
[#1893](https://github.com/lvlup-sw/exarchos/issues/1893) are created and linked from Strategos #167.
Their eventual adoption PRs and independent consumer results remain outside this producer slice.

**Open questions:**

- None; the dedicated repositories own the linked adoption issues.

Evidence: `obligations/inventory-promise-12-downstream-and-delivery.md`.

### compensation-handoff-to-169 — #169 reuses the occurrence identity rather than inventing another map

| | |
|---|---|
| **Claim** | Until #169 lands, configured compensation makes a bound workflow unprovable with AGWF042; #169 then consumes `WorkflowActionReference`/the closed proof graph as its forward identity and does not add a parallel CLR-type map. |
| **Scope** | `Compensate<T>` topology closure, AGWF042, issue #169, and the future rollback planner. |
| **Consequence** | #167 falsely certifies rollback behavior or #169 creates a second action identity that can drift from proof/runtime routing. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Current compensation rejection fixture plus #169's derived-plan tests over the existing occurrence identity. |
| **Why not cheaper** | Types expose the identity but cannot establish the future inverse/rollback algorithm's use of it. |
| **Failure signal** | AGWF042 before #169; after #169, a mismatch must be a dedicated build diagnostic. |
| **Rollback** | Keep compensation excluded from bound workflows until the downstream proof exists. |
| **Lenses** | Claim derivation, dependency coverage, wildcard. |

**Verdict:** Unproven by design. The #167 rejection half passed at exact-current `98fabb4`; #169's
durable occurrence/scope journal, inverse proof, and derived rollback consumer do not yet exist.

**Open questions:**

- None for #167; #169 owns the future proof.

Evidence: `obligations/inventory-promise-12-downstream-and-delivery.md`.

## Refuted

### empty-workflow-entry-proof-defined — Empty workflows require binding semantics

**Refuted by reachability.** Imported workflows with `Steps.Count == 0` are rejected by
`WireToModelBridge` with `NoStepsFound` before a `WorkflowModel` exists. The public C# path cannot
return a `WorkflowDefinition<TState>` from `Workflow<TState>.Create` alone; `Finally<TStep>` is the
terminal operation and throws unless `StartWith` established an entry. A compiler-valid authored or
imported bound workflow therefore cannot reach `PhaseGraph.CompletedPhase` directly from entry. The
`ProveEntry` empty branch is defensive internal handling, not an active #167 correctness obligation.

Refutation evidence: `evaluation/refutation.md` and
`obligations/empty-workflow-entry-proof-defined.md`.

No surviving obligation depends on empty-workflow support. `closed-topology-completeness` continues
to require that both public front ends reject malformed/unsupported topology before proof.

## Run-wide open questions

- Protected PR CI and the one completed-diff CodeRabbit pass remain delivery evidence to collect.
- The future `contracts-v0.11.0` tag must regenerate and bind its own published nupkg bytes; PR
  evidence cannot authorize a different release artifact.
- Basileus/Exarchos adoption implementations and #169 remain intentionally outstanding under their
  linked issues.
