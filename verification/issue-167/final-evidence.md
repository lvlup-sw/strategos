# Final evidence for issue #167

This record binds the local verification results to the immutable product commit
`98fabb410e4432cc39fd71ec92651e6ceecfcc7c` (tree
`f7271c9cb517e30c658b6d9245a739a9dc8d365d`) over base
`45c86a63a9437abd920240b4dc95b235c0f72d37`.

This is a **local evidence snapshot**, not a verify-code `Verified` verdict. Commit `595a949` contains
the proof-harness migrations, rooted-descriptor nonvacuity kill, exact `RecordEmitter` boundary test,
and shared-type fork completed-event/`NotFound` alignment. Commit `6851fe7` then serialized the local-
feed MSBuild pack invocation after three diagnostic-free local exit-2 results. Current commit
`98fabb4` adds the missing exact nonwhitespace constraint to `WorkflowDefinitionV1.name`, regenerates
standalone/bundled schemas and C#, and adds read/write semantic tests. The serial Release solution
build, complete test portfolio, Contracts/codegen, live schema comparison, docs build, fresh package
digests, and external consumer below now bind exactly to `98fabb4`. All protected PR evidence remains
pending.

## Environment and policy

- Environment: Pop!_OS 24.04 LTS, linux-x64, local Codex worktree.
- .NET SDK: 10.0.202; runtime reported by TUnit: .NET 10.0.6.
- Test platform: Microsoft Testing Platform 2.0.2; TUnit 1.2.11.
- Node.js: 24.12.0; npm: 11.10.1; TypeSpec compiler: 1.12.0.
- Workflow lint: actionlint 1.7.7; shellcheck 0.10.0.
- Required reusable CI policy is pinned to
  `ffd6fb4979fba8a4317cca6fd7ad9ec4090acff9`.

## Locally revision-bound execution

Rows identify their evidence subject explicitly.

| Proof | Result |
|---|---|
| Serial Release solution build (`98fabb4`) | Passed in 1m25.41s with 0 errors. Two `NU1900` warnings were advisory-feed lookup failures caused by the restricted local network; compilation completed. |
| Aggregate Release test run (`98fabb4`) | Passed: 5,517 succeeded, 0 failed, 16 skipped, 5,533 total in 8m25.389s. |
| Strategos.Generators.Tests (`98fabb4`) | Passed 1,761/1,761 as part of the exact-current solution run. |
| Contracts regeneration (`98fabb4`) | `npm ci` and `scripts/contracts-codegen.sh` passed after the workflow-name constraint change; generated C#, schemas, and diagnostics had zero tracked diff. Stable generated-tree digest: `6ae1bf5767a6f48070526f14b3e3e172e2e3c39672f03837b73e5ea3da581b5e`. |
| Strategos.Contracts.Tests (`98fabb4`) | Passed 185/185, including workflow-name read/write nonblank semantics. |
| Structural schema comparison (`98fabb4`) | The live gate passed Contracts 0.4.0 to 0.11.0 against published nupkg SHA-256 `503f565462a48518367fd4366bec6698c092c91d2a4dbde2d4f4925c1139ee0b`; four breaking changes were permitted by the pre-1.0 minor-version policy. |
| Documentation (`98fabb4`) | Locked `npm ci && npm run build` emitted 80 pages and Pagefind indexed all 80. The nonfatal missing-entry diagnostic for `docs -> 404` did not prevent `/404.html` generation; npm reported 2 low and 7 high audit findings. |
| Static and policy gates (`98fabb4`) | AGAG, catch, prose, builder API, shellcheck (including the hardened pack script), actionlint, and `node --check` all passed. |
| DBSF parity (`98fabb4`) | Passed with `qdrant-client` 1.12.1 installed in an isolated `/tmp` target. |
| Design-invariant audit (`98fabb4`) | Passed with no findings. Mechanical INV-1–INV-8 checks found no hand-authored sagas/core runtime references or ad hoc stores, ontology-generator role drift/runtime coupling, old MCP pins, graph-nomenclature additions, duplicate/removed diagnostics, new nonsealed/virtual DSL or descriptor types, mutable-state additions, or `ClrType` dereferences; SymbolKey-only tests remain present. |
| Fresh package build (`98fabb4`) | Passed for the six packages consumed by the isolated proof; exact hashes are below. Output: `/tmp/issue167-pack-98fa.5QVTg4`. |
| Package consumer proof (`98fabb4`) | The legal external consumer compiled with 0 warnings and 0 errors, and the illegal consumer failed closed with AGWF041. |
| Basileus smoke guard (`98fabb4`) | The hardened exact `pack-to-local-feed.sh` completed successfully and restore/run passed 2/2. The expected `NU1603` warning resolved published core during the smoke. The three earlier diagnostic-free exit-2 attempts belong to the superseded parallel script and are historical. |
| Diff integrity (`98fabb4`) | `git diff --check` passed. Product diff: 171 files, 20,606 insertions, 1,150 deletions. |

The exact-current sorted TRX SHA-256 manifest has digest
`cb9d7faac12bba1ecabe979ea791b2c846ae7807880a2b6faefc396846a0eb3c`.
Critical report hashes are:

- Contracts: `8a982a4a2d0a2f6af13f6273142dfe18bea1137a1b32ac11af409a0e3fd6331b`.
- Behavioral: `67115ce8be94c4a0eb5ef57d086dd34411d9ed4262e82c1e0ca648fbaf24225c`.
- Generator 1,761/1,761: `dea2cc1b26650a0180a7a44ffaca82ff410ef1960649f8eef3e8acd7ce12f619`.

## Packed artifact binding

| Artifact | SHA-256 |
|---|---|
| `LevelUp.Strategos.2.10.1-alpha.0.17.nupkg` | `c50b9cf1139c6079de956ce9c9e8d42dfa43e735981d976cef43fd018812d791` |
| `LevelUp.Strategos.Agents.2.10.1-alpha.0.17.nupkg` | `2f88da0d29262f01a2b052509f617b6bded92f37f94a43911b5ecbfebbfb5b74` |
| `LevelUp.Strategos.Contracts.0.11.0.nupkg` | `9fa47ba6a373d3565d3ef9328beb41f277a348cd2d0517f741cf7557f0d724b5` |
| `LevelUp.Strategos.Generators.2.10.1-alpha.0.17.nupkg` | `26d6e8dcde591e18dcef5dd8bd6c5a1f31735be95d79acab93f18b51def8a482` |
| `LevelUp.Strategos.Identity.Abstractions.2.10.1-alpha.0.17.nupkg` | `305a61e2b7e308b162e8254aabc7dc3e718c958947f07c9d49bb7747b52b9b20` |
| `LevelUp.Strategos.Ontology.2.10.1-alpha.0.17.nupkg` | `23d434d758eb66a32efcc2cf822f8f7970700f7e3857fb33e64a5503f6f856e8` |

These package digests bind current commit `98fabb4` to the exact bytes consumed by the external
consumer proof. They are not future release digests: the Contracts tag must regenerate, digest-bind,
and validate its own artifacts.

## External adoption and remaining protected evidence

- Basileus adoption: [lvlup-sw/basileus#493](https://github.com/lvlup-sw/basileus/issues/493).
- Exarchos adoption: [lvlup-sw/exarchos#1893](https://github.com/lvlup-sw/exarchos/issues/1893).
- Producer linkage: [issue #167 comment](https://github.com/lvlup-sw/strategos/issues/167#issuecomment-5564475759).

Protected PR CI and the single completed-diff CodeRabbit review remain pending.
Those are delivery evidence, not substitutes for the local proofs above. The
Contracts tag publication path must bind new release bytes when a tag is created.
