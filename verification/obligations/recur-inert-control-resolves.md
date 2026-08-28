# recur-inert-control-resolves

Open class **R3** (CI/config axis). Guard candidate **G-R3**. T3 is a path-token fix. The old 404 subject was not kept.

## What led here

Issue #185 named this as the slice’s most portable result: six controls that looked present and were inert. On the CI/config axis: (1) INV-3 deny-list omitted the superseded revision (#178); (2) substring parity (#145 / DR-5); (3) `renovate.json` extended a 404 path (#181, ~7 months from `1926a7f` to this branch); (4) completion oracle from document absence (#180); (5) invariant catalog gitignored (#178); (6) diagnostic aimed at an unauthorable shape (R7). Siblings: #147 unused OIDC (out of wave), #133 Dependabot absent, #163 AONT205 without a producer.

This diff: `334f64c` rewrites the second `extends` token to `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json`. `887eb9a` adds INV-3 Checks 3.4 / 3.5 as documented greps. Neither is a job.

Decay rule: another correct instance of the pattern is not the fix. The bypass (#181 parse-valid 404) is evidence the convention does not hold without enforcement.

## Surfaces at 324768f

- `renovate.json:3–6` — two `extends` tokens. No in-repo proof either resolves (existing-proof P44).
- `.github/workflows/ci.yml:169–186` — `quality-gates` runs `scripts/check-agag-hygiene.sh`, `check-catch-discipline.sh`, `check-prose.sh` only.
- `.agents/skills/strategos-design-invariants/references/deterministic-checks.md` Checks 3.1–3.5 — human recipes. Check 3.4 is `grep -L ResultType` (substring).
- Missing kill: the 2026-01-09 token `local>lvlup-sw/lvlup-claude:renovate-config/presets/dotnet.json` was deleted, not preserved. Guards.md: if the class is already fixed everywhere, keep the old defect as a fixture.

#147 unused `id-token: write` and #133/#174 live proof are out of wave. They remain class members. Do not invent a Trusted-Publishing obligation.

## Failure

Renovate never opens a PR. The file reads as working automation. `auto-merge-renovate` is dead automation that looks live. INV-3 greps that nobody runs cannot fail a stale tree. Who observes it: nobody, for months, until a human notices the Dependency Dashboard is empty (#181 acceptance text).

## Expensive to find again

- A 200 on the contents API is not the #181 acceptance (live Renovate run). Encoding only the 200 and calling the class closed repeats the inert-control class.
- `npm ci || npm install` and `contracts-schema-diff` skip-and-pass are the same operator on other jobs (existing-proof P23). G-R3’s self-test must treat “step absent” and “API error” as fail/indeterminate, not pass.

## Open questions (with stakes)

- Does `gh api` on `lvlup-sw/lvlup-claude` still 200 at `tools/renovate-config/presets/dotnet.json` after the `exarchos` rename? A 404 means T3 is another inert path token. A 200 still leaves live apply unproven. Stakes: the instance-fix may already be false; the class is open either way.

### Investigation Log

#### Are INV-3 deterministic greps executed in CI?

- Read: `ci.yml` `quality-gates` job (`:169–186`); searched workflow text for INV-3 / deterministic-checks / renovate-resolve.
- Found: AGAG, catch discipline, prose only.
- Not found: any job that runs Checks 3.1–3.5 or resolves `extends`.
- Conclusion: checklist-only. Checks 3.4/3.5 inherit the stale-deny-list decay. Resolved for “in CI?” = no.

#### Was the 404 extends token kept as a fixture?

- Read: `renovate.json` at HEAD; recurrence seed kill-fixture register.
- Found: current token uses `tools/renovate-config/presets/dotnet.json`. Seed lists `*(missing)* renovate 404 extends`.
- Conclusion: no kill fixture. G-R3 must reconstruct the 2026-01-09 token. This is a finding about the proof system, not only about Renovate.
