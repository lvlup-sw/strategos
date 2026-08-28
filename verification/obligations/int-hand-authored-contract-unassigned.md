# int-hand-authored-contract-unassigned

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | A shipped `DescriptorSource` member that AONT205 and merge treat as first-class has a production assignment site, or it is not a live composition output. |
| **Scope** | `DescriptorSource.HandAuthoredContract = 2`, ontology builder / graph-builder / `MergeTwo`, in-repo `IOntologySource` implementations. |
| **Consequence** | The new enum member exists on PublicAPI and in tests. Shipped composition never produces `2`. Merge restamps object `Source` to `HandAuthored`. AONT205 retarget (skip unless `Ingested`) is live; the value it was added to protect is test-invented. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | An assignment-site scan: production `src/` (exclude `*Tests*`) must contain `Source = DescriptorSource.HandAuthoredContract` or an equivalent cast of `2`, *or* a documented out-of-repo producer must be named and bound. |
| **Why not cheaper** | Adding an enum member is a compiler fact. Tests that construct `Source = HandAuthoredContract` prove the skip path, not a producer. |
| **Failure signal** | Nothing. Callers observe `HandAuthored` (0) or `Ingested` (1). |
| **Rollback** | Revert the enum member. Published `= 2` is a compatibility event if any out-of-repo producer already emitted it. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Is there an out-of-repo TypeSpec/JSON ontology ingest (Exarchos or similar) that is supposed to set `HandAuthoredContract = 2`? Nothing in this repository does. If that producer does not exist, the member is a stub. If it does, merge restamp and unwidened `== HandAuthored` branches still drop or misclassify `2`.

**Confidence:** high for in-repo composition. Out-of-repo producer is unknown.

## What led here

Production-path survey §5b. CHANGELOG Residue claims `HandAuthoredContract` was appended as `2` and "AONT205 retargets to mechanical ingestion, so TypeSpec / JSON contract-authored actions survive graph merge."

Competing explanation: `ObjectTypeFromDescriptor` or a contracts-package ingest sets `2`. `ObjectTypeFromDescriptor` preserves whatever `Source` the caller set. Nothing in-repo sets it.

## Composition

Enum member: `DescriptorSource.cs:63`. Remarks (`:28-31`, `:56-61`) name TypeSpec / JSON contract authoring in `Strategos.Contracts` (`op`, `interface`, `extern dec`) as the producer. Default on `ObjectTypeDescriptor.Source` is `HandAuthored`. PublicAPI: `PublicAPI.Unshipped.txt:328`.

`rg 'Source\s*=\s*DescriptorSource\.HandAuthoredContract'` hits **only** tests:

- `AONT205Tests.cs:213`
- `HandAuthoredContractMergeTests.cs:36`
- `IOntologyBuilderInvariantTests.cs:204`

No production assignment in `src/` outside tests. This repo has no `IOntologySource` implementation except test doubles (`TestOntologySource`, per-test nested types). `Strategos.Contracts` has no `DescriptorSource` assignment.

AONT205 skip-unless-Ingested **is** reached (`IngestedIntentInvariant.cs:22-25`; `OntologyBuilder.cs:202,207,263-276`; freeze scan `OntologyGraphBuilder.cs:485-504`). `IsHandSide` treats `HandAuthored` **or** `HandAuthoredContract` as the hand lattice (`OntologyBuilder.cs:164-165`).

`MergeTwo.Merge` restamps the merged object to `Source = DescriptorSource.HandAuthored` (`MergeTwo.cs:67`). Actions still come from the hand side (`:78`). A descriptor that entered as `2` leaves merge as `0` at object level. Merge tests assert `position.Source == DescriptorSource.HandAuthored` (`HandAuthoredContractMergeTests.cs:87`).

Unwidened `== HandAuthored` / `!= HandAuthored` branches remain at `OntologyGraphBuilder.cs:330`, `:409`, `:566`. A live `2` would skip AONT201 property-lattice membership, AONT203 hand-prop collection, and hand-referenced-type collection.

## Path tests reach that shipping does not

Runtime AONT205 tests and merge tests construct `Source = HandAuthoredContract` in the fixture. That is how they prove the retarget negative (contract + Actions does not throw). The shipped builder never hands them that value.

## Why cheaper rungs fail

- **Rung 1:** enum is hand-authored, not generated from an ingest catalog.
- **Rung 2:** `= 2` is representable. Nothing forces a producer.
- **Rung 4:** existing tests are the wrong subject (they *are* the producer).

## Failure scenario

A TypeSpec/JSON contract author expects provenance `2` and AONT205 exemption as a distinct source. In-repo fluent/merge paths never stamp `2`. After merge the object reads as `HandAuthored`. Downstream `== HandAuthored` checks either accept it (after restamp) or, if someone did stamp `2` and skipped merge, exclude it from hand-side scans.

## Code read (this revision)

- `src/Strategos.Ontology/Descriptors/DescriptorSource.cs:1-64`
- `src/Strategos.Ontology/Internal/IngestedIntentInvariant.cs:18-25`
- `src/Strategos.Ontology/Builder/OntologyBuilder.cs:164-165`, `:195-207`, `:242-276`
- `src/Strategos.Ontology/Merge/MergeTwo.cs:52-78`
- `src/Strategos.Ontology/OntologyGraphBuilder.cs:330`, `:409`, `:484-504`, `:566`
- `src/Strategos.Ontology.Tests/Merge/HandAuthoredContractMergeTests.cs:36`, `:87`
- `CHANGELOG.md:192-194`

### Investigation Log

#### Does any in-repo production path assign HandAuthoredContract?

- Read: `rg HandAuthoredContract` and `rg 'Source\s*=\s*DescriptorSource\.HandAuthoredContract'` under `src/`, excluding bin/obj.
- Found: enum, docs, `IsHandSide`, invariant comments, PublicAPI, tests.
- Not found: a production assignment. No non-test `IOntologySource`.
- Conclusion: unreached in shipped in-repo composition. Out-of-repo producer remains an open question.
