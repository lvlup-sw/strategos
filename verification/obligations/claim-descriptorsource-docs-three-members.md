# claim-descriptorsource-docs-three-members — Docs name which authoring surface maps to which DescriptorSource

Lens: 6 Claim Derivation
Disposition: unsupported-claim finding
Inventory claims: 35
Confidence: high that the documentation claim is not exhibited on the pages this diff edited

## Finding

Claim 35 (plan T5): "Document which authoring surface maps to which value."

This wave adds `HandAuthoredContract = 2`. The competing explanation: the plan states the docs work, and the pages this diff edited still describe a two-member enum.

Survey backbone §10: DescriptorSource docs on pages this diff edited still list two members (`source.md:63–66`, `ontology-sources.md:40–43`). Nothing in the survey exhibits a three-member mapping table (HandAuthored / Ingested / HandAuthoredContract) on those surfaces.

Code comments on `DescriptorSource.cs` (claims 128–131) describe the three values. Those are not the authoring-surface documentation claim 35 asked for.

## Ledger (for the claim that failed promotion)

| | |
|---|---|
| **Claim** | Documentation states which authoring surface maps to `HandAuthored`, `Ingested`, and `HandAuthoredContract`. |
| **Scope** | Ontology reference/guide pages this diff edited; `DescriptorSource` consumer docs. |
| **Consequence** | Authors cannot tell whether TypeSpec/JSON ingest is `Ingested` or `HandAuthoredContract`. That is the same producer question as `claim-handauthoredcontract-ingest-assignment`. |
| **Proof rung** | (none — unsupported) |
| **Failure signal** | Nothing. |
| **Rollback** | Not applicable. |
| **Lenses** | 6 Claim Derivation. Survey lens 3. |

**Open questions:**

- Did any page this lens was not allowed to search document the third member? This derivation lens did not search the docs tree beyond the survey citation. An investigator should read `source.md` and `ontology-sources.md` at the cited lines and any sibling page this diff added.

Line anchors from survey at revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`: `docs/src/content/docs/reference/ontology/api/source.md:63–66`; ontology-sources.md:40–43.
