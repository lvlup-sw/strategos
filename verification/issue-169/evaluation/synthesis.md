---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
implementation_fingerprint: 2d0f3027308580376819e7b56649592cd0a784bc
implementation_fingerprint_command: git rev-parse 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab^{tree}
evidence_binding: Stage 3 synthesis of the four exact-precursor evaluation records, exact-final local artifact record, and exact-precursor full-test/Basileus smoke record; concurrent working-tree fixes are outside this subject and receive no verdict here
cost_setting: high
scope_rule: synthesize all PF-1 through PF-12, COV-01 through COV-07, W-1 and W-2, the 31 active-obligation refutation verdicts, five rejected candidate claims, and the hermetic Basileus feed failure over the recorded reverse dependency closure
updated: 2026-09-07
skipped: Stage 4 ledger, obligation-evidence, and guard edits are intentionally deferred; in-progress fixes are not evaluated
lens: stage-3-synthesis
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: authoritative derived-compensation intent and acceptance criteria
  - path: https://github.com/lvlup-sw/strategos/issues/172
    why: action-calculus and operator-ordering context
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: preceding typed-workflow proof boundary
  - path: https://github.com/lvlup-sw/strategos/issues/168
    why: predicate and effective-guarantee proof semantics reused by inverse derivation
  - path: https://github.com/lvlup-sw/basileus/issues/495
    why: downstream adoption coordination and consumer boundary
  - path: https://github.com/lvlup-sw/exarchos/issues/1895
    why: downstream adoption coordination and consumer boundary
---

# Stage 3 synthesis — issue #169

## Decision boundary

This synthesis describes the immutable precursor commit
`42b4ed741c1f406a2de2fdcdaf602ca53dd91dab`, tree
`2d0f3027308580376819e7b56649592cd0a784bc`, over base
`0ac93e916849cceada616a0e15dd7e6c83b34af1`. It combines:

- `verification/issue-169/evaluation/refutation.md`;
- `verification/issue-169/evaluation/proof-layer-fit.md`;
- `verification/issue-169/evaluation/coverage.md`;
- `verification/issue-169/evaluation/wildcard.md`;
- `verification/issue-169/final-artifact-checks.md`; and
- `verification/issue-169/full-test-checks.md`.

Concurrent working-tree changes are not part of that commit or tree. The five fixes identified below
are **in progress and unverified**. Their presence in a working tree, a test name, or this disposition
does not make any precursor obligation pass, does not repair the recorded Basileus failure, and does
not provide protected or downstream evidence.

## Synthesis result

The refutation lens tried three independent attacks against every active obligation. All **31 of 31
active obligations survived**; none should be removed as unreal or duplicate. All five claims that
Stage 2 had rejected before promotion were independently **refuted** and remain inactive. The other
three lenses produced 21 named findings, and the exact-precursor Basileus smoke contributes one
additional determinate harness/product-composition finding.

| Disposition class | Finding count | Meaning at this stage |
|---|---:|---|
| Product fix now | 4 | A concrete product or proof-harness correction is being implemented; no pass is claimed. |
| Ledger/proof-assignment refinement | 16 | Stage 4 must change claim granularity, rung, artifact wording, or add a missing obligation; no ledger file is changed by this synthesis. |
| Future delivery/adoption Indeterminate | 1 | The claim depends on a future hosted, published, or external event and cannot be resolved from this precursor. |
| Retained limitation | 1 | The product's narrower semantic boundary remains; public claims must state it precisely. |
| **Total named findings** | **22** | PF-1..PF-12, COV-01..COV-07, W-1/W-2, and BAS-01 are all accounted for below. |

## Finding dispositions

### Proof-layer-fit findings

| Finding | Classification | Synthesis and Stage 4 disposition |
|---|---|---|
| **PF-1** — R1 construction does not establish emission reachability | **Ledger/proof-assignment refinement** | Keep `nonproven-inverse-is-never-executable`, which survived refutation. Split the R1 core result/plan invariant from an R3 analyzer-to-emitter closure claim, or assign the combined claim R3. The Error diagnostics are an additional safety mechanism, not proof that the public plan and emission surfaces are one construction root. |
| **PF-2** — live diagnostic metadata has a hand-authored root | **Ledger/proof-assignment refinement** | Split TypeSpec-derived enum/catalog/schema/Markdown consistency at R1 from live `WorkflowDiagnostics` parity at R4. Clean regeneration cannot establish the hand-authored descriptor half. |
| **PF-3** — API drift and API intent are different proof layers | **Ledger/proof-assignment refinement** | Split compiler-enforced shapes at R2, baseline/allowlist/reflection drift at R3, and intended compatibility classification at R6. Keep the API obligation active; its selective policy remains a real drift boundary. |
| **PF-4** — docs build cannot establish semantic honesty | **Ledger/proof-assignment refinement** | Split Starlight build/link integrity at R4 from semantic claim review at R6. This refinement also governs the contract-set wording response to W-1; a successful docs build alone cannot close that response. |
| **PF-5** — final-subject binding and human merge authority do not share R5 | **Ledger/proof-assignment refinement** | Split deterministic SHA/tree/digest/check freshness at R3 from finding disposition and merge authorization at R6; treat R5 product-path results as bound inputs. The hosted evidence itself remains future and Indeterminate. |
| **PF-6** — nested-scope state-machine semantics are cheapest at R4 | **Ledger/proof-assignment refinement** | Move exact innermost-scope selection and preservation of enclosing history to R4 and add the later-enclosing-failure component case. Create a narrow R5 clause only if actual serialization/reload or host scheduling is required. |
| **PF-7** — proof-artifact fields name artifacts that do not exist | **Ledger/proof-assignment refinement** | Mark the route inventory, crash-window fixture, persisted-corruption reload fixture, nested/fork host fixtures, terminal redelivery fixture, source-ownership guard, and neutral corpus as `Proposed:` or `Missing:`. Name existing lower-rung backstops separately; do not make readers reconcile artifact existence from the State paragraph. |
| **PF-8** — no shared inverse corpus exists | **Ledger/proof-assignment refinement** | Keep the semantic-equivalence and shared-authority obligations. Mark the corpus proposed, and later run one neutral set of positive, weaker, stronger, opaque, invalid, frame, subject, authority, precedence, and witness vectors through runtime, AONT216, and AGWF044 adapters. Local runtime mutation evidence is not cross-front-end parity. |
| **PF-9** — three Why-not-cheaper explanations are incomplete | **Ledger/proof-assignment refinement** | Amend the rows for single/mandatory compensation, rollback-ID mapping, and wire round-trip to exclude every lower rung on soundness grounds: types cannot decide imported whole-program mode; source shape cannot establish mapped/persisted behavior; and types/source inspection cannot establish malformed-input and round-trip agreement across independent roots. |
| **PF-10** — rollback-ID boundary samples do not prove universal injectivity | **Ledger/proof-assignment refinement** | Preserve the real ID obligation. Name the direct nonzero-domain permutation argument and add a property/metamorphic kill fixture, or narrow the tested claim. Do not call the three-value boundary fixture a universal property proof. |
| **PF-11** — downstream release policy and actual adoption are different claims | **Ledger/proof-assignment refinement** | Split a reachable rule that forbids claiming adoption without evidence from the R5 proposition that named consumer revisions actually adopt exact bytes. The latter remains Indeterminate for Basileus and Exarchos; open coordination issues are not implementation evidence. |
| **PF-12** — the packed-consumer narrative predates exact-final local evidence | **Ledger/proof-assignment refinement** | Refresh the state text to “exact-final local pass; protected binding absent.” The exact-precursor record binds codegen, Contracts, package digests, legal warning-free compilation, cause-specific workflow diagnostics, and the exit-3 infrastructure path locally. It does not make the obligation Verified. |

### Coverage findings

| Finding | Classification | Synthesis and Stage 4 disposition |
|---|---|---|
| **COV-01** — cancellation has a test but no claim | **Ledger/proof-assignment refinement** | Add a narrow obligation: cancellation of inverse analysis propagates as `OperationCanceledException` before any semantic verdict and through solver calls. Bind the existing entry cancellation test and a mid-analysis fixture. No precursor product defect was established. |
| **COV-02** — status/diagnostic precedence and stable witnesses are unowned | **Ledger/proof-assignment refinement** | Add an active decision-table obligation spanning runtime status selection, AONT216, AGWF044 versus AGWF045, earlier-root-cause suppression, deterministic obligation ordering, and normalized witnesses. It may reuse PF-8's neutral corpus but is not the generated-metadata parity claim. |
| **COV-03** — durable completion to post-completion failure transition is incomplete | **Ledger/proof-assignment refinement** | Add or widen an obligation over `dispatch claim -> reducer + completion journal -> optional post-completion failure claim -> consumed rollback trigger`, including every producer, exact kind/scope/high-water identity, duplicate use, and crash/reload boundary. The evaluator found missing coverage, not a demonstrated incorrect transition. |
| **COV-04** — public parallel plan to generated serial execution is prose-only | **Ledger/proof-assignment refinement** | Add a generated-runtime refinement claim for quiesced multi-lane rollback: preserve the public parallel plan, then dispatch generic-state inverses serially in descending completion sequence with reducer visibility between lanes. Keep arbitrary external-effect ordering under the consumer trust boundary. |
| **COV-05** — exact-byte probe omits `Ontology.Generators` and AONT216 | **Product fix now** | Extend the isolated package probe to select, hash, restore byte-for-byte, and load `LevelUp.Strategos.Ontology.Generators`; compile a legal pair and require an exclusive enabled-by-default Error AONT216 negative. This packed analyzer/AONT216 work is currently being implemented. It has **not** passed on a new bound subject. |
| **COV-06** — inverse-timeout configuration lacks end-to-end proof | **Product fix now** | Add source and imported-JSON fixtures for custom and default timeout values, with exact extraction/IR/topology/journal/scheduled-message ticks and altered, zero/negative, stale, or forged negatives as applicable. Custom/default propagation tests are currently being implemented; no result is claimed. A Stage 4 obligation still must own the completed proof. |
| **COV-07** — publication stops at local pack and PR merge | **Future delivery/adoption Indeterminate** | Add a producer publication obligation when entering delivery: bind product and independent Contracts tags to the approved SHA/tree, trusted publication result, intended IDs/versions, NuGet provenance, and downloaded bytes. No tag publication or registry retrieval exists for this subject, so it remains Indeterminate rather than a current local fix. |

### Wildcard findings

| Finding | Classification | Synthesis and disposition |
|---|---|---|
| **W-1** — `Proven` returns to a requirement set, not the concrete pre-state | **Retained limitation** | Retain the unary-contract model: proof establishes that the authored compensation accepts the forward effective-guarantee region, re-establishes the forward hard-requirement region, and matches declared semantic authority and may-touch frame. It does **not** establish object equality with the concrete pre-forward state, old/new relational inversion, or reversal of append-only event history. Precise contract-set language is currently being implemented across public XML and docs. That wording change is unverified and does not add relational semantics. The non-singleton `x > 0` and event-frame counterexamples should remain claim-boundary evidence. |
| **W-2** — rollback identity is hidden in tracing-only `CorrelationId` | **Product fix now** | Expose explicit immutable compensation identity through `StepContext` and populate it at the worker boundary; add a pre-execution metadata guard so an inverse body cannot run with absent or incoherent rollback metadata. Verify stable redelivery identity, forward/inverse distinction, public API/docs, and forged/missing metadata rejection. The explicit `StepContext` rollback identity and guard are currently being implemented and have not passed a bound suite. |

### Exact-precursor Basileus smoke finding

| Finding | Classification | Synthesis and disposition |
|---|---|---|
| **BAS-01** — the fresh Basileus feed omits the packed dependency closure | **Product fix now** | `full-test-checks.md` records a determinate restore failure: the fresh feed contained `LevelUp.Strategos.Agents 2.7.0-smoke`, whose nuspec requires `LevelUp.Strategos >= 2.10.1-alpha.0.27`, but the feed contained no matching core package and NuGet.org offered only `2.10.0`. Restore exited 1 with NU1102; the smoke executable did not run, so behavioral totals are Indeterminate while the release-readiness gate is Fail. Make `pack-to-local-feed.sh` produce and byte-check the complete in-repository dependency closure at coherent versions, then restore/build/test with a fresh feed and cache. Hermetic feed closure is currently being implemented; the precursor failure remains authoritative until a new subject is tested. |

## Cross-lens resolutions

### No active obligation is removed

Proof-layer shortcomings do not refute the underlying obligations. In particular, PF-1, PF-2,
PF-3, PF-4, PF-5, PF-6, PF-8, PF-10, and PF-11 challenge rung, granularity, or artifact fit; the
refutation pass independently found reachable consequences for each affected obligation. Stage 4
must refine those rows rather than move them to `## Refuted`.

Coverage findings COV-01 through COV-04 identify missing active claims, not established product
failures. They require targeted inventory/ledger work before any new proof can be assigned. COV-05
and COV-06 are different because a concrete shipped-composition omission or directly actionable
end-to-end proof harness is already identified and is being corrected now.

### The two package results do not conflict

PF-12 is about `verify-generator-consumer-build.sh`: its exact-precursor local workflow-package probe
restored from its own isolated feed, compared restored packages with source bytes, compiled the legal
consumer warning-free, emitted the expected AGWF041/AGWF044 negatives, and distinguished restore
Indeterminate from product failure. BAS-01 is about the separate release-readiness path through
`pack-to-local-feed.sh` and the Basileus smoke project. That feed contained only the Agents package,
so its exact declared core dependency could not restore. One local probe passing does not make the
other feed complete.

COV-05 identifies a third package distinction: the precursor workflow probe did not load the
separately packed ontology analyzer or exercise AONT216. The in-progress extension must receive its
own exact-byte, analyzer-load, legal-positive, contradictory-negative, and tri-state evidence.

### W-1 is not the existing arbitrary-code trust boundary

`executable-inverse-effects-remain-a-consumer-trust-boundary` survived refutation and remains valid:
Strategos cannot prove arbitrary CLR or external effects. W-1 is narrower and survives even if both
forward and compensation implementations perfectly satisfy their declared unary contracts. The
synthesis therefore retains the weaker contract-set semantics and corrects claims; it does not imply
that the current calculus is a concrete state-transition inverse.

### External coordination, local smoke, and adoption remain separate

Fixing BAS-01 will establish only that a Strategos-owned Basileus-shaped smoke project can restore and
execute against a hermetic candidate feed. It will not prove that the Basileus or Exarchos repositories
have adopted, tested, released, or deployed exact package bytes. PF-11's actual-adoption half and
COV-07's publication event remain future Indeterminate until exact consumer revisions, protected
results, tags, published provenance, and retrieved package digests exist.

## Refuted candidate claims retained as boundaries

The following five claims remain rejected and must not be promoted into active obligations or public
guarantees:

| Rejected claim | Retained supported boundary |
|---|---|
| Inverse execution is exactly once. | Delivery is at-least-once; stable rollback identity supports consumer-owned idempotency and reconciliation. |
| Parallel rollback workers execute concurrently. | Public planning preserves independent parallel structure, while generated generic-state inverse workers execute serially in reverse completion order. |
| Legacy `Compensate<T>()` receives static proof. | Legacy compensation remains runtime-only; typed identity is required for static proof. |
| Typed EventSourced compensation is sound when `ApplyEvent` compiles. | Typed derived compensation is SagaDocument-only; a compiling or no-op consumer fold is not replay proof. |
| Successful rollback makes the workflow successful. | Rollback preserves the initiating failure/audit semantics and precedes failure handling; lifecycle completion is not semantic success. |

These refutations are retained limitations/boundaries, not five removed active rows: they were never
promoted. Their discriminating evidence is recorded in `evaluation/refutation.md`.

## In-progress work and required rebinding

Five responses are being implemented outside the precursor subject:

1. explicit `StepContext` rollback identity plus a pre-execution metadata guard (W-2);
2. precise requirement-set, authority, and may-touch-frame language without a concrete-pre-state
   restoration claim (W-1, coordinated with PF-4);
3. complete hermetic Basileus feed closure (BAS-01);
4. exact-byte packed `Ontology.Generators` loading and AONT216 positive/negative coverage (COV-05);
5. custom/default inverse-timeout propagation tests (COV-06).

None has a pass in this synthesis. After the changes settle, evidence must bind the new commit and
tree, regenerate package digests where applicable, run the cheapest sound focused checks, then rerun
the affected package/smoke/full-suite paths. The ledger, obligation evidence, and guard register must
be updated only in Stage 4 against that new subject.

## Evidence state after synthesis

- The precursor local Release build completed with zero errors. The 15-project test aggregate had
  5,475 successes, zero observed failures, and 16 skips; under the three-outcome rule, the aggregate
  is Indeterminate rather than Pass.
- The precursor Basileus release-readiness smoke is **Fail** at restore for the determinate missing
  dependency closure. Its test execution is **Indeterminate/unrun**.
- The exact-final workflow package probe and Contracts/codegen checks have revision-bound local Pass
  evidence as stated in PF-12, but no active obligation becomes Verified without its assigned
  protected proof.
- Protected CI membership/results, PR head, review freshness, finding disposition, merge
  authorization, publication, downloaded registry bytes, and actual downstream adoption remain
  Indeterminate.
- The Stage 3 result is therefore a disposition plan, not merge authorization and not a verification
  verdict for the in-progress working tree.
