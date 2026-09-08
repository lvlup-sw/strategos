# published-packages-bind-approved-subject

## Why this obligation exists

COV-07 found that the recorded closure reaches publication, but active claims stopped at local pack,
PR merge, and downstream adoption. Candidate bytes are not registry delivery.

## Failure scenario and boundary

A product or Contracts tag points at another commit, trusted publication omits/mismatches a package,
or retrieved NuGet provenance/digests do not correspond to the approved subject.

## Assigned proof

- Rung: R5, future production publication path.
- Missing evidence: tag-to-SHA/tree, trusted workflow result, complete published manifest/provenance,
  and post-publish retrieval/digest binding.
- Current disposition: Indeterminate; no release event exists.

## Investigation Log

### Must trusted publication reproduce candidate bytes or rebuild equivalent provenance?

- Read: publish workflows, candidate artifact record, COV-07, and evidence-binding rules.
- Found: separate product/Contracts tag paths and trusted OIDC publication intent.
- Not found: a stated rebuild-versus-byte-identity policy for downloaded packages.
- Conclusion: `(needs human input)` before the future event can be judged.
