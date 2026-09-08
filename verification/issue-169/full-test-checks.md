---
repo_path: /home/reedsalus/.codex/worktrees/751e/strategos
target_kind: final-subject-test-check
revision: 263cc5720818b13268214c5df84d7575fd74a6d7
base_revision: 362c45f1ebc812ba2e2abb419e47fef29cb1d622
target_ref: codex/169-derived-compensation
implementation_fingerprint: 24be1d97dfaedd88bebc7f33dda831d9b36b1ba1
updated: 2026-09-08
evidence_binding: same-tree pre-rebase cdaa73a Release build and complete local test matrix plus exact-final package, packed-consumer, feed-closure, and Basileus-smoke checks; hosted CI, review, and merge evidence remain pending
---

# Issue #169 final-subject build and test checks

## Current verdict

**Final-revision local CI verdict: Indeterminate.** Revision `263cc57` has the same tree as tested
pre-rebase revision `cdaa73a`, but the completed processes name the earlier source revision. The
earlier Release build and all 17 test projects had zero observed failures; sixteen declared tests
did not run, and NuGet vulnerability metadata was unavailable. Neither a byte-identical tree nor an
absence becomes an exact-revision success under the evidence-binding rule.

Same-tree pre-rebase aggregate, including the separate Contracts suite: **5,749 total; 5,733
succeeded; 0 failed; 16 skipped**.

## Subject binding

- Final target revision: `263cc5720818b13268214c5df84d7575fd74a6d7`
- Tested pre-rebase revision: `cdaa73a960acbf1f5a28ac0793b91c2e52de791e`
- Shared Git tree: `24be1d97dfaedd88bebc7f33dda831d9b36b1ba1`
- Final diff base: `362c45f1ebc812ba2e2abb419e47fef29cb1d622`
- Configuration: Release
- .NET SDK: `10.0.202`
- Test runner: Microsoft Testing Platform with TUnit

## Completed same-tree pre-rebase checks

| Check | Total | Succeeded | Failed | Skipped | Result |
|---|---:|---:|---:|---:|---|
| Release solution build | — | — | 0 errors | — | **Pass** for compilation; 14 NU1900 warnings are classified separately |
| `Strategos.Contracts.Tests` | 191 | 191 | 0 | 0 | **Pass** |
| `Strategos.Generators.Tests` | 1,915 | 1,915 | 0 | 0 | **Pass** |
| Remaining 14 completed nonbehavioral projects | 3,549 | 3,533 | 0 | 16 | **Indeterminate (skips)** |
| **Nonbehavioral subtotal** | **5,655** | **5,639** | **0** | **16** | **Indeterminate** |
| `Strategos.Generators.Behavioral.Tests` | 94 | 94 | 0 | 0 | **Pass** |
| **Complete test aggregate** | **5,749** | **5,733** | **0** | **16** | **Indeterminate** |

The Contracts suite completed in 36.306 seconds. The generator suite ran with fixed maximum
parallelism four and completed in 49.351 seconds. It
includes the post-rebase ratchet fixes, AGWF044/045 `NotConfigurable` assertions, competing terminal
signal arrival orders, imported compensation-timeout rejection, and the rest of the final generator
corpus.

The behavioral suite ran through the supported rootless Podman host, completed 94/94 in 7m46.322s,
and left no residual containers. This is a same-tree pre-rebase local production-path pass; it is
neither a final-revision nor protected hosted result.

The sixteen skips retain their prior explicit classifications until a final complete matrix record
supersedes this placeholder:

- one source-declared `Strategos.Ontology.Generators.Tests` case remains skipped with reason
  `requires four-input fold`;
- fifteen `Strategos.Ontology.Npgsql.Tests` cases require `STRATEGOS_PG_TEST_CONN`, which was absent.

The Release build emitted 14 NU1900 warnings because the NuGet vulnerability feed could not be
reached. Compilation completed with zero errors; vulnerability-feed freshness remains
**Indeterminate** and is not a security-audit pass.

## Pending final-revision evidence

| Evidence | State | Required update |
|---|---|---|
| Final-revision build/test binding | **PENDING / INDETERMINATE** | Record whichever subject-sensitive build/test/static checks are rerun at `263cc57`; do not relabel `cdaa73a` process metadata. |
| Exact package and packed-consumer verification | **PASS** | At `263cc57`: 14 binary packages; legal build 0 warnings/errors; exclusive AONT216/AGWF041/AGWF044; exact digests in `final-artifact-checks.md`. |
| Basileus smoke | **PASS** | At `263cc57`: hermetic four-package source/restored-byte closure; 2 passed, 0 failed/skipped. |
| GitHub PR / required CI | **PENDING / INDETERMINATE** | Bind check URLs and conclusions to the exact PR head or merge subject. |
| Final review and merge authorization | **PENDING / INDETERMINATE** | Bind post-fix disposition and explicit authorization after the last product change. |

## Rebase and review-cycle corrections exercised locally

Rebasing onto the issue-167 PR head activated three existing ratchets. All three passed in the
1,915-test same-tree pre-rebase generator suite:

1. `CompletedStemSpelling_EveryEmitterFile_CitesSanctionedSourceOrIsAllowlisted` rejected free-hand
   completion-event stems in the compensation emitter. Events, workers, and saga handlers now derive
   rollback and legacy completion names through `NamingHelper.GetCompletedEventName`.
2. `UnvalidatedEntryPoints_PerFileCallSites_EqualRecordedCeilings` observed five legacy calls in
   `ConfidenceLoweringTests`, below the stale ceiling of six. The ceiling ratcheted down to five.
3. `StepConfigParity_OccurrenceMetadata_MatchesReflectedSurfaceAndNamesRunningProofs` found typed
   `Compensate(WorkflowActionReference)` missing from occurrence-metadata policy. It now names its
   running extraction and wire-round-trip proofs.

An independent review also found that AGWF044 and AGWF045 could be suppressed even though they are
proof-refutation outcomes. Both descriptors now carry Roslyn's `NotConfigurable` tag, and the shared
fail-closed descriptor fixture covers AGWF041 through AGWF045.

## Limits

These are same-tree pre-rebase local candidate results. They do not establish exact-final revision
test execution, protected CI, package publication,
CodeRabbit disposition, downstream Basileus/Exarchos adoption, merge, or post-merge main-branch
state. A later product commit invalidates this record until it is rebound and affected checks rerun.
