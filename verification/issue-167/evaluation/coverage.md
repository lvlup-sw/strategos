---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
cost_setting: high
scope_rule: ledger coverage checked against the Stage 0/1 reverse-dependency closure and the 171-file product diff at final subject 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: acceptance and compatibility surface
  - path: https://github.com/lvlup-sw/strategos/issues/153
    why: downstream contract-consumer rule
  - path: https://github.com/lvlup-sw/strategos/issues/169
    why: next consumer of the occurrence identity
  - path: https://github.com/lvlup-sw/strategos/issues/204
    why: explicit cross-assembly boundary follow-up
---

# Evaluation — coverage against the scope set

## Scope-to-obligation map

| Scope member | Ledger coverage | Assessment |
|---|---|---|
| Typed action-to-workflow binding and ordinal lookup | `workflow-resolution-and-emission-identity`, `legacy-binding-hash-stable` | Covered, including the removed writable property, preserved string overload, and normalized generated-name collision. |
| Bound workflow behavioral subtype | `workflow-contract-refinement`, `runtime-generator-refinement-parity` | Covered across requirement, guarantee, subject, frame, authority, opaque, and invalid decisions. |
| C# topology extraction and emitted transition graph | `closed-topology-completeness`, `fork-noninterference-and-join` | Covered with both conservative closure and semantic graph obligations, including structural loop ancestry independent of display names. |
| Occurrence identity/public callbacks | `occurrence-action-identity`, `public-api-authority` | Covered across value shape, all 12 configured callbacks, extraction, and exact API scope. |
| Runtime projection and wire import | `contracts-wire-contract`, `merged-frontends-share-proof`, `wire-echo-semantic-equivalence` | Covered, including malformed presence, legacy omission, named reuse, and duplicated projection echoes. |
| TypeSpec/generated validation | `contracts-wire-contract`, `generated-validation-semantics`, `generated-diagnostic-authority` | Covered at generation and behavioral layers, including exact nonblank read/write behavior for action and workflow-name identities. |
| Hand-authored analyzer DTO parity | `contracts-wire-contract`, `wire-echo-semantic-equivalence` | Covered by schema conformance and exhaustive fingerprint property mutation. |
| Executable ontology catalog ownership | `rooted-descriptor-catalog`, `compilation-local-proof-boundary` | Covered for rooted/unrooted source declarations and explicit referenced-assembly exclusion. |
| Diagnostic partition and vocabulary | `workflow-resolution-and-emission-identity`, `occurrence-action-identity`, `workflow-contract-refinement`, `generated-diagnostic-authority` | Covered for AGWF039–AGWF043 authority, behavior classes, and ambiguous-identity-before-cycle precedence. |
| Proof harness and test subject | `proof-fixture-subject-binding` | Covered as an obligation. Commit `595a949` closes the identified raw import/approval paths, and the complete exact-current `98fabb4` portfolio passed; protected execution and review remain pending. |
| Schema compatibility and release | `schema-compatibility-fails-closed`, `contracts-release-revalidates-artifacts` | Covered as obligations for published-baseline comparison and the future tag path; the tag has not run. |
| Packed generator/analyzer | `packed-consumer-binding` | Covered at rung 5; exact-current `98fabb4` packages, digests, legal/exclusive-AGWF041 consumer result, hardened pack script, and Basileus smoke exist; protected artifacts do not yet exist. |
| CI selection and docs | `ci-proof-policy-version-pinned`, `docs-build-before-merge` | Covered, including immutable reusable-workflow SHAs and PR docs build. |
| External consumers | `downstream-contract-adoption`, `compensation-handoff-to-169` | Covered but unproven. Basileus #493 and Exarchos #1893 exist; downstream implementation results and #169 product evidence remain outstanding. |

## Findings

### COV-1 — Duplicate occurrence configuration was absent from the Stage 2 portfolio

The initial occurrence obligation followed action identity through projection/import but did not ask
whether two representations of the *same stable ID* could disagree on other behavior. Adversarial
wire cases showed that an echo check based on phase/type/action could still discard compensation,
retry, timeout, confidence, instance, terminal/runtime, or gate configuration.

**Action taken:** added `wire-echo-semantic-equivalence` to the ledger and a mechanically exhaustive
wire-fingerprint guard. This is a gap found and addressed, not a refutation of occurrence identity.

### COV-2 — Generated-name injectivity was missing from exact workflow resolution

Ordinal workflow IDs can be distinct yet normalize to the same generated PascalCase type/source-hint
identity. That is neither AGWF039 catalog ambiguity nor an action-refinement failure; without a
separate pre-emission check it can become a generator collision.

**Action taken:** expanded `workflow-resolution-and-emission-identity` to include AGWF043 and the
C#/JSON collision matrix. The generated diagnostic authority now includes AGWF043 as well.

### COV-3 — Transparent receivers could truncate the accepted fluent chain

A parenthesized fluent receiver is semantically the same C# chain, but direct syntax-kind checks
could stop traversal at the parentheses. Depending on position, that either hid an entry occurrence
from proof or rejected a workflow that should be closed.

**Action taken:** invocation walkers/extractors strip transparent syntax consistently. Helper,
entry-occurrence, and branch-path tests cover the same shape at increasing proof layers.

### COV-4 — Loop names were an unsound ownership authority

The extraction/proof graph previously inferred loop membership or ancestry from flattened
underscore-delimited phase prefixes. An underscore-bearing body step could erase its back-edge. A
top-level sibling loop named `A_B` was treated as a descendant of `A`, omitting the seam from `A`
into `A_B`; a depth check still failed when `A_B` began with nested loop `C`. The malformed graphs
produced zero AGWF diagnostics despite deliberately incompatible contracts.

**Action taken:** ownership now uses immutable outer-to-inner paths of exact `RepeatUntil` call
identities (`ArgumentList.SpanStart`), with separate kill regressions for the sibling and nested-prefix
cases. Display phase names remain presentation/emission data, not ancestry authority.

### COV-5 — Root failure handling was missing from fork interference

A fork worker can publish the root failure trigger while sibling workers remain active. Treating the
root failure handler only as post-path control flow omitted its reads/writes from the concurrent
footprint, so a real write/write conflict could compile.

**Action taken:** fork footprint construction includes root failure diversions, with a dedicated
conflict kill and the existing legal/disjoint controls.

### COV-6 — Ambiguous occurrence identity could be misclassified as recursion

Recursive dependency discovery could follow an occurrence whose action tuple resolved to multiple
catalog declarations. When one declaration was the workflow-bound action itself, the analyzer could
report an AGWF042 recursive cycle before the more specific identity error.

**Action taken:** cycle discovery now traverses only uniquely resolved occurrences. The ambiguous
self-reference regression requires exactly AGWF040, preserving the diagnostic partition.

### COV-7 — The candidate closes the identified false-green harness routes

At the historical discovery snapshot, older import runners called `RunGenerators` directly and two
approval-continuation paths used an unvalidated parser context. The shared
`RunGeneratorWithValidInput` helper itself rejects authored-input, driver/generator, and updated-
compilation failures and has four channel-kill self-tests. Commit `595a949` migrated the remaining
raw import runners and approval call sites to those validated helpers.

**Current disposition:** `proof-fixture-subject-binding` is `Indeterminate`, not violated: the
committed change and its affected suites passed the complete exact-current `98fabb4` local
portfolio. Protected execution and review remain pending.

### COV-8 — Exact-current local evidence is temporally atomic; protected evidence remains pending

The exact-current `98fabb4` subject has a serial Release build, complete solution run, Contracts and
codegen results, live published-schema comparison, locked docs build, standalone policy gates,
fresh digest-bound nupkgs and external consumer, hardened pack script, and Basileus smoke. This
closes the earlier local evidence split across immutable parents. None has run on protected CI, and
the future Contracts tag must create its own release-byte evidence. Treating local evidence as
protected or future-release evidence would still be stale-evidence success.

**Action required:** execute the protected suite and pack probes on `98fabb4`, complete review, and
bind their check URLs. This is an evidence gap, not another product test case.

### COV-9 — External adoption remains uncovered by executable evidence

The two adoption markdown files have been posted as Basileus #493 and Exarchos #1893. Those issue URLs
establish coordination, not a package upgrade, enum update, API-mirror result, or downstream proof.

**Action required:** keep downstream consumer tests open until their adoption implementations and PRs
run; keep the separate #169 semantic handoff unproven until its implementation exists.

## Deletion and reverse-compatibility check

No tracked product file is deleted. The material API deletion is
`ActionDescriptor.BoundWorkflowName`; the typed replacement, retained string overload, public API
baseline, wrapper-only hash oracle, and migration text are represented by active obligations. The
wire change is additive/optional; the reverse direction—new readers accepting action-omitting legacy
JSON—is explicitly covered. The graph-hash claim is scoped to the wrapper-only byte replacement and
does not erase #168's intentional action-contract hash rollover.

## Distribution check

The ledger uses rung 1 for generated diagnostic authority, rung 3 for catalog/API/schema/policy
structure, rung 4 for semantics and adapters, and rung 5 for docs/package/release/external consumer
composition. The distribution is not concentrated only in component or integration tests.

## Passes

- Every Stage 0 reverse-dependency member now maps to at least one obligation.
- The rooted-descriptor, emitter-boundary, harness, and fork-event/`NotFound` tests are committed and
  passed in the complete exact-current `98fabb4` local portfolio; protected execution and review
  remain pending.
- Compatibility is considered in both directions: old authoring/data under new code and new
  contracts/tokens under old external consumers.

## Uncertainties

- The recorded revision/fingerprint and complete local evidence portfolio all bind final subject
  `98fabb4`/`f7271c9`.
- The protected PR/check result, future Contracts-tag result, downstream adoption implementation
  results, and #169 product evidence do not yet exist. Basileus #493 and Exarchos #1893 do exist.
