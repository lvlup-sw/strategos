# int-inv-3-checks-not-in-ci

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | The INV-3 deterministic checks this wave extended (3.4 `ResultType` on every Hosting `CallToolResult` file, 3.5 `Icons` on `OntologyToolDescriptor`) run as a fail-closed gate on the protected path. |
| **Scope** | `.agents/skills/strategos-design-invariants/references/deterministic-checks.md` Checks 3.3–3.5; `.github/workflows/ci.yml` `quality-gates` job; remaining workflows under `.github/workflows/`. |
| **Consequence** | CHANGELOG Residue says "INV-3 now denies the pre-2026-07-28 response shape." The deny-list is a skill checklist. `quality-gates` runs AGAG, catch-discipline, and prose greps only. A new `CallToolResult` that omits `ResultType`, or a descriptor file that drops `Icons`, can merge. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A workflow-graph check: some CI job must execute Checks 3.4 and 3.5 (or an equivalent fail-closed grep) and must not treat skip as pass. |
| **Why not cheaper** | Markdown existing is not a registration. The compiler does not run skill greps. A human checklist is rung 6. |
| **Failure signal** | Nothing. The skill is unused unless a person opens it. |
| **Rollback** | Revert the INV-3 markdown edits. Leaves CI as it is. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Is the skill *intended* as a human-only audit, with Hosting tests as the real gate? CHANGELOG wording is "INV-3 now denies," which reads as a control. Check 3.4 is file-level `grep -L ResultType` and can pass on a comment.
- Do required GitHub checks include any out-of-repo invariant runner that this tree does not list? Branch-protection names are not in-repo.

**Confidence:** high that no in-repo workflow invokes these checks.

## What led here

Existing-proof P32 and the known lead "INV-3 checks not in CI." This wave updated INV-3 / deterministic-checks for `resultType` vs icon gap. Integration completeness asks whether that control is in the composition root of CI.

Competing explanation: `quality-gates` already runs the INV-3 greps, or another workflow does. False.

## Composition

Declared control (skill, not CI):

- `deterministic-checks.md` Check 3.4 (`:112-124`) — files that mention `CallToolResult` must also mention `ResultType`
- Check 3.5 (`:126-133`) — `OntologyToolDescriptor.cs` must contain the substring `Icons`
- Check 3.3 (`:98-110`) — no pre-2026-07-28 revision strings
- INV-3 spec acceptance questions (`INV-3-mcp-first-class-latest-spec.md:7-15`) including "Does every `CallToolResult` construction set `ResultType`?"

CI composition (`ci.yml:169-186` `quality-gates`):

1. `scripts/check-agag-hygiene.sh`
2. `scripts/check-catch-discipline.sh`
3. `scripts/check-prose.sh`

`rg -n -i 'INV-3|deterministic-check|mcp-first-class|Check 3\.|Icons|resultType|VersionOverride' .github/workflows/` — **no matches**.

Workflows present: `ci.yml`, `contracts-codegen-guard.yml`, `contracts-schema-diff.yml`, `public-api-drift.yml`, `publish-contracts.yml`, `publish.yml`, `docs.yml`, `project-automation.yml`, `benchmark-full.yml`, `benchmark-regression.yml`. None of them execute the INV-3 greps.

`builder-api-stability` scopes the seven workflow builder interfaces only. `public-api-drift.yml` is the same seven, push-to-main, fail-soft on PAT. MCP/Hosting PublicAPI is out of those jobs.

The grep shape itself is weak (file-level substring; a comment satisfies 3.4; 3.5 does not assert null-when-unset). That is a proof-quality fact. This obligation is registration: even a perfect grep is not in the pipeline.

## Why cheaper rungs fail

- **Rung 1:** no generated CI job from the skill catalog.
- **Rung 2:** skill markdown is not compiled.
- **Rung 4:** Hosting tests cover some `ResultType` / null-icons cases and are the wrong subject for "INV-3 runs."

## Failure scenario

A new Hosting helper constructs `new CallToolResult { IsError = true }` without `ResultType`, in a file that already mentions `ResultType` (3.4 would miss it) *or* in a new file (3.4 would catch it **if run**). CI `quality-gates` stays green. CHANGELOG still says INV-3 denies the old shape.

## Code read (this revision)

- `.agents/skills/strategos-design-invariants/references/deterministic-checks.md:98-133`
- `.agents/skills/strategos-design-invariants/references/INV-3-mcp-first-class-latest-spec.md:7-26`, `:39-41`
- `.github/workflows/ci.yml:169-186` (and the rest of the file's job list)
- `.github/workflows/*` filename list; `rg` over that directory for INV-3 tokens
- `CHANGELOG.md:187-190`

### Investigation Log

#### Do any CI workflows execute INV-3 / deterministic-checks 3.4 or 3.5?

- Read: `ci.yml` `quality-gates` and the other workflow filenames; `rg` over `.github/workflows` and `scripts/` for INV-3 / Check 3.4 / 3.5 / mcp-first-class / deterministic-checks.
- Found: quality-gates runs three unrelated shell greps. Scripts directory has no INV-3 runner.
- Not found: any workflow step that invokes those checks.
- Conclusion: INV-3 checks are not in the CI composition. Whether branch protection adds an unlisted gate is an open question.
