# wild-handauthoredcontract-lost-at-merge

`DescriptorSource.HandAuthoredContract = 2` exists so post-merge readers can tell contract hand from fluent hand and so AONT205 can skip both. `MergeTwo` always writes `Source = HandAuthored`. After one merge the new ordinal is unreadable. Tests assert that collapse. Nobody states that the three-way policy must survive merge.

## What led here

Residue (`CHANGELOG.md:192-194`) and the enum remarks (`DescriptorSource.cs:56-61`) say contract-authored intent is first-class and survives graph merge; AONT205 retargets to mechanical ingestion.

`MergeTwo` remarks state the lattice rule: `Source` is always `HandAuthored` — "hand wins on composition" (`MergeTwo.cs:19`). The implementation assigns `Source = DescriptorSource.HandAuthored` (`:67`). Intent collections are copied from hand (`:78`). Actions survive; provenance does not.

`HandAuthoredContractMergeTests` asserts `position.Source == HandAuthored` after merge (`HandAuthoredContractMergeTests.cs:87`) and that actions remain. The test locks the erasure.

`IngestedIntentInvariant` skips unless `Source == Ingested` (`IngestedIntentInvariant.cs:22-25`). After merge, a contract-authored descriptor is indistinguishable from fluent hand. AONT205 cannot fire on it either way. Any later reader of `Source` that wanted the three-way split sees two values.

No production assignment of `HandAuthoredContract` was found; every `Source = DescriptorSource.HandAuthoredContract` is a test fixture. Even if an out-of-repo producer stamps `2`, the first `MergeTwo` with ingested structure makes it `0`.

## Failure scenario

A TypeSpec / JSON contract producer (the surface the enum remarks name) stamps `HandAuthoredContract` and merges with an ingested structural contribution. A later policy that must treat contract-authored graphs differently from fluent `DomainOntology.Define()` — audit, emit, or a future AONT — reads `HandAuthored` and takes the fluent path. The three-way split exists only between construction and the first merge.

The merge test stays green: it requires the collapse.

## Code paths read (rev `324768f`)

- `src/Strategos.Ontology/Descriptors/DescriptorSource.cs:27-31`, `:56-63`
- `src/Strategos.Ontology/Merge/MergeTwo.cs:19`, `:24-34`, `:67`, `:78`
- `src/Strategos.Ontology/Internal/IngestedIntentInvariant.cs:5-25`
- `src/Strategos.Ontology.Tests/Merge/HandAuthoredContractMergeTests.cs:84-87`
- `CHANGELOG.md:192-194`

## Why not cheaper

- **Rung 1.** `Source` after merge could be derived from the hand-side ordinal. It is overwritten with a literal `0`.
- **Rung 2 is the cheapest sound rung.** If `MergeTwo` preserved the hand-side `Source` when it is `HandAuthored` or `HandAuthoredContract`, the three-way split would remain representable. Today's assignment makes `HandAuthoredContract` unrepresentable on a merged descriptor. A test that asserts the collapse is the opposite of this obligation.
- A cheaper rung than 2 does not exist for "this enum value must remain observable." Generation is not involved.

The current code proves the contrary at rung 4 (the merge test). That is not a cheaper proof of this claim; it is a proof of the violating lattice rule.

## What is expensive to find again

CHANGELOG and the enum remarks say "survives graph merge." Merge remarks say "Source always HandAuthored." Both are true of different fields (actions vs provenance). The merge test reads as coverage of #163. It is coverage of the collapse.

## Open questions

- Does any out-of-repo TypeSpec / JSON ingest already stamp `HandAuthoredContract`? If yes, this obligation is a present compatibility event for that producer the moment they merge. If no, the ordinal is still unobservable after merge the first time a producer appears; #163 remains the inert-enum issue 185 named, plus a merge rule that would keep it inert.
- Is the lattice rule "Source always HandAuthored" the intended published contract, and the enum remarks the drift? If yes, the obligation moves: the remarks and Residue must not claim provenance survives. The three-way split then has no post-merge reader, and `HandAuthoredContract` is a pre-merge-only tag.
