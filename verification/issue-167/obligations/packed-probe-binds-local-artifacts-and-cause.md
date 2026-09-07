# packed-probe-binds-local-artifacts-and-cause — Packed probe binds local artifacts and exact failure cause

| | |
|---|---|
| **Claim** | The production-path consumer probe restores the exact nupkgs produced by the same CI run, compiles the legal #167 binding, and rejects the illegal binding specifically because one expected AGWF041 seam proof failed, with no unrelated compile or generator errors. |
| **Scope** | `ci.yml` pack-verify job and `scripts/verify-generator-consumer-build.sh`, including NuGet source selection, analyzer loading, and the two consumer builds. |
| **Consequence** | CI can pass against a package from another source/revision, or count an unrelated failed build as proof merely because its log also contains `AGWF041`; the shipped package may omit or break #167. |
| **Proof rung** | Rung 5 — production-path integration test. |
| **Proof artifact** | A hermetic local-feed restore whose assets/diagnostic log prove the exact package IDs, versions, and SHA-256 digests selected; a legal build with zero errors; and an illegal build with an exact, exclusive AGWF041 reason/witness. Include wrong-package and unrelated-error kill probes. |
| **Why not cheaper** | Rung 1 proves package contents are generated consistently, not that NuGet selects them. Rung 2 proves source shape only. Rung 3 can inspect nuspec/assets but cannot prove analyzer load and consumer compilation. Rung 4 project-reference tests bypass the packed composition. |
| **Failure signal** | A downstream restore/build failure is visible. The hardened probe partitions setup/restore/compile failure from the deliberately exclusive AGWF041 rejection and fails on package identity, version, source, or byte mismatch. |
| **Rollback** | Pin consumers to the prior known-good Strategos package set or withdraw/reissue the unpublished package. Re-running the probe with populated remote sources is not a rollback. |
| **Lenses** | False-green shapes: bypassed packaging, stale evidence, substring assertion, unrelated error. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. The probe resolves exact nuspec IDs and versions, maps all `LevelUp.Strategos*` packages only to the local feed, records six SHA-256 hashes, compares restored nupkg bytes and source metadata, compiles the legal consumer, and requires the illegal consumer to fail exclusively with AGWF041 and the exact seam witness. The digest-bound local probe, hardened pack script, and Basileus smoke all passed on that subject. Protected `pack-verify` execution and review remain pending.

**Open questions:**

- None. Record the protected check URL after execution.

### Investigation Log

#### Can the packed probe prove which artifacts and which failure caused its verdict?

- Read: the `pack-verify` job in `.github/workflows/ci.yml` and all of `scripts/verify-generator-consumer-build.sh`.
- Historical finding: the probe once added a feed, selected a filename prefix, recorded no digest/source provenance, and accepted any nonzero build whose log contained AGWF041.
- Current finding: exact nuspec identity/version selection, package-source mapping, six digests, restored-byte/source checks, legal compilation, and exclusive exact-cause parsing close those routes. `final-evidence.md` records the successful exact-current `98fabb4` run, package hashes, serialized pack, and Basileus smoke.
- Not found: protected `pack-verify` execution or completed review.
- Conclusion: the path is locally bound to exact-current artifacts and cause; its current verdict is `Indeterminate` pending protected evidence.
