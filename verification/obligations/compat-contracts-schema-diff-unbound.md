# compat-contracts-schema-diff-unbound — The breaking-change gate does not bind to the contracts tag this bump versions

| | |
|---|---|
| **Claim** | The structural proof that Contracts `0.7.0` is an additive-only minor must compare this schema set to the previous **contracts** publish, not to a product `v*` tag, and must not report pass when that comparison did not run. |
| **Scope** | `.github/workflows/contracts-schema-diff.yml` plus `scripts/contracts-schema-diff.mjs`, which are the T30 gate for the published schema family. |
| **Consequence** | A removed, renamed, or narrowed member can ship as a minor if the job skips. An added enum member (`AGWF037`) that should produce a NOTICE (and a consumer-notice changelog line) can also go unseen. Either way the gate that is supposed to classify this bump does not speak to this subject. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The workflow itself, rewritten so `PREV` is `git describe --match 'contracts-v*'` (or an explicit package baseline), and so a missing baseline is a failed or skipped-non-success outcome. Today's unit tests (`SchemaDiffTests`) prove the *rules*, not that CI applied them to this revision. |
| **Why not cheaper** | Generation cannot bind a CI subject. The compiler does not see workflows. A contract test over in-memory fixtures (`SchemaDiffTests`) already exists and is the wrong subject: it never opens `AgwfCode.json` at this revision against the previous contracts tag. |
| **Failure signal** | Nothing. A skipped job is green. NOTICE does not fail the gate even when the job runs (`contracts-schema-diff.mjs:20-23`). |
| **Rollback** | Revert the workflow path match. Does not reverse a publish that already happened without a real diff. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high — the match string and skip branch are in the workflow file. |

**Compatibility class:** compatibility gate whose subject is unbound (stale / wrong baseline; skip reports success).

**Reverse dependency closure:**

- Workflow: `.github/workflows/contracts-schema-diff.yml:42` — `git describe --tags --abbrev=0 --match 'v*'`.
- Skip: `:44-46` sets `have_prev=false` when no `v*` tag; `:52-54` same if the tag predates `schemas/`. `:58` runs the diff only when `have_prev == true`.
- Publish authority: `.github/workflows/publish-contracts.yml:8-15` triggers on `contracts-v*`, and `:52-57` requires the tag to equal `<ContractsVersion>`.
- Tags at this clone: `contracts-v0.4.0` is the newest contracts tag; `v2.10.0` is the newest product tag. No `contracts-v0.5.0`, `0.6.0`, or `0.7.0`.
- Script policy: new schema file is NON-BREAKING (`contracts-schema-diff.mjs:24-26`); added enum member is NOTICE (`:20-23`); only BREAKING fails.

**What this revision does to the gate**

Nothing. The 0.7.0 bump adds `AgwfEntryDuplicatePermittedForkTrigger.json` and `"AGWF037"` on `AgwfCode.json`. Those are exactly the shapes the differ knows how to classify — if it runs against a contracts baseline. Against `v2.10.0` (Contracts 0.4.0) the same job, if it ran on a PR to main, would also see AGWF035 and AGWF036 as additions. Against no previous schemas it would skip and succeed.

**Reverses?** The workflow file reverses by revert. A green check already recorded for a skip does not reverse.

**Open questions:**

- Is `contracts-schema-diff` a required check on this branch or only on PRs to `main`? Survey recorded no PR for `cursor/c801a047`. If the job never ran, this revision has no bound T30 result at all.
- Should the baseline be `contracts-v0.4.0` (last publish) or an untagged 0.6.0 tree? The csproj comment presents 0.7.0 as one added code over 0.6.0. Those two answers classify different deltas.

**What is expensive to find again**

The publish workflow and the diff workflow use two different tag namespaces on purpose (`publish-contracts.yml:8-10`). The diff workflow was never retargeted when that split landed. A later reader who greps only `v*` will think the gate covers 0.7.0.
