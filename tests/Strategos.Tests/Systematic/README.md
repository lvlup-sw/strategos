Run the transition model with:

```sh
dotnet run --project tests/Strategos.Tests -c Release -- --treenode-filter '/*/*/WorkflowEdgesGeneratedTests/*' --minimum-expected-tests 12
```

Each operation replaces the workflow's outgoing edge set. The model compares the current edge list and the prior definition's preserved list. The concrete replay catches an implementation that appends edges rather than replacing them.

Regenerate from `workflow-edges.testgen.json` with `testgen generate_stateful_tests`. Use an unused class and path when replaying. This checks in-memory definitions. The repository's contract and provider suites own wire validation and runtime orchestration.
