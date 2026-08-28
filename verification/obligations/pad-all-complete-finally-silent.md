# pad-all-complete-finally-silent

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 14, 15, 18, 44, 63, 108, 110, 114

| | |
|---|---|
| **Claim** | All-`Complete()` exclusive paths plus `Finally<T>` stay silent. The under-reach arm fires only for constructs marked rejoin. |
| **Scope** | `AddBranchRejoinDispatchers`, `CollectConstructDispatchers`, `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire`. |
| **Consequence** | If the claim is false, every all-terminal branch with a declared `Finally` fails the build (the false-positive trap named in issue 185 and plan T1). |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire` — calls `Report` on the parsed model *and* `GeneratorTestHelper.RunGenerator` on the same source. |
| **Why not cheaper** | Silence is a semantic property of a specific authored shape. Types cannot express “this branch is all-terminal.” A structural scan cannot distinguish legitimate zero-dispatch from a dropped rejoin without the same IR walk the guard already does. |
| **Failure signal** | AGWF035 on a legal all-Complete + Finally workflow (authors observe at compile). |
| **Rollback** | Revert the under-reach arm. The false-positive trap returns only if a naive “something must list the terminal” check is added back. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — mechanism and generator-path assertion both present. |

**Open questions:**

- None on the C# all-Complete branch + Finally shape covered by the fixture. JSON import currently maps `Branches: null` (`WireToModelBridge.cs:240`), so this shape is not an import subject today.

## Discriminating detail

`AddBranchRejoinDispatchers` (`TerminalReachabilityGuard.cs:246-260`) skips a case when `branchCase.IsTerminal` or the branch’s `RejoinStepName` is not the declared terminal.

`CollectConstructDispatchers` (`:375-409`) excludes a branch/fork predecessor from the linear-predecessor scan so an all-Complete branch whose next main-flow step is the terminal is not treated as a missing dispatcher (`:366-371`).

The test (`TerminalReachabilityDiagnosticTests.cs:508-522`) asserts AGWF035 is absent from both the direct `Report` call (production `Build`, no `WithoutSuccessor`) and the real generator. Fixture comment at `:273-275`.

## Disposition

Inventory 14, 15, 18, 44, 63, 108, 110, 114: **supported** for the C# all-Complete branch + Finally shape. Fire-only-on-rejoin is the mechanism that makes silence hold.
