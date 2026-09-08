# Promise obligation 10 — public-surface and generated-authority governance

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final gate run was pending.
- **Promise sources:** `IC-CHANGELOG-001`, `IC-AGWF-AUTHORITY-001`, `IC-CODEGEN-001`, `IC-COMMENT-011`, claim seed 10.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

New/changed public builder members and value objects must be represented in public API baselines;
TypeSpec must remain the mechanically checked authority for generated workflow/diagnostic
contracts; diagnostic descriptors must use generated IDs and agree with the catalog; and runtime
projection must have a forcing function so a new builder step kind cannot silently omit action
identity.

The cheapest sufficient proof is **R3/R4 executable drift gates**. Hand-matched files and prose are
not sufficient evidence of authority.

## Delivery evidence

- `src/Strategos/PublicAPI/PublicAPI.Unshipped.txt:2-9` stages all five newly declared builder
  overloads. `.editorconfig`/`PublicApi.globalconfig` expand local analyzer coverage to the three
  continuation interfaces outside the historical seven and to `WorkflowActionReference`.
- `BuilderApiBaselineTests.cs:48-108,192-240` reflects exactly the ten tracked interfaces and pins
  their declared members to shipped+unshipped ledgers. Its later tests cover the new value object
  and `StepDefinition.Action` shape.
- `check-builder-api-stability.sh:7-55` performs a warnings-as-errors build and retains the exact
  downstream remediation message; `.github/workflows/ci.yml:186-205` documents the historical
  Exarchos subset separately from the expanded local gate.
- `contracts-codegen-guard.yml` regenerates schemas and generated C# then diffs them. The narrowed
  `RecordEmitter` projects the exact non-whitespace schema pattern onto `ActionReferenceV1` and the
  existing `WorkflowDefinitionV1.Name` identity; current read/write tests make the latter intentional
  rather than collateral.
- `AgwfCatalogEmitter.cs:99-119` enforces equality between the public enum schema and entry metadata;
  live Roslyn descriptors use generated `AgwfCodes` constants at `WorkflowDiagnostics.cs:638-676`.
- `ProjectionExhaustivenessTests` and the public callback inventory in
  `StepExtractorActionReferenceTests.cs:23-63` are forcing functions over projection and occurrence
  authoring surfaces.

## Boundary

Most explanatory product documentation remains intentionally hand-authored; it is evidence of the
declared contract, not an executable authority. Generated `docs/diagnostics/agwf.md` is the one
mechanically derived documentation surface. The API and codegen gates later passed at exact-current
`98fabb4`; protected execution and review remain pending.
