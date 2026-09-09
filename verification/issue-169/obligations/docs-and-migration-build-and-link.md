# docs-and-migration-build-and-link

## Why this obligation exists

PF-4 separated mechanical documentation integrity from semantic honesty. The Starlight toolchain can
prove that required pages participate in the build and links resolve, even though it cannot decide
whether the prose tells the truth.

## Failure scenario and boundary

The issue-169 migration, calculus, API, diagnostics, changelog, or Contracts guidance is omitted from
the content collection, or a new link is broken. Semantic review cannot help a page that never ships.

## Assigned proof

- Rung: R4, documentation artifact build/link test.
- Existing artifact: repository Starlight build. Missing: an explicit link checker.
- Proposed addition: issue-169 required-page/link inventory so silently dropped pages fail.
- Current disposition: Unproven. Same-tree pre-rebase revision `cdaa73a` produced 80 Starlight pages
  locally after shared-file reconciliation; the explicit link inventory remains Proposed and no
  final-revision or protected result is bound.

## Stage 3 disposition

`docs-and-migration-state-exact-boundaries` now owns semantic truth at R6, including W-1 contract-set
wording. This row owns only mechanical inclusion and link integrity.

## Investigation Log

No unresolved question.
