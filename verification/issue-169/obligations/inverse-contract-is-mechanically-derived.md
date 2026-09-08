# inverse-contract-is-mechanically-derived

## Why this obligation exists

Issue #169 states the inverse laws, while #168 gives `ensures(A)` the sound meaning of the forward
**effective guarantee**, including preserved requirements outside the write frame. The discriminating
implementation is `ActionCalculus.AnalyzeInverse` at
`src/Strategos.Ontology/Descriptors/ActionCalculus.cs:11`: it first obtains one
`ActionContractProof`, then constructs the inverse from that proof. This is stronger than swapping the
authored `Preconditions` and `Ensures` collections.

## Failure scenario and boundary

If construction uses the raw guarantee, a forward requirement on untouched `TenantId` disappears and
the inverse can be accepted without preserving/restoring it. The claim stops at the declared action
contract; it does not establish the CLR inverse body or an external effect.

## Assigned proof

- Rung: R1, construction and generation.
- Primary authority: the single `ActionInverseContract` construction in `ActionCalculus` from
  `ActionContractProof.EffectiveGuarantee`, `HardRequirement`, subject, authority, and canonical frame.
- Sensitivity fixture: `ActionInverseTests.DerivationSwapsEffectiveGuaranteeAndHardRequirement`
  (`src/Strategos.Ontology.Tests/Descriptors/ActionInverseTests.cs:34`) and the preserved-requirement
  case at line 131.
- Current disposition: Unproven until the exact revision receives protected build/test and kill
  evidence. Local presence and local execution are not `Verified`.

## Refutation attempts for Stage 3

1. Replace effective guarantee with explicit `Ensures`; the preserved-requirement fixture must fail.
2. Independently construct subject/frame from the authored inverse; subject/frame mismatch fixtures
   must fail.
3. Return a contract after an invalid/opaque forward proof; closed-result tests must fail.

## Open questions

None.

## Investigation Log

No unresolved question required investigation at Stage 2. The survey's `ensures` ambiguity was
resolved by the #168 proof model and the implementation's explicit effective-guarantee use.
