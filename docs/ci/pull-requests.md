# Pull request validation

Open a non-draft PR and use the five sections in the PR template. Authors with
access to the private CI checkout can run the existing PR-body validator locally.
Run it from the product checkout with the body in `/tmp/pr-body.md`.

```sh
PYTHONPATH="$CONTROL_CHECKOUT/actions/pr-body" python3 -m pr_body \
  --template .github/PULL_REQUEST_TEMPLATE.md --body-file /tmp/pr-body.md
```

GitHub does not run a PR-body job.

A current maintainer approval starts the applicable Buildkite checks for human
PRs. A maintainer can also comment `/ci validate`. Dependabot always needs that
explicit request. `/ci full` requests the complete proof set for the same
candidate and grants no release permissions.

The controller binds each comment to the observed head, base and tested merge.
After changing the head, ask for a fresh approval or leave a new comment. A base
change retains a current human approval and requires a fresh tested merge.
An explicit request needs a new comment after either SHA changes. If the
controller missed the original creation event or had yet to observe the
candidate, post a fresh comment after reconciliation. Edited comments cannot
authorize CI.

GitHub lists **Buildkite / Strategos** as the required check. It links to
the admitted build and lists its jobs. Open or draft PRs can have a required
check waiting for validation before Buildkite has started. An authenticated
failure must be resolved before merging.
