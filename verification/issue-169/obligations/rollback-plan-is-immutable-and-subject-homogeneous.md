# rollback-plan-is-immutable-and-subject-homogeneous

## Why this obligation exists

`ActionRollbackPlan` (`src/Strategos.Ontology/Descriptors/ActionRollbackPlan.cs:128`) is the public
closed syntax tree for Identity, Leaf, Sequence, Parallel, and Scope. Its factories must snapshot
children and derive one validated subject/frame/read footprint, because proof would otherwise be
invalidated by caller mutation or subject mixing after construction.

## Failure scenario and boundary

A caller passes a mutable list, receives a Proven plan, then replaces a child or injects a different
subject before runtime consumes it. A malformed kind/child combination can also make a downstream
switch assume fields that are absent.

## Assigned proof

- Rung: R1, construction and generation.
- Primary authority: private validated construction, immutable-array snapshots, closed enum, and
  subject checks in plan factories.
- Backstop: `ActionInverseTests.RollbackFactoriesSnapshotInputsAndRejectSubjectMixing`
  (`ActionInverseTests.cs:622`) and nested read-footprint snapshot case at line 487.
- Current disposition: Unproven pending protected API/build and mutation evidence.

## Refutation attempts for Stage 3

1. Retain the caller collection rather than snapshotting it.
2. Remove the same-subject check from one composite factory.
3. Permit a kind with an illegal null/non-null child shape.

## Open questions

None.

## Investigation Log

No unresolved question. Reflection can always violate private invariants, but it is outside the
supported public construction boundary and does not weaken the R1 assignment.
