# inverse-step-receives-durable-rollback-identity

## Why this obligation exists

W-2 found that the historical precursor generated command carried `RollbackId`/`IsCompensation`, but
the public step contract exposed only a tracing-described `CorrelationId`. Consumer-owned idempotency
needs a named, stable business identity before user code runs.

## Failure scenario and boundary

An inverse body runs with no explicit rollback identity/role, receives a different ID on redelivery,
or must guess that a tracing string is the durable key. Missing or forged metadata reaches external
effects before validation.

## Assigned proof

- Rung: R4, public context/worker component contract.
- Existing artifact: explicit immutable context metadata, pre-execution rejection of
  absent/incoherent inverse metadata, exact worker mapping, and stable-redelivery cases.
- Current disposition: Unproven. The same-tree pre-rebase generator suite at `cdaa73a` exercises the
  boundary, but no final-revision or protected result binds it.

## Investigation Log

No unresolved question. This supplies the input for consumer idempotency; it does not claim Strategos
implements or proves the consumer's external deduplication. Final-subject `StepContext.IsCompensation`
and `RollbackId` are explicit immutable values; generated inverse workers reject absent, empty,
non-positive-sequence, or role-incoherent metadata before executing consumer code.
