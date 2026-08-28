# compat-descriptorsource-handauthoredcontract-collapse — DescriptorSource=2 is ordinal-additive and loses its identity on merge

| | |
|---|---|
| **Claim** | `DescriptorSource.HandAuthoredContract = 2` must not move `HandAuthored = 0` or `Ingested = 1`. After `MergeTwo`, a contract-authored descriptor is recorded as `HandAuthored`. Call sites that test `== HandAuthored` do not treat `2` as hand. |
| **Scope** | Published public enum `Strategos.Ontology.Descriptors.DescriptorSource` and every merge / AONT / graph-builder consumer of `Source`. |
| **Consequence** | A caller who persists or switches on `Source` after merge never observes `2`. A caller who wrote an exhaustive two-member switch fails to compile. A caller who compares `== HandAuthored` for AONT201/203/204 skips contract-authored properties and links. Docs edited in this diff still list two members, so the additive member is invisible on the pages that describe the contract. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `DescriptorSourceTests` (ordinals 0/1/2, default 0) and `HandAuthoredContractMergeTests` (actions survive; `position.Source == HandAuthored` at `:87`). No production assignment of `2` exists in-repo. |
| **Why not cheaper** | Ordinals are a compiler/enum fact (rung 2) and the tests already lock them. The compatibility claim that matters is post-merge identity and the unwidened `== HandAuthored` branches — those are runtime lattice rules the type system allows (`2` is a valid `DescriptorSource`). Generation does not own this enum. |
| **Failure signal** | Nothing. Collapse is the implemented lattice (`MergeTwo.cs:67`). A consumer who expected `2` to survive merge sees `0` and has no diagnostic. |
| **Rollback** | Revert `662f0d1`. If anyone has already persisted `Source = 2`, revert is a read-compat event for that store. No in-repo persistence of `Source` was found. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high for ordinals, default, merge collapse, and unwidened `== HandAuthored` sites. high that in-repo production never assigns `2`. |

**Compatibility class:** extended enum (additive, explicit ordinals) plus a default-preserving merge that erases the new member; exhaustive-consumer break; docs still two-valued.

**Impact**

- Additive member. `Ingested` stays `1`. Default stays `HandAuthored` (`DescriptorSource.cs:40-63`; property defaults on `ObjectTypeDescriptor` / `PropertyDescriptor` / `LinkDescriptor`).
- Not a removed/renamed field. Not a serialization-format change of the enum itself (no `[JsonConverter]` on `DescriptorSource`; it is a CLR field on in-memory descriptors).
- Semantic collapse: `MergeTwo.Merge` always writes `Source = DescriptorSource.HandAuthored` (`MergeTwo.cs:19`, `:67`). CHANGELOG says contract-authored *actions* survive merge (`CHANGELOG.md:193-194`); the test asserts that and also asserts `Source == HandAuthored` (`HandAuthoredContractMergeTests.cs:84-87`).
- Unwidened equality (treat `2` as non-hand):
  - `OntologyGraphBuilder.cs:330` — AONT201/202 only walk `property.Source != HandAuthored` → continue, so contract-authored properties are skipped.
  - `:409` — AONT203 hand set is `Source == HandAuthored` only.
  - `:566` — AONT204 hand-reference collection skips any descriptor whose `Source != HandAuthored`.
- Widened: `OntologyBuilder.IsHandSide` is `HandAuthored or HandAuthoredContract` (`OntologyBuilder.cs:164-165`); `IngestedIntentInvariant` skips AONT205 unless `Ingested` (`IngestedIntentInvariant.cs:22-24`).
- Docs still two members: `docs/.../api/source.md:63-66`, `docs/.../ontology-sources.md:40-43`. `polyglot-descriptors.md:35` still says Source is HandAuthored or Ingested.

**Reverse dependency closure:**

1. Public API: `src/Strategos.Ontology/PublicAPI.Unshipped.txt:328` adds `HandAuthoredContract = 2`. `PublicAPI.Shipped.txt` is empty (one blank line). The live RS0016 surface is Unshipped.
2. Fluent / descriptor authors; TypeSpec/JSON contract ingest (no in-repo producer assigns `2`).
3. `MergeTwo`, `OntologyBuilder.TryCrossProvenanceMerge`, `OntologyGraphBuilder` AONT201–205, `IngestedIntentInvariant`.
4. Tests that construct `HandAuthoredContract` (`HandAuthoredContractMergeTests`, `AONT205Tests`, `IOntologyBuilderInvariantTests`).
5. Out-of-repo: any consumer with `switch (source)` over the old two members (compile break); any store that serialized `Source` as an int (new value 2).

**Persisted data older code must read:** no in-repo writer of `Source` to a store was found. If an out-of-repo graph snapshot used numeric enums, `2` is a new token; default `0` is unchanged. Older code that does not know `2` still reads `0` and `1`.

**Reverses?** Source: yes. Published enum value `2` if any consumer compiled against this package: removing it is a break. Merge collapse reverses with the revert (contract-authored actions would again be subject to whatever AONT205 did before the retarget).

**Open questions:**

- Is `HandAuthoredContract` assigned by any out-of-repo producer (Exarchos TypeSpec ingest, a private ontology)? If no, `2` is an unreached public member and the collapse is latent. If yes, those producers already depend on merge writing `0`.
- Do any consumers persist `ObjectTypeDescriptor` (or a DTO of `Source`) across process restarts? That would make `2` a stored-data event.

**What is expensive to find again**

AONT205 retarget is `!= Ingested`, not `== HandAuthoredContract`. The new member is a hole in the `== HandAuthored` scans, not a third lattice side. Merge comments at `MergeTwo.cs:19` still say “always HandAuthored — hand wins on composition.”
