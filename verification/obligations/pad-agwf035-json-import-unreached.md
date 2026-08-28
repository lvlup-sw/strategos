# pad-agwf035-json-import-unreached

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 41 (“AGWF035 now decides route under-reach”) as a generator-wide claim; contrast inventory 49 (AGWF037 on both paths)

| | |
|---|---|
| **Claim** | Route under-reach is decidable at generation for authored workflows. |
| **Scope** | JSON import pipeline: `BridgeImportFile` → `WireToModelBridge.Bridge` → `EmitWorkflowSources`. |
| **Consequence** | An imported twin of a dropped-Finally (or dropped join) shape emits a saga with no AGWF035. C# extract of the same graph would report (IR-level) or stay silent (#184-class). The two authoring surfaces do not share the control CHANGELOG presents as “now.” |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Call-graph of `TerminalReachabilityGuard.Report`. The existing call-site scan only looks at that identifier; it would stay green if import never calls it — and import does not. |
| **Why not cheaper** | Reachability of a guard is a graph property (rung 3). A component test of `Report` does not prove the import pipeline invokes it. |
| **Failure signal** | Nothing on import. A JSON workflow with a missing rejoin edge lowers. |
| **Rollback** | Calling `Report` from the import path. Reverting the C# arm does not create an import arm. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — one production `Report` call site, on the C# transform only. |

**Open questions:**

- WireToModelBridge currently sets `Loops: null` and `Branches: null` (`WireToModelBridge.cs:240-241`). Forks are mapped. An imported fork under-reach is the live import subject. Whether import of loop-exit / branch Finally is even representable today is a separate surface.

## Discriminating detail

`TerminalReachabilityGuard.Report` is invoked only from `WorkflowIncrementalGenerator.cs:1038` (C# `TransformToResult`) and from tests.

`BridgeImportFile` (`:230-231`) returns `WireToModelBridge.Bridge(...)` and never calls the guard.

Import source output (`:109-112`) emits whenever `bridged.Model is not null`. AGWF037 *does* reject on import (`WireToModelBridge.cs:459-492`) and suppresses the model.

The CHANGELOG sentence is not scoped to C#. AGWF037’s sentence *is* scoped to both surfaces and is implemented on both. That contrast is the declared-control-unreached shape for AGWF035.

## Disposition

Inventory 41 read as a generator-wide guarantee: **declared control that nothing makes reachable on JSON import.** Inventory 49 (AGWF037 both paths) is the working twin.
