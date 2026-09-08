# typed-compensation-is-single-and-mandatory-per-occurrence

## Why this obligation exists

Before #169, a second compensation call could silently replace the first. `StepConfigurationBuilder`
now tracks declaration state; typed `CompensationConfiguration` records CLR type and inverse action,
and `WorkflowBindingProofAnalyzer.cs:735` rejects `RequiredOnFailure=false` for typed programs.

## Failure scenario and boundary

The first declaration supplies the action proved by AGWF044, while a later declaration supplies the
CLR type executed in production. Alternatively, a typed program compiles but opts out at the exact
failure point where its safety claim matters.

## Assigned proof

- Rung: R3, deterministic structural analysis. R1 construction covers fluent duplicates only; R2
  types still permit imported opt-out and cross-occurrence program mixtures.
- Artifacts: builder tests at `StepConfigurationBuilderTests.cs:168`, `:187`, and `:201`;
  configuration immutability/default tests at `CompensationConfigurationTests.cs:47`, `:96`, and
  `:164`; source/import analyzer cases for mandatory typed rollback; and the issue-167
  `StepConfigParity_OccurrenceMetadata_MatchesReflectedSurfaceAndNamesRunningProofs` ratchet, which now
  registers typed `Compensate` with its extraction and wire proofs.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed the 1,915-test
  generator suite after the occurrence-metadata policy correction; final-revision and protected
  cross-front-end binding remain absent.

## Refutation attempts for Stage 3

1. Restore last-write-wins and require duplicate test to fail.
2. Allow typed `RequiredOnFailure=false` and require AGWF045 test to fail.
3. Make `WithTimeout` drop inverse identity and require immutability/round-trip tests to fail.

## Open questions

None.

## Investigation Log

No unresolved question. The source-breaking duplicate-declaration exception is recorded in migration
and public API evidence.

## Stage 3 disposition

PF-9 supplies the missing R2 exclusion; no proof/state upgrade follows.
