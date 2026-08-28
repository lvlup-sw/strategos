# diagnostic-fork-primary-ctor-bypasses-create

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

A `DiagnosticForkModel` (and `PermittedForkTriggerModel`) must not be representable with empty anchors, empty or duplicate trigger names, an empty compensation seed, or `MaxForks < 1`. `Create` is not a type.

## What led here

This wave added a throw inside `Create` for duplicate trigger names and a diagnostic path that avoids `Create`. The records still expose their primary constructors. The invalid states `Create` rejects remain constructible. Survey mechanism items 11–12.

## Code at this revision

- `src/Strategos.Generators/Models/DiagnosticForkModel.cs:54-58` — `internal sealed record DiagnosticForkModel(...)` with a public-to-the-assembly primary constructor. No invariant on the positional parameters.
- `src/Strategos.Generators/Models/DiagnosticForkModel.cs:90-138` — `Create` validates floors and, new in this wave, throws `ArgumentException` when `FindDuplicateTriggerNames` is non-empty (`:125-132`).
- `src/Strategos.Generators/Models/DiagnosticForkModel.cs:199-244` — `PermittedForkTriggerModel` is the same shape: record primary constructor plus `Create` that rejects empty names / empty evidence.
- `src/Strategos.Generators/Helpers/DiagnosticForkExtractor.cs:174-190` — on duplicate, report AGWF037 and `return false` so `Create` is not called. On a well-formed unique edge, `Create` is called.
- `src/Strategos.Generators/Import/WireToModelBridge.cs:130-134, 214, 916-939` — import rejects first (`CollectImportRejections`); `MapDiagnosticForks` still calls `PermittedForkTriggerModel.Create` / `DiagnosticForkModel.Create` when rejections are empty.

Two failure channels for one invalid state: authoring gets a diagnostic; a direct `new DiagnosticForkModel(...)` or a missed rejection gets a throw (or a silent invalid record). Tests exercise `Create` (`DiagnosticForkModelTests.cs`). Nothing stops `new`.

## Failure scenario

A future mapper, a test helper, or a `with` expression uses the primary constructor. Duplicate triggers, `MaxForks = 0`, or empty anchors become IR. Saga lowering (#151, deferred) would then switch on a non-unique trigger or emit a bound of zero. The AGWF037 diagnostic never fires because `Create` was not the construction path.

## Why not cheaper

Rung 1: the IR is not generated from a schema that forbids these fields. Situational.

Rung 2: hide the primary constructor (`private` / `init`-only through `Create`). That is the cheapest sound proof. The throw in `Create` is a runtime check on one factory, not a type.

Rung 4: `Create_WithDuplicateTrigger_ThrowsArgumentException` does not constrain `new DiagnosticForkModel(...)`.

## Failure signal

Nothing, unless the caller used `Create` and an exception escapes the generator (CS8785). The diagnostic channel is only on the extractor / import rejection paths.

## Rollback

Make the primary constructor private. Callers that already used `new` fail to compile. No persisted data.

## Open questions

- Does any in-repo production caller use the primary constructor today? A grep of `new DiagnosticForkModel` / `new PermittedForkTriggerModel` outside tests would resolve this. If none exist, the hole is latent. If one exists, the obligation is already a constructed invalid IR.
- Does `#151` lowering assume `Create` was the only writer? If the emitter switches on `TriggerName` without re-checking uniqueness, a bypassed `Create` becomes a CS0152 or a silent first-wins at emit time.

## What is expensive to find again

Comments say “do not first-wins-dedup” and “Create would throw.” Readers conclude the type is closed. The type is open; the factory is closed.
