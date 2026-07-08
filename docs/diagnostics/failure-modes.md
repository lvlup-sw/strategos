# Failure-mode enumeration — v2.10.0 strategy-compiler bundle (DR-18)

Every failure mode introduced by the v2.10.0 strategy-compiler bundle maps to a **typed
channel** (a build diagnostic, a generated runtime guard, a typed response, or a
schema-validation rejection) — never a stringly-typed or silent failure — and each is
covered by at least one test. This is the DR-18 failure-mode doc-check; the AGWF ids are
catalogued in [`agwf.md`](./agwf.md).

## Build diagnostics — JSON import (DR-12 / DR-13 / DR-14 / DR-2 / DR-3)

| Failure mode | Channel | Covering test |
|---|---|---|
| Malformed import JSON | `AGWF023` MalformedWorkflowJson | `WireIrReaderTests.MalformedWorkflowJson_ReportsStableDiagnostic` |
| `schemaVersion` ≠ `"1.0"` (incl. missing) | `AGWF024` UnsupportedSchemaVersion | `WireIrReaderTests.UnsupportedSchemaVersion_ReportsStableDiagnostic` |
| Unresolvable step moniker | `AGWF025` UnresolvableStepMoniker | `WireMonikerResolverTests.Resolve_NoMatchingType_ReportsUnresolvableDiagnostic` |
| Ambiguous step moniker | `AGWF026` AmbiguousStepMoniker | `WireMonikerResolverTests.Resolve_TwoCandidatesSharingName_ReportsAmbiguousDiagnostic_DeterministicOrder` |
| Delegate (lambda) step at import | `AGWF027` ImportRejectedDelegateStep | `ImportRejectionTests.DelegateStep_IsRejected_WithDiagnosticAndNoSaga` |
| Branch point at import | `AGWF028` ImportRejectedBranchPoint | `ImportRejectionTests.BranchPoint_IsRejected_WithDiagnosticAndNoSaga` |
| Loop (`RepeatUntil`) at import | `AGWF029` ImportRejectedLoop | `ImportRejectionTests.Loop_IsRejected_WithDiagnosticAndNoSaga` |
| Validation predicate at import | `AGWF030` ImportRejectedValidationPredicate | `ImportRejectionTests.ValidationPredicate_IsRejected_WithDiagnosticAndNoSaga` |
| Approval-with-context at import | `AGWF031` ImportRejectedApprovalContext | `ImportRejectionTests.ApprovalWithContext_IsRejected_WithDiagnosticAndNoSaga` |
| Dangling `gateId` (DR-3 semantic) | `AGWF032` ImportDanglingGateId | `ImportRejectionTests.DanglingGateId_IsRejected_WithDiagnosticAndNoSaga` |
| Reliability-bearing gate declaration (DR-2 machine-check) | `AGWF033` ImportReliabilityBearingGate | `ImportRejectionTests.ReliabilityBearingGate_IsRejected_WithDiagnosticAndNoSaga` |
| Fork trigger with empty `requiredEvidenceFields` (DR-8 evidence floor) | `AGWF034` ImportForkTriggerWithoutEvidence | `ImportRejectionTests.ForkTriggerWithEmptyEvidence_IsRejected_WithDiagnosticAndNoSaga` |

Every rejected import emits **no saga** for that workflow (proven per-case), and the whole
corpus partitions into importable-equivalent vs specifically-rejected with no silent third
bucket (`RoundTripEquivalenceTests.EveryCorpusFixture_LandsInExactlyOneBucket`).

## Generated runtime guards — diagnostic fork (DR-9)

| Failure mode | Channel | Covering test |
|---|---|---|
| `maxForks` exceeded | generated `ForkBlocked` human-escalation terminal | `DiagnosticForkLoweringTests.Behavioral_ForkExceedingMaxForks_RoutesToBlockedTerminal` |
| Fork without complete evidence | generated evidence-required guard (refuse) | `DiagnosticForkLoweringTests.Behavioral_ForkWithoutEvidence_IsRefused` |

## Typed response — licensed abstention (DR-11)

| Failure mode | Channel | Covering test |
|---|---|---|
| No records retrieved (closed-book) | `NoAnswerRecorded` union arm + `ontology.abstained` audit | `AbstentionUnionTests.Compose_WithEmptyRetrieval_YieldsNoAnswerRecordedWithNearest` |
| Uncited answer attempted | composer guard clause (refuse) | `AbstentionUnionTests.Answer_RefusesEmptyCitations` |

## Schema-validation rejection (DR-2 / DR-8)

| Failure mode | Channel | Covering test |
|---|---|---|
| Reliability block without `source` | JSON Schema `required` (validation fails) | `GateDeclarationSchemaTests.GateReliability_WithoutSource_FailsSchemaValidation` |
| Fork occurrence missing required evidence | JSON Schema `required` (validation fails) | `ForkTriggerSchemaTests.ForkOccurrence_MissingEvidence_FailsSchemaValidation` |

## Schema-evolution guardrail (DR-18)

The full v2.9.1 → v2.10.0 schema delta is machine-verified NON-BREAKING by the CI
schema-diff driver (`scripts/contracts-schema-diff.mjs`): 24 new schema documents + 2
optional root slots, zero breaking changes. Enum-member removal/rename would classify
BREAKING and block merge; enum additions are flagged NOTICE
(`SchemaDiffTests.SchemaDiff_AddedEnumMember_IsNoticeNotBreaking`), and emitted converters
stay strict — an unknown wire member throws `JsonException`
(`StrictEnumConverterTests.EmittedEnumConverter_UnknownMember_ThrowsJsonException`).
