# public-api-gate-covers-binding-surface — Public API gate covers the #167 binding surface

| | |
|---|---|
| **Claim** | Every intentionally public #167 authoring/value/definition member is included in an exact, build-breaking API baseline, and removing the new unshipped member or its analyzer scope makes the gate fail. |
| **Scope** | Ten changed builder interfaces, `WorkflowActionReference`, `StepDefinition.Action`, `src/Strategos/.editorconfig`, PublicAPI ledgers, and the builder API CI job. |
| **Consequence** | A later refactor can remove or reshape a cross-product contract while CI remains green; Exarchos/Basileus or user source then fails to compile. |
| **Proof rung** | Rung 3 — deterministic structural analysis. |
| **Proof artifact** | PublicApiAnalyzer over the exact reviewed file/type set plus structural allowlist/ledger closure and mutation probes that remove one new #167 unshipped member, drop each scope section, and alter `StepDefinition.Action`. |
| **Why not cheaper** | Rung 1 does not generate these hand-authored APIs. Ordinary rung-2 compilation accepts intentional signature drift; the added analyzer/config/baseline relationship is a structural policy over two representations and therefore belongs at rung 3. |
| **Failure signal** | Downstream compile failure. For the ten interfaces, `WorkflowActionReference`, and `StepDefinition`, CI reports PublicApiAnalyzer drift; the exact-scope and shape tests also fail when the reviewed file/type set changes. |
| **Rollback** | Restore the prior signature or add a compatibility overload/property before release. Updating the baseline to accept accidental drift is not a rollback. |
| **Lenses** | False-green shapes: partial subject, mutation cannot fail on new path, convention-only scope. |

**Confidence:** High.

**Current proof posture:** Indeterminate. Exact reflected lists match the ten-interface and two-definition `.editorconfig` scope; both `WorkflowActionReference` and the complete `StepDefinition` surface, including `Action`, are in the PublicApiAnalyzer baselines. The exact-current `98fabb4` solution build/tests exercised the scope, and the standalone analyzer build and shared shipped-member mutation also passed on that subject. Protected execution and review remain pending.

**Open questions:**

- None. `StepDefinition` is intentionally included as the second exact definition file.

### Investigation Log

#### Can the public API guard fail on each newly protected #167 path?

- Read: `src/Strategos/.editorconfig`, `PublicApi.globalconfig`, both PublicAPI ledgers as summarized by Stage 1, `BuilderApiBaselineTests`, `GateFailClosedTests`, `check-builder-api-stability.sh`, and the CI job.
- Found: exact scope closure covers ten builder interfaces plus `WorkflowActionReference` and `StepDefinition`; shipped/unshipped baseline closure includes every reviewed top-level type and public member. The real RS0016 mutation establishes analyzer fail-closed behavior, and `StepDefinition.Action` has an exact init-only type-shape test.
- Not found: protected execution or completed review bound to `98fabb4`. A separate mutation for every new line would duplicate the same analyzer mechanism and is not required by the cheapest-sound proof assignment.
- Conclusion: the API mechanism and new scope are locally supported; active status remains `Indeterminate` pending protected binding.
