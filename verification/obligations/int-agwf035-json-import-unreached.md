# int-agwf035-json-import-unreached

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | Every authoring front that emits `ValidTransitions` also runs `TerminalReachabilityGuard.Report`, including the AGWF035 under-reach arm. |
| **Scope** | `WorkflowIncrementalGenerator` JSON import pipeline (`BridgeImportFile` → `EmitWorkflowSources`) vs C# `[Workflow]` pipeline (`TransformToResult`). |
| **Consequence** | A JSON-imported workflow whose rejoin last step never dispatches the declared terminal still emits a saga and a `ValidTransitions` table. The consumer sees a green compile. The C# twin of the same IR would report AGWF035. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A call-graph / source-output closure check: every `RegisterSourceOutput` that calls `EmitWorkflowSources` must also call `TerminalReachabilityGuard.Report` on that model. |
| **Why not cheaper** | Generation cannot derive a missing call. The compiler cannot see that the JSON pipeline omits the guard. A component test of `Report` (or a C# `RunGenerator` fixture) does not prove the import composition invokes it. |
| **Failure signal** | Nothing. AGWF035 is the signal, and it does not fire on this front. |
| **Rollback** | Revert the under-reach arm. Does not restore AGWF035 on JSON import, because that path never had it. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Do any in-production consumers declare `*.workflow.json` AdditionalFiles? If none do, the hole is latent. If any do, shipped JSON workflows skip AGWF035 today.
- Is AGWF035-without-gating (Error, still emits) intentional? It does not change that the JSON front never calls the guard.

**Confidence:** high. Discriminating evidence is the two `RegisterSourceOutput` bodies.

## What led here

Production-path survey §1b named JSON import as unreached for AGWF035. CHANGELOG Residue claims "`AGWF035` now decides route under-reach" and that the guard and `ValidTransitions` "share one `PhaseGraph` so they cannot drift." JSON import *does* emit `ValidTransitions` via `PhaseGraph.Build`. It does *not* call the guard.

Competing explanation: the JSON pipeline reports the diagnostic through `bridged.Diagnostics` after the bridge runs the same guard. That explanation is false. `BridgeImportFile` only calls `WireToModelBridge.Bridge`.

## Composition

C# front — **reached** (`WorkflowIncrementalGenerator.cs:75-87`, `:1038-1043`):

```
[Workflow] → TransformToResult → TerminalReachabilityGuard.Report → EmitWorkflowSources
```

`Report` sits *after* the `hasErrors` gate (`:933-1045`). Under-reach does not suppress emission. Over-reach is the same report-and-still-emit shape.

JSON front — **unreached** (`:98-113`, `:209-231`):

```
AdditionalFiles *.workflow.json
  → BridgeImportFile → WireToModelBridge.Bridge
  → RegisterSourceOutput → EmitWorkflowSources
```

`WireToModelBridge.Bridge` has no `TerminalReachabilityGuard` call. The only production caller of `Report` is `TransformToResult`. Other hits are tests in `TerminalReachabilityDiagnosticTests.cs`.

Both fronts share `EmitWorkflowSources` (`:127`, C# at `:86`, JSON at `:111`), which calls `TransitionsEmitter.Emit` → `PhaseGraph.Build` (`TransitionsEmitter.cs:56`). The shared graph type reaches the published transition table on JSON import. The diagnostic that CHANGELOG pairs with that table does not.

## Paths tests reach that shipping does not

`TerminalReachabilityDiagnosticTests` positives inject `PhaseGraph.Build(model).WithoutSuccessor(...)` and call `Report` directly. `GeneratorTestHelper.RunGenerator` covers C# sources. `ImportRejectionTests` drives the real generator over `*.workflow.json` for AGWF037, not AGWF035.

No AdditionalFiles glob ships from `LevelUp.Strategos.Generators` (no `.props`/`.targets` in the project; `Directory.Build.{props,targets}` do not add `*.workflow.json`). Behavioral tests opt in per-file (`Strategos.Generators.Behavioral.Tests.csproj`). A consumer that never declares the item never hits import at all. That is standing opt-in. The new hole is: a consumer that *does* opt in still skips AGWF035.

## Why cheaper rungs fail

- **Rung 1:** no generated registration list for generator pipelines.
- **Rung 2:** both pipelines compile. Absence of a call is representable.
- **Rung 4:** existing tests prove the guard function and the C# front. They do not close the import composition.

## Failure scenario

Author imports a `.workflow.json` whose model would fail under-reach on C# (rejoin last step never dispatches `Finally<T>`). Generator emits saga + `ValidTransitions`. Runtime can walk past the declared terminal. AGWF035 never appears.

## Code read (this revision)

- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:19-29`, `:54-113`, `:209-246`, `:929-1045`
- `src/Strategos.Generators/Diagnostics/TerminalReachabilityGuard.cs` (production `Report` at generator `:1038`)
- `src/Strategos.Generators/Strategos.Generators.csproj:71-75` (analyzer pack; no MSBuild import glob)
- `src/Strategos.Generators.Tests/Diagnostics/TerminalReachabilityDiagnosticTests.cs` (direct `Report` / C# driver)
- `CHANGELOG.md:172-177` (claim inventory lead, not a fact)

### Investigation Log

#### Does BridgeImportFile or WireToModelBridge call TerminalReachabilityGuard?

- Read: `WorkflowIncrementalGenerator.Initialize` both `RegisterSourceOutput` bodies; `BridgeImportFile`; `rg TerminalReachabilityGuard` under `src/`.
- Found: production call only at `WorkflowIncrementalGenerator.cs:1038` inside `TransformToResult`. JSON path reports `bridged.Diagnostics` from `WireToModelBridge.Bridge` only.
- Not found: any import-side `Report` / `UnreachableTermination` construction.
- Conclusion: unreached on JSON import. Question closed for the in-repo composition.
