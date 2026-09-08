# merged-frontends-share-binding-proof — C# and JSON workflows share one binding proof

| | |
|---|---|
| **Claim** | C# and imported JSON workflows enter one ordinal identity catalog and one semantic binding proof: legal imported bindings lower, illegal seams emit the same stable AGWF041 class, and every C#/C#, C#/JSON, or JSON/JSON duplicate emits AGWF039 before source-hint collision. |
| **Scope** | `WorkflowIncrementalGenerator` aggregate pipeline, `WireToModelBridge`, workflow catalog uniqueness, binding analyzer, and generated-source emission ordering. |
| **Consequence** | Imported workflows bypass proof, duplicate definitions crash generation, or equivalent contracts receive inconsistent results depending on authoring front end. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | `ImportedWorkflowBindingProofTests` through real `AdditionalText` and the real generator, with legal/illegal and all three duplicate combinations, exact exclusive Errors, deterministic witness, and generated-output compilation supplied by the authoritative harness. |
| **Why not cheaper** | Rung 1 only keeps wire records generated. Rung 2 checks model shapes. Rung 3 can check catalog key construction but not end-to-end parse/merge/proof/diagnostic precedence. |
| **Failure signal** | Consumer build failure, generator crash, or absent diagnostic; the exact signal depends on where the front ends diverge and there is no runtime reconciliation. |
| **Rollback** | Disable imported workflow binding or reject mixed catalogs until both paths share the aggregate proof. Retaining JSON parsing without proof does not reverse the semantic exposure. |
| **Lenses** | False-green shapes: bypassed path, unrelated error, source-hint preemption. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. The 11-test suite uses real `AdditionalText`, requires saga emission for the legal case, one exclusive AGWF041 with repeated full diagnostic equality for the illegal case, and one exclusive AGWF039 for each duplicate combination. Its runner uses the shared closed harness, which validates authored input, driver/generator diagnostics, and updated compilation. The suite passed in the complete local portfolio. Protected execution and review remain pending.

**Open questions:**

- None.

### Investigation Log

#### Are the imported-workflow claims proved through the aggregate production component?

- Read: all test methods and the runner in `ImportedWorkflowBindingProofTests`, the aggregate generator ordering described in the survey, and the duplicate-emission production change.
- Found: legal and illegal imported bindings, deterministic counterexample, exact single Error counts, all duplicate front-end pairings, and updated-compilation validation use the real generator and shared closed harness. Duplicate saga emission is suppressed so AGWF039 can win.
- Not found: protected execution or completed review bound to `98fabb4`.
- Conclusion: the semantic subject and negative controls are locally supported; formal status remains `Indeterminate` pending protected binding.
