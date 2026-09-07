# runtime-generator-refinement-parity — Runtime and generator refinement classifications agree

| | |
|---|---|
| **Claim** | For every shared closed action-refinement vector, the public runtime API and #167 generator classify legality identically and identify the same primary failed obligation. |
| **Scope** | `ActionCalculus.AnalyzeRefinement`, source-linked formula/solver/parser semantics, `WorkflowBindingProofAnalyzer`, and `ActionRefinementProofVectors`. |
| **Consequence** | An action implementation can be accepted at runtime but rejected at build time, or vice versa, making contracts nonportable and diagnostics misleading. |
| **Proof rung** | Rung 4 — shared contract tests. |
| **Proof artifact** | One linked vector corpus executed independently against runtime and real generator, comparing status plus a stable semantic failure token; mutation of either implementation must fail one side. Expand representative domains when #167 orchestration differs from pure refinement. |
| **Why not cheaper** | Rung 1 source-linking reduces kernel drift but does not unify the separate orchestration paths. Rung 2 cannot encode logical refinement. Rung 3 can inventory shared files and vector inclusion but cannot establish behavioral equality. |
| **Failure signal** | Build/runtime disagreement observed by users; no automatic production channel reconciles it. A generator substring mismatch is visible in tests but is weaker than comparing a structured obligation. |
| **Rollback** | Revert the divergent orchestration or route both callers through a single public semantic result. Duplicating expected strings in two suites is not a rollback. |
| **Lenses** | False-green shapes: duplicated oracle, stale parallel fixtures, substring assertion. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. The corpus is physically linked into both test assemblies and covers requirement, guarantee, frame, authority, subject, opaque, and invalid cases. Runtime checks exact status and primary obligation; generator checks an exact diagnostic ID plus a stable message fragment through the shared harness, including generated-output compilation. The fragment remains the semantic join key, but the local parity run passed. Protected execution and review remain pending.

**Open questions:**

- Should the generator expose or test a structured failure category rather than infer the primary obligation from a message substring? The answer determines whether the existing vector proof can become robust without freezing prose.

### Investigation Log

#### Do runtime and generator consume the same vectors through independent implementations?

- Read: `ActionRefinementProofVectors`, its linked `Compile` item in the generator tests, `ActionRefinementProofVectorTests`, and `ActionRefinementProofVectorGeneratorTests`.
- Found: one source corpus and two real production entry points. Generator rejects unrelated generator Errors, exact-counts the expected binding diagnostic, and validates updated compilation through the shared harness.
- Not found: a structured generator failure token comparable to the runtime obligation enum, or mutation evidence for each path.
- Conclusion: this is a sound, locally executed rung-4 design with a remaining substring-based parity seam; formal status stays `Indeterminate` pending protected binding.
