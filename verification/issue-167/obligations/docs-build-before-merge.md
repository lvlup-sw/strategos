# docs-build-before-merge — Changed documentation builds before merge

| | |
|---|---|
| **Claim** | The exact #167 revision's changed Starlight documentation successfully installs and builds before merge; a skipped documentation job is indeterminate, never green evidence. |
| **Scope** | All changed `docs/**` files, Astro/Starlight content collection, links/imports, and `.github/workflows/docs.yml`. |
| **Consequence** | A PR can satisfy all required checks and merge documentation that fails only on the subsequent `main` deployment, leaving the published site stale or broken. |
| **Proof rung** | Rung 5 — production-path integration test. |
| **Proof artifact** | A PR-triggered docs build using the locked Node dependencies and the same `npm ci && npm run build` entry point as deployment, bound to the PR head/merge revision. Deployment may remain push-only. |
| **Why not cheaper** | Rung 1 does not generate the changed prose/site structure. Rung 2 cannot compile Astro content. Rung 3 can lint paths/frontmatter but cannot exercise Starlight integration. Rung 4 isolated markdown tests do not build the deployable site. |
| **Failure signal** | The PR `docs-build` job or push-to-main deployment build fails. A skipped/missing protected result is indeterminate rather than success. |
| **Rollback** | Revert the broken docs commit on `main` or repair and redeploy. A manual post-merge build detects but does not prevent the bad merge. |
| **Lenses** | False-green shapes: skipped-as-pass, stale evidence. |

**Confidence:** High.

**Current proof posture:** Indeterminate. `ci.yml` runs the locked `npm ci && npm run build` path for pull requests and `main`, while `docs.yml` retains deployment on `main`. The exact-current `98fabb4` local locked build produced and Pagefind-indexed 80 pages successfully. No protected PR result or completed review is recorded.

**Open questions:**

- None.

### Investigation Log

#### Does a PR changing docs execute the deployable site's build?

- Read: all of `.github/workflows/docs.yml`, the `ci.yml` job list, and the #167 changed-file scope recorded in Stage 0/1.
- Historical finding: only the deployment workflow built docs after a push to `main`.
- Current finding: `ci.yml` contains a PR/main `docs-build` job using the locked install and Starlight build commands; exact-current local execution at `98fabb4` built and Pagefind-indexed 80 pages.
- Not found: exact-current or protected PR execution bound to `98fabb4`.
- Conclusion: the pre-merge mechanism exists, while its protected evidence remains `Indeterminate`.
