# compat-mcp-resulttype-icons-wire — MCP resultType is a protocol addition; Icons stay absent unless set

| | |
|---|---|
| **Claim** | Every factory-constructed `CallToolResult` carries `resultType: complete` (2026-07-28). `OntologyToolDescriptor.Icons` stays null when unset and is omitted from the protocol tool. Hosting’s `ModelContextProtocol` pin is 2.2.0 so the field exists. |
| **Scope** | Network/protocol contract of `LevelUp.Strategos.Ontology.MCP.Hosting` (`OntologyServerToolFactory`) and the published `OntologyToolDescriptor` / `ToolIcon` types in `Strategos.Ontology.MCP`. |
| **Consequence** | A client that already requires `resultType` can talk to this host. A client that rejects unknown fields on an older `CallToolResult` shape sees a new field. A revert of the pin removes `ResultType` from the SDK and drops the discriminator those clients now expect. `ErrorResult` also sets `complete` with `isError: true`. |
| **Proof rung** | Production-path integration tests |
| **Proof artifact** | A tools/call round-trip that asserts the JSON `resultType` on success and error, and asserts `icons` absent when Discover did not set them. Factory tests assign `ResultType` in-process and assert `protocolTool.Icons` null (`OntologyServerToolFactoryTests.cs:59-61`). They do not open the wire, and they do not lock the 2.2.0 pin in the packed nuspec. |
| **Why not cheaper** | `resultType` is a protocol field on an SDK type, not a Strategos-generated artifact. The compiler cannot see MCP clients. Structural INV-3 greps (rung 3) can deny a missing-`resultType` *source* shape; they cannot prove the hosted process emits it. Component tests (rung 4) prove the factory property, not the bytes on the socket. |
| **Failure signal** | Client-side protocol error or a tool result the client ignores. Nothing in this host pages. The channel does not separate “client too old” from “host omitted the field.” |
| **Rollback** | Revert `887eb9a` and drop `VersionOverride="2.2.0"`. Does not reverse clients that already require `resultType`. Icons addition reverses cleanly (optional; Discover never sets it). |
| **Lenses** | 5. Exposure And Compatibility (diff form) |
| **Confidence** | high for factory assignment and null-icons path. medium for four discovered tools (SDK wrap, not factory `CallToolResult`). low for whether `ErrorResult` + `complete` is protocol-legal. |

**Compatibility class:** additive protocol field; optional Icons; Hosting dependency bump 1.3.0 → 2.2.0 via `VersionOverride`; reversal is asymmetric.

**Impact**

- `resultType`: added on `MapTraversalResult` and `ErrorResult` (`OntologyServerToolFactory.cs:51-57`, `:386`, `:412`). Always `"complete"`. Comment says `input_required` is not emitted.
- Four discovered tools (`ontology_explore` / `query` / `action` / `validate`) return typed results; the factory does not construct `CallToolResult` for them (`BuildHandler` at `:138-149`). Their `resultType` depends on MCP SDK 2.2.0 wrap. CPM still lists `ModelContextProtocol` `1.3.0` (`Directory.Packages.props:52`).
- `Icons`: new optional init property, default null (`OntologyToolDescriptor.cs:43`). `ToolIcon` is a new public record with `src` / `mimeType` / `sizes` / `theme` (`ToolIcon.cs:12-25`). `ApplyIcons` returns without setting `options.Icons` when null (`:248-254`). `Discover()` never sets `Icons` (`OntologyToolDiscovery.cs:48-60` and siblings).
- No removed or renamed descriptor fields. Constructor `(Name, Description)` unchanged. No persisted store of tool descriptors.
- PublicAPI.Unshipped records `Icons` get/init and the whole `ToolIcon` type (`PublicAPI.Unshipped.txt:127-128`, `:194-203`).

**Reverse dependency closure:**

1. `AddOntologyTools` → `CreateServerTools` (Hosting).
2. MCP clients speaking 2026-07-28 (Cursor and others).
3. INV-3 deny-list / checklist (now denies pre-2026-07-28 response shape).
4. Hosting package consumers who restore MCP transitively — they get 2.2.0 from this project, not 1.3.0 from CPM.
5. Tests: `OntologyServerToolFactoryTests`, `OntologyToolDescriptorTests`.

**Persisted data:** none. Protocol messages are not a Strategos store. Older hosts omit `resultType`; older clients that ignore unknown fields still read content.

**Reverses?** Source + pin: yes. Clients that already expect `resultType`: no. Icons: yes (optional; omitting them is the production path today).

**Open questions:**

- Is `ErrorResult` with `resultType: complete` and `isError: true` legal on 2026-07-28? If not, the additive field is a protocol break on the error path.
- Do the four SDK-wrapped tools emit `resultType` under 2.2.0? If the wrap omits it, CHANGELOG’s “every constructed CallToolResult” is true and “every tool result” is not.
- Is MCP 2.2.0 source-compatible for Hosting consumers who compiled against 1.3.0 APIs? A `VersionOverride` can be a silent breaking dependency bump.

**What is expensive to find again**

The pin lives only on the Hosting csproj (`VersionOverride`). CPM still advertises 1.3.0. A later reader of `Directory.Packages.props` will think the host cannot set `ResultType`.
