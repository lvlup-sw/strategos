# downstream-adoption-is-bound-before-claimed

## Why this obligation exists

#153 requires published-contract consumer coordination. Basileus #495 and Exarchos #1895 name
Contracts 0.12, `inverseAction`, exact identity, diagnostic additions, legacy omission, and retained
reconciliation state. They are external mutable authorities.

Stage 3 narrowed this file to the factual proposition that exact downstream revisions adopted the
bytes. `release-claims-require-bound-adoption-evidence` separately owns the policy that forbids an
unsupported adoption claim.

## Failure scenario and boundary

Strategos calls adoption complete because issues exist, while a consumer's closed diagnostic enum
rejects AGWF045, its DTO drops inverse identity, or its runtime deletes an OutcomeUnknown saga.

## Assigned proof

- Rung: R5, production-path integration in each consumer repository.
- Required evidence: exact consumer commit, package version/digest, lock/restore evidence, protected
  schema/API/behavior fixtures, and release/deployment disposition linked on each issue.
- Current disposition: Indeterminate. Issue text is coordination evidence only; no implementation
  revision/build is bound.

## Refutation attempts for Stage 3

1. Search issues for an exact commit/build artifact rather than inferring from status.
2. Test older/omitted inverse documents and new diagnostic tokens in each consumer.
3. Bind package digest, not only semantic version.

## Open questions

- Which exact Basileus and Exarchos commits implement the adoption? `(needs human input)`

## Investigation Log

### Are the adoption issues implementation evidence?

- Read: `basileus-adoption.md`, `exarchos-adoption.md`, and Stage 1 authority survey.
- Found: complete coordination requirements and issue links.
- Not found: consumer commit, package lock/digest, build, fixture, release.
- Conclusion: Indeterminate; do not report an adoption percentage.
