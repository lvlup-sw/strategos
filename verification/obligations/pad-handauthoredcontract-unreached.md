# pad-handauthoredcontract-unreached

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 32–35, 56, 57, 76, 77, 128–133

| | |
|---|---|
| **Claim** | `DescriptorSource.HandAuthoredContract = 2` is the TypeSpec/JSON contract authoring surface; AONT205 retargets to mechanical ingestion so contract-authored actions survive graph merge. |
| **Scope** | Enum; `IngestedIntentInvariant`; `MergeTwo.Merge`; production assignment sites. |
| **Consequence** | The additive member exists and tests construct it. Nothing in production sets `Source = HandAuthoredContract`. Merge then writes `Source = HandAuthored`. The CHANGELOG “so TypeSpec / JSON contract-authored actions survive graph merge” names a path no producer enters. AONT205 skip-unless-Ingested *is* reached for `Ingested`. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | Assignment search: `HandAuthoredContract =` appears only at the enum member (`DescriptorSource.cs:63`). Tests assign `Source = HandAuthoredContract` in object initializers. |
| **Why not cheaper** | An unused enum member compiles (rung 2). Reachability of a provenance value is a graph property. |
| **Failure signal** | Nothing. The member looks shipped. Contract ingest that still stamps `Ingested` still fails AONT205 — the class this wave claimed to close. |
| **Rollback** | Revert the enum and the invariant widening. `Ingested = 1` does not move either way. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — no production assignment in this repository. |

**Open questions:**

- Is `HandAuthoredContract` assigned by any out-of-repo producer (Exarchos / TypeSpec ingest)? This repository has no such assignment. Stakes: if an external producer already stamps `2`, the invariant retarget is live and this obligation narrows to merge collapse (`MergeTwo.cs:67`). If not, the member is a declared control nothing makes reachable.

## Discriminating detail

Enum is additive (`DescriptorSource.cs:46-63`): `HandAuthored = 0`, `Ingested = 1`, `HandAuthoredContract = 2`. Inventory 32 / 56 / 128: **ordinal claim supported.**

AONT205 retarget (`IngestedIntentInvariant.cs:22-24`): `if (descriptor.Source != DescriptorSource.Ingested) return null;`. Inventory 33 / 129 / 132: **skip-unless-Ingested supported** when a descriptor arrives with `Source` set.

`MergeTwo.Merge` (`MergeTwo.cs:67`) always writes `Source = DescriptorSource.HandAuthored`. A `HandAuthoredContract` hand side loses its member on merge. Remarks at `:19` still say “always HandAuthored — hand wins.” Inventory 34 / 57 / 130 / 133 (“survives graph merge” as *Source identity*): **not supported.** Actions *payload* is taken from `hand.Actions` (`:78`), so a test-constructed contract descriptor’s actions survive as `HandAuthored` content.

`OntologyBuilder.IsHandSide` (`:164-165`) treats both hand values as the hand lattice side. That is the only production *recognition* of member 2.

Unwidened `== HandAuthored` / `!= HandAuthored` branches remain at `OntologyGraphBuilder.cs:330`, `:409`, `:566`. A live `HandAuthoredContract` descriptor would be skipped by AONT201/203/hand-reference collection.

`OntologyGraphBuilder.cs:477-496` freeze-time AONT205 uses `IngestedIntentInvariant` (retarget reached). Compile-time AONT205 (`OntologyDiagnosticIds.IngestedContributesToIntentOnly`) has no `ReportDiagnostic` site in this repository (survey). That is a second unreached control, pre-existing.

## Disposition

- Inventory 32, 56, 128 (additive `= 2`): **supported.**
- Inventory 33, 129, 132 (AONT205 only when Ingested): **supported as the scan.**
- Inventory 34, 57, 77, 130, 133 (contract-authored actions survive merge / TypeSpec-JSON path): **declared control with no production assignment; merge erases Source 2.**
- Inventory 35 (document which surface maps to which value): **not delivered** on the pages this diff edited — see `pad-descriptor-source-docs-omit-member-2`.
