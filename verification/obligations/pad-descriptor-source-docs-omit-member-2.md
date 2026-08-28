# pad-descriptor-source-docs-omit-member-2

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 35

| | |
|---|---|
| **Claim** | Document which authoring surface maps to which `DescriptorSource` value. |
| **Scope** | Docs this wave edited: `docs/src/content/docs/reference/ontology/api/source.md`, `docs/src/content/docs/guide/ontology/ontology-sources.md`. |
| **Consequence** | A reader of the pages this diff touched still sees a two-member enum. Contract authoring is undocumented on the provenance list. Authors who follow the pages stamp `Ingested` and still fail AONT205. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The two lists. No test asserts the third member appears on those pages. |
| **Why not cheaper** | Documentation is not a type. A grep lock could require `HandAuthoredContract` on those pages; none exists. |
| **Failure signal** | Nothing. Humans read a two-member enum. |
| **Rollback** | Add the third bullet. The enum value remains. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High — both pages list two members. |

**Open questions:**

- None on the edited provenance lists. `DescriptorSource.cs` remarks *do* document all three members (`:12-32`). The plan asked for authoring-surface mapping in docs; the code comment is not that page.

## Discriminating detail

`source.md:65-66`:

- `HandAuthored` — declared via the `DomainOntology` builder DSL.
- `Ingested` — emitted by an `IOntologySource` implementation; `SourceId` is also set.

`ontology-sources.md:42-43`: the same two bullets (`DescriptorSource.HandAuthored` / `DescriptorSource.Ingested`). AONT205 sentence at `:47` still says ingested must leave Actions/Events/Lifecycle empty and does not name `HandAuthoredContract`.

This wave’s T5 / T6 doc commits edited other ontology pages (CLR-free path, Requires obsolete). The provenance lists on pages in the same cluster were not updated.

## Disposition

Inventory 35: **claimed and not implemented** on the provenance pages this corpus still publishes.
