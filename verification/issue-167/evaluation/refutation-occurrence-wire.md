---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 98fabb410e4432cc39fd71ec92651e6ceecfcc7c
base_revision: 45c86a63a9437abd920240b4dc95b235c0f72d37
target_ref: codex/167-typed-workflow-binding
implementation_fingerprint: f7271c9cb517e30c658b6d9245a739a9dc8d365d
cost_setting: high
scope_rule: issue 167 occurrence identity and public/wire reverse-dependency boundary
updated: 2026-09-06
skipped: other Stage 3 lenses remain owned by the root verification run
external_references:
  - path: https://github.com/lvlup-sw/strategos/issues/167
    why: authoritative acceptance criteria for typed workflow binding
---

# Adversarial refutation — occurrence identity and wire/public boundaries

## Result

One concrete false-green defect was reproduced and closed. `WireToModelBridge` historically
recognized a top-level step as the serialization echo of a fork-path step using only lowered phase
and type. The first repair added stable step id and action identity, but refutation showed that it
still discarded conflicting occurrence configuration. A duplicated stable id could therefore
carry different compensation, retry, timeout, confidence routing, gate, terminal/runtime, or
instance semantics and be accepted according to list position.

The bridge now treats a repeated stable id as a legal projection echo only when it is exactly one
top-level plus one fork-path occurrence and every parsed wire field is field-equivalent, recursively
including confidence-handler steps. Any conflicting or multiply represented stable id reports
AGWF042 and emits no saga. A different-id/same-phase collision remains AGWF003. Exact configured
round-trip echoes remain accepted.

The fingerprint is mechanically exhaustive rather than a manual field list: it walks every public
instance property on the closed wire DTO graph in ordinal order and rejects an unsupported scalar
kind. `WireStepFingerprintCoverageTests` discovers every DTO type reachable from every step arm,
mutates each declared public property, and requires the fingerprint to change. A future DTO field
therefore enters the kill test automatically instead of relying on a reviewer to update a manifest.

## Refutation matrix

| Boundary | Adversarial case | Evidence |
|---|---|---|
| Runtime value/API | null, empty, whitespace names; mutation surface; duplicate `.Performs` | `WorkflowActionReferenceTests` (10/10) |
| Callback inventory | entry, linear, terminal, branch, loop, fork, join, failure, approval continuations | `StepExtractorActionReferenceTests` (17/17); reflection pins 12 configured callbacks |
| C# extraction | direct constants, named arguments, aliases, captures, factories, duplicate declarations, per-occurrence reuse | `StepExtractorActionReferenceTests` (17/17) |
| Present wire action | null, scalar, array, missing member, whitespace member | `ImportFrontEndRobustnessTests` (9/9); malformed presence is AGWF023, never omission |
| Wire/schema parity | optional action on all five arms and DTO/schema field agreement | `StepDefinitionSchemaTests` (8/8), `WireDtoSchemaConformanceTests` (7/7) |
| Projection/import | tuple preservation, named reuse, legacy omission, exact fork echo | `ProjectionTests` (8/8), `RoundTripIrFidelityTests` (12/12), `ImportIdentityGateTests` (13/13) |
| Collision boundary | different id, conflicting action, configuration, instance, terminal/runtime, and gate; multiple identities | `ImportIdentityGateTests`; seven new configuration/metadata cases failed before the second repair |
| Fingerprint exhaustiveness | every public property on all reachable step/config/action DTOs changes structural identity | `WireStepFingerprintCoverageTests` (1/1), reflection-driven property mutation |
| Front-end merge | C#/JSON, JSON/JSON, and C#/C# workflow identity collisions | `ImportedWorkflowBindingProofTests` (11/11) |
| Legacy graph identity | old string binding retains the independent pinned hash | `OntologyGraphVersionTests` (34/34), including `Version_WorkflowBindingReference_PreservesSerializedHash` |
| Public API scope | exact ten interface files plus two definition carriers; analyzer build | `BuilderApiBaselineTests` (12/12), `PublicApiBoundaryTests` (1/1), `check-builder-api-stability.sh` green |

## Historical discovery and kill evidence

The counts in this subsection describe the pre-`4110b512` discovery sequence. They preserve how the
false green was reproduced and repaired; they are not the current proof verdict.

- Before the stable-id/action repair, the focused identity class had 2 failures out of 5: a
  different occurrence id and a conflicting action were both accepted.
- With that first repair in place but before full field comparison, the expanded identity class had
  7 failures out of 12: compensation, retry, timeout, confidence, instance, terminal/runtime, and
  gate conflicts all emitted a saga without AGWF042.
- After the final repair, the identity class passes 13/13, including an exact fully configured
  positive echo. Generator-test build succeeds with zero warnings and zero errors.

In that historical snapshot, the complete generator suite was also run serially to avoid its parallel output-cleanup race:
1745/1746 passed. The sole failure is outside this refutation and outside the edited paths:
`WorkflowBindingProofAnalyzerTests.ParenthesizedMainChainReceiver_DoesNotHideEntryOccurrence`
reported an unexpected AGWF009 (`Finally` seen as the entry). No proof-calculus file was changed in
that pass.

The authoritative current evidence is `../final-evidence.md`: at exact-current `98fabb4`, the
aggregate solution run reported 5,517 succeeded, 0 failed, and 16 skipped (5,533 total), while the
generator project reported 1,761/1,761. Contracts 185/185, stable codegen, live schema, docs, fresh
packages and external consumer, standalone gates, serialized pack script, and Basileus smoke also
passed. Protected execution and review remain pending, so the active obligations remain
`Indeterminate`.

## Residual risks and boundaries

- Public API mutation testing removes one historical shipped member to prove the analyzer gate
  fails closed. The exact new #167 files and members are pinned by reflection plus a green analyzer
  build, but each newly added callback is not independently mutation-tested.
- Most builder positions expose either an instance-name overload or a configuration callback, not a
  combined overload. Consequently, some same-CLR-type occurrences cannot simultaneously author a
  distinct phase name and `.Performs`; the public documentation discloses this current boundary.
- The vendored JSON reader follows last-member-wins behavior for duplicate JSON object keys. Schema
  validation and generated `System.Text.Json` models do not provide a distinct duplicate-key
  policy, so this remains a general wire-format ambiguity rather than an issue-167 action-specific
  omission.
