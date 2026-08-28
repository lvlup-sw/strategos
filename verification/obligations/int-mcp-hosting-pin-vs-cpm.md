# int-mcp-hosting-pin-vs-cpm

Lens: **4. Integration Completeness**. Revision `324768f4d4f6d292e7d86045f711c6c50946b8c9`.

## Ledger

| | |
|---|---|
| **Claim** | Hosting's 2026-07-28 `resultType` capability (factory `CallToolResult` constructions *and* the four-tool SDK wrap) is bound to a locked MCP SDK version. CPM and sibling packages cannot silently bind 1.3.0 for that composition. |
| **Scope** | `Strategos.Ontology.MCP.Hosting.csproj` `VersionOverride="2.2.0"`, `Directory.Packages.props` `ModelContextProtocol` 1.3.0, `Strategos.Agents.Mcp` (no override), Hosting tests (own override), factory `ResultType` sites. |
| **Consequence** | Four discovered tools never assign `ResultType` in this repo. Their wire `resultType` is an SDK 2.2.0 wrap. CPM still publishes 1.3.0 (no `CallToolResult.ResultType`). Hosting tests pin 2.2.0 independently, so they cannot fail a Hosting pin drop if constructions are also removed. `Agents.Mcp` already binds 1.3.0. |
| **Proof rung** | Deterministic structural analysis |
| **Proof artifact** | A pin-graph check: Hosting (and only Hosting, if that is the contract) must keep `VersionOverride` ≥ the first SDK that exposes `ResultType`, *or* CPM must move and every MCP-shipping project must take it. Pack/csproj tests must read the production pin, not the test pin. |
| **Why not cheaper** | The compiler enforces `ResultType` on Hosting's own constructions only while the override exists. Types in tests come from the test project's own 2.2.0 override (`Hosting.Tests.csproj:11`). No generator pins SDK versions. |
| **Failure signal** | Nothing in CI. A client that requires 2026-07-28 `resultType` sees an older shape. INV-3 check 3.4 would catch a missing assignment *if someone ran it* (see `int-inv-3-checks-not-in-ci`). |
| **Rollback** | Revert the Hosting override. Protocol clients that already expect `resultType` then see the old omission. Downgrading constructions to compile against 1.3.0 is a second edit. |
| **Lenses** | 4. Integration Completeness |

**Open questions:**

- Does MCP SDK 2.2.0 set `resultType: complete` for every non-`CallToolResult` handler return, or only when `UseStructuredContent` is true? Only `ontology_query` was observed in `ProviderBoundDispatchTests`. Traverse is factory-assigned and serialized in `TraversalToolHostingTests`.
- Is `Agents.Mcp` on CPM 1.3.0 in-scope for this wave? Stage 0 names it so it is not mistaken for the Hosting pin. It remains a second MCP composition on the old SDK.

**Confidence:** high on the split pin and the four-tool wrap. Wrap completeness across explore/action/validate/error is not fully observed.

## What led here

Production-path survey §3 and finding 4. CHANGELOG Residue: "The 1.3.0 MCP SDK has no `ResultType`; Hosting pins 2.2.0 so every constructed `CallToolResult` can set the 2026-07-28 complete discriminator."

Competing explanation: CPM already moved to 2.2.0, or the factory assigns `ResultType` on every tool. Both false.

## Composition

Hosting pin: `Strategos.Ontology.MCP.Hosting.csproj:18-20` — `VersionOverride="2.2.0"` with a comment that 1.3.0 has no `CallToolResult.ResultType`.

CPM: `Directory.Packages.props:52` — `<PackageVersion Include="ModelContextProtocol" Version="1.3.0" />`.

Factory constructions that assign `ResultType = CompletedResultType`:

- `MapTraversalResult` success (`OntologyServerToolFactory.cs:384-386`)
- `ErrorResult` (`:410-412`)

Those two cover `ontology_traverse` only. `CreateServerTools` always adds traverse (`:91`).

Four discovered tools (`BuildHandler` `:138-149`): `ExploreHandler` / `QueryHandler` / `ActionHandler` / `ValidateHandler` return typed results (`:155-159` and siblings). The factory never constructs `CallToolResult` for those names. `ProviderBoundDispatchTests.cs:130-131` asserts `result.ResultType == CompletedResultType` on `ontology_query` through `AddMcpServer().AddOntologyTools` + a real client/server loop. That value is produced by the 2.2.0 SDK wrapper, not by a factory field set.

`Strategos.Agents.Mcp.csproj:21` references `ModelContextProtocol` **without** override → CPM 1.3.0. Different package.

Hosting packaging test (`PackagingTests.cs:62-64`) asserts the assembly references *some* name containing `ModelContextProtocol`. It does not assert version 2.2.0.

Hosting tests also set `VersionOverride="2.2.0"` (`Hosting.Tests.csproj:11`). The test compilation can see `ResultType` even if a future edit aligns Hosting to CPM and deletes the two assignments so Hosting compiles against 1.3.0. The four-tool wrap test would then be asserting the *test* SDK, not the production pin.

## Why cheaper rungs fail

- **Rung 1:** no generated Directory.Packages lock for this override.
- **Rung 2:** `ResultType` exists in Hosting *because* of the override. The pin itself is MSBuild, not a type. Sibling projects still compile against 1.3.0.
- **Rung 4/5:** transport tests prove wrap behavior under the test project's 2.2.0 pin.

## Failure scenario

CPM stays at 1.3.0. A maintainer "cleans up" Hosting's VersionOverride to match CPM and removes the two `ResultType =` initializers so Hosting compiles. Traverse loses the discriminator. Four-tool wrap depends on whichever SDK the host loads. Hosting tests still override 2.2.0 and stay green. `Agents.Mcp` never had the pin.

## Code read (this revision)

- `src/Strategos.Ontology.MCP.Hosting/Strategos.Ontology.MCP.Hosting.csproj:18-20`
- `src/Directory.Packages.props:51-52`
- `src/Strategos.Ontology.MCP.Hosting/OntologyServerToolFactory.cs:74-91`, `:138-159`, `:384-412`
- `src/Strategos.Ontology.MCP.Hosting.Tests/PackagingTests.cs:57-68`
- `src/Strategos.Agents.Mcp/Strategos.Agents.Mcp.csproj:21`
- `CHANGELOG.md:187-190`

### Investigation Log

#### Which projects pin ModelContextProtocol, and who assigns ResultType?

- Read: every `*.csproj` `ModelContextProtocol` reference; factory `CallToolResult` sites; Hosting packaging test.
- Found: Hosting + Hosting.Tests override 2.2.0; Agents.Mcp and CPM are 1.3.0; factory assigns `ResultType` only on traverse success/error.
- Not found: a pack or csproj test that asserts Hosting's VersionOverride value.
- Conclusion: resultType closure is a pin-graph property. Four-tool path is wrap-only.
