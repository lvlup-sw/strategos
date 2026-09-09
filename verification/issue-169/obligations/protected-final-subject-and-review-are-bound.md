# protected-final-subject-and-review-are-bound

## Why this obligation exists

Evidence becomes stale when the PR head changes after local execution or review. The requested
delivery permits zero or one CodeRabbit review round and requires final authorization before merge.
CI for a stacked/non-main base may not run the same workflow set.

## Failure scenario and boundary

A green result belongs to the previous tree, a review approval predates a fix, an optional/skipped
check appears satisfied, or package digests do not match the reviewed source.

## Assigned proof

- Rung: R3, deterministic hosted evidence binding.
- Present local evidence: final 40-character source head/tree plus candidate nupkg digests and exact
  local package-consumer results. Missing evidence: hosted PR head/check URLs and statuses,
  final-revision test execution, post-fix hosted review disposition, merge subject, and published
  package provenance. Zero CodeRabbit rounds is permitted; more than one is rejected; any result
  must bind final head.
- Current disposition: Indeterminate. The exact local SHA/tree and local package digests are frozen,
  but the PR, hosted review, required-check, merge, and publication records are absent for this
  subject.

## Refutation attempts for Stage 3

1. Compare every result/review SHA to final head.
2. Treat missing, skipped, timeout, or infrastructure exit 3 as Indeterminate.
3. Modify product code after review and require rebind/rerun.

## Open questions

- Will hosted review or CI remediation change product SHA from
  `263cc5720818b13268214c5df84d7575fd74a6d7`?

## Investigation Log

No hosted PR subject existed at Stage 2. The branch was subsequently rebased onto merged main and the
dossier was bound to local SHA `263cc57`/tree `24be1d97`; exact-final package verification passed
locally. Hosted evidence is still a legitimate Indeterminate delivery state, not a product failure
and not a success.

## Stage 3 disposition

PF-5 split human finding disposition and merge authorization into
`final-review-disposition-and-merge-authorization-are-current` at R6. A CodeRabbit round is optional;
if used, it must bind the final subject.
