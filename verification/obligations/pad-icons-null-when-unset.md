# pad-icons-null-when-unset

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 30, 31, 54, 124, 125, 146, 148, 152

| | |
|---|---|
| **Claim** | `OntologyToolDescriptor.Icons` stays null when unset — no placeholder icon. Discovery leaves it unset. |
| **Scope** | `OntologyToolDescriptor.Icons`; `ApplyIcons`; `OntologyToolDiscovery.Discover`; factory tests. |
| **Consequence** | A placeholder icon is worse than an absent one (INV-3). Clients would render a fake mark. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `CreateServerTools_PreservesOutputSchemaAndAnnotations` asserts `descriptor.Icons` and `protocolTool.Icons` are null for discovery-derived tools. `CreateServerTool_WithIcons_MapsOntoProtocolTool` covers the non-null map when a test constructs a descriptor. |
| **Why not cheaper** | Nullability of the property (rung 2) permits null; it does not forbid a placeholder assignment in `Discover`. That needs a call-site assertion or a structural ban on assignment. |
| **Failure signal** | Protocol `Tool.icons` present when no source supplied one. |
| **Rollback** | Remove the property. Protocol clients then see the pre-#177 omission (INV-3 previously flagged that gap). |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High for discovery-null. |

**Open questions:**

- None on production assignment: `rg 'Icons\s*='` in `src/` hits only tests and `ApplyIcons`’s write onto `options.Icons`. `Discover` never sets the property.

## Discriminating detail

Property (`OntologyToolDescriptor.cs:40-43`) is `IReadOnlyList<ToolIcon>?` with no default placeholder.

`ApplyIcons` (`OntologyServerToolFactory.cs:248-254`) returns when `icons is null` and does not invent a list.

Factory test (`OntologyServerToolFactoryTests.cs:59-61`) asserts both descriptor and protocol icons are null after `Discover` + `CreateServerTools`.

INV-3 check 3.5 (`deterministic-checks.md:126-133`) is `grep -L 'Icons' OntologyToolDescriptor.cs` — presence of the identifier, not the null-when-unset invariant. The checklist no longer flags a missing placeholder as a gap (inventory 31 / 55 / 152): **supported as a text change.**

## Disposition

Inventory 30, 54, 124, 125, 146, 148, 152: **supported.** Non-null path is test-only; that is the intended “source supplies none” production path.
