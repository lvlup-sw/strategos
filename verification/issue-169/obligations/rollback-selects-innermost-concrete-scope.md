# rollback-selects-innermost-concrete-scope

## Why this obligation exists

Issue #169 promises nested isolation. `CompensationTopology` assigns structural parentage and
concrete loop/path keys; `BeginCompensationScope` filters journal entries for the selected concrete
scope and should preserve enclosing history after inner rollback.

## Failure scenario and boundary

An inner branch/loop failure unwinds root work, or successful inner rollback marks/deletes outer
history so a later enclosing failure cannot undo it. Reused phase/type names make heuristic ownership
especially unsafe.

## Assigned proof

- Rung: R4, compiled generated component semantics.
- R4 backstops: `Execute_GeneratedSaga_InnerBranchFailureDoesNotUnwindOuterScope`
  (`DerivedCompensationRuntimeTests.cs:684`), approval preservation at `:767`, nested loop selection
  at `:443`, deep-name topology at `:466`.
- Proposed R4 addition: later enclosing failure after successful inner rollback. Persisted reload is
  outside this narrowed claim and requires a separate R5 obligation if desired.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` ran the R4 cases locally in
  the 1,915-test generator suite, but the later-enclosing-failure case is Missing and
  final-revision/protected binding is absent.

## Refutation attempts for Stage 3

1. Select root instead of deepest matching scope.
2. Match loop scope template without concrete iteration key.
3. Remove/purge enclosing history after inner completion.

## Open questions

- Is R4 the intended v2.13 assurance ceiling, or must a real-host nested fixture block merge?
  `(needs human input)`

## Investigation Log

### Is there a nested R5 fixture?

- Read: Stage 1 proof/production inventories and behavioral test workflows.
- Found: nested dynamic generated-handler execution; real host covers only linear A/B/C.
- Not found: nested PostgreSQL workflow.
- Conclusion: PF-6 resolved the current claim at R4. No R5 host fixture is silently required by this
  file; a future persisted-reload claim must be inventoried explicitly.

## Stage 3 resolution

The active question is removed from the ledger. The historical investigation remains above as an
audit trail. Wrong trace/scope selection or lost outer history is Fail; component nonexecution is
Indeterminate.
