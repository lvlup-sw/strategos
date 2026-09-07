---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
cost_setting: high
scope_rule: three-angle refutation of every synthesized obligation plus focused adversarial wire execution
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: source of acceptance claims challenged by this lens
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: source of the proof semantics whose reuse was challenged
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: source of the future compensation premise
---

# Evaluation — adversarial refutation

## Method

Each active obligation received three different attacks:

1. **Reachability:** can the prohibited failure actually reach a supported product path?
2. **Premise/duplication:** is the obligation based on an unvalidated claim or already guaranteed by
   another obligation?
3. **Proof kill:** can the named proof remain green when the owning mechanism is removed, weakened,
   or pointed at the wrong subject?

`Survived` means the correctness claim is real. It does not mean the claim is verified. Formal proof
status remains in `ledger.md`: the complete local portfolio, including the solution, Contracts/
codegen, schema, docs, standalone gates, fresh packages and consumer, hardened pack script, and
Basileus smoke, binds to exact-current `98fabb4`. Protected execution and review remain pending.

## Per-obligation verdicts

| Obligation | Reachability attack | Premise/duplication attack | Proof-kill attack | Verdict |
|---|---|---|---|---|
| `workflow-resolution-and-emission-identity` | Bound actions enter the aggregate catalog, and normalized names feed source hints/types. | Ordinal uniqueness and generated-name injectivity are different constraints; neither subsumes the other. | Zero/many/dynamic and normalized-collision fixtures must change diagnostic class or crash if grouping/checking is removed. | Survived. |
| `workflow-contract-refinement` | A successful compilation is the only gate before the generated saga; no runtime refinement backstop exists. | #168 kernel correctness does not prove #167 graph/formula orchestration. | Shared negative vectors and entry/exit/frame/authority fixtures turn permissive edits into false greens. | Survived. |
| `closed-topology-completeness` | Public runtime callbacks/collections can add routes the syntax analyzer cannot execute; transparent receivers can truncate a chain, and non-injective phase names can misassign already parsed routes. | Semantic refinement presupposes graph completeness; it does not guarantee syntax traversal or structural ownership. | Parenthesized-chain cases, delegate/dynamic/nested refusals, an underscore-bearing back-edge, plus sibling `A`/`A_B` and nested `A_B`/`C` incompatible-ingress contracts kill missing or misowned edges. | Survived; transparent-receiver and loop false greens found and repaired. |
| `occurrence-action-identity` | The tuple crosses builder, syntax, projection, JSON, bridge, and proof maps. | Runtime immutability guarantees only one representation, not transport parity or diagnostic precedence. | Same-CLR-type named reuse and malformed/duplicate vectors fail if identity collapses or is dropped; an ambiguous self-reference must remain one AGWF040 rather than AGWF042 cycle. | Survived; precedence defect found and repaired. |
| `wire-echo-semantic-equivalence` | Real projection can represent a fork step at top level and in its path; bridge list order previously selected one. | This is not ordinary duplicate-ID rejection: a genuine echo must remain accepted. | Seven conflicting configuration cases were green after the first partial fix; the final field-exhaustive fingerprint and exact-echo control kill that class. | Survived; concrete defect found and repaired. |
| `fork-noninterference-and-join` | Confidence handlers and a triggered root failure handler can execute while sibling workers remain concurrent; joins consume all tails. | Ordinary one-edge seam checks deliberately skip fork tail-to-join edges and do not derive concurrent diversions. | Removing confidence/root-failure traversal or a tail changes dedicated conflict/weakened-tail negatives while disjoint/legal cases control over-rejection. | Survived; root-failure footprint false green found and repaired. |
| `legacy-binding-hash-stable` | Graph version is consumed by freeze/cache behavior and the field occupies a canonical byte slot. | #168's broader one-time hash rollover is held fixed, so it does not refute wrapper-only compatibility. | A base-derived constant plus changed-ID sensitivity prevents simply blessing any after-value. | Survived. |
| `runtime-generator-refinement-parity` | Both public runtime analysis and compiler diagnostics are observable and separately orchestrated. | Shared solver/parser source does not make the two callers equivalent. | One neutral vector corpus feeds independent entry points; the generator's message-fragment join remains a durability weakness. | Survived. |
| `contracts-wire-contract` | The optional field, workflow-name lookup identity, and closed enum cross the NuGet/JSON boundary. | “Additive field” does not cover closed-enum consumer compatibility, and schema acceptance must agree with runtime/import identity validity. | Missing/blank read/write, all-five-arm, workflow-name nonblank, projection/import, omission, and package-content cases reject common drift. | Survived; the whitespace-name mismatch was found and repaired at `98fabb4`. |
| `generated-validation-semantics` | Generated callbacks execute during real serialization/deserialization. | Clean regeneration is circular evidence for emitter correctness, not a substitute. | Action-reference and workflow-name cases kill missing/wrong nonblank callbacks; a synthetic emitter test distinguishes required/optional exact-pattern properties from unrelated regex and scalar-alias boundaries. | Survived; Contracts/codegen and the complete exact-current portfolio passed at `98fabb4`, with protected execution/review pending. |
| `merged-frontends-share-proof` | C# and `AdditionalText` models join before binding proof and source emission. | Per-front-end validity cannot establish aggregate duplicate precedence or equivalent proof behavior. | Legal/illegal imported bindings and three duplicate pairings exercise the real generator through the closed shared harness. | Survived; protected final-revision execution pending. |
| `rooted-descriptor-catalog` | Dead direct descriptors previously satisfied lookup while never entering a runtime ontology graph. | It is distinct from compilation-local scope: source-visible syntax can still be unrooted. | In addition to unrooted, subject-mismatch, conditional, escaped, and legal-rooted controls, the committed fixture makes a valid rooted bound descriptor reject a missing configured occurrence with AGWF040. | Survived; selective-vacuity kill passed in the complete exact-current `98fabb4` portfolio, with protected execution/review pending. |
| `proof-fixture-subject-binding` | Synthetic compilations are the only subject many analyzer tests execute. | Product correctness does not make a test harness authoritative. | The shared helper rejects input, driver/generator, and updated-compilation channels; commit `595a949` migrates the remaining raw import and approval-parser routes to validated helpers. | Survived and `Indeterminate`; complete exact-current local evidence passed, with protected execution/review pending. |
| `public-api-authority` | External implementers compile against the added interfaces/value/carrier. | Reflection shape alone is weaker than the exact PublicApiAnalyzer policy. | The real baseline mutation kills the common gate; exact file/member closure prevents scoped sections from silently matching nothing. | Survived. |
| `schema-compatibility-fails-closed` | Published schemas are consumed independently and cannot be replaced in place. | Current-schema regeneration says nothing about prior-package compatibility. | Empty baseline, nested narrowing, reference, union, discriminator, item, unknown-keyword, version, and Node/C# parity vectors attack the classifier. | Survived. |
| `generated-diagnostic-authority` | Generated closed enums/catalog/docs and live descriptors are separately consumed. | Behavior tests do not guarantee representation equality. | Enum/entry disagreement makes generation fail; clean regeneration and descriptor parity cover derived outputs. | Survived. |
| `contracts-release-revalidates-artifacts` | The tag workflow is the route that creates immutable NuGet bytes. | A prior PR check can predate tag changes or toolchain drift. | Tag/checkout, regeneration-cleanliness, test, schema-baseline, and digest steps each block a distinct stale-subject route. | Survived; execution pending. |
| `packed-consumer-binding` | NuGet selection and analyzer loading occur only in a package consumer. | Project-reference tests and listing DLLs in a nupkg cannot prove that path. | Local-only feed, manifest/digest, exact legal build, exclusive AGWF041 negative, and unrelated-error controls reject wrong-subject success. | Survived; the exact-current `98fabb4` digest-bound consumer, serialized pack, and Basileus smoke passed; protected execution/review remains pending. |
| `ci-proof-policy-version-pinned` | Reusable workflows select the actual test projects and coverage policy. | Historically, a nearby SHA comment did not constrain a movable `@v1`; all three refs now use the immutable SHA. | Replacing the pinned SHA with a tag is structurally detectable; final CI still must name the invoked revision. | Survived; protected execution pending. |
| `docs-build-before-merge` | The diff changes Starlight docs and merge can precede deployment. | Post-main deployment failure is detection, not pre-merge proof. | Removing/breaking the PR `docs-build` job leaves no bound site result and must be treated as indeterminate. | Survived. |
| `compilation-local-proof-boundary` | Catalog construction enumerates syntax trees/AdditionalFiles, not referenced metadata or runtime sources. | This is an explicit supported boundary with #204, not a claim that portable proof exists. | Unrooted/metadata-shaped inputs cannot be counted as proved; docs and implementation must remain aligned. | Survived. |
| `downstream-contract-adoption` | New closed enum members and interface methods are consumed outside this repository. | Producer-side compatibility tests cannot establish independent generated consumers. | Basileus #493 and Exarchos #1893 establish coordination only; no downstream implementation/test result exists. | Survived and unproven. |
| `compensation-handoff-to-169` | `Compensate<T>` is authorable now, while rollback semantics are intentionally absent from #167. | Occurrence identity availability does not prove a future planner reuses it. | Current AGWF042 negative kills premature acceptance; only #169 tests can kill a second-map implementation. | Survived and unproven. |

## Refuted obligation

### `empty-workflow-entry-proof-defined`

The obligation assumed a supported front end can produce an empty workflow model. The competing
explanation is true:

- imported definitions with zero steps are rejected by `WireToModelBridge` with `NoStepsFound`
  before a model reaches binding proof;
- `Workflow<TState>.Create` returns `IWorkflowBuilder<TState>`, not a definition; and
- the public terminal operation `Finally<TStep>` returns the definition only after checking that
  `StartWith` established `_entryStep`.

A compiler-valid public C# definition or accepted JSON import therefore cannot select
`PhaseGraph.CompletedPhase` as its entry. The empty branch in `ProveEntry` is defensive internal code.
The refutation does not weaken `closed-topology-completeness`, which still requires deterministic
front-end rejection.

**Verdict:** Refuted by reachable-path evidence. No dissenting refutation attempt found a valid public
construction.

## Historical focused execution and mutation evidence

The detailed occurrence/wire attack is in `refutation-occurrence-wire.md`. Before repair, two
identity collisions passed. After the first partial repair, seven configuration/runtime collisions
still passed. At product fingerprint
`515b3a3bbe878dfe574ab495ead7041d1fa51c0653ea07f393d3b95002905b1c`, the following fresh local
observations succeeded:

- Release generator-test build: 0 warnings, 0 errors;
- `ImportIdentityGateTests`: 13/13;
- `WireStepFingerprintCoverageTests`: 1/1; and
- `MinimalJsonReaderTests`: 14/14.

The independent proof refutation also found parenthesized-receiver truncation, an underscore-named
body-step back-edge omission, two false greens caused by sibling-loop name parsing, and a root
failure-handler omission from fork footprints. `SiblingLoop_WithPrefixedName_StillChecksIngress` and
`PrefixedSiblingLoop_StartingWithNestedLoop_StillChecksNestedIngress` both returned zero AGWF
diagnostics before their respective fixes. With structural loop paths in that historical working-tree
snapshot, the focused topology class was reported 23/23; LoopExtractor 7/7 and FluentDslParser 52/52 also passed. The final
audit reported the workflow-binding analyzer class 29/29 after adding the ambiguous-self-reference
AGWF040-precedence regression. A full generator run of 1751/1751 occurred immediately before the
last structural-path edit, so it is explicitly stale and not current full-suite evidence. These are
useful historical observations, not current evidence for immutable evidence subjects `595a949`,
`6851fe7`, or `98fabb4`.

An earlier full generator run at a different dirty snapshot reported 1745/1746, with one unrelated
parenthesized-main-chain AGWF009 failure. It is stale and is **not** accepted as evidence for this
fingerprint or the final product.

The authoritative local evidence is instead `../final-evidence.md`: at exact-current `98fabb4`, the
aggregate solution run passed 5,517 with 16 skipped (5,533 total), and the generator project passed
1,761/1,761. Contracts 185/185, stable codegen, docs, live schema comparison, API and standalone
gates, digest-bound package consumer, exact serialized script, and Basileus smoke also passed.
Protected CI and completed-diff review remain pending.

## Uncertainties

- The complete local evidence portfolio binds to current `98fabb4`; no protected PR result or
  completed-diff review exists.
- The final Contracts tag/release result, downstream adoption implementations, and #169 product proof
  remain absent.
