# inverse-subject-frame-and-authority-are-exact

## Why this obligation exists

Rollback identity is not only a predicate pair. `ActionCalculus` compares subject, canonical frame,
and semantic authority before returning Proven. The authority comparison is lattice semantic rather
than string equality; frame equality ignores authored order/duplicates but not resources.

Relevant evidence anchors are `ActionInverseTests.SubjectAndFrameMustMatchExactly`
(`ActionInverseTests.cs:152`), authority alias/stronger/weaker cases at lines 173, 198, and 224, and
canonical-frame handling at line 250. AONT216 equivalents begin at
`AONT216CompensationTests.cs:9`, `:96`, and `:119`.

## Failure scenario and boundary

A wrong subject dispatches compensation for another ontology object. Frame containment instead of
equality permits missing restoration or undeclared mutation. Literal authority comparison rejects
legal aliases; partial-order containment admits excess privilege.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Primary artifacts: exact subject/frame comparisons and two semantic authority implications in the
  calculus and both analyzer paths.
- Current disposition: Unproven until shared vectors and mutation evidence run on a protected exact
  subject.

## Refutation attempts for Stage 3

1. Change frame equality to subset and require a missing/extra resource vector to pass incorrectly.
2. Compare authority names literally and require the alias positive to fail.
3. Compare only one authority direction and require stronger/weaker cases to expose it.

## Open questions

None.

## Investigation Log

No unresolved question. The code and tests distinguish canonical representation from semantic
equality and preserve exact ontology-name identity rather than CLR type identity.
