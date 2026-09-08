# inverse-message-role-is-isolated-from-forward-flow

## Why this obligation exists

The same CLR step type may appear in normal, `OnFailure`, and inverse roles. #169 emits distinct
inverse command/result/failure/timeout types and role-aware identities so phase/type collisions do not
route inverse output through forward successor logic. Inverse failure must not recursively compensate.
The rebase-activated completion-stem ratchet additionally requires every emitted completion type to
derive from the shared naming authority rather than a free-hand suffix.

## Failure scenario and boundary

An inverse completion triggers the forward step-completed handler and dispatches a successor, or an
inverse failure reaches the ordinary compensation trigger and creates an infinite at-least-once loop.

## Assigned proof

- Rung: R4, contract and component tests.
- Artifacts: `FailureHandlerRoleIdentityTests`, generated handler registration tests, and
  `DerivedCompensationRuntimeTests.Emit_InverseTypeAlsoUsedByOnFailure_RoutesInverseErrorsWithoutRecursion`
  (`DerivedCompensationRuntimeTests.cs:101`) plus malformed inverse routing at `:87` and
  `CompletedStemSpelling_EveryEmitterFile_CitesSanctionedSourceOrIsAllowlisted`.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed all 1,915 generator
  cases after Events, Worker, and Saga emitters adopted `NamingHelper.GetCompletedEventName`;
  final-revision/protected compiled/invoked collision binding remains absent.

## Refutation attempts for Stage 3

1. Remove role from handler identity.
2. Reuse forward result type for inverse completion.
3. Route inverse failure through the generic failure trigger.
4. Spell a rollback completion type free-hand in one emitter so publisher and handler can diverge.

## Open questions

None.

## Investigation Log

No unresolved question. This protects framework routing; consumer external effects remain outside
the claim.
