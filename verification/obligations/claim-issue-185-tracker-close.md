# claim-issue-185-tracker-close — Does merging this branch close issue 185's "still open" list?

Lens: 6 Claim Derivation
Disposition: open question
Inventory claims: 87, 89, 91
Confidence: n/a — tracker-state vs delivery, no PR

## Open question

Issue 185 is still OPEN. Comment 2 (rsalus, dated in the inventory as 2026-08-28) calls it "the residue tracker, not a completed work item" (claim 87) and lists as still open by design: AGWF035 under-reach (claim 89) and paved-road items #147, #181, #163, #115, #156, #176, #177 (claim 91). This branch claims to implement several of those numbers. No PR exists for `cursor/c801a047` (intent-and-claims: `gh pr list` returned `[]`).

**Stakes.** If merge is supposed to close 185 or any of those numbers, the tracker comment is stale and will mislead the next residue pass. If 185 stays the residue tracker, claims 87/89/91 are historical comment-time state and must not be read as "this branch left them unimplemented." Intent-and-claims already recorded that reading.

**Competing explanation.** Tracker text describes comment-time state. CHANGELOG Residue describes this branch's delivery. Both can be true at different times. Nothing in-repo binds merge of this branch to issue-state transitions.

## What would settle it

A PR body or maintainer decision listing which issue numbers this merge closes. Out of wave: Option B (claim 90), #147 (claim 3), #133/#174 (claims 4, 92). Those stay open regardless.

This file is not an obligation.
