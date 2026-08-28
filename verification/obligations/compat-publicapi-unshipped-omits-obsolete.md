# compat-publicapi-unshipped-omits-obsolete — PublicAPI tracks additive members and cannot see Obsolete

| | |
|---|---|
| **Claim** | RS0016/RS0017 must keep every new public member of this diff on `PublicAPI.Unshipped.txt`, and they cannot be the proof that `Requires` is obsolete or that `HandAuthoredContract` / `ToolIcon` shipped. |
| **Scope** | `src/Strategos.Ontology/PublicAPI.Unshipped.txt`, `src/Strategos.Ontology.MCP/PublicAPI.Unshipped.txt`, and the PublicApiAnalyzers bootstrap on those projects (`RS0016`/`RS0017` only; `RS0026`/`RS0027` suppressed). |
| **Consequence** | A reviewer who treats Unshipped as the compatibility ledger will see additive Icons / ToolIcon / `HandAuthoredContract = 2` and will not see that `Requires` changed. Dropping `[Obsolete]` does not fail RS0016. Moving a member to `Shipped.txt` never happened — both `PublicAPI.Shipped.txt` files are empty — so “unshipped” is not “not yet in the wild.” |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | The Unshipped diffs at this revision, plus a check that Obsolete attributes on listed members are recorded (the current analyzer does not do that). Hosting’s comment at `Strategos.Ontology.MCP.Hosting.csproj:23-30` states the DECLARE/REMOVE scope. |
| **Why not cheaper** | PublicAPI files are not generated from the types. The compiler will compile an Obsolete method that is missing from Unshipped only if RS0016 is off. The missing Obsolete column is a graph/metadata fact, not a type-shape fact. |
| **Failure signal** | RS0016/RS0017 on add/remove. Nothing on attribute changes. |
| **Rollback** | Revert the Unshipped lines. Does not reverse packages already compiled against those members. |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high. |

**Compatibility class:** additive PublicAPI entries; tracking gap for deprecation; Shipped baseline empty.

**This revision’s Unshipped delta**

- Ontology: `DescriptorSource.HandAuthoredContract = 2` inserted between `HandAuthored = 0` and `Ingested = 1` (`PublicAPI.Unshipped.txt:327-329`). Requires line `:109` unchanged (no Obsolete suffix).
- MCP: `OntologyToolDescriptor.Icons` get/init; new `ToolIcon` type and members (`PublicAPI.Unshipped.txt:127-128`, `:194-203`).
- No removals. No signature changes. Hosting Unshipped `CreateServerTools` signature unchanged.

**Reverse dependency closure:**

1. PublicApiAnalyzers on Ontology, Ontology.MCP, Ontology.MCP.Hosting.
2. Every obligation on those surfaces (`compat-requires-obsolete-warning-break`, `compat-descriptorsource-handauthoredcontract-collapse`, `compat-mcp-resulttype-icons-wire`).
3. Reviewers and later verification runs that treat Unshipped as the shipping ledger.

**Reverses?** The text files reverse by revert. Analyzer history does not. Empty `Shipped.txt` means there is no prior shipped snapshot to restore.

**Open questions:**

- Is leaving everything on Unshipped a standing repo convention, or an unfinished bootstrap? If convention, “unshipped” must not be read as “unstable / not published.” If unfinished, the first move to Shipped will look like a mass API freeze that this diff is not.

**What is expensive to find again**

Hosting csproj `:27-30` says backcompat overload opinions (RS0026/RS0027) are out of scope. That sentence is the reason an added optional property on a record does not get an overload-design review from the analyzer.
