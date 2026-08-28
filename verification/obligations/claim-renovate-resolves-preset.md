# claim-renovate-resolves-preset — Renovate resolves the organisation's dotnet preset

Lens: 6 Claim Derivation
Disposition: open question
Inventory claims: 51
Confidence: n/a — unvalidated external-process claim

## Open question

Does the Renovate GitHub App resolve `local>lvlup-sw/lvlup-claude:tools/renovate-config/presets/dotnet.json` and apply that preset?

**Stakes.** This is a highest-stakes CHANGELOG guarantee (`CHANGELOG.md:184`, claim 51). If the bot 404s on the org slug, the repo, or the path, #181 repeats: a control that looks present and is inert. The in-repo path-token fix (`claim-renovate-path-token`) can be true while this claim is false.

**Competing explanation.** The description states "Renovate resolves." The code changes one path token. Nothing in this repository observes a Renovate run (existing-proof P44: no proof; survey: bot apply unobserved). Per `validating-claims.md` the claim does not become an obligation until an exhibit separates "token looks right" from "the bot applied the preset."

## What would settle it

A Renovate log or reproduction that the extends entry resolves and that the applied config differs from the 404 baseline. Out of wave: do not migrate Renovate → Dependabot (claim 26). Maintainer portal work is out of wave.

This file is not an obligation. Promoting claim 51 without that exhibit would treat a lead as a fact.
