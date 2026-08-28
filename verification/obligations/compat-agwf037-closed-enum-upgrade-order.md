# compat-agwf037-closed-enum-upgrade-order — AGWF037 is an additive minor that breaks un-upgraded strict consumers

| | |
|---|---|
| **Claim** | Adding `AGWF037` as Contracts `0.6.0 → 0.7.0` is a closed-enum extension: the new member is additive in schema-diff, and any consumer still pinned to a prior `AgwfCode` converter throws if it deserializes the new wire token. Consumer upgrade must precede producer emission. |
| **Scope** | Published `LevelUp.Strategos.Contracts` wire surface: TypeSpec `AgwfCode` / `AgwfEntryDuplicatePermittedForkTrigger`, generated `AgwfCode` / `AgwfCodes`, `agwf-catalog.json`, `AgwfCode.json`, and the packed nupkg consumed by Exarchos. |
| **Consequence** | An Exarchos (or other) process still on 0.6.0 that ingests a 0.7.0 catalog entry or a payload carrying `AGWF037` throws at deserialize. A producer that emits the new id before consumers upgrade looks like an additive ship and is a hard break at the other process. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `StrictEnumConverterTests` (class: unknown member throws) plus `SchemaDiffTests.SchemaDiff_AddedEnumMember_IsNoticeNotBreaking`. No test binds that class to `AgwfCode` / `AGWF037` specifically, and the package CHANGELOG Unreleased section never names 0.7.0. |
| **Why not cheaper** | Generation (rung 1) derives the member from TypeSpec; it does not prove out-of-repo consumers can read it. The compiler (rung 2) cannot see Exarchos. Structural schema-diff (rung 3) classifies the addition as NOTICE and stays green — that is the policy that *requires* the upgrade-order rule, not a proof that consumers upgraded. |
| **Failure signal** | Consumer-side `JsonException` on unknown `AgwfCode`. Nothing in this repository pages or fails the producer build when a stale consumer is still live. The channel does not separate fail from indeterminate on the producer side. |
| **Rollback** | Revert `12098da` / the catalog member. Source revert does not reverse a published `contracts-v0.7.0` tag (this branch does not create one). Already-upgraded consumers keep the member. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high for the in-repo mechanism (strict converter + NOTICE policy + appended member). medium for whether any out-of-repo consumer is currently pinned below 0.7.0. |

**Compatibility class:** breaking change presented as additive (closed-enum extension; DR-18 upgrade-order).

**Impact:** additive schema / minor bump; breaking for strict deserializers that have not upgraded.

**Reverse dependency closure:**

1. `src/Strategos.Contracts/Diagnostics/AgwfCatalog.tsp:55` (`DuplicatePermittedForkTrigger: "AGWF037"`) and model at `:358-366`.
2. Generated C# `AgwfCode.DuplicatePermittedForkTrigger` (`AgwfCode.g.cs:143-145`) with `[JsonConverter(typeof(JsonStringEnumConverter<AgwfCode>))]` at `:20`.
3. Generated constants `AgwfCodes.DuplicatePermittedForkTrigger = "AGWF037"` (`AgwfCodes.g.cs:109`).
4. Catalog JSON `count` 30→31; new entry `since: 2.11.0`; `catalog_version` stays `"0.2.0"` (`agwf-catalog.json:2-3`, `:246-253`).
5. Enum schema gains `"AGWF037"` (`AgwfCode.json:36`); new file `AgwfEntryDuplicatePermittedForkTrigger.json`.
6. In-repo producer: `WorkflowDiagnostics.DuplicatePermittedForkTrigger` (`WorkflowDiagnostics.cs:611-618`) on C# extract and JSON import (survey production path 2a/2b).
7. Out-of-repo: Exarchos extracts `agwf-catalog.json` and JSON Schema from the nupkg (`Strategos.Contracts.csproj:56-73`). `ContractsJson.Options` is the canonical serializer; enums bind by name, never ordinal (`AgwfCatalog.tsp:15-17`).
8. Packaging test asserts nupkg name `LevelUp.Strategos.Contracts.0.7.0.nupkg` (`PackagingTests.cs:107`) and does not require the AGWF037 schema or catalog file inside the archive.

**What this revision does not do**

- No removed or renamed AGWF member. Existing remediations for AGWF001–036 are unchanged in the catalog diff (only the new entry and `count`).
- No serialization format change for other families. `ContractsJson` options are unchanged.
- No persisted workflow-IR field change. AGWF037 is a diagnostic identity, not a stored saga event.
- Package `CHANGELOG.md` Unreleased still describes AGWF036 / `0.5.0 → 0.6.0` and never names AGWF037 / 0.7.0. The consumer-notice line the NOTICE policy asks for is missing on the package changelog.

**Reverses?** Source: yes, by reverting the catalog commit. Published tag: no (`contracts-v0.5.0`, `0.6.0`, and `0.7.0` are absent; last contracts tag is `contracts-v0.4.0`). Already-emitted diagnostic ids in consumer logs: no.

**Open questions:**

- Has `contracts-v0.7.0` (or 0.5.0 / 0.6.0) been published to nuget.org by some path other than this branch? A live 0.7.0 makes source revert a compatibility event for already-upgraded consumers. A never-published 0.7.0 means the first publish is a jump from last tagged `contracts-v0.4.0`.
- Does Exarchos regenerate its TypeScript `AgwfCode` from each nupkg, or pin a snapshot? A snapshot means upgrade is a human step; a regenerate-on-restore means the throw happens only while the old package is still restored.

**What is expensive to find again**

The upgrade-order rule is written next to AGWF035/036 in `src/Strategos.Contracts/CHANGELOG.md:24-26` and in `StrictEnumConverterTests.cs:12-19`. The 0.7.0 bump reused the mechanism and omitted the consumer-notice line. `catalog_version` staying at `0.2.0` while `count` moves is easy to miss on a later bump.
