# Promise obligation 08 — Contracts 0.11 wire shape and validation

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final package binding was separate in obligation 09.
- **Promise sources:** `IC-CHANGELOG-005`, `IC-MIGRATION-006`, `IC-CONTRACTS-001`/`002`, `IC-TYPESPEC-001`/`002`, `IC-GENERATED-CONTRACT-001`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

Contracts 0.11 must define one optional `action` field on all five workflow step arms. When present,
its domain/object/action strings must all exist and contain a non-whitespace character; generated
C# must reject malformed read and write values. When absent, the configured serializer must omit
the field so legacy workflow JSON shape is unchanged. Projection/import must preserve the tuple.
AGWF039–AGWF043 must be represented consistently in the closed diagnostic vocabulary.

The cheapest sufficient proof is **R4, schema/generated-model round-trip tests**. Reading TypeSpec
(R1) cannot establish generated callbacks, serializer omission, or import projection.

## Delivery evidence

- `ActionReference.tsp:12-27` has three required strings with `@minLength(1)` and
  `@pattern(".*\\S.*")`; `StepDefinition.tsp:22-48` single-sources optional `action` via
  `StepCommon`, which all five arms spread.
- `Strategos.Contracts.csproj:41-45` sets the single package version to 0.11.0 and packs all schemas
  at `:61-79`.
- Generated `ActionReferenceV1.g.cs:19-56` implements both JSON callbacks and required/nonblank
  validation. Each of `SkillStep`, `HandlerStep`, `GateStep`, `DelegateStep`, and `ApprovalStep`
  has a nullable `ActionReferenceV1` property.
- `ContractsJson.cs:40-45` uses `WhenWritingNull`, so an absent action emits no property.
- `StepDefinitionSchemaTests.cs:88-246` compiles TypeSpec, checks all five optional references and
  three constraints, round-trips a present value, and rejects missing/empty/whitespace fields on
  both read and write.
- `ProjectionTests.cs:31-71` proves tuple projection and absent-field omission;
  `RoundTripIrFidelityTests.cs:234-299` proves projection/import and malformed-versus-missing state.
- `AgwfCatalogEmitter.cs:99-119` now fails generation unless `AgwfCode.json` exactly matches the
  `AgwfEntry*.json` IDs and order; its focused contract test is at
  `AgwfCatalogEmitterTests.cs:78-104`.

The current generated diff confirms the exact non-whitespace callback appears on
`ActionReferenceV1` and, after the `98fabb4` authority-topology repair, on the existing
`WorkflowDefinitionV1.Name` lookup identity. Read/write tests pin both intended validators; unrelated
regex and scalar-alias constraints remain outside the callback rule.
