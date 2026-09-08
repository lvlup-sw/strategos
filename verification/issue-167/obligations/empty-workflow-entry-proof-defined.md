# empty-workflow-entry-proof-defined — Empty-workflow entry proof is defined

| | |
|---|---|
| **Claim** | If the supported front end can produce an empty workflow, binding proof accepts it only when the bound requirement implies the bound guarantee; otherwise the front end or proof rejects it deterministically. |
| **Scope** | `PhaseGraph` entry expansion, `WorkflowBindingProofAnalyzer.ProveEntry`, and the accepted C#/JSON workflow grammar. |
| **Consequence** | An empty workflow can be certified as an implementation of an action while performing no state transition capable of establishing the action guarantee. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | A real-front-end test demonstrating either (a) legal and refuted empty bindings with the expected implication result, or (b) deterministic rejection before binding proof plus a structural proof that the `Completed` entry branch is unreachable. |
| **Why not cheaper** | Rung 1 has no generated prohibition. Rung 2 permits empty collections and does not define their action semantics. Rung 3 can establish grammar rejection if that is the product decision, but if empty workflows are supported the implication behavior remains a component semantic. |
| **Failure signal** | Nothing if the analyzer certifies the empty implementation; dispatch completes without the guaranteed state. A front-end rejection would be visible, but no inspected fixture establishes that path. |
| **Rollback** | Reject empty bound workflows explicitly or remove empty workflows from the supported grammar. An ordinary action with `True` predicates is not a sound substitute for identity. |
| **Lenses** | False-green shapes: uncovered branch, skipped-as-pass, cannot-fail. |

**Confidence:** High.

**Current proof posture:** Refuted by reachable-path evidence; this is not an active obligation. Imported JSON with zero steps is rejected by `WireToModelBridge` before proof. Public C# authoring cannot return a `WorkflowDefinition` without `StartWith` establishing `_entryStep` and a terminal operation such as `Finally`. The `Completed` entry branch is defensive internal code, not a supported empty-workflow front end.

**Open questions:**

- None for #167. Any future public empty-workflow feature would create a new obligation before exposure.

### Investigation Log

#### Is the empty-workflow proof branch reachable from a supported authoring path?

- Read: `WorkflowBindingProofAnalyzer.ProveEntry`, the topology semantics suite, import zero-step rejection, `Workflow<TState>.Create`, and the public builder terminal operations.
- Found: imported workflows with no steps are rejected before proof; `Create` returns a builder, and `Finally` returns a definition only after `_entryStep` was established by `StartWith`.
- Not found: any supported C# or JSON path that produces an empty workflow model.
- Conclusion: refuted as an active #167 obligation; deterministic front-end rejection remains covered by topology completeness.
