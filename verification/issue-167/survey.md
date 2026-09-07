---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
evidence_binding: Stage 1 discoveries are historical; final subject is 98fabb410e4432cc39fd71ec92651e6ceecfcc7c, with all recorded local proofs exact-current, including serial solution build, complete tests, Contracts/codegen, schema, docs, standalone gates, fresh packages and consumer, hardened pack script, and Basileus smoke; protected CI and review remain unbound
cost_setting: high
scope_rule: reverse dependency closure of every changed public, generated, analyzer, runtime, packaging, and documentation surface against merge-base 45c86a63a9437abd920240b4dc95b235c0f72d37
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: merged action-calculus semantics reused by the binding proof
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: immediate downstream consumer of occurrence-level action identity
  - path: https://github.com/lvlup-sw/strategos/issues/204
    why: explicit follow-up for portable cross-assembly proof catalogs
---

# Stage 1 survey synthesis — issue #167

## Subject and reading discipline

The seven directed survey lenses plus the independent wildcard lens originally inspected a changing
pre-commit working tree and preserve that discovery history. The current immutable product subject is
commit `98fabb410e4432cc39fd71ec92651e6ceecfcc7c` with tree
`f7271c9cb517e30c658b6d9245a739a9dc8d365d`. Historical commit `595a949` introduced the closed raw
import/approval harness routes, rooted-descriptor and emitter-boundary kills, and shared-type fork
`NotFound` alignment; `6851fe7` serialized the pack script; and current `98fabb4` aligns workflow-
name nonblank validation. The complete local evidence portfolio binds to `98fabb4`. Protected PR
execution and review remain pending, so no active obligation is verified.

The detailed evidence remains in `verification/issue-167/survey/`. This synthesis records the scope,
deduplicated findings, and Stage 2 seeds; it does not replace the lens records.

## Scope set

1. Typed ontology-to-workflow binding: `WorkflowBindingReference`, both action-builder families,
   `ActionDescriptor`, graph hashing, and existing string-overload compatibility.
2. Occurrence-level ontology action identity: `WorkflowActionReference`, all configured fluent
   builder surfaces, `StepDefinition`, projection, TypeSpec 0.11.0, generated C#/JSON Schema, and
   imported JSON.
3. Closed proof integration: compilation-local ontology/action cataloging, C# and JSON workflow
   catalogs, topology closure, shared predicate parsing/finite solver, entry/seam/exit, failure and
   approval ingress, frame, authority, fork noninterference, recursion, and diagnostic precedence.
4. Runtime refinement: action/composite behavioral subtyping, total result types, authority product
   order, frame containment, cancellation, and runtime/generator proof parity.
5. Delivery boundaries: generator packaging, isolated NuGet consumer compilation, public API
   ledgers, TypeSpec regeneration, schema compatibility, docs, changelogs, downstream consumers,
   and stable graph identity.

## Deduplicated survey findings and current disposition

### Compilation-local proof boundary

The production-path lens established that the workflow generator can inspect only ontology and
workflow declarations visible to the same Roslyn compilation. Referenced assemblies and runtime
`IOntologySource` contributions are not re-proved. This is a real boundary, not a runtime fallback.
The implementation now states that boundary in the action-calculus, migration, workflow API, and
diagnostic documentation. Portable catalogs are explicitly deferred to #204. This disposition also
preserves the architectural rule that `Strategos.Ontology.Generators` remains analyzer-only.

### Executable catalog ownership

The production-path lens reproduced a false proof in which unrelated static `ActionDescriptor`
constructors satisfied workflow leaves despite never reaching an ontology graph. The catalog now
admits direct descriptors only when they occur inline under
`DomainOntology.Define -> ObjectTypeFromDescriptor -> ObjectTypeDescriptor.Actions`. Unrooted
workflow bindings remain visible as AGWF042, while unrooted leaf descriptors cannot resolve an
occurrence. The positive ownership fixture is SymbolKey-only, so this restriction does not restore
CLR identity as authority.

### Fork confidence-handler footprint

The wildcard lens reproduced a write/write race that passed because a fork path's low-confidence
handler was omitted from its concurrent footprint. Fork footprint construction now walks transitive
low-confidence handler chains. Dedicated write/write, write/read, and disjoint-frame cases exercise
the repaired class; Stage 3 must mutation-check that removing the traversal makes the conflict
fixtures fail to detect the defect.

### Public API authority

The authority-topology lens found the original public API gate covered only part of the changed
surface. The gate now includes the three changed continuation interfaces,
`WorkflowActionReference`, and the complete `StepDefinition` surface in its exact analyzer baseline,
while keeping the historical seven entry points. A shared analyzer mutation proves the mechanism and
exact-scope/shape tests bind the new paths. The exact-current `98fabb4` gate passed; protected
execution and review remain pending.

### Proof-harness and integration gaps

The existing-proof lens found missing imported-workflow proof, topology-semantic coverage, packed
consumer proof, authored-input validation, unexpected-error rejection, and independent provenance
for the legacy graph hash. The working tree now contains:

- real `AdditionalText` JSON legal/illegal proof and C#/JSON, JSON/JSON, and C#/C# duplicate-catalog
  diagnostics;
- a 17-case real-generator topology matrix for branches, loops, approvals, failure handlers,
  low-confidence routing, and fork/join;
- shared runtime/generator refinement vectors;
- a shared generator harness that rejects authored-input, driver/generator, and updated-compilation
  errors, plus validated parser entry points used by the raw import and approval call sites;
- an artifact/source/digest-bound isolated packed consumer that compiles a legal binding and requires
  an illegal seam to fail exclusively with the expected AGWF041 witness; and
- a legacy hash constant independently reconstructed from base revision `45c86a6`.

These mechanisms passed as part of the complete exact-current `98fabb4` local portfolio. The fresh
package/consumer probe, serialized pack script, shellcheck, and Basileus smoke likewise have exact-
current local evidence in `final-evidence.md`; protected evidence remains pending.

### Stable duplicate diagnostics and workflow vocabulary

An independent invariant pass found that duplicate C# workflow identities could still collide at
`AddSource` before aggregate AGWF039 analysis, and that two proof reasons exposed graph terminology.
Bound duplicate emission is now suppressed across C#/C#, C#/JSON, and JSON/JSON cases only long
enough for the aggregate proof to emit AGWF039. User-facing reasons use workflow transitions and
successful completion terminology. A second invariant pass found no remaining violation.

## Claim-derived seed set

Each seed below must become an obligation, an evidence-backed refutation, or an explicit open
question in Stage 2.

1. Every bound workflow identity resolves ordinally to exactly one C# or imported workflow; zero or
   multiple matches fail the build with AGWF039 and no generator crash.
2. The bound action requirement implies every possible workflow entry requirement, every successful
   workflow exit implies the bound guarantee, workflow authority is no stronger, and workflow frame
   is contained by the bound action frame.
3. Every reachable non-failure transition is composable under the same closed predicate semantics as
   runtime action refinement; opacity, contradiction, invalidity, recursion, and dynamic topology
   fail closed rather than becoming `True`.
4. Every executable occurrence in the supported closed topology has one exact names-only action
   identity, and C# authoring, runtime projection, TypeSpec JSON, import, and proof preserve it.
5. Fork paths are noninterfering over every occurrence reachable before join, including confidence
   diversions, and the joint tail guarantee is used at the join.
6. The string `BoundToWorkflow` overload remains source-compatible, the typed property occupies the
   legacy graph-hash slot, and an unchanged binding retains the independently reconstructed hash.
7. Public refinement result types reject contradictory or undefined states and runtime/generator
   classifications agree for the shared vectors.
8. Contracts 0.11.0 packages the optional action member, rejects present malformed identities,
   accepts older action-omitting JSON, and regenerates deterministically under the repository's
   SymbolKey-only constraints.
9. The packaged generator is loaded in a consumer using only produced Strategos nupkgs plus declared
   runtime dependencies, compiles the legal binding, and blocks the illegal binding with AGWF041.
10. Public API, diagnostic, schema, projection, and documentation representations are governed by an
    executable authority rather than matching only by convention.
11. Compilation-local/source-visible proof is the supported #167 boundary; portable referenced-
    assembly catalogs remain unresolved product work in #204 rather than an implied success path.
12. #169 can consume the occurrence-level action identity and closed proof graph without inventing a
    parallel workflow/action mapping; compensation itself remains deliberately rejected by #167.

## Recurrence-to-guard seed set

| Recurrent class | Prior evidence | Present exposure | Candidate earliest guard |
|---|---|---|---|
| Accepted fluent surface omitted or under-scoped downstream | Repeated #135/#143/#144/#187/#196 fixes | Every configured occurrence and topology extractor | Structural inventory plus real-generator kill fixtures for each accepted callback shape |
| Semantic identity reduced to a weaker key | #31/#189/#190/#191/#196 | Phase identity versus CLR step type and full action triple | Names-only value objects, conflict detection, and cross-front-end round trips |
| Green control observes the wrong or incomplete subject | At least four historical harness failures | Synthetic generator fixtures, packed analyzer, CI exclusions | Compiler-clean fixture helper, unexpected-error rejection, isolated package probe, and guard self-tests |
| Canonical hash or ordering omits a structural field | PR #49 | Typed workflow binding replaces the old string property | Total canonical serializer plus before/after and permutation fixtures |
| Generated diagnostic metadata drifts from live descriptors | #102/#105/#109 | AGWF039-AGWF043 span TypeSpec, C#, schema, docs | TypeSpec generation plus catalog/live-descriptor parity and mutation checks |

## Current evidence boundaries after Stage 3

- Structural closure, semantic legal/refuted topology vectors, and the rooted-descriptor nonvacuity
  kill passed in the complete exact-current `98fabb4` local portfolio; protected execution and
  review remain pending.
- The schema classifier now recursively covers the relevant constraint/reference/union vocabulary
  against the latest stable published nupkg; its protected result is pending.
- CI contains pinned reusable-workflow refs and packed-consumer/API/docs jobs, but no protected check
  URL is yet bound to final subject `98fabb4`.
- Basileus #493 and Exarchos #1893 establish coordination only. Their adoption implementations/tests
  and #169's consumer semantics remain `Unproven`.
- A future Contracts tag must create fresh release evidence; the workflow's presence and PR-local
  package digests cannot establish that event.
