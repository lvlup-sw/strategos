# pad-agwf035-message-lie

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 16, 101, 111

| | |
|---|---|
| **Claim** | Prefer the existing AGWF035 message template. Widen catalog remediation only if that sentence becomes a lie when `{2}` is the missing dispatcher. |
| **Scope** | `AgwfCatalog.tsp` AGWF035 remediation; `WorkflowDiagnostics.UnreachableTermination.messageFormat`; under-reach `Report` argument order. |
| **Consequence** | An author of a dropped-Finally workflow is told the declared terminal “chains to” the last step and that “the saga runs past its declared termination.” Both clauses are the opposite of under-reach. The diagnostic is an Error that still emits (`pad-agwf035-error-still-emits`), so the lie is the only signal. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Catalog string identity tests exist; they lock the *old* sentence. No assertion that the rendered under-reach message is true of the fault. |
| **Why not cheaper** | Generation (rung 1) already copies the catalog into C# and markdown. That derivation preserves a sentence that is false for the new arm. Types cannot judge English truth. |
| **Failure signal** | The diagnostic text itself. Authors observe a false remediation. |
| **Rollback** | Widen the catalog (this wave already paid Contracts 0.7.0). Leaving the sentence is the current delivery. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — argument order and catalog sentence are both in source. |

**Open questions:**

- None on whether the over-reach sentence is false of under-reach. The plan’s “unless it becomes a lie” predicate is satisfied; they did not widen.

## What the target claims

Plan T1: “Prefer the existing message template: `{0}` = declared terminal, `{2}` = the last step that should have dispatched it. Only widen catalog remediation if that sentence becomes a lie.”

`TerminalReachabilityGuard.cs:139-140`: “Argument 0 is the declared terminal; argument 2 is the last step that should have dispatched it.”

## Competing explanation

The existing sentence still names a broken pair and is “close enough.”

## Discriminating detail

Catalog / descriptor sentence (`AgwfCatalog.tsp:344`, `WorkflowDiagnostics.cs:564`):

> Step '{0}' in workflow '{1}' chains to '{2}', which is not on the workflow's main flow. … the saga runs past its declared termination instead of completing.

Under-reach call (`TerminalReachabilityGuard.cs:157-163`) passes `declaredTerminalStepName` as `{0}` and `lastStep` as `{2}`.

The terminal does not chain to the last step. The last step failed to dispatch the terminal. “Which is not on the workflow's main flow” is false of a linear predecessor and often false of a rejoin last step that *is* the construct’s own last step. “Runs past its declared termination” is the over-reach story; under-reach never starts the terminal.

T2 already shipped Contracts 0.7.0, so the “do not widen; it costs a bump” reason that justified keeping the sentence is gone.

`WorkflowDiagnostics.cs:545-553` remarks now describe both arms and the inverted argument meaning. The `messageFormat` was not updated. The remarks and the user-visible sentence disagree.

## Disposition

Inventory 16 / 101 / 111: **constraint violated.** They kept the template. The sentence is a lie for the new arm. The plan required a widen in that case.
