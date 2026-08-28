### contracts-pack-omits-agwf037 — 0.7.0 pack test must require the AGWF037 artifacts

| | |
|---|---|
| **Claim** | A green `Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent` run must mean the nupkg contains `AgwfEntryDuplicatePermittedForkTrigger.json` under `contentFiles/any/any/schemas/` and `agwf-catalog.json` under `contentFiles/any/any/diagnostics/`. |
| **Scope** | S2. `src/Strategos.Contracts.Tests/PackagingTests.cs:84-157` as retargeted on `324768f` (0.6.0 → 0.7.0). Pack items in `Strategos.Contracts.csproj:58-73`. |
| **Consequence** | Exarchos extracts catalog and JSON Schema from the NuGet package. A 0.7.0 nupkg that lost the AGWF037 schema or the catalog still passes the test this wave updated. The comment on that test says 0.7.0 adds the duplicate-permitted-fork-trigger id. The assertions do not name that file. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | The existing pack test, with named-entry asserts for `AgwfEntryDuplicatePermittedForkTrigger.json` and `contentFiles/any/any/diagnostics/agwf-catalog.json` (or the equivalent path the csproj writes). `Nupkg_Contains_SchemasUnderContentFiles` (`IsNotEmpty`) is not this artifact. |
| **Why not cheaper** | Pack membership is a result of `dotnet pack` and the `Content` items, not a C# type. A structural read of the csproj can list intended items (rung 3) and still miss a glob that fails at pack time; the claim is what Exarchos extracts from the nupkg, so the pack is the subject. |
| **Failure signal** | Nothing in this repo. A missing catalog is an Exarchos codegen failure after upgrade, or a silent stale enum if Exarchos caches. |
| **Rollback** | Revert the 0.7.0 pack asserts and the csproj version. A published `contracts-v0.7.0` tag does not reverse for consumers who already upgraded. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Does Exarchos require `AgwfEntryDuplicatePermittedForkTrigger.json` as its own file, or only `agwf-catalog.json`? The csproj packs the catalog under `diagnostics/`, not `schemas/`. If Exarchos only reads the catalog, a named assert on the entry schema is extra; if it derives Zod from entry schemas, omitting that file is the consumer break.
- Is `agwf-catalog.json` present in a nupkg packed from this revision? The csproj item exists (`68-73`). This obligation is that the *test* would still pass if that item were deleted, not that today's pack is missing the file.

## What led here

This wave retargeted the 0.6.0 pack test to 0.7.0 and wrote that 0.7.0 adds the duplicate-permitted-fork-trigger id. Competing explanation: `Content Include="schemas/**/*.json"` plus the catalog `Content` item make the files unavoidable, so naming them in the test is redundant. Discriminating detail: `Nupkg_Contains_SchemasUnderContentFiles` asserts `schemaEntries` is not empty (`63`). The 0.7.0 test asserts `SdlcEventEnvelope.json`, `WorkflowDefinitionV1.json`, and `InvariantEntry.json` (`126-137`). Deleting the catalog `Content` item, or excluding `AgwfEntryDuplicatePermittedForkTrigger.json` from the glob, leaves those three names in place. The test this wave updated stays green.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `PackagingTests.cs:72-80` — comment: "0.7.0 adds the duplicate-permitted-fork-trigger diagnostic id."
- `PackagingTests.cs:107-119` — version pin 0.7.0 on file name and nuspec.
- `PackagingTests.cs:126-137` — three family representatives; no AGWF037 name; no `diagnostics/` path.
- `PackagingTests.cs:39-63` — any schema `.json` under `contentFiles/any/any/schemas/` is enough.
- `Strategos.Contracts.csproj:58-73` — `schemas/**/*.json` → `contentFiles/any/any/schemas/`; `Generated/agwf-catalog.json` → `contentFiles/any/any/diagnostics/`.

`git diff` on `PackagingTests.cs` is version-string retarget only. No new named artifact for AGWF037.

## Kill probe

Remove the `Content Include="Generated/agwf-catalog.json"` item. Run `Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent`. Expected if the claim held: fail. Actual asserts: version + three unrelated schemas + fixtures + dll. Pass.

Exclude `AgwfEntryDuplicatePermittedForkTrigger.json` from the schema glob. Same test: pass.

## Failure scenario

A pack glob cleanup drops `Generated/` content or moves the catalog path. 0.7.0 tests pass. Exarchos extracts 0.7.0, does not find AGWF037, and either keeps a 0.6.0 catalog or fails later when a producer emits the new code.

## Open questions (full stakes)

### Which packed path does Exarchos read?

The csproj comment (`62-64`) says Exarchos extracts `agwf-catalog.json` to derive a TypeScript `AgwfCode` enum. If that is the only consumer path, the named assert that matters is the catalog under `diagnostics/`, and the entry schema is an in-repo identity artifact. If Exarchos also compiles the entry schemas, both names are required. The obligation's proof artifact list changes with that answer. Exarchos is out of this repository; the in-repo comment is a lead.

### Is today's nupkg missing the files, or only the assert?

The csproj items are present. A pack from this revision likely contains both files. This obligation is about the test staying green if they vanish, not a claim that 0.7.0 is already shipping empty. Confirming a packed nupkg listing would separate "false-green test" from "missing product file." The latter is integration completeness, which this lens does not own.
