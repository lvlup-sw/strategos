# legacy-binding-hash-stable — Legacy workflow binding hash stays stable

| | |
|---|---|
| **Claim** | Replacing the legacy workflow-name field with `WorkflowBindingReference.WorkflowId` writes the identical canonical token in the identical serializer slot, so the same graph retains its version while a different workflow ID changes it. |
| **Scope** | `ActionDescriptor` workflow binding representation, `OntologyGraphHasher`, persisted graph versions/caches, and the source-compatible string overload. |
| **Consequence** | Unchanged ontology graphs appear changed after upgrade, invalidating caches/freezes and creating noisy or unsafe migration behavior. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | A base-revision-derived fixed hash fixture plus a sensitivity fixture for changed IDs, executed against the final revision; retain the base SHA and exact legacy fixture as reproducible provenance. |
| **Why not cheaper** | Rung 1 does not generate the old and new serializers from one source. Rung 2 sees both fields as strings but not byte ordering/normalization. Rung 3 can inspect the slot syntactically but cannot prove the complete hash of the graph fixture. |
| **Failure signal** | Version churn at graph freeze/cache comparison; there is no diagnostic explaining that the typed representation caused it. |
| **Rollback** | Restore the legacy canonical token/slot or add a versioned migration that recognizes the prior hash. Renaming the test constant to the new value is not a rollback. |
| **Lenses** | False-green shapes: stale oracle, after-value captured as before-value. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. The test records base SHA `45c86a6`, the exact legacy initializer, and a fixed before hash; an adjacent test establishes sensitivity to rebinding. Independent static inspection confirms the legacy name occupied the same serializer slot now used by `BoundWorkflow.WorkflowId`, and the immutable local suite passed. Protected execution and review remain pending.

**Open questions:**

- None after the final revision's protected run reruns the fixture.

### Investigation Log

#### Is the fixed hash a real before-value rather than an after-value blessed by the new code?

- Read: `OntologyGraphVersionTests.Version_WorkflowBindingReference_PreservesSerializedHash`, the adjacent rebinding test, current `OntologyGraphHasher`, and `git show 45c86a6` for the legacy serializer slot.
- Found: the test documents the exact base revision and initializer; the base and current serializers write the same workflow-name string in the same position; a changed ID changes the hash.
- Found later: the hash golden and changed-ID sensitivity test passed in the exact-current `98fabb4` local portfolio.
- Not found: protected execution or completed review bound to `98fabb4`.
- Conclusion: provenance and local behavior are supported; the active verdict remains `Indeterminate` pending protected binding.
