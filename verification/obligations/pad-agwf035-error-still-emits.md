# pad-agwf035-error-still-emits

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 99 (INV-5 “stops the sixth appending block”); `WorkflowDiagnostics.cs:556-558` (“a workflow that cannot reach its termination does not run”)

| | |
|---|---|
| **Claim** | AGWF035 is an error that stops generation: a workflow that cannot reach its termination does not run. |
| **Scope** | `hasErrors` in `WorkflowIncrementalGenerator.TransformToResult` vs `TerminalReachabilityGuard.Report` placement. |
| **Consequence** | An Error diagnostic and a generated saga coexist. A consumer that treats “build produced saga sources” as success still ships the unreachable machine. The INV-5 “only thing that stops the sixth appending block” claim is then a warning-shaped Error. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The `hasErrors` predicate itself. No test asserts that AGWF035 joins it. AGWF037 *does* join it — the contrast is in the same method. |
| **Why not cheaper** | Types cannot express “this diagnostic id gates emission.” The gate is a boolean in one method. |
| **Failure signal** | Compiler Error plus generated files. The Error channel does not separate “no saga” from “saga + diagnostic.” |
| **Rollback** | Adding AGWF035 to `hasErrors` is the reverse of the current omission. Reverting the under-reach arm does not change the over-reach half’s same omission. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — `hasErrors` list is explicit. |

**Open questions:**

- Is AGWF035-without-gating intentional house style (report, still emit, let the consumer’s treat-warnings-as-errors / analyzer config decide)? Stakes: if yes, the CHANGELOG/remarks sentence “does not run” is still a false claim about this composition; if no, the omission is a defect. Survey already listed this as run-wide.

## Discriminating detail

`hasErrors` (`WorkflowIncrementalGenerator.cs:930-941`) is:

- duplicate steps
- path-end type collisions
- missing StartWith
- fork without join
- empty loops
- **AGWF037** (`hasDuplicatePermittedForkTrigger`)

AGWF035 is reported *after* that gate, on a fully built model (`:1038-1045`), and the method returns `new WorkflowGeneratorResult(model, diagnostics)` with a non-null model. The C# pipeline then `EmitWorkflowSources` when `result.Model is not null` (`:84-87`).

`WorkflowDiagnostics.cs:556-558`: “An error, not a warning: a workflow that cannot reach its termination does not run.”

AGWF037’s remarks (`:603-606`) state the opposite policy and implement it.

## Disposition

Inventory 99 and the descriptor remarks: **claimed and not implemented** for emission gating. The diagnostic fires; generation does not stop.
