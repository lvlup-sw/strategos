# Promise obligation 09 — packed generator consumer enforcement

- **Historical Stage 2 status:** unproven
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; no final nupkg digest existed at that stage.
- **Promise sources:** claim seed 9; CI `pack-verify` comments; Contracts packaging claims.

This file preserves the Stage 2 inventory record. `../final-evidence.md` now records exact-current
`98fabb4` package digests, a passing hardened consumer probe, the hardened pack script, and the
Basileus smoke. Protected package evidence remains `Indeterminate` until the PR gate runs.

## Obligation

From freshly produced Strategos nupkgs and only their declared consumer/runtime dependencies, an
isolated consumer must restore and compile a legal typed workflow binding. The same package set
must load the shipped analyzer and reject an illegal seam with AGWF041. The exact package digest,
test result, and source commit must agree.

The cheapest sufficient proof is **R5, isolated packaged-consumer build**. In-repository component
tests and inspecting analyzer files inside a nupkg are too weak: they do not prove analyzer loading,
dependency flow, or build-failing severity in an actual package consumer.

## Candidate control

- `.github/workflows/ci.yml:81-107` packs the solution and runs the consumer probe after build/test.
- `scripts/verify-generator-consumer-build.sh:38-94` selects packed core, agents, generator, and
  ontology packages and creates a hermetic consumer with explicit Wolverine/Marten dependencies.
- Its legal source at `:96-185` uses typed `WorkflowBindingReference` and per-occurrence
  `WorkflowActionReference`; `:187-204` isolates the NuGet cache and requires a successful build.
- `:206-231` rebuilds with an intentionally incompatible downstream requirement, requires nonzero
  exit, and requires AGWF041 in the log.
- `PackagingTests.cs:83-180` independently checks the 0.11.0 contracts package and embedded
  `ActionReferenceV1.json`.

## Historical missing evidence and current disposition

At Stage 2, a prior working-tree pack/probe reportedly passed, but product and harness files changed
afterward and no immutable artifact digest was recorded. That historical evidence was superseded by
the exact-current `98fabb4` snapshot, which records SHA-256 for every consumed nupkg, exact local-
source restore, legal/exclusive-illegal results, the successful hardened pack script, and the 2/2
Basileus smoke. The obligation is still `Indeterminate`, not a promoted proof verdict, because the
protected-CI package path and review have not run.
