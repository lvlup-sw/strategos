# typed-compensation-wire-roundtrips-and-import-fails-closed

## Why this obligation exists

Typed identity crosses TypeSpec, generated C#/two schemas, hand DTO, `MinimalJsonReader`,
`WireToModelBridge`, projection, generator IR, topology, and journal. Historical precursor
`42b4ed7` (rebased as `0a34ee9`) added `@minLength(1)` and `@pattern(".*\\S.*")` so the schema's
`compensationStepType` acceptance matches the reader's nonblank rule. Final-subject remediation also
rejects a malformed, non-string, zero, or negative imported compensation timeout rather than
silently treating it as omitted.

## Failure scenario and boundary

A whitespace moniker passes schema then fails import, an incomplete inverse object becomes legacy,
or projection/import changes one ontology name and proves/dispatches another action.

## Assigned proof

- Rung: R4, contract and component tests. R1 covers generated representations only; R2 still
  represents malformed/unresolved imported states; R3 source inspection cannot establish behavioral
  round-trip agreement across independent reader/resolver/projection roots.
- Artifacts: `ImportFrontEndRobustnessTests.PresentMalformedInverseActionToken_FailsClosed_WithStableDiagnostic`
  (`ImportFrontEndRobustnessTests.cs:316`), moniker matrix at `:356`, round trip at
  `RoundTripIrFidelityTests.cs:337`, compensation-timeout token/range matrix, projection and
  fingerprint tests, schema conformance.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` had clean codegen and passed
  191 Contracts tests plus the 1,915-test generator suite locally; final-revision/protected
  malformed/import-resolution/round-trip/fingerprint binding remains absent.

## Refutation attempts for Stage 3

1. Remove the TypeSpec nonblank pattern; schema test must fail.
2. Treat malformed/present inverse as omitted legacy.
3. Drop one identity field in DTO/bridge/fingerprint/projection.
4. Resolve CLR moniker by a guessed/ambiguous type.
5. Parse a malformed or wrong-kind compensation timeout as `null`, or accept a non-positive value.

## Open questions

None.

## Investigation Log

The Stage 1 F-7 acceptance mismatch is resolved by the TypeSpec constraint and regenerated schemas/C#.
The later timeout review finding is resolved by fail-closed raw token/kind/range validation before
wire lowering. Same-tree pre-rebase codegen/Contracts/generator evidence covers these implementations
but does not answer whether the final-revision/protected matrix binds malformed presence, symbol
resolution, round trip, and fingerprint to the same final subject.

## Stage 3 disposition

PF-9 closes the lower-rung explanation. COV-06 gives timeout propagation its own end-to-end claim
rather than overloading inverse-identity round trip.
