---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
cost_setting: high
scope_rule: cheapest-sound-rung review of every synthesized issue-167 obligation
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: requested enforcement class and acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: proof-kernel semantics reused by workflow refinement
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: downstream compensation proof boundary
---

# Evaluation — proof-layer fit

## Findings

### PLF-1 — Split the former downstream/delivery obligation by proof rung

The Stage 2 promise file combined #167 compensation rejection, future #169 semantic reuse, external
adoption issues, and PR/merge evidence. No one rung can establish that claim. The ledger now splits it
into `compensation-handoff-to-169` at rung 4 and `downstream-contract-adoption` at rung 5. Delivery
authorization remains process state, not a product correctness proof.

**Disposition:** refinement applied to the ledger.

### PLF-2 — Generated diagnostic consistency belongs at generation, not component-test gravity

AGWF039–AGWF043 ID/order/metadata consistency is a repeated-representation claim. The current emitter
can reject enum/entry disagreement and derive C#, catalog, and docs. Its cheapest sound rung is 1, with
a clean regeneration. Descriptor emission behavior remains covered by the workflow component
obligations rather than inflating this one to rung 4.

**Disposition:** refinement applied as `generated-diagnostic-authority`.

### PLF-3 — Topology closure needs rung 4 because its claim includes semantics

`TopologyClosureInspector` is a rung-3 structural guard, but `closed-topology-completeness` also claims
that the represented graph contains the semantic entry/seam/exit obligations used at runtime. Merely
counting callbacks or graph edges cannot establish that. The latest attack demonstrated why: parsing
all loops still omitted seams when ownership was inferred from flattened underscore-delimited names.
The structural outer-to-inner `RepeatUntil` invocation path is a rung-3 mechanism; the refuted
underscore-bearing back-edge and two sibling-ingress contracts are the rung-4 proof that the
resulting graph has the required semantics.
Keeping the combined obligation at rung 4 is therefore sound.

**Disposition:** assigned rung retained.

### PLF-4 — Wire fingerprint coverage is a rung-3 guard inside a rung-4 boundary proof

Reflection makes omission of a DTO property from the fingerprint mechanically detectable. That is a
structural guard. Acceptance/rejection of duplicated JSON occurrences still crosses parsing,
identity classification, import composition, diagnostics, and saga emission, so the full
`wire-echo-semantic-equivalence` obligation stays at rung 4.

**Disposition:** new obligation assigned to the cheapest sound combined rung.

### PLF-5 — Fixture subject integrity is correctly rung 4 and has committed local support

The compiler exposes input, driver, generator, and updated-compilation diagnostics, but no type-system
rule makes a TUnit fixture fail on them. A shared component harness plus kill cases is therefore the
right rung. `RunGeneratorWithValidInput` collects all those channels and has four self-tests. Commit
`595a949` also migrated the remaining raw import runners and approval-continuation parser calls to
validated helpers; the complete local portfolio passed at exact-current `98fabb4`. Protected
execution and review remain pending.

**Disposition:** rung retained; current verdict is `Indeterminate` pending protected execution and
review.

### PLF-6 — Package and release claims cannot be moved down

`packed-consumer-binding`, `contracts-release-revalidates-artifacts`, and
`downstream-contract-adoption` require real package selection, analyzer loading, toolchain execution,
or independent consumer behavior. Project-reference tests and nupkg inspection do not establish those
facts. Rung 5 is justified despite its cost.

**Disposition:** assigned rungs retained.

### PLF-7 — Diagnostic precedence is part of occurrence-resolution behavior

AGWF040 versus AGWF042 is not just generated metadata. Recursive-binding discovery previously had an
opportunity to traverse an ambiguous occurrence before occurrence resolution rejected it. The
cheapest sound proof is the real analyzer component case whose ambiguous self-reference must produce
one AGWF040 and no cycle diagnostic; a catalog-shape check alone cannot establish execution order.

**Disposition:** kept inside the rung-4 `occurrence-action-identity` obligation.

### PLF-8 — Fork footprint closure requires execution semantics

The root failure handler is stored outside each fork path, but a failing path worker can publish its
trigger before sibling workers finish. A syntax-tree or list-containment check cannot prove that
concurrency relation. The footprint mechanism may be structurally inspected at rung 3, but the
incompatible sibling/root-failure write set must be exercised through the real proof analyzer at
rung 4.

**Disposition:** retained inside `fork-noninterference-and-join` at rung 4.

## Per-obligation assessment

| Obligation | Rung | Fit | What a cheaper layer lets through |
|---|---:|---|---|
| `workflow-resolution-and-emission-identity` | 3 | Fits | Valid individual identifiers that collide only in a compilation-wide catalog/generated-name namespace. |
| `workflow-contract-refinement` | 4 | Fits | Structurally closed graphs whose logical implication, frame, or authority relation is false. |
| `closed-topology-completeness` | 4 | Fits | An edge inventory that does not establish its semantic seam/exit formulas. |
| `occurrence-action-identity` | 4 | Fits | A valid immutable tuple lost or rebound by Roslyn/projection/import. |
| `wire-echo-semantic-equivalence` | 4 | Fits | Structurally fingerprinted data never driven through import rejection/emission. |
| `fork-noninterference-and-join` | 4 | Fits | Correct edge counts with wrong read/write sets or guarantee conjunction. |
| `legacy-binding-hash-stable` | 4 | Fits | Source-compatible overloads with different canonical bytes. |
| `runtime-generator-refinement-parity` | 4 | Fits | Shared kernel source with divergent orchestration or precedence. |
| `contracts-wire-contract` | 4 | Fits | Clean generation with incorrect serializer/adapter behavior. |
| `generated-validation-semantics` | 4 | Fits | An emitter consistently generating the wrong callback. |
| `merged-frontends-share-proof` | 4 | Fits | Separately valid DTOs/models that bypass or collide in aggregate generator execution. |
| `rooted-descriptor-catalog` | 4 | Fits | Correct syntax ancestry whose parsed contracts never participate in proof. |
| `proof-fixture-subject-binding` | 4 | Fits | A compilation object exists but its error channels are ignored by the assertion. |
| `public-api-authority` | 3 | Fits | Ordinary compilation accepts an unreviewed public API change. |
| `schema-compatibility-fails-closed` | 3 | Fits | Current schema compiles/regenerates while narrowing prior published wire behavior. |
| `generated-diagnostic-authority` | 1 | Fits after refinement | Hand synchronization can drift even with passing descriptor examples. |
| `contracts-release-revalidates-artifacts` | 5 | Fits | Project tests do not bind the published nupkg or tag toolchain. |
| `packed-consumer-binding` | 5 | Fits | Package contents can exist while NuGet selects another package or the analyzer never loads. |
| `ci-proof-policy-version-pinned` | 3 | Fits | Comments and green runs do not constrain a movable workflow ref. |
| `docs-build-before-merge` | 5 | Fits | Markdown/link checks do not compile the deployed Starlight site. |
| `compilation-local-proof-boundary` | 3 | Fits | Types cannot prove catalog reachability across Roslyn compilation inputs. |
| `downstream-contract-adoption` | 5 | Fits after split | Producer tests cannot establish an independent consumer's generated enum/API mirror. |
| `compensation-handoff-to-169` | 4 | Fits after split | Presence of an occurrence field does not establish the future rollback planner's use of it. |

## Passes

- The ledger spans all six rungs rather than defaulting every claim to tests.
- Every obligation above rung 1 states a structural reason cheaper evidence is insufficient.
- The broadest tests are reserved for boundaries that actually cross generator, wire, package, or
  external-consumer composition.

## Uncertainties

- Final proof status is not a rung question. The exact-current `98fabb4` local portfolio includes
  digest-bound package bytes, the complete solution, script/smoke, Contracts/codegen, schema, docs,
  and standalone gates. Even well-placed committed artifacts remain indeterminate until required
  results bind to the protected path and, for a future Contracts release, the tag-produced bytes.
