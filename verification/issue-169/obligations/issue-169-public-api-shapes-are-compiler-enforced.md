# issue-169-public-api-shapes-are-compiler-enforced

## Why this obligation exists

PF-3 separated three authorities previously collapsed under R2. This file owns only the shapes the
C# compiler can make unrepresentable; API inventory drift is R3 and compatibility intent is R6.

## Failure scenario and boundary

A caller mutates a returned plan, omits a required typed identity, subclasses a result meant to be
closed, or passes an untyped/ambiguous value through a weakened signature. Runtime checks would be
later and less complete than rejecting the program.

## Assigned proof

- Rung: R2, compiler and type system.
- Existing artifact: sealed/closed public declarations, get-only/immutable collection exposure,
  required non-null annotations, and strongly typed signatures under strict compilation.
- Proposed sensitivity: compile-negative consumers for mutation, null omission, subclassing, and
  signature misuse with warnings as errors.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` passed the Release solution
  build and builder API gate locally, but no final-revision/protected focused negative evidence is
  bound.

## Investigation Log

No unresolved question. Addition/removal/baseline drift remains in
`public-api-authority-covers-issue-169`; product intent remains in
`issue-169-public-api-intent-is-reviewed`.
