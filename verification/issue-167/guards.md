---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
cost_setting: high
scope_rule: recurrence-to-guard conversion for the issue-167 reverse-dependency closure
updated: 2026-09-06
skipped: none
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/143
    why: earlier declared/lowered workflow omission class
  - path: https://github.com/lvlup-sw/strategos/pull/187
    why: prior wrong-subject and under-scoped workflow checks
  - path: https://github.com/lvlup-sw/strategos/pull/196
    why: prior first-of-many routing/control defect
  - path: https://github.com/lvlup-sw/strategos/issues/31
    why: prior validated identity discarded before downstream dispatch
  - path: https://github.com/lvlup-sw/strategos/pull/49
    why: prior incomplete canonical ordering/hash sensitivity defect
  - path: https://github.com/lvlup-sw/strategos/issues/105
    why: prior generated/live diagnostic metadata drift
---

# Issue #167 guard register

## Status rule

Every guard below is `Indeterminate` for the current pre-PR subject, commit
`98fabb410e4432cc39fd71ec92651e6ceecfcc7c` (tree
`f7271c9cb517e30c658b6d9245a739a9dc8d365d`). The exact-current local portfolio recorded in
`final-evidence.md` passed: the serial Release build, 5,517-success/5,533-total solution run,
Contracts 185/185, stable regeneration, schema and documentation gates, all standalone policy gates,
fresh package/consumer probe, hardened pack script, and Basileus smoke. No assigned proof has yet run
on the protected PR path, so local success does not promote any guard to `Verified`. A future
Contracts release tag must produce its own artifact-bound evidence; it cannot inherit PR package
digests.

## Ranked guards

### 1. Accepted occurrence/topology surface cannot disappear downstream

```text
class: A public fluent occurrence or legal runtime route is accepted but omitted or under-scoped by extraction, projection, import, PhaseGraph, or proof.
first instance: Issue #143 / PR #144 — accepted declarative features disappeared before lowering.
second instance: PR #187 and PR #196 — successor/routing controls omitted whole construct classes or all but the first sibling.
earliest sound layer: Layer 4 structural inventory for surface closure, plus layer 6 component/property tests for semantic extraction and graph behavior.
policy data location: StepExtractorActionReferenceTests configured-callback signature table; TopologyClosureInspector's symbol-owned accepted/refused forms; topology semantic fixture matrix; fork diversion footprint policy.
mechanism: Reflect the public callback surface, strip transparent fluent syntax, reject unclassified analyzer-visible topology with AGWF042, include every concurrently reachable diversion in footprints, and drive every accepted topology through parser/generator legal and refuted vectors.
kill fixture: Parenthesized receiver helper/entry/branch cases; delegate/dynamic callback/collection/nested handler cases in TopologyClosureProofTests; branch/loop/fork/approval/failure/confidence cases plus Fork_WithRootFailureHandlerConflict_ReportsStableAgwf041 in WorkflowBindingTopologySemanticsTests.
guard self-test: Removing a supported topology arm or classification must either fail the exact surface inventory or turn a required AGWF041/042 case green/red incorrectly.
protected paths: Strategos.Generators.Tests in required build-test CI; source generator Error diagnostics block consumer compilation.
pass signal: Exact reflected surface equals policy data and all legal/refused/semantic vectors pass on the bound revision.
fail signal: Missing/unexpected callback, diagnostic mismatch, generated topology mismatch, or component test failure.
indeterminate signal: Fixture compile/generator/updated-compilation failure unrelated to the expected diagnostic, or a candidate/protected-path revision mismatch.
resource limits: Roslyn cancellation tokens; deterministic finite proof kernel; ordinary CI timeout.
temporary exceptions: Documented v2.13 exclusions (dynamic helpers, selected nested failure/approval routes, compensation) have issue owner #169 or explicit docs; no silent allowlist.
owner: Strategos workflow generator maintainers.
expiry: Exceptions expire when their linked implementation issue lands; the guard itself does not expire.
status: Indeterminate. The exact-current 98fabb4 local surface/topology portfolio passed, including the closed harness routes. Protected execution and review remain pending.
```

This guard removes the most findings because every semantic proof depends on graph completeness.

### 2. Loop ownership cannot be inferred from display/phase names

```text
class: A parsed loop or step is assigned to the wrong parent because flattened underscore-delimited display names are treated as structural membership or ancestry.
first instance: An underscore-bearing direct body-step name changed the apparent name depth and removed its loop back-edge proof.
second instance: Sibling loop A_B was classified as a descendant of top-level loop A; adding a prefix/depth check still failed when A_B began in nested child C, producing two more false greens.
earliest sound layer: Layer 3 immutable structural syntax identity, followed by layer 4 semantic edge-kill tests.
policy data location: StepInfo.StructuralLoopPath and LoopExtractor ownership/continuation queries; paths are ordered outer-to-inner RepeatUntil ArgumentList.SpanStart values.
mechanism: Carry exact syntax-call ancestry from extraction into loop modeling; use path equality for direct membership and true path-prefix comparison for ancestry. Never derive ownership from emitted phase text.
kill fixture: Loop_WithUnderscoreNamedBodyStep_StillChecksBackEdge, SiblingLoop_WithPrefixedName_StillChecksIngress, and PrefixedSiblingLoop_StartingWithNestedLoop_StillChecksNestedIngress, each with an intentionally incompatible back-edge or ingress contract.
guard self-test: Replacing structural equality/prefix checks with LoopName/PhaseName prefix or depth parsing makes at least one kill return zero AGWF diagnostics.
protected paths: WorkflowBindingTopologySemanticsTests in required generator build-test CI.
pass signal: All three cases produce one stable AGWF041 for the omitted edge and the full topology class passes on the bound revision.
fail signal: No binding diagnostic, a different seam, or ownership derived from a normalized/display identifier.
indeterminate signal: Authored-input, generator-driver, or updated-compilation errors outside the expected binding diagnostic partition.
resource limits: Linear traversal of finite Roslyn syntax paths with ordinal integer comparison; normal compiler cancellation.
temporary exceptions: None. Unsupported dynamic topology must fail AGWF042 rather than use a name heuristic.
owner: Strategos workflow extraction/proof maintainers.
expiry: Never.
status: Indeterminate. All three structural-ownership kills passed in the exact-current 98fabb4 local portfolio; protected execution and review remain pending.
```

### 3. Semantic identity may not degrade to a weaker downstream key

```text
class: A validated domain/object/action or occurrence identity is reduced to CLR type, phase, list position, normalized name, or a partial field set downstream.
first instance: Issue #31 — a validated descriptor name was discarded before storage dispatch.
second instance: PRs #194/#196 — bare phase/type keys collided across exclusive paths.
earliest sound layer: Layer 1 immutable names-only value objects, layer 4 collision analysis, and layer 6 cross-front-end/mutation tests.
policy data location: WorkflowActionReference and WorkflowBindingReference value shapes; ActionIdentity ordinal comparer; PathRoutingKey; WireStepFingerprintCoverageTests reachable DTO-type/property discovery.
mechanism: Carry full ordinal tuples per occurrence, resolve an occurrence uniquely before recursive-binding traversal, derive every completed-event consumer stem from ForkPathCompletedNaming, reject ambiguous phase/action identities, reject normalized workflow emission collisions with AGWF043, and accept a duplicated wire step only when its entire DTO graph is field-equivalent.
kill fixture: Same-CLR-type distinct occurrence vectors; shared-type fork-only and fork-plus-linear completed-event/NotFound vectors; ambiguous self-reference requiring exactly AGWF040 rather than AGWF042; AGWF043 normalization collision cases; ImportIdentityGateTests action/configuration/runtime conflicts.
guard self-test: WireStepFingerprintCoverageTests mutates every public property of every reachable DTO and requires the fingerprint to change; removing a field from traversal fails automatically. SagaNotFoundHandlersEmitterTests require qualified handlers for fork-only shared types and retain the unqualified handler only for genuine linear reuse.
protected paths: Runtime/core tests, Contracts/projection tests, and generator import/proof tests in required CI.
pass signal: Exact tuple/occurrence round trips and all collision negatives pass with stable exclusive diagnostics.
fail signal: Tuple mismatch, accepted conflicting echo, wrong diagnostic, or fingerprint mutation with unchanged result.
indeterminate signal: Parser/compiler/generator failure outside the classified identity diagnostic set.
resource limits: MinimalJsonReader depth 128; fingerprint traversal linear in the finite acyclic parsed DTO graph; invariant ordinal formatting.
temporary exceptions: General duplicate JSON member policy and documented builder ergonomics are outside this guard; no action-identity exception.
owner: Strategos workflow/contracts maintainers.
expiry: Never.
status: Indeterminate. Tuple, collision, projection, recursive DTO mutation, and completed-event/NotFound alignment tests passed in the exact-current 98fabb4 local portfolio, including the 16/16 NotFound, 5/5 naming, and 35/35 event-emitter focused checks. Contracts 185/185 also passed after aligning the workflow-name identity. Protected execution and review remain pending.
```

### 4. Verification results must bind to the intended compilation and cause

```text
class: A green test/gate observes invalid input, incomplete generated output, a stale artifact, one of many subjects, or an unrelated failure that happens to contain the expected token.
first instance: PR #187 — substring/suppressed-test and absent-saga checks reported success on the wrong subject.
second instance: PR #196 — a live control applied only to the first of several sibling routes.
earliest sound layer: Layer 6 component/mutation harness with three outcomes and layer 7 packaged production-path probe.
policy data location: Expected diagnostic allowlists in proof helpers; package manifest/digest emitted by verify-generator-consumer-build.sh; verification ledger revision/digest fields.
mechanism: Reject authored, driver, generator, and updated-compilation errors outside the expected set; use exclusive diagnostic parsing; bind consumer restore to a local-only package feed and hashes.
kill fixture: Invalid authored parser fixture; unrelated compiler/generator error; wrong-package source; illegal seam whose only error must be AGWF041.
guard self-test: Each diagnostic channel and wrong-package/unrelated-error route must produce fail or indeterminate, never pass.
protected paths: Required build-test and pack-verify CI, plus final verify-code evidence promotion.
pass signal: Bound subject has only expected diagnostics, generated output compiles where applicable, and exact package hashes match restore inputs.
fail signal: Expected product obligation is refuted on the intended subject.
indeterminate signal: Compile/tool/restore/network/driver failure or subject/digest mismatch.
resource limits: Bounded test/process timeouts; no retry that converts indeterminate to pass.
temporary exceptions: None.
owner: Strategos test-infrastructure and release maintainers.
expiry: Never.
status: Indeterminate. At exact-current 98fabb4 the closed common harness, including the migrated raw import and approval paths, passed the complete local suite; the fresh digest-bound consumer, hardened pack script, and Basileus smoke also passed. Protected execution and review remain pending.
```

### 5. Public API analyzer scope cannot silently stop matching the reviewed files

```text
class: A new public builder/value/carrier member lands outside the deliberately scoped PublicApiAnalyzer baseline or a stale path glob matches nothing.
first instance: Historical #51 seven-interface gate established after cross-product API drift.
second instance: Initial #167 diff added three continuation methods and WorkflowActionReference outside that scope.
earliest sound layer: Layer 4 deterministic structural analysis.
policy data location: TrackedBuilderFileStems and TrackedDefinitionFileStems in BuilderApiBaselineTests; PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt.
mechanism: Exact reflection-to-file-list-to-editorconfig closure plus PublicApiAnalyzer warnings-as-errors build.
kill fixture: GateFailClosedTests removes a real shipped member line and drives check-builder-api-stability.sh.
guard self-test: The mutated build must fail with RS0016 and the stable remediation; the restored baseline must pass.
protected paths: builder-api-stability required CI job and Strategos.Tests.
pass signal: Exact allowed types/members are baselined and analyzer build exits zero.
fail signal: RS0016/RS0017-family drift or exact-scope test failure.
indeterminate signal: Restore/MSBuild/process timeout or non-analyzer build failure; never normalized to pass.
resource limits: Five-minute mutation process timeout; MSBuild serialized with -m:1.
temporary exceptions: Only the explicitly listed ten builder interfaces and two definition files; scope changes require reviewed policy-data edits.
owner: Strategos public API and Exarchos integration maintainers.
expiry: Never.
status: Indeterminate. Exact scope tests and the standalone analyzer/mutation gate passed at exact-current 98fabb4. Protected execution and review remain pending.
```

### 6. Published schema compatibility cannot skip or compare the wrong release line

```text
class: A compatibility gate reports success with no/read-failed/wrong baseline or ignores a nested narrowing.
first instance: Pre-#167 schema workflow selected product v* tags and treated absent schema directories as a successful skip.
second instance: The shallow classifier ignored minLength/pattern/$ref/discriminator/union/item changes introduced or consumed by #167.
earliest sound layer: Layer 4 deterministic dependency/contract analysis over immutable published package bytes.
policy data location: JsonSchemaDiff recursive keyword policy and mirrored Node classifier; ContractsVersion progression rules.
mechanism: Download latest stable LevelUp.Strategos.Contracts nupkg, record its digest, require readable complete schemas, recursively compare all files/keywords, and enforce SemVer policy.
kill fixture: Empty baseline, missing required property, nested constraint, changed ref, removed union arm, changed discriminator, narrowed item, unknown validation keyword, and invalid version vectors.
guard self-test: C# and Node classifiers must agree on the same mutations; unavailable/empty baseline must exit indeterminate nonzero.
protected paths: contracts-schema-diff PR job and publish-contracts tag job.
pass signal: Nonbreaking comparison against named published version/digest with valid forward version.
fail signal: Classified breaking change or invalid version progression.
indeterminate signal: Package index/download/unzip/read/tool failure; exit 2 and block.
resource limits: Finite schema tree traversal; Node/.NET process timeouts supplied by CI.
temporary exceptions: None; incompatible change requires the declared version bump rather than an allowlist.
owner: Strategos.Contracts maintainers.
expiry: Never.
status: Indeterminate. The exact-current 98fabb4 comparison against published Contracts 0.4.0 nupkg 503f565462a48518367fd4366bec6698c092c91d2a4dbde2d4f4925c1139ee0b passed; four breaking changes were permitted by the pre-1.0 minor-version policy. Protected execution and review remain pending.
```

### 7. Generated diagnostic identity and metadata remain derived

```text
class: TypeSpec enum, entry schemas, generated C#/catalog/docs, and live Roslyn descriptors drift independently.
first instance: PR #102 introduced ID generation while metadata remained hand-authored.
second instance: Issue #105 / PR #109 found live descriptor metadata outside the first guard.
earliest sound layer: Layer 2 generated contract, with layer 5 generated conformance for live descriptors.
policy data location: AgwfCatalog.tsp entry models and AgwfCode enum.
mechanism: AgwfCatalogEmitter rejects enum/entry mismatch and derives constants, enum, catalog, and docs; parity tests compare live descriptor metadata.
kill fixture: AgwfCatalogEmitterTests enum/entry divergence and codegen guard's generated-file mutation.
guard self-test: Divergence must make generation/test fail; clean regeneration must leave no unexplained diff.
protected paths: Contracts tests, contracts-codegen-guard, and publish-contracts regeneration.
pass signal: Clean generation and exact live-descriptor parity for AGWF039–AGWF043.
fail signal: Generator rejection, parity mismatch, or dirty generated tree.
indeterminate signal: Codegen tool absent/crashed or regeneration did not execute.
resource limits: Locked Node dependencies and ordinary CI timeout.
temporary exceptions: None.
owner: Contracts and workflow-diagnostics maintainers.
expiry: Never.
status: Indeterminate. Clean regeneration, Contracts 185/185, and descriptor/catalog parity passed at exact-current 98fabb4. Protected execution and review remain pending.
```

### 8. Canonical graph structure requires sensitivity as well as stability

```text
class: A canonical serializer omits a structural field or uses an incomplete ordering key, so changed graphs collide or insertion order changes hashes.
first instance: PR #49 review found incomplete canonical ordering/tie-breakers.
second instance: Typed workflow binding replaces a string carrier in the same canonical slot and could be accidentally omitted or relocated.
earliest sound layer: Layer 6 deterministic/property tests over the production hasher.
policy data location: OntologyGraphHasher canonical write order and base-revision golden fixture provenance.
mechanism: Fixed before-value plus changed-ID sensitivity and registration-order permutation tests.
kill fixture: Rebinding one workflow ID must change the hash; same ID through typed wrapper must keep the base hash.
guard self-test: Deleting the binding write makes the sensitivity case fail; changing its slot/value makes the golden fail.
protected paths: Strategos.Ontology.Tests in required build-test CI.
pass signal: Golden, sensitivity, and permutation fixtures pass on the bound revision.
fail signal: Hash mismatch or lost sensitivity.
indeterminate signal: Test not run, fixture provenance missing, or subject revision mismatch.
resource limits: Deterministic finite graph fixture.
temporary exceptions: #168's documented one-time action-contract rollover is held outside the wrapper-only fixture.
owner: Ontology graph maintainers.
expiry: Never.
status: Indeterminate. The base golden, changed-ID sensitivity, and ordering tests passed in the exact-current 98fabb4 local portfolio. Protected execution and review remain pending.
```

### 9. Release evidence must be regenerated or promoted by digest

```text
class: PR evidence or a movable policy is reused to authorize different tag/package bytes.
first instance: Required CI reusable workflows used movable @v1 references while comments named a SHA.
second instance: Contracts tag publishing packed committed generated outputs without rerunning TypeSpec or Contracts tests.
earliest sound layer: Layer 4 workflow/dependency policy plus layer 7 production-path release proof.
policy data location: SHA-pinned reusable workflow uses in ci.yml; publish-contracts.yml tag/revision/regeneration/test/schema/digest steps.
mechanism: Pin proof policy, verify checkout equals tag, regenerate/test/diff, bind schema baseline and candidate nupkg hashes, then publish.
kill fixture: Movable reusable ref; dirty regeneration; tag/checkout mismatch; unavailable published baseline; package digest mismatch.
guard self-test: Each precondition exits nonzero before publish, and no skip branch can report success.
protected paths: Required PR CI and contracts-v* environment-protected publication.
pass signal: Named commit/policy/toolchain and artifact digests with every prerequisite successful.
fail signal: Product/schema/release policy violation.
indeterminate signal: Missing tool, unavailable network/baseline, skipped job, timeout, or digest mismatch.
resource limits: CI job timeouts and immutable package/version selection.
temporary exceptions: None; an unavailable baseline blocks rather than creating an allowlist.
owner: Strategos release maintainers.
expiry: Never.
status: Indeterminate. The exact-current 98fabb4 solution portfolio, standalone policy gates, fresh package/consumer probe, serialized pack script, and Basileus smoke passed locally. Protected PR execution and review are pending, and the future Contracts tag run remains an intentionally fresh evidence event that cannot inherit the local candidate digests.
```

## Remaining open class

No repeated defect class found by this run remains without a structural guard. External consumer
adoption and #169 compensation semantics are separate product obligations, not missing #167 guards.
