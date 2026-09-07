# contracts-release-revalidates-artifacts — Contracts release revalidates published artifacts

| | |
|---|---|
| **Claim** | A `contracts-v0.11.0` release either rebuilds and revalidates TypeSpec schemas/generated C#/Contracts tests at the tagged revision or publishes the exact digest already verified for that revision; earlier PR evidence is never assumed to cover a later tag. |
| **Scope** | `.github/workflows/publish-contracts.yml`, Contracts codegen, generated schemas/records, package contents, and NuGet publication. |
| **Consequence** | Stale or hand-edited generated artifacts can be packed and irreversibly published even though an earlier PR ran codegen/tests; #167 consumers then receive a package different from the reviewed contract. |
| **Proof rung** | Rung 5 — production-path integration test. |
| **Proof artifact** | On the tag job, run locked TypeSpec/codegen with a clean-tree assertion and the Contracts tests before pack, then record/verify the nupkg digest; alternatively promote an immutable CI artifact whose source SHA and digest match the tag. |
| **Why not cheaper** | Rung 1 clean regeneration is necessary but does not by itself bind the published nupkg. Rung 2 compilation omits schema/toolchain semantics. Rung 3 can enforce workflow shape but cannot prove the package bytes and generated behavior. Rung 4 Contracts tests through project outputs still bypass publication composition. |
| **Failure signal** | Downstream validation/deserialization failure after NuGet publication. NuGet package versions are immutable, so the channel is late and cannot be repaired in place. |
| **Rollback** | Publish a corrected higher Contracts version and have consumers pin/upgrade; an already published 0.11.0 cannot be overwritten. Deleting a GitHub release does not remove NuGet bytes. |
| **Lenses** | False-green shapes: stale evidence, bypassed generation, packaging subject mismatch. |

**Confidence:** High.

**Current proof posture:** Indeterminate until a future Contracts tag runs. `publish-contracts.yml` now verifies checkout equals the tag, provisions Node 24, restores the locked TypeSpec toolchain, regenerates schemas/C#, runs `Strategos.Contracts.Tests`, rejects tracked or untracked generated drift, compares against the latest stable published Contracts nupkg with version/digest binding, packs, and records the candidate nupkg digest. This is strong candidate structure, but no `contracts-v*` execution or released-package digest exists for the future release event.

**Open questions:**

- None.

### Investigation Log

#### Does the Contracts tag workflow re-establish the evidence used to authorize published bytes?

- Read: all of `.github/workflows/publish-contracts.yml`, the relevant comments in `publish.yml`, the contracts-codegen guard summarized in Stage 1, and Contracts package/version assertions.
- Historical finding: the tag job once packed committed generated output without Node/TypeSpec regeneration or Contracts tests.
- Current finding: tag/checkout equality, locked regeneration, Contracts tests, tracked-and-untracked cleanliness, published-baseline digest/version comparison, exact candidate selection, and candidate digest recording all precede publication.
- Not found: an executed future `contracts-v*` job, its check URL, or the resulting published candidate digest.
- Conclusion: the release path is candidate-guarded but release evidence is necessarily fresh and remains `Indeterminate` until the tag runs.
