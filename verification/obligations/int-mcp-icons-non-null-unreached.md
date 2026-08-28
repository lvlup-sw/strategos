# int-mcp-icons-non-null-unreached

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | The non-null `OntologyToolDescriptor.Icons` → protocol `Tool.icons` mapping that Hosting tests exercise is reachable from the public composition root (`AddOntologyTools` / `CreateServerTools`). |
| **Scope** | `OntologyServerToolFactory`, `OntologyToolDiscovery`, public `OntologyToolDescriptor.Icons` / `ToolIcon` surface. |
| **Consequence** | Clients speaking 2026-07-28 never receive `Tool.icons` through shipped hosting. The mapping exists, is packed, and is proven on an internal seam. A host cannot supply icons to the factory that actually registers tools. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A composition-graph check: every `Icons =` assignment in `src/` that is not a test must flow into `CreateServerTools`, *or* `CreateServerTools` must accept a consumer descriptor list. |
| **Why not cheaper** | The type system permits null and non-null. `ApplyIcons` compiling does not prove a producer. The existing factory test calls internal `CreateServerTool` with a hand-built descriptor. |
| **Failure signal** | Nothing. `list/tools` omits icons. No host-side error. |
| **Rollback** | Revert `Icons` / `ApplyIcons`. Removes the dark mapping; does not create a producer. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Is consumer-supplied `Icons` intended for a future `CreateServerTools` overload? If yes, the current public property is a stub and the internal test is a preview, not shipped wiring.
- Does any out-of-repo host construct `OntologyToolDescriptor` with icons and call internal `CreateServerTool` via InternalsVisibleTo? This repo's InternalsVisibleTo is the test assembly only.

**Confidence:** high.

## What led here

Production-path survey §4b. CHANGELOG Residue claims "`OntologyToolDescriptor.Icons` stays null when unset." That null path is reached and is the INV-3 invariant. This obligation is the other half: tests reach the non-null mapping; shipped wiring cannot.

Competing explanation: `CreateServerTools` accepts host descriptors, or `Discover` sets icons from the graph. Both are false.

## Composition

Public root: `AddOntologyTools` → `OntologyServerToolFactory.CreateServerTools` (`OntologyMcpServerBuilderExtensions.cs:28`, `:54`; factory `:74-93`).

`CreateServerTools` is the only public factory. Signature is `(OntologyGraph graph)` only. It always builds descriptors via `new OntologyToolDiscovery(graph).Discover()` (`:78-80`) and then `CreateServerTool` (`:85`).

`Discover` builds four descriptors (`OntologyToolDiscovery.cs:31-43`, `:48-116`). None set `Icons`. Default is null (`OntologyToolDescriptor.cs:43`).

`CreateServerTool` (`:113`, internal) calls `ApplyIcons(options, descriptor.Icons)` (`:130`). `ApplyIcons` (`:249-262`) returns immediately when `icons is null` (`:251-254`). That early-return is the shipped `list/tools` effect: `ProtocolTool.Icons` unset on all four discovered tools.

`CreateTraverseTool` (`:293-311`) never calls `ApplyIcons`. Traverse has no icons slot.

Factory remarks on `CreateServerTools` (`:60-62`) say the adapter preserves "icons". Preservation of a field `Discover` never populates is vacuous.

## Path tests reach that shipping does not

`OntologyServerToolFactoryTests.CreateServerTool_WithIcons_MapsOntoProtocolTool` (`:66-91`) constructs `Icons = [icon]` and calls **internal** `CreateServerTool`, not `CreateServerTools` / `Discover` / `AddOntologyTools`.

`OntologyToolDescriptorTests` (`:33`) uses `with { Icons = [icon] }` on a record the test built.

The only production `Icons =` assignment in `src/` is `options.Icons =` inside `ApplyIcons` (`OntologyServerToolFactory.cs:256`). No production `descriptor.Icons =` / `Icons = [` exists outside tests.

`ToolIcon` is on `PublicAPI.Unshipped.txt:194-203`. Presence of the type is not reachability.

## Why cheaper rungs fail

- **Rung 1:** no generated tool catalog that includes icons.
- **Rung 2:** `IReadOnlyList<ToolIcon>?` makes the unreached state representable. Null-when-unset is the type default.
- **Rung 4:** the mapping test is the wrong subject (internal seam + fixture descriptor).

## Failure scenario

A host wants 2026-07-28 `Tool.icons` on `ontology_query`. They set `OntologyToolDescriptor.Icons` on a descriptor they built. `CreateServerTools` never reads that instance. `list/tools` stays icon-less. The Hosting test that "proves" mapping stays green.

## Code read (this revision)

- `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs:60-93`, `:113-130`, `:249-262`, `:293-311`
- `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs:31-116`
- `src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs:39-43`
- `src/Strategos.Ontology.MCP.Hosting.Tests/OntologyServerToolFactoryTests.cs:59-91`
- `CHANGELOG.md:187-190`

### Investigation Log

#### Does Discover or CreateServerTools set Icons?

- Read: `Discover` four `Build*Descriptor` methods; `CreateServerTools`; `rg 'Icons\s*='` under `src/`.
- Found: production assignment only at `ApplyIcons` options mapping. Descriptor-level assignment only in tests.
- Not found: a public factory overload that accepts consumer descriptors.
- Conclusion: non-null path unreached through shipped public composition.
