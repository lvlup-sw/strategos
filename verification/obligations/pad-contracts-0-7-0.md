# pad-contracts-0-7-0

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 24, 50, 69, 123

| | |
|---|---|
| **Claim** | `Strategos.Contracts` bumps 0.6.0 → 0.7.0 and the package versions at exactly 0.7.0. |
| **Scope** | `Strategos.Contracts.csproj` `ContractsVersion`; `PackagingTests.Package_Version_Is_0_7_0_WithEventsIrAndDiagnosticsContent`. |
| **Consequence** | A source bump that does not match the packed version ships the wrong identity to Exarchos. A catalog member (`AGWF037`) without a version bump leaves converters throwing on unknown members after a silent add. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `PackagingTests` asserts nupkg filename and nuspec `<version>0.7.0</version>`. Catalog/enum tests list `AGWF037`. |
| **Why not cheaper** | The version is a project property (rung 1-adjacent) but pack identity is a packaging property. Compiler does not see NuGet version. |
| **Failure signal** | Pack test (when run). A published tag `contracts-v0.7.0` is not created by this branch. |
| **Rollback** | Revert `ContractsVersion`. A published 0.7.0 tag would not reverse for already-upgraded consumers. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High for the in-repo source and pack-test identity. |

**Open questions:**

- Is `contracts-v0.7.0` published? This branch does not create the tag (stage0). Stakes: Exarchos consumers that must upgrade first have no package until a release job runs.
- `PackagingTests` does not require the AGWF037 JSON Schema / catalog entry to be *inside* the nupkg (survey lens 5). The version claim can hold while the new member is missing from contentFiles. Stakes: Exarchos extracts catalog from the package; a 0.7.0 nupkg without AGWF037 is a false identity.

## Discriminating detail

```37:40:src/Strategos.Contracts/Strategos.Contracts.csproj
    <ContractsVersion>0.7.0</ContractsVersion>
    <MinVerSkip>true</MinVerSkip>
    <Version>$(ContractsVersion)</Version>
    <PackageVersion>$(ContractsVersion)</PackageVersion>
```

`AgwfCatalog.tsp:55` / `:358-365` add `DuplicatePermittedForkTrigger: "AGWF037"`.

`PackagingTests.cs:107-119` asserts the packed filename and nuspec version.

## Related contradiction (same CHANGELOG file)

Product `CHANGELOG.md:17` (2.11.0 lede, already on the base and left in place) still says Contracts **0.4.0 → 0.6.0**. Residue at `:182` says **0.6.0 → 0.7.0**. See `pad-contracts-changelog-contradicts-0-7-0`.

## Disposition

Inventory 24, 50, 69, 123: **source bump and pack-test identity supported.** Publication and nupkg *content* for AGWF037 are not established by this diff.
