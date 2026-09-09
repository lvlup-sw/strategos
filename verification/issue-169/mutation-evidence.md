---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: diff
revision: 42b4ed741c1f406a2de2fdcdaf602ca53dd91dab
base_revision: 0ac93e916849cceada616a0e15dd7e6c83b34af1
target_ref: codex/169-derived-compensation
cost_setting: high
scope_rule: destructive mutation probes for the highest-consequence proof and durable-rollback controls in the reverse dependency closure
updated: 2026-09-07
skipped: none
external_references: []
---

# Mutation evidence

> Historical evidence only. This record remains intentionally bound to precursor revision
> `42b4ed741c1f406a2de2fdcdaf602ca53dd91dab` and tree
> `2d0f3027308580376819e7b56649592cd0a784bc`. It was not rebound to final subject
> `263cc5720818b13268214c5df84d7575fd74a6d7`/tree
> `24be1d97dfaedd88bebc7f33dda831d9b36b1ba1`, because those mutants were not rerun there. The
> intervening same-tree `cdaa73a` test execution did not include these mutation probes either.

All probes ran locally on Pop!_OS 24.04 LTS, `linux-x64`, .NET SDK/runtime 10.0.6,
TUnit 1.2.11.0, and Microsoft Testing Platform 2.0.2. Each probe began from product
revision `42b4ed741c1f406a2de2fdcdaf602ca53dd91dab` (tree
`2d0f3027308580376819e7b56649592cd0a784bc`), changed exactly one production
condition with `apply_patch`, ran the named test, restored the inverse patch, and
required `git diff --exit-code -- src` to return zero.

These are local kill results, so they establish that the named test artifacts detect
the injected faults. They do not by themselves establish protected-path execution;
that binding remains **Indeterminate** until the final PR revision receives required CI.

## Bidirectional inverse equivalence

- Mutation: remove the authored-to-derived implication from
  `ActionCalculus.AddEquivalenceFailures`, leaving only derived-to-authored implication.
- Command: `dotnet test --project src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj --configuration Release --no-restore -- --no-progress --treenode-filter "/*/*/ActionInverseTests/*"`
- Expected kill: the suite must reject a one-way approximation presented as an exact inverse.
- Result: **Pass (killed)**. Exit 2; 30 tests ran, 29 succeeded, and
  `AuthoredGuaranteeMustRestoreTheForwardRequirement` failed because it expected both
  implication failures and observed only one.
- Self-test signal: a surviving mutant would exit zero; an infrastructure failure is
  separately recognizable from the assertion failure above.

## Reverse completed-prefix order

- Mutation: replace the top-level pending rollback ordering with ascending journal
  sequence instead of `OrderByDescending(entry => entry.Sequence)`.
- Command: `dotnet test --project src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj --configuration Release --no-restore -- --no-progress --treenode-filter "/*/*/DerivedCompensationRuntimeTests/*"`
- Expected kill: the first inverse for an `A -> B -> C(fail)` prefix must be `UndoB`, not
  `UndoA`.
- Result: **Pass (killed)**. Exit 2; 56 tests ran, 48 succeeded, and 8 failed. The
  discriminating failure observed `ExecuteUndoAStepWorkerCommand` where
  `ExecuteUndoBStepWorkerCommand` was required; timeout, stale-start, validation,
  reducer-failure, fork, and terminality scenarios also rejected the bad order.
- Self-test signal: a surviving mutant would exit zero; each observed failure was a
  product assertion or missing expected inverse, not a skipped check.

## Stable occurrence identity

- Mutation: make the completed-execution occurrence comparison compare the persisted
  occurrence key to itself, so a reused execution ID could authorize a different stable
  occurrence.
- Command: `dotnet test --project src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj --configuration Release --no-restore -- --no-progress --treenode-filter "/*/*/DerivedCompensationRuntimeTests/Execute_GeneratedSaga_CompletedExecutionIdRejectsDifferentStableOccurrence"`
- Expected kill: a completion carrying the wrong occurrence key must fail closed.
- Result: **Pass (killed)**. Exit 2; the single test failed because the mutated saga
  remained in `BStep` instead of entering `Failed`.
- Self-test signal: the exact unauthorized transition is the assertion subject; no
  substring or whole-file proxy is involved.

## Event-sourced typed-compensation exclusion

- Mutation: make the EventSourced predicate in `WorkflowBindingProofAnalyzer` impossible,
  disabling the AGWF045 restriction without removing other proof analysis.
- Command: `dotnet test --project src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj --configuration Release --no-restore -- --no-progress --treenode-filter "/*/*/WorkflowBindingProofAnalyzerTests/TypedCompensation_WithEventSourcedNoOpFold_ReportsAgwf045"`
- Expected kill: a typed compensation program over EventSourced persistence must receive
  exactly one AGWF045 diagnostic.
- Result: **Pass (killed)**. Exit 2; the single test found zero binding diagnostics where
  one was required.
- Self-test signal: removing the restriction makes the diagnostic absence observable;
  analyzer crashes and compilation failures have distinct result text.

## Restoration binding

- Product-source restoration: **Pass** — `git diff --exit-code -- src` returned zero after
  all four inverse patches.
- Restored-subject positive runs: **Pass** — rebuilt from restored source at the bound
  revision. `ActionInverseTests` passed 30/30, `AONT216CompensationTests` passed
  10/10, and `DerivedCompensationRuntimeTests` passed 56/56. The AONT216 build emitted
  NU1900 because vulnerability metadata was unreachable; test execution itself completed,
  while vulnerability-feed freshness remains Indeterminate.
- Protected mutation execution: **Indeterminate** — these manual mutations are not a
  required CI job. The committed regression fixtures remain the durable guard; the manual
  kill record proves their present sensitivity at the bound product revision.
