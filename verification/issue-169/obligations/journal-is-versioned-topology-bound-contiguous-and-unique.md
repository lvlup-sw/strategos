# journal-is-versioned-topology-bound-contiguous-and-unique

## Why this obligation exists

The generated durable protocol trusts the journal to represent completed work. The emitter creates
schema/version, high-water, sequence, topology, execution, rollback, pending, and consumed-claim
checks; `SagaCompensationComponentEmitter.cs:1360` onward validates journal uniqueness/continuity
before selection at `:1634`.

## Failure scenario and boundary

An old or corrupted persisted document contains a sequence gap, duplicate rollback ID, wrong scope,
or contradictory claim. Permissive reload converts it into an executable prefix, duplicating or
skipping external inverse work and destroying audit evidence.

## Assigned proof

- Rung: R5, production-path integration.
- R4 backstops: topology contradiction at `DerivedCompensationRuntimeTests.cs:847`/`:864`,
  noncanonical scope at `:948`, corrupt failure claims at `:1701`, null/corrupt dispatch at `:2295`.
- Missing R5 artifact: write malformed serialized saga through Marten and reload via real handlers.
- Current disposition: Unproven.

## Refutation attempts for Stage 3

1. Remove schema-version rejection.
2. Accept sequence gaps or duplicate execution/rollback IDs.
3. Persist malformed JSON/document state and confirm reload dispatches no inverse.

## Open questions

- Which malformed vectors are injected after real serialization/reload? `(partial: equivalent
  in-memory vectors exist; storage conversion/default behavior remains unread)`

## Investigation Log

### Does in-memory mutation cover persistence corruption?

- Read: generated validators, in-memory adversarial suite, behavioral host.
- Found: broad direct-object corruption vectors and one normal host workflow.
- Not found: corrupt persisted-document reload fixture.
- Conclusion: component semantics are strong; storage-bound claim remains Unproven at R5.

## Stage 3 disposition

PF-7 records in-memory corruption cases as existing backstops and the persisted malformed-document
reload fixture as Missing. No prospective fixture is described as already present.
