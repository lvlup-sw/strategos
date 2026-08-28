# int-contracts-pack-agwf037-unasserted

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | The `LevelUp.Strategos.Contracts` 0.7.0 nupkg contains the AGWF037 catalog entry and `AgwfEntryDuplicatePermittedForkTrigger.json`, and a check fails if either is absent. |
| **Scope** | Contracts pack recipe (`Strategos.Contracts.csproj` Content items) and `PackagingTests` pack assertions. Exarchos extracts catalog/schema from the nupkg. |
| **Consequence** | A 0.7.0 pack can omit the AGWF037 schema and/or `agwf-catalog.json` and the existing pack test still passes. Out-of-repo consumers that derive TypeScript `AgwfCode` from the package miss the new id. In-repo catalog tests read the source tree, not the nupkg. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A named-entry inventory of the packed nupkg (or of the `Content` items that produce it) that requires `agwf-catalog.json` plus `AgwfEntryDuplicatePermittedForkTrigger.json`. |
| **Why not cheaper** | Files existing under `schemas/` and `Generated/` do not prove they are inside the nupkg. The compiler does not pack NuGet content. The current pack test asserts *other* named schemas. |
| **Failure signal** | Nothing in this repo. Exarchos would fail later, outside this build. |
| **Rollback** | Revert the 0.7.0 bump. Does not add a pack requirement. A published 0.7.0 tag is a different artifact. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Does a `dotnet pack` of this revision actually embed those two files? The csproj recipe includes `schemas/**/*.json` and `Generated/agwf-catalog.json`. This run did not open a nupkg. The obligation is the missing closure check, not a proven omission.
- Has `contracts-v0.7.0` been published? Stage 0: this branch does not create the tag.

**Confidence:** high that the pack test does not require AGWF037 artifacts. The files are present in the source tree and the recipe *declares* them.

## What led here

Existing-proof P24 and production-path §2c. CHANGELOG Residue claims Contracts bumps 0.6.0 → 0.7.0 because AGWF037 exists. Exarchos extracts `agwf-catalog.json` and JSON Schema from the NuGet package.

Competing explanation: `Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent` already names the AGWF037 schema. False. It names `SdlcEventEnvelope`, `WorkflowDefinitionV1`, and `InvariantEntry`.

## Composition

Source artifacts **exist**:

- `src/Strategos.Contracts/Generated/agwf-catalog.json` contains `"id": "AGWF037"` (entry name `DuplicatePermittedForkTrigger`)
- `src/Strategos.Contracts/schemas/json-schema/AgwfEntryDuplicatePermittedForkTrigger.json`

Pack recipe **declares** them:

- `Strategos.Contracts.csproj:59` — `<Content Include="schemas/**/*.json" Pack="true" PackagePath="contentFiles/any/any/schemas/" />`
- `:68-73` — `Generated/agwf-catalog.json` packed to `contentFiles/any/any/diagnostics/`

Pack test **does not require** them (`PackagingTests.cs:84-157`):

- nupkg name `LevelUp.Strategos.Contracts.0.7.0.nupkg`
- nuspec `<version>0.7.0</version>`
- named schemas: `SdlcEventEnvelope.json`, `WorkflowDefinitionV1.json`, `InvariantEntry.json`
- ≥100 builder fixtures
- `lib/**/Strategos.Contracts.dll`

`Nupkg_Contains_SchemasUnderContentFiles` (same file `:39-64`) asserts the schemas contentFiles set `IsNotEmpty`. One unrelated schema satisfies it.

`rg` of `PackagingTests.cs` for `AgwfEntryDuplicate|agwf-catalog|AGWF037`: no hits.

Catalog contents are asserted on the committed source tree (`AgwfCatalogEmitterTests`, `AgwfCatalogSchemaTests`), not on the nupkg. Codegen-guard path-filters to contracts / `docs/diagnostics` and does not open a pack.

## Why cheaper rungs fail

- **Rung 1:** TypeSpec → committed JSON is generation of the *source* artifacts. Pack membership is a second representation.
- **Rung 2:** packing is not a type property.
- **Rung 4/5 as currently written:** the pack test is the wrong closure (0.7.0 version + three older named schemas). A new pack test that listed the two files would be rung 5 paying for a rung 3 claim. The cheap proof is a structural inventory of required Content paths.

## Failure scenario

Someone deletes `AgwfEntryDuplicatePermittedForkTrigger.json` or drops the catalog `Content` item. `dotnet pack` still produces `LevelUp.Strategos.Contracts.0.7.0.nupkg`. `Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent` stays green. Exarchos extracts 0.7.0 and does not see AGWF037.

## Code read (this revision)

- `src/Strategos.Contracts/Strategos.Contracts.csproj:36-40`, `:56-73`
- `src/Strategos.Contracts.Tests/PackagingTests.cs:39-64`, `:84-157`
- `src/Strategos.Contracts/Generated/agwf-catalog.json` (AGWF037 entry)
- `src/Strategos.Contracts/schemas/json-schema/AgwfEntryDuplicatePermittedForkTrigger.json` (exists)
- `CHANGELOG.md:179-182`

### Investigation Log

#### Does PackagingTests require the AGWF037 schema or catalog inside the nupkg?

- Read: both pack tests in `PackagingTests.cs`; csproj Content items; `rg` for AGWF037 names in that test file.
- Found: recipe includes the files; test requires three other schema names and a non-empty schemas folder.
- Not found: an assertion on `AgwfEntryDuplicatePermittedForkTrigger.json` or `agwf-catalog.json` in the archive.
- Conclusion: pack closure for AGWF037 is unasserted. Whether this revision's nupkg actually contains them was not verified by opening a pack.
