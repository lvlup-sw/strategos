# pad-hosting-pin-and-resulttype

Lens: Promise Against Delivery (inventory 2)
Revision: `324768f4d4f6d292e7d86045f711c6c50946b8c9` vs `4d060f4ca9e0c9844c2cd051965c217a3b0b4ffa`
Claims confronted: inventory 27, 28, 53, 66, 126, 127, 144, 145, 147, 149, 150, 151

| | |
|---|---|
| **Claim** | Hosting pins MCP SDK 2.2.0 so every constructed `CallToolResult` can set the 2026-07-28 `resultType` discriminator, and every construction sets it. |
| **Scope** | Hosting `VersionOverride`; `OntologyServerToolFactory` constructions; SDK wrap of the four discovered tools. |
| **Consequence** | A 1.3.0 compile would not have `ResultType`. A construction that omits it emits the pre-2026-07-28 shape INV-3 now calls HIGH. Clients that require the field reject the call. |
| **Proof rung** | Contract and component tests |
| **Proof artifact** | `TraversalToolHostingTests.AssertResultTypeComplete` (in-memory + wire JSON). `ProviderBoundDispatchTests.Query_ExecutesAgainstConfiguredProvider_ReturnsRealRows` asserts `ResultType` on `ontology_query`. INV-3 check 3.4 is a grep, not a CI binder observed in this run. |
| **Why not cheaper** | The pin is a csproj property. “Every construction assigns `ResultType`” is a set of call sites; a type cannot force an object initializer to include the property. Check 3.4 is the cheap structural lock and is checklist-only. |
| **Failure signal** | Protocol clients. Absent field is legal-as-complete for old clients; new clients that require the field fail the call. |
| **Rollback** | Revert the pin and the two assignments. Protocol clients that already expect `resultType` then see the old omission. |
| **Lenses** | Promise Against Delivery |
| **Confidence** | High for the two factory constructions and the 2.2.0 pin. Medium for “every” once SDK wrap of explore/action/validate is included. |

**Open questions:**

- Do `ontology_explore`, `ontology_action`, and `ontology_validate` emit `resultType` on the wire via the 2.2.0 wrap? Query is asserted. The other three return domain objects from handlers (`OntologyServerToolFactory.cs:155-225`) and rely on the SDK. Stakes: if wrap omits the field, INV-3 “every CallToolResult” is false for three of five tools.
- Is `ErrorResult` with `resultType: complete` protocol-legal? Factory comment (`:51-55`) asserts yes (`isError: true` is still a finished call). Not validated against the 2026-07-28 spec text in this lens.

## Discriminating detail

Hosting pin:

```18:20:src/Strategos.Ontology.MCP.Hosting/Strategos.Ontology.MCP.Hosting.csproj
    <!-- 1.3.0 (CPM pin) has no CallToolResult.ResultType; 2.2.0 is the first
         current line that exposes the 2026-07-28 protocol field (#176). -->
    <PackageReference Include="ModelContextProtocol" VersionOverride="2.2.0" />
```

CPM remains 1.3.0 (`src/Directory.Packages.props:52`). Tests project also overrides 2.2.0.

The only `new CallToolResult` / `new()` inferred as one in Hosting are `MapTraversalResult` (`:384-386`) and `ErrorResult` (`:410-412`). Both set `ResultType = CompletedResultType`.

Check 3.4 (`deterministic-checks.md:114-124`) greps that files mentioning `CallToolResult` mention `ResultType`. A comment can satisfy it. Hosting pack tests do not assert the 2.2.0 pin.

## Disposition

- Inventory 27, 53, 126, 145, 147, 151: **supported** for factory-constructed results and for `ontology_query` wrap.
- Inventory 28 (round-trip at MCP boundary): **supported** for traverse (`AssertResultTypeComplete` serializes and deserializes).
- Inventory 150 (Hosting included in check 3.3 so a pre-2026 shape cannot be reintroduced): **checklist text, not a running binder.**
- “Every” across all five tools: **partial** — three tools unasserted.
