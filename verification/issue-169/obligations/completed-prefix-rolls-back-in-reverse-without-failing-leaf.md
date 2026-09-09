# completed-prefix-rolls-back-in-reverse-without-failing-leaf

## Why this obligation exists

The principal #169 acceptance claim is computed completed-prefix rollback. Public sequence derivation
starts at `ActionCalculus.cs:646`; generated concrete selection is emitted by
`SagaCompensationComponentEmitter` at `:1634`, with descending journal sequence at `:1715`.
Pre-completion failure consumes a dispatch claim and has no completion journal entry for the failing
leaf.

## Failure scenario and boundary

A/B complete and C fails. Ascending selection executes UndoA before UndoB; journaling C before a
successful completion dispatches UndoC for work never done. Either can violate external invariants.

## Assigned proof

- Rung: R5, production-path integration.
- R5 carrier: `CompensationBehaviorTests.Saga_TypedCompensation_DerivesAndExecutesReversedCompletedPrefix`
  (`src/Strategos.Generators.Behavioral.Tests/CompensationBehaviorTests.cs:102`) through the real
  `CompensationHostFixture` and `DerivedCompensationWorkflow`.
- R4 backstop: `Execute_GeneratedSaga_CFailureRollsBackCompletedPrefixInReverse`
  (`DerivedCompensationRuntimeTests.cs:585`).
- Current disposition: Unproven. `mutation-evidence.md` records a local historical precursor
  `42b4ed7` kill of ascending selection: 8 of 56 focused cases failed, including UndoA where UndoB
  was required. Same-tree pre-rebase revision `cdaa73a` passed 94/94 through the real host;
  final-revision mutation/execution and protected R5 binding remain absent.

## Refutation attempts for Stage 3

1. Change descending pending selection to ascending; both R4/R5 traces must fail.
2. Add a journal entry before forward completion validation; the no-UndoC fixture must fail.
3. Reuse authored plan order rather than journal order; loop/fork occurrence cases must discriminate.

## Open questions

- Does protected CI provision PostgreSQL and report absent infrastructure as Indeterminate rather
  than a skipped pass?

## Investigation Log

### Is there a real shipped-composition fixture?

- Read: behavioral workflow, host fixture, and test.
- Found: concrete A/B/C and UndoB/UndoA bodies, Wolverine host, Marten, PostgreSQL, and trace assertion.
- Found after rebase: same-tree pre-rebase `cdaa73a`/tree `24be1d97` local behavioral execution
  passed 94/94 through rootless Podman and left no residual containers.
- Not found: protected exact-SHA result.
- Conclusion: local proof artifact and execution exist; obligation remains Unproven until protected
  binding.
