# fork-join-conjunction-proof-nonvacuous — Fork join conjunction proof is nonvacuous

| | |
|---|---|
| **Claim** | At every supported fork join, #167 conjoins every path-tail effective guarantee and proves that conjunction implies the join requirement; omitting any tail or bypassing the check must produce AGWF041 in a refuted fixture. |
| **Scope** | `WorkflowBindingProofAnalyzer.ProveForkJoins`, fork-tail edge suppression in ordinary seam checking, and the real-generator topology matrix. |
| **Consequence** | The analyzer certifies a workflow whose join assumes a fact no completed parallel path establishes, so runtime reaches an action outside its precondition. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Paired legal/refuted real-generator fixtures where the join requires facts contributed jointly by distinct tails; each single-tail removal or weakened guarantee must produce one stable AGWF041 witness. Mutation bypassing `ProveForkJoins` or dropping a tail must be killed. |
| **Why not cheaper** | Rung 1 cannot derive semantic topology tests. Rung 2 only checks the contracts' C# shapes. Rung 3 can count paths/edges but cannot establish implication over effective guarantees. |
| **Failure signal** | No dedicated runtime signal; the first observable failure is an action dispatched in an invalid state, if that action checks its own precondition. A missing compile diagnostic is otherwise silent. |
| **Rollback** | Reject bound workflows containing forks, or serialize/restructure the fork until joint reasoning is proven. Keeping only the legal positive is not a rollback. |
| **Lenses** | False-green shapes: positive-only assertion, vacuity, cannot-fail. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. Production explicitly conjoins every resolved path tail's `EffectiveGuarantee` and checks implication. `DisjointFork_WithJointGuarantee_IsProved` covers the positive; `DisjointFork_WithMissingTailGuarantee_ReportsStableAgwf041` weakens one tail and requires the fork-specific stable AGWF041 witness. Bypassing `ProveForkJoins` or dropping that tail makes the negative fail. Protected execution and review remain pending.

**Open questions:**

- None.

### Investigation Log

#### Does a negative fixture require the fork-specific conjunction path to run?

- Read: `ProveGraphSeams`, `ForkTailEdges`, `ProveForkJoins`, and every test in `WorkflowBindingTopologySemanticsTests`.
- Found: ordinary tail-to-join seam checks are skipped, making `ProveForkJoins` the semantic authority. The paired positive and weakened-tail negative exercise that path; the negative passed in the exact-current `98fabb4` portfolio.
- Not found: protected execution or completed review bound to `98fabb4`.
- Conclusion: the nonvacuous proof shape is locally supported, with formal status `Indeterminate` pending protected binding.
