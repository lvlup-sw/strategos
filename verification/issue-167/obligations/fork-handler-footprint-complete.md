# fork-handler-footprint-complete — Fork handler footprint is complete

| | |
|---|---|
| **Claim** | Fork noninterference includes the reads and writes of every occurrence that can execute before the join on a supported path, including transitive low-confidence handler chains. |
| **Scope** | `BuildForkFootprint`, `EnumerateForkPathOccurrences`, supported fork-path diversion topology, and AGWF041 conflict reporting. |
| **Consequence** | Concurrent paths can race on a property/link/relation/external/event resource even though build-time analysis reports the fork safe. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | Real-generator write/write, both write/read directions, and disjoint controls over nested handler chains and each supported resource kind; mutation-removing handler traversal must make at least one negative fixture fail to detect the race. |
| **Why not cheaper** | Rung 1 cannot derive reachable occurrences. Rung 2 does not encode concurrency or resources. Rung 3 can check topology closure, but read/write sets and their semantic intersection require the analyzer component under representative contracts. |
| **Failure signal** | Usually nothing deterministic; downstream state corruption or lost updates may appear later. AGWF041 is preventive and absent when the footprint is incomplete. |
| **Rollback** | Serialize affected fork paths, reject the diversion shape for bound workflows, or revert the handler-enabled fork support. Adding locks outside the generated workflow does not restore the static contract. |
| **Lenses** | False-green shapes: recurrence, omitted subject, mutation sensitivity. |

**Confidence:** High for low-confidence handlers; Medium for the phrase “every supported diversion.”

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. Production breadth-first traversal follows transitive `OnLowConfidenceHandlerChain` steps and includes the root failure diversion because a path worker can publish that trigger while siblings remain active. Stable write/write, write/read, disjoint-frame, transitive-confidence, and root-failure conflict cases passed in the immutable local portfolio. Protected execution and review remain pending.

**Open questions:**

- Are low-confidence handlers the only diversion accepted inside a fork path before join? `(partial: production traversal names confidence chains plus the concurrent root-failure diversion; topology-closure tests reject other handler shapes)`. If another diversion becomes accepted, it would refute the claim until its occurrence enumerator is included.

### Investigation Log

#### Does footprint construction observe nested handler actions, and can the tests fail without it?

- Read: `BuildForkFootprint`, `EnumerateForkPathOccurrences`, the three confidence-footprint tests, topology-closure tests summarized in Stage 1, and the wildcard recurrence record.
- Found: production queues each confidence handler step transitively. Negative tests require same-resource conflicts and compare one repeated diagnostic exactly; the disjoint test controls over-rejection.
- Found later: the complete local topology class passed at exact-current `98fabb4`; the accepted grammar and footprint cases are covered by the closure suite.
- Not found: protected execution or completed review bound to `98fabb4`, or a single machine-derived enumeration of every accepted pre-join diversion kind.
- Conclusion: the specific regression is locally supported; evidence remains `Indeterminate` until final protected binding.
