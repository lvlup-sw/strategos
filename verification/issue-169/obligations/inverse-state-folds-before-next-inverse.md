# inverse-state-folds-before-next-inverse

## Why this obligation exists

The derived inverse requirement for UndoA may depend on the state produced by UndoB. The generated
inverse completion path applies `UpdatedState` through the configured reducer before marking the
journal entry RolledBack and before selecting the next pending entry. The emitted transition is near
`SagaCompensationComponentEmitter.cs:1877` through `:1903`.

## Failure scenario and boundary

UndoB succeeds externally and reports stage 1, but UndoA starts with stale stage 2. It either fails a
precondition or performs an invalid effect. Running the failure handler first also exposes partial
restoration as final failure state.

## Assigned proof

- Rung: R5, production-path integration.
- R4 backstop: `DerivedCompensationRuntimeTests.Emit_RollbackCompletion_AppliesReducerOrEventStreamBeforeNextInverse`
  (`DerivedCompensationRuntimeTests.cs:290`) and reducer-before-failure-handler execution at `:1247`.
- R5 carrier: real A/B/C workflow whose inverse steps enforce stage order.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed the behavioral host
  suite 94/94 locally, including the A/B/C carrier; final-revision/protected execution and a
  final-revision ordering kill are absent.

## Refutation attempts for Stage 3

1. Move reducer application after next dispatch.
2. Mark RolledBack before reducer success.
3. Invoke the failure handler before the pending inverse list is empty.

## Open questions

None beyond protected binding.

## Investigation Log

The real behavioral fixture crosses step bodies and state folding for one linear path. It passed in
the same-tree pre-rebase 94-test rootless-Podman run at `cdaa73a` and left no residual containers,
but it cannot prove arbitrary consumer reducers or external effects and is neither final-revision nor
protected-path evidence.
