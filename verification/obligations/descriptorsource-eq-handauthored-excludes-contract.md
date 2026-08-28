# descriptorsource-eq-handauthored-excludes-contract

Lens: **3. Representable Invalid States**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Claim

A descriptor or field whose `Source` is `HandAuthoredContract` (2) must not be treated as “not hand-authored” by `== DescriptorSource.HandAuthored` branches, and a merge must not collapse 2 to `HandAuthored` (0) while leaving nested `Source` values at 2.

## What led here

The enum was widened by an additive member `HandAuthoredContract = 2`. Consumers that still compare to the old two-value set now have a third representable value that means different things in different places. Survey backbone item 6; wildcard W4. Docs edited in this diff still list two members.

## Code at this revision

**Widened type**

- `src/Strategos.Ontology/Descriptors/DescriptorSource.cs:40-63` — `HandAuthored = 0`, `Ingested = 1`, `HandAuthoredContract = 2`. Default on `ObjectTypeDescriptor` / `PropertyDescriptor` / `LinkDescriptor` remains `HandAuthored`.
- No production assignment of `2` in `src/Strategos.Ontology` (every `Source = HandAuthoredContract` is a test fixture). The value is still representable via `ObjectTypeFromDescriptor` / object initializers.

**Unwidened equality (treats 2 as not-hand)**

- `src/Strategos.Ontology/OntologyGraphBuilder.cs:330` — AONT201/202 loop: `if (property.Source != DescriptorSource.HandAuthored) continue;`. A property tagged `2` is skipped.
- `src/Strategos.Ontology/OntologyGraphBuilder.cs:409` — AONT203 hand set: `.Where(p => p.Source == DescriptorSource.HandAuthored)`. Contract-authored properties are not “hand.”
- `src/Strategos.Ontology/OntologyGraphBuilder.cs:566` — `CollectHandReferencedTypeNames`: `if (descriptor.Source != DescriptorSource.HandAuthored) continue;`. An unmerged contract descriptor’s links are not hand references for AONT204.

**Widened correctly (0 and 2 are the hand side)**

- `src/Strategos.Ontology/Builder/OntologyBuilder.cs:164-165` — `IsHandSide` is `HandAuthored or HandAuthoredContract`. Cross-provenance merge *is* reached for `2`.
- `src/Strategos.Ontology/Internal/IngestedIntentInvariant.cs:22-25` — AONT205 skip-unless-`Ingested`. Value `2` passes through (intentional per enum remarks).

**Merge collapse (2 → 0 on the descriptor; nested Source preserved)**

- `src/Strategos.Ontology/Merge/MergeTwo.cs:19, 67` — `Source = DescriptorSource.HandAuthored` always on the merged *descriptor*.
- `src/Strategos.Ontology/Merge/MergeTwo.cs:114-119` — hand properties “pass through untouched (preserving their original Source).” A property with `Source = 2` stays `2` under a parent whose `Source` is now `0`.
- `src/Strategos.Ontology.Tests/Merge/HandAuthoredContractMergeTests.cs:87` — asserts the collapse: `position.Source == HandAuthored`.

**Docs still two-valued**

- `docs/src/content/docs/reference/ontology/api/source.md:63-66`
- `docs/src/content/docs/guide/ontology/ontology-sources.md:40-43`

## Failure scenario

1. **Unmerged contract descriptor** (`ObjectTypeFromDescriptor` with `Source = 2`, no ingest). AONT201/203/204 hand-side scans skip it. Contract-authored properties that disagree with a later ingest are invisible until someone merges (at which point the parent becomes `0` and property-level `2` is still skipped by `:330`).
2. **Merged contract + ingest.** Parent `Source` is `0`. A later reader cannot tell fluent hand from contract hand. CHANGELOG / enum remarks say contract-authored intent “survives graph merge.” Actions survive (`MergeTwo.cs:78`); provenance does not. Nested properties may still say `2` while the parent says `0` — a contradictory pair on one object graph.

## Why not cheaper

Rung 1: generate match arms / a `IsHandSide` helper from the enum. Situational: no such generator. `OntologyBuilder.IsHandSide` already exists and is not used by `OntologyGraphBuilder`.

Rung 2: exhaustive switch on `DescriptorSource` (or a shared `IsHandSide` used everywhere). `== HandAuthored` is a loosened comparison after an additive enum. Do not test each call site.

Rung 2 also owns the merge stamp: `Source` on the merged descriptor should be a value the type can still distinguish, or nested fields should be restamped with the parent. Unrestricted `Source = HandAuthored` is field assignment that erases 2.

## Failure signal

Nothing, unless an AONT201/203/204 test is written against a `Source = 2` property. Current merge tests assert the collapse. “Nothing” in production; the enum value has no production writer.

## Rollback

Revert the enum member. Value `2` if already published is a compatibility event (stage0 S4). Restoring `== HandAuthored` as the only hand check does not need a revert if `2` is never assigned.

## Open questions

- Is `HandAuthoredContract` assigned by any out-of-repo producer (Contracts ingest, Exarchos, a private source)? If no producer exists, 2 is a compile-time constant plus test fixtures and the unwidened branches are latent. If a producer exists, AONT201/203/204 are already wrong for that graph.
- After merge, is parent `Source = HandAuthored` the intended lattice identity (hand wins on composition) or a leftover from the two-value enum? If intended, the obligation narrows to “nested `Source` must not remain 2 under a parent 0” and “unmerged 2 must use `IsHandSide`.” If not intended, the stamp at `MergeTwo.cs:67` is the defect.

## What is expensive to find again

`IsHandSide` and `!= HandAuthored` coexist in one component. A reader of `OntologyBuilder` concludes the three-way split is wired. A reader of `OntologyGraphBuilder` sees a two-way split. Merge tests lock the collapse as success.
