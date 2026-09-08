# Promise obligation 01 — exact workflow resolution

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over `45c86a63a9437abd920240b4dc95b235c0f72d37`; final commit/fingerprint binding was pending stabilization.
- **Promise sources:** issue #167 acceptance criterion 1; `IC-167-004`, `IC-167-006`, `IC-MIGRATION-005`, `IC-DIAGNOSTICS-002`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; current formal posture and exact-commit evidence are in `../ledger.md` and
`../final-evidence.md`.

## Obligation

Every source-visible workflow binding must preserve its exact ordinal identifier and resolve to
exactly one merged C#/imported workflow model. Zero or multiple matches must produce the Error
diagnostic AGWF039, not a generator exception or a permissive result. A dynamic binding must remain
distinguishable and fail as AGWF042.

The cheapest sufficient proof is **R3, build-time structural analysis**: this behavior is itself a
compiler diagnostic. A unit test of the value object (R2) cannot establish merged-catalog behavior
or that Roslyn receives an Error diagnostic.

## Delivery evidence

- `WorkflowBindingProofAnalyzer.cs:18-78` builds the action catalog, groups all supplied workflow
  models with `StringComparer.Ordinal`, analyzes only bound actions, and emits AGWF039 unless the
  exact group has length one.
- `WorkflowIncrementalGenerator.cs:145-157` joins authored and imported workflow models into that
  single proof callback. Its duplicate-emission suppression at `:78-104` and `:121-143` prevents an
  `AddSource` hint collision from pre-empting AGWF039.
- `WorkflowDiagnostics.cs:638-646` declares AGWF039 as enabled-by-default Error severity.
- `ImportedWorkflowBindingProofTests.cs:21-168` includes a positive import and C#/JSON, JSON/JSON,
  and C#/C# collisions, including the distinct dynamic-binding classification.
- `WorkflowBindingProofAnalyzerTests.cs:20-55,221-236` contains the legacy-string success case,
  ordinal constant identity, and missing-workflow failure.

Commands observed during this lens:

```text
dotnet build src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj \
  --configuration Release --no-restore --disable-build-servers -m:1 -v minimal
=> Build succeeded; 0 warnings; 0 errors (working-tree fingerprint 64a0eca0...).

dotnet test --project src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj \
  --configuration Release --no-build --no-restore
=> 1700/1700 passed on earlier stabilized fingerprint 7265dfa5... .
```

The later full-suite rerun occurred while the root agent was deliberately changing the shared
fixture compiler and is not used as product evidence. Re-run this obligation after stabilization
and bind it to the final commit.

## Boundary

“Workflow catalog” in v2.13 means the current compilation plus workflow JSON supplied as
`AdditionalFiles`. Referenced-assembly and runtime-source catalogs are explicitly out of scope and
tracked by #204; see obligation 11.
