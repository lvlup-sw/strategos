# identity-inverse-is-empty-frame-only

## Why this obligation exists

Issue #169 needs an empty rollback identity without making an ordinary `True`/`True` action identity.
`ActionCalculus` returns identity only for a valid empty canonical frame. An explicit but broken
`CompensatedBy` name remains an error; a non-empty frame without authored executable code is Missing.

## Failure scenario and boundary

Treating every absent inverse as identity silently drops writes. Treating a broken named inverse as
identity hides an ontology typo. Dispatching CLR code for the structural identity creates an effect
where the calculus promised none.

## Assigned proof

- Rung: R4, contract and component tests.
- Artifacts: `ActionInverseTests.EmptyFrameDerivesExecutableIdentityUnlessAnExplicitInverseIsBroken`
  (`ActionInverseTests.cs:291`), identity leaf at `:312`, non-empty missing at `:327`, and
  `DerivedCompensationRuntimeTests.Emit_EmptyFrameIdentityLeaf_DoesNotBecomeMissingInverseFailure`
  (`DerivedCompensationRuntimeTests.cs:411`).
- Current disposition: Unproven until exact protected execution.

## Refutation attempts for Stage 3

1. Remove the frame-empty condition.
2. Ignore an unresolved explicit inverse name.
3. Emit an inverse worker for an identity journal entry; component execution must fail.

## Open questions

None.

## Investigation Log

No unresolved question. Identity is a distinct structural plan node tied to an `ActionSubject`, not
an authored action with vacuous predicates.
