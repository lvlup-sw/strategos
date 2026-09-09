# durable-completion-transition-is-authorized

## Why this obligation exists

COV-03 found that pre-completion dispatch authority, topology membership, and journal integrity did
not state the whole completion-to-post-completion-failure transition.

## Failure scenario and boundary

A completion consumes authority but crashes after reduction and before journaling/routing, or a
reducer/successor failure creates a duplicate, altered-kind, wrong-scope, or unauthenticated rollback
trigger. Completed work is skipped or rollback starts twice.

## Assigned proof

- Rung: R5, supported Wolverine/Marten/PostgreSQL path with fault injection and reload.
- Existing backstops: generated-runtime exact claim, reducer order, journal, and failure-trigger
  cases.
- Missing artifacts: machine-readable post-completion producer matrix and real-host crash/reload
  transition fixture.
- Current disposition: Unproven.

## Investigation Log

### Which host transaction makes the transition durable?

- Read: generated ordering, Stage 1 production trace, COV-03, and the forward-authority evidence.
- Found: source ordering and in-memory capability validation.
- Not found: a bound Wolverine/Marten transactional-mode guarantee or crash-window fixture.
- Conclusion: `(partial: emitted order exists; host crash semantics remain unexhibited)`.
