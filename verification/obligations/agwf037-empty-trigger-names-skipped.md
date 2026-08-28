# agwf037-empty-trigger-names-skipped

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

Two permitted triggers with empty or null names on one diagnostic-fork edge must not be treated as unique. The AGWF037 uniqueness check must not skip the empty string.

## What led here

`FindDuplicateTriggerNames` documents “Empty names are ignored.” The JSON import coalesces a missing wire `Trigger` to `""` and then uses that helper as the AGWF037 authority. Empty-name twins therefore produce no diagnostic. Survey mechanism item 12; single-lens finding (L1) “empty-name AGWF037 hole.”

## Code at this revision

- `src/Strategos.Generators/Models/DiagnosticForkModel.cs:141-169` — `if (string.IsNullOrEmpty(name)) continue;` before the `HashSet`. Two `""` values (or a `null` enumerated as empty) yield an empty duplicate list.
- `src/Strategos.Generators/Import/WireToModelBridge.cs:459-492` — import uniqueness: `triggerNames[j] = permittedTriggers[j].Trigger ?? string.Empty`, then `FindDuplicateTriggerNames`. Null and missing names become `""` and are skipped. No AGWF037.
- `src/Strategos.Generators/Import/WireToModelBridge.cs:130-134` — any rejection returns `BridgeResult(null, rejections)` and never maps. Empty names that are *not* flagged leave this gate.
- `src/Strategos.Generators/Import/WireToModelBridge.cs:916-939` — `MapDiagnosticForks` then calls `PermittedForkTriggerModel.Create(trigger.Trigger ?? string.Empty, …)`. `Create` throws `ArgumentException` on null/whitespace (`DiagnosticForkModel.cs:221`).
- C# extract (`DiagnosticForkExtractor.cs:142-151, 236-251`) uses `seenTriggerNames.Add(trigger.TriggerName)` **without** skipping empty, but `TryGetForkTriggerName` returns false for empty, so the C# path cannot produce this state. The hole is the import + shared helper.

Whitespace `" "` is `IsNullOrEmpty` false, so it *is* counted as a duplicate key, then `Create` throws on `IsNullOrWhiteSpace`. Empty/`null` is the skipped case.

## Failure scenario

A `*.workflow.json` edge lists two permitted triggers with omitted or null `trigger`. `CollectImportRejections` does not add AGWF037. Mapping calls `Create("")` and the generator throws (CS8785) instead of a stable AGWF code. Or, if a future mapper uses the primary constructor (see `diagnostic-fork-primary-ctor-bypasses-create`), the edge lowers with two empty names and no diagnostic.

One empty name plus one real name: no duplicate, then `Create("")` throws. Same crash, different count.

## Why not cheaper

Rung 1: the wire schema already has `@minItems(1)` on evidence fields (AGWF034). A required non-empty trigger name on the wire type would make null unrepresentable at bind time. Situational if the TypeSpec/JSON schema for the trigger name is already optional.

Rung 2: `FindDuplicateTriggerNames` must not skip empty; treat `""` as a name that collides with itself, or reject empty as a distinct AGWF before uniqueness. The skip is a loosened validator on this wave’s new check. Do not test the skip away — close it in the helper.

Rung 4: a fixture with two null triggers. That is one case. The next sentinel (`" "`, `"\t"`) repeats unless the helper stops special-casing.

## Failure signal

A generator crash, or silence if `Create` is bypassed. Not a stable AGWF037. The crash vs diagnostic split is the failure mode.

## Rollback

Remove the `IsNullOrEmpty` continue, or reject empty names as AGWF034-adjacent before uniqueness. Import files that relied on skipped empties do not exist as a supported contract.

## Open questions

- Is the skip a leftover from treating unparsed C# names as empty, now dead because `TryGetForkTriggerName` already returns false? If yes, deleting the continue is a no-behavior-change on C# and a fail-closed change on JSON. Stakes: the comment “Empty names are ignored” may be the only reason the continue exists.
- Does the wire `Trigger` field allow null in the published JSON schema? If the schema already forbids it, bind-time validation should have rejected the file before `CollectImportRejections`. If the reader coerces missing to null, the helper is the last check and it no-ops.

## What is expensive to find again

C# extract and JSON import advertise one uniqueness authority (`FindDuplicateTriggerNames`). They do not implement the same empty-name policy. The extractor’s `HashSet` would have caught `""` had that string been parseable.
