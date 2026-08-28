### schema-diff-skip-succeeds — Schema-diff job must not succeed when it did not compare

| | |
|---|---|
| **Claim** | The `contracts-schema-diff` job must report a non-success outcome when it did not run the structural diff — no previous `v*` tag, previous tag missing `src/Strategos.Contracts/schemas/json-schema`, or the compare step skipped. Skip and pass must not be the same result. |
| **Scope** | S2 surrounding gate. `.github/workflows/contracts-schema-diff.yml:37-62`. This wave adds `AgwfEntryDuplicatePermittedForkTrigger.json` and edits `AgwfCode.json`. The workflow file itself is unchanged on `324768f`. |
| **Consequence** | A breaking schema change, or this wave's new AGWF037 entry schema, is "checked" by a green job that printed "no diff to run." Branch protection that requires the job name sees success. Exarchos consumers take a narrowed or removed schema as an additive 0.7.0 bump. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The workflow itself: `have_prev=false` must `exit 1` (or a dedicated `skipped` / cancelled conclusion that is not success), plus a self-test that a checkout with no `v*` tags fails the job. |
| **Why not cheaper** | Job conclusion is not a C# type and is not generated from the schema set. A component test of `JsonSchemaDiff` does not run when the workflow skips the `node` step. |
| **Failure signal** | The job name staying green. That channel does not separate "compared, additive" from "did not compare." |
| **Rollback** | Revert the workflow fail-closed change. This wave's schema files revert with the contracts commit. A published 0.7.0 tag does not reverse. |
| **Lenses** | False-Green Shapes |

**Open questions:**

- Is `contracts-schema-diff` a required check on `main` PRs? If it is not required, skip-vs-run never gates merge and this obligation's consequence is a badge, not a merge decision.
- On a `fetch-depth: 0` checkout of this branch, does `git describe --tags --abbrev=0 --match 'v*'` resolve to `v2.10.0`, and does that tag contain `src/Strategos.Contracts/schemas`? This worktree can see `v2.10.0` and that path. A PR checkout that does not fetch tags would still take the skip path.

## What led here

The user lead and survey P23 named skip-and-succeed. The workflow was not edited in `4d060f4...324768f`. This lens still owns it because this wave's new schema is the subject the job is supposed to classify, and the skip path reports the same conclusion as "additive-only."

Competing explanation: this repo always has `v*` tags that contain `schemas/`, so the skip is dead. Discriminating detail: the YAML sets `have_prev=false` in two branches (`44-46`, `52-54`) and the diff step is `if: steps.prev.outputs.have_prev == 'true'` (`58`). A job with a skipped step and no later failure is success. That is independent of whether today's default checkout finds `v2.10.0`.

Path filter is `src/Strategos.Contracts/schemas/**` only. A TypeSpec-only edit that did not commit the schema file never starts the job. Starting vs skipping after start are different holes; this obligation is the after-start skip.

## Code read (revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`)

- `contracts-schema-diff.yml:14-20` — PR path filter: `schemas/**`, the script, the workflow.
- `contracts-schema-diff.yml:41-55` — `PREV_TAG="$(git describe --tags --abbrev=0 --match 'v*' 2>/dev/null || true)"`; empty tag or missing path ⇒ `have_prev=false` and a log line.
- `contracts-schema-diff.yml:57-62` — compare step gated on `have_prev == 'true'`.
- This wave's schema paths: `src/Strategos.Contracts/schemas/json-schema/AgwfEntryDuplicatePermittedForkTrigger.json`, `AgwfCode.json`.

Local `git tag` / `git ls-tree v2.10.0` in this worktree: `v2.10.0` exists and lists `src/Strategos.Contracts/schemas`. That is this clone, not a proof that every CI checkout fetches tags.

## Kill probe

Run the job with `git describe` failing (no tags). Expected if the claim held: job failure. Actual YAML: job success, log "no previous v* tag found — treating current schemas as the baseline (no diff to run)."

## Failure scenario

A shallow or tag-less checkout of a PR that also rewrites an existing schema to remove a required property. `have_prev=false`. Job green. 0.7.0 is treated as additive. Exarchos converters that throw on unknown or missing members fail after upgrade — or worse, accept a narrowed schema.

## Open questions (full stakes)

### Is the job a required check?

Not in this repository's workflow YAML. Branch protection lives on GitHub. If the job is not required, a skip-success and a missing job are the same merge signal. The obligation then moves from "fail the job when skipped" to "the gate does not exist as a merge control," which is a different claim and may belong with the `contracts-test` required-check question in the survey.

### Does CI on this branch actually see `v2.10.0`?

This worktree does. `actions/checkout@v4` with `fetch-depth: 0` fetches history; it does not always fetch tags unless `fetch-tags: true`. If CI here already fetches tags, the skip is latent and the obligation is still the YAML shape. If CI does not fetch tags, this wave's schema-diff run is already a skip-success. That is the difference between a structural hole and a current false-green of this PR.
