# rollback-id-is-stable-distinct-and-injective

## Why this obligation exists

External inverse delivery is at-least-once. `CreateCompensationRollbackId` is emitted at
`SagaCompensationComponentEmitter.cs:303`; journal validation recomputes it at line 438 and also
requires uniqueness. This ID is the consumer's durable deduplication/correlation key.

## Failure scenario and boundary

Redelivery receives a fresh ID and performs a refund twice, or two different forward executions map
to one rollback ID and the second legitimate inverse is suppressed. Equality with the forward ID can
also collide with consumer namespaces.

## Assigned proof

- Rung: R4, contract and component tests.
- Existing artifacts: direct construction argument that successor-with-wrap is one cycle on the
  nonzero 128-bit GUID domain and predecessor-with-wrap is its inverse, hence the result is nonempty,
  fixed-point-free, and injective;
  `DerivedCompensationRuntimeTests.Execute_GeneratedSaga_RollbackIdentityMappingIsStableDistinctAndInjectiveAtBoundary`
  (`DerivedCompensationRuntimeTests.cs:2263`), and journal/redelivery cases. Proposed: protected
  property/metamorphic collision kill.
- Current disposition: Unproven until protected property/mutation evidence is bound.

## Refutation attempts for Stage 3

1. Return `Guid.NewGuid()` on each call.
2. Return the forward execution ID unchanged.
3. Zero/overwrite distinguishing bytes and find a collision.

## Open questions

- No semantic question remains about the pure mapping; the boundary fixture alone is still only a
  sample and protected mutation/property sensitivity remains unbound.

## Investigation Log

### What does the existing evidence establish?

- Read: emitted transform/validator and named boundary test.
- Found: deterministic reversible-looking byte mutation and sampled stable/distinct/injective cases.
- Established by direct construction: successor-with-wrap is one cycle and predecessor-with-wrap is
  its inverse over all nonempty GUID values, proving nonempty, distinct, and injective results.
- Conclusion: keep R4 for the combined persisted/redelivery behavior and name the direct proof plus
  Proposed property kill honestly; do not call the three-value fixture universal proof.

## Stage 3 disposition

PF-10 leaves the obligation active but separates the universal mapping argument from its sampled
boundary fixture. The property/metamorphic sensitivity artifact is Proposed and has not passed.
