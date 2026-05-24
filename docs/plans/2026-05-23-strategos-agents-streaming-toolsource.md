# Implementation Plan: Strategos.Agents — streaming + in-process tool sources

## Source Design
Link: [`docs/designs/2026-05-23-strategos-agents-streaming-toolsource.md`](../designs/2026-05-23-strategos-agents-streaming-toolsource.md)

## Scope
**Target:** Full design (all 12 requirements: DR-1 through DR-12 — streaming DR-1..DR-5, tool sources DR-6..DR-10, cross-cutting DR-11..DR-12).
**Excluded:** None. The three Open Questions are deferred *decisions* resolved inline (AGAG008 reserve-vs-assign → reserve; legacy-type deletion → audit in T-018; version placement → flagged for plan-review), not deferred work.

## Summary
- **Total tasks:** 21
- **Parallel groups:** 6 waves
- **Estimated test count:** 26+ (1 acceptance + ~21 unit/integration + cross-product smoke)
- **Design coverage:** all sections (Problem, Chosen Approach, Design rationale table, Technical Design, Requirements DR-1..DR-12, Integration Points, Out of scope, Risks, Testing Strategy, Open Questions, Alternatives, References)

**Toolchain reminders (Strategos conventions):**
- Test runner: TUnit. Invocation MUST use `dotnet test --project <proj> -- --treenode-filter "/*/*/*/Name"` (NOT `dotnet test --filter`). See [[feedback_tunit_test_invocation]].
- Mocking allowed only at the `IChatClient` boundary. Internal collaborators (`AgentStepBuilder`, `AgentToolSource`, `StrategosFunctionsChatClient`, `AgentStepConfiguration`) constructed real. See [[feedback_implementer_no_exarchos_mcp]].
- All new public types `sealed` ([[INV-6]]); value types are records ([[INV-7]]).
- `AgentDiagnostics` lives at `src/Strategos.Agents/Diagnostics/AgentDiagnostics.cs` (namespace `Strategos.Agents.Diagnostics`) — no separate Abstractions project; abstractions live under `src/Strategos.Agents/Abstractions/`.
- **Clean break:** `IMcpToolSource`/`WithMcpToolSource` are *deleted*, not shimmed — no `[Obsolete]` ([[feedback_no_handwavy_mitigations]]).

## Spec Traceability

| DR | Acceptance criteria addressed | Task(s) |
|---|---|---|
| DR-1 Streaming orchestration path | streaming uses `GetStreamingResponseAsync`; accumulates → same AGAG005/006/002 checks; buffered default unchanged; `ApplyResult` once-terminal | T-014, T-020 |
| DR-2 `WithStreaming` opt-in | chainable; null→ANE; double-call→IOE; reaches config; sealed reflection | T-010, T-012 |
| DR-3 Per-update token delivery; non-durable (INV-1) | ordered token callbacks + one completion; workflowId/stepName from context; grep gate no `IProgressEventStore`/`StreamingTokenReceived` | T-015 |
| DR-4 Streaming failure & cancellation | cancellation unwrapped; handler-throw→AGAG009 + state ref-equal; zero-update→AGAG006; no bare catch | T-016 |
| DR-5 Streaming docs & legacy reconciliation | ≤15-line README; consumer audit recorded; prose gate | T-018 |
| DR-6 `IToolSource` replaces `IMcpToolSource` | `IMcpToolSource` gone; port MCP-free; `McpToolSource:IToolSource` | T-004 |
| DR-7 `McpToolSource` retargeted, full spec | full-spec resolve; AGAG004 redacted; `IAsyncDisposable`; version pin in CHANGELOG | T-005, T-017 |
| DR-8 `AgentToolSource` reflection adapter | `FromObject` over `[AgentTool]`; `[Description]` flows; `FromDelegates`; empty→empty; AGAG007; sealed; no MCP ref | T-006, T-007, T-008 |
| DR-9 `.WithToolSource` + multi-source merge | `WithMcpToolSource` gone; accumulate; precedence host>Strategos>source; resolve-once cache; lazy; AGAG003 dup | T-009, T-011, T-013 |
| DR-10 Migration & docs (clean break) | CHANGELOG BREAKING + recipe; README dual example; grep gate | T-017 |
| DR-11 Diagnostics AGAG007–009 | consts; reflection over `AgentException` subclasses; README enumerates; grep gate | T-002, T-003, T-008, T-016, T-019 |
| DR-12 Real-chain integration extension | streaming typed output + handler-before-apply ordering; AgentToolSource invoked; MCP resolves; two-source precedence; no category-skip; mock only at IChatClient | T-001, T-020 |
| Testing Strategy (3-layer) | unit + integration + cross-product smoke | T-001, T-020, T-021 |

Every DR → ≥1 task. Provenance chain complete.

## Task Breakdown

### Task 1: Real-chain acceptance test scaffold (stays RED until T-020)
**Description:** Pin the DR-12 end-to-end test up front: full `ChatClientBuilder` pipeline with a fake `IChatClient` at the bottom, exercising streaming typed output, an in-process `AgentToolSource` round-trip, an MCP `IToolSource`, and two-source precedence. Stays RED (compile failure) until the orchestrator, builder, port, and adapters land.
**Phase:** RED · **Test Layer:** acceptance · **Implements:** DR-12, DR-1, DR-8, DR-9

1. [RED] Write test: `FullChain_StreamingWithToolSourceAndMcp_RoundTripsThroughPipeline`
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseIntegrationTests.cs`
   - Expected failure: `IToolSource`, `AgentToolSource`, `WithToolSource`, `WithStreaming` do not exist → `CS0246`.
   - Run: `dotnet build` MUST fail.
2. [GREEN] Deferred — passes only after T-020.
3. [REFACTOR] N/A — shaped by spec.

**Verification:** [ ] compile failure observed before prod code · [ ] `Method_Scenario_Outcome` name · [ ] not `[Skip]`/category-filtered (DR-12).
**Dependencies:** None. **Parallelizable:** No (pins spec first).
**testingStrategy:** `{ layer: "acceptance", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 2: Allocate `AGAG007` / `AGAG009` (reserve `AGAG008`)
**Description:** Add diagnostic literals per INV-5: `AGAG007` (in-process tool-source resolution failure), `AGAG009` (streaming-handler failure). `AGAG008` is *reserved* (documented, not declared) pending a build-time validation case (Open Question 1).
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-11

1. [RED] Write test: `AgentDiagnostics_AGAG007AndAGAG009_DeclaredAsConstMatchingPattern`
   - File: `src/Strategos.Agents.Tests/Unit/Diagnostics/AgentDiagnosticsTests.cs`
   - Behavior: reflection finds `AGAG007`,`AGAG009` `const string` matching `^AGAG\d{3}$`; `AGAG008` intentionally absent.
   - Expected failure: fields do not exist.
2. [GREEN] Add the two consts to `src/Strategos.Agents/Diagnostics/AgentDiagnostics.cs` with a comment reserving `AGAG008`.
3. [REFACTOR] One-line domain-voice XML doc per code (prose-gated in T-019).

**Verification:** [ ] witnessed fail · [ ] green · [ ] AGAG008 not declared.
**Dependencies:** None. **Parallelizable:** Yes.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 3: `AgentToolSourceException` (AGAG007) + `AgentStreamingException` (AGAG009)
**Description:** Two sealed `AgentException` subclasses mirroring `AgentMcpException`'s shape. `AgentToolSourceException` names the failing source type; `AgentStreamingException` carries the streaming failure context. Extend the hierarchy reflection test.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-11, DR-8, DR-4

1. [RED] Write tests: `AgentToolSourceException_Always_CarriesAGAG007`, `AgentStreamingException_Always_CarriesAGAG009`, and extend `AgentExceptionHierarchy_EverySubclass_DeclaresPopulatedDiagnostic`.
   - File: `src/Strategos.Agents.Tests/Unit/Exceptions/AgentExceptionHierarchyTests.cs`
   - Expected failure: types do not exist.
2. [GREEN] Add `src/Strategos.Agents/Exceptions/AgentToolSourceException.cs` and `AgentStreamingException.cs` (sealed, `Diagnostic` override).
3. [REFACTOR] Align constructor messages with the existing "what/why/Diagnostic:" pattern.

**Verification:** [ ] witnessed fail · [ ] reflection green over all subclasses · [ ] both sealed.
**Dependencies:** T-002. **Parallelizable:** Yes (after T-002).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 4: Declare `IToolSource`; delete `IMcpToolSource` (DR-6)
**Description:** Introduce `IToolSource` (`Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken)`) in `src/Strategos.Agents/Abstractions/`; delete `IMcpToolSource.cs`. Port stays MCP-dependency-free (INV-3 separation).
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-6

1. [RED] Write test: `IToolSource_IsMcpFreePortWithSingleResolveMethod` + `IMcpToolSource_DoesNotExist` (reflection over the `Strategos.Agents` assembly; grep gate as backstop).
   - File: `src/Strategos.Agents.Tests/Unit/Abstractions/IToolSourceContractTests.cs` (rename from `IMcpToolSourceContractTests.cs`).
   - Expected failure: `IToolSource` missing / `IMcpToolSource` still present.
2. [GREEN] Add `IToolSource.cs`; delete `IMcpToolSource.cs`.
3. [REFACTOR] Domain-voice summary; no `Mcp` term on the port.

**Verification:** [ ] fail witnessed · [ ] `grep -rn IMcpToolSource src/` returns zero (excluding CHANGELOG) · [ ] port has no MCP using.
**Dependencies:** None. **Parallelizable:** No (broad rename root; downstream tool-source tasks consume this port).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 5: Retarget `McpToolSource` onto `IToolSource` (DR-7)
**Description:** `McpToolSource` (in `Strategos.Agents.Mcp`) implements `IToolSource`; keep handshake, endpoint redaction, AGAG004, `IAsyncDisposable` in the adapter.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-7

1. [RED] Update `McpToolSourceTests`: `McpToolSource_Implements_IToolSource`, retain `..._HandshakeFailure_ThrowsAgentMcpExceptionWithAGAG004Redacted`.
   - File: `src/Strategos.Agents.Mcp.Tests/McpToolSourceTests.cs`
   - Expected failure: `McpToolSource` still names `IMcpToolSource`.
2. [GREEN] Change base list to `IToolSource`; full-spec resolve unchanged.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] AGAG004 redaction test green · [ ] `IAsyncDisposable` retained.
**Dependencies:** T-004. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 6: `[AgentTool]` discovery attribute (DR-8)
**Description:** Opt-in marker `[AgentTool]` (method-targeted, optional `Name`) — NOT `[AgentFunction]` (INV-4). Foundation for `AgentToolSource` discovery.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-8

1. [RED] Write test: `AgentToolAttribute_TargetsMethods_WithOptionalNameOverride`.
   - File: `src/Strategos.Agents.Tests/Unit/AgentToolAttributeTests.cs`
   - Expected failure: attribute does not exist.
2. [GREEN] Add `src/Strategos.Agents/Abstractions/AgentToolAttribute.cs` (sealed, `AttributeUsage(Method)`).
3. [REFACTOR] Domain-voice summary.

**Verification:** [ ] fail witnessed · [ ] `AttributeUsage` = Method · [ ] sealed.
**Dependencies:** None. **Parallelizable:** Yes.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 7: `AgentToolSource.FromObject` reflection adapter — happy path (DR-8)
**Description:** Sealed `AgentToolSource : IToolSource`. `FromObject(instance)` reflects `[AgentTool]` methods via `AIFunctionFactory.Create`; `[Description]` flows into the `AIFunction`.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-8

1. [RED] Write tests: `FromObject_AnnotatedMethods_YieldOneAIFunctionEach`, `FromObject_MethodDescription_FlowsToAIFunction`.
   - File: `src/Strategos.Agents.Tests/Unit/AgentToolSourceTests.cs`
   - Expected failure: `AgentToolSource` missing.
2. [GREEN] Add `src/Strategos.Agents/AgentToolSource.cs`; `GetToolsAsync` returns the cached reflected list.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] one tool per annotated method · [ ] description flows · [ ] no `ModelContextProtocol` using (grep).
**Dependencies:** T-004, T-006. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 8: `AgentToolSource.FromDelegates` + empty source + AGAG007 (DR-8, DR-11)
**Description:** `FromDelegates(params Delegate[])` escape hatch; empty source yields empty (non-null) list; a reflection/factory failure surfaces `AgentToolSourceException` (AGAG007) naming the source type.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-8, DR-11

1. [RED] Write tests: `FromDelegates_TwoDelegates_YieldTwoTools`, `FromObject_EmptyType_YieldsEmptyNotNull`, `GetToolsAsync_FactoryThrows_RaisesAGAG007NamingSource`.
   - File: `src/Strategos.Agents.Tests/Unit/AgentToolSourceTests.cs`
   - Expected failure: members/behavior missing.
2. [GREEN] Implement `FromDelegates`; wrap resolution failure as `AgentToolSourceException`.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] empty≠null · [ ] AGAG007 names source.
**Dependencies:** T-007, T-003. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 9: `AgentStepConfiguration`: `McpToolSource` → `IReadOnlyList<IToolSource> ToolSources` (DR-9)
**Description:** Replace the single `IMcpToolSource? McpToolSource` field with `IReadOnlyList<IToolSource> ToolSources` (non-null, no null entries — mirror the `Tools` guard).
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-9

1. [RED] Update `AgentStepConfigurationTests`: `Configuration_ToolSources_RejectsNullEntries`, `Configuration_NoMcpToolSourceMember`.
   - File: `src/Strategos.Agents.Tests/Unit/Configuration/AgentStepConfigurationTests.cs`
   - Expected failure: `ToolSources` missing / `McpToolSource` still present.
2. [GREEN] Edit `src/Strategos.Agents/Configuration/AgentStepConfiguration.cs`.
3. [REFACTOR] Update XML doc.

**Verification:** [ ] fail witnessed · [ ] null-entry guard · [ ] no `McpToolSource` member.
**Dependencies:** T-004. **Parallelizable:** No (shared config file with the next task).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 10: `AgentStepConfiguration`: add `StreamingHandler` field (DR-2)
**Description:** Add `IStreamingHandler? StreamingHandler { get; }` to the config record + constructor.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-2

1. [RED] Write test: `Configuration_StreamingHandler_RoundTrips`.
   - File: `src/Strategos.Agents.Tests/Unit/Configuration/AgentStepConfigurationTests.cs`
   - Expected failure: member missing.
2. [GREEN] Add the nullable field/ctor param (default null).
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] null default · [ ] sealed record intact.
**Dependencies:** T-009. **Parallelizable:** No (same file as T-009).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 11: Builder `.WithToolSource` (accumulate); delete `WithMcpToolSource` (DR-9)
**Description:** Replace `_mcpToolSource` + `WithMcpToolSource` with a `List<IToolSource>` + `WithToolSource(IToolSource)` (accumulating, null-guarded). `Build()`/`ComposeChatClient` pass the list through.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-9

1. [RED] Update `AgentStepBuilderToolsTests`: `WithToolSource_MultipleCalls_Accumulate`, `WithMcpToolSource_DoesNotExist`, `WithToolSource_Null_ThrowsArgumentNull`.
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBuilderToolsTests.cs`
   - Expected failure: method shape changed.
2. [GREEN] Edit `src/Strategos.Agents/AgentStepBuilder.cs`; thread `toolSources` into config + `ComposeChatClient`.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] accumulation · [ ] `WithMcpToolSource` gone · [ ] AGAG003 dup-name guard intact.
**Dependencies:** T-009. **Parallelizable:** No (shared builder file with the next task).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 12: Builder `.WithStreaming(IStreamingHandler)` + guards (DR-2)
**Description:** Add `WithStreaming(IStreamingHandler)` — chainable, null→ANE, second call→`InvalidOperationException` (mirrors `WithChatOptions`). Threads into config.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-2

1. [RED] Write tests: `WithStreaming_Null_ThrowsArgumentNull`, `WithStreaming_CalledTwice_ThrowsInvalidOperation`, `WithStreaming_ReachesConfiguration`, `NewAgentTypes_AreSealed` (reflection).
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBuilderOptionsTests.cs`
   - Expected failure: method missing.
2. [GREEN] Add field + method; pass to `AgentStepConfiguration`.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] guards · [ ] sealed reflection green.
**Dependencies:** T-010, T-011. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 13: `StrategosFunctionsChatClient` multi-`IToolSource` merge (DR-9)
**Description:** Generalize `_mcpToolSource`→`_toolSources` (list), `ResolveMcpToolsAsync`→`ResolveToolsAsync` (resolve each once, cache, aggregate). Merge precedence unchanged: host > Strategos `WithTool` > tool-sources (registration order). Failure of any source wraps via that source's own exception (AGAG004 for MCP / AGAG007 for in-process) — propagate, don't reclassify.
**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-9

1. [RED] Update `StrategosFunctionsChatClient*InjectionTests`: `TwoToolSources_BothResolved_MergedByPrecedence`, `ToolSource_ResolvedAtMostOnce_Cached`, `HostTool_WinsOverSourceTool_OnNameCollision`.
   - Files: `src/Strategos.Agents.Tests/Unit/Configuration/StrategosFunctionsChatClientMcpInjectionTests.cs` (+ rename to drop `Mcp`).
   - Expected failure: single-source shape.
2. [GREEN] Edit `src/Strategos.Agents/Configuration/StrategosFunctionsChatClient.cs` + `UseStrategosFunctions` signature (`IReadOnlyList<IToolSource>`).
3. [REFACTOR] Keep `MergeToolsAsync` precedence logic; only the source-resolution loop changes.

**Verification:** [ ] fail witnessed · [ ] resolve-once cache · [ ] precedence preserved · [ ] per-source exception not reclassified.
**Dependencies:** T-009, T-011. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 14: `AgentStepBase` streaming branch (DR-1)
**Description:** When `Configuration.StreamingHandler` is set, call `GetStreamingResponseAsync`, accumulate updates via `ToChatResponse()` into a `ChatResponse<TResult>`, then run the **existing** terminal block (AGAG005/006/002, `ApplyResult`). Buffered path (no handler) byte-for-byte unchanged.
**Phase:** RED → GREEN · **Test Layer:** integration · **Implements:** DR-1

1. [RED] Write tests: `Execute_StreamingConfigured_UsesStreamingNotBuffered` (fake records method), `Execute_StreamingMalformedPayload_ThrowsAGAG002`, `Execute_StreamingValidPayload_AppliesTypedResultOnce`.
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseStreamingTests.cs`
   - Expected failure: streaming branch absent.
2. [GREEN] Edit `src/Strategos.Agents/AgentStepBase.cs` — branch on handler; share the terminal checks (extract a private `FinalizeAsync(ChatResponse<TResult>, ...)` so both paths converge).
3. [REFACTOR] Ensure buffered tests still green (no behavioral drift).

**Verification:** [ ] fail witnessed · [ ] streaming→same AGAG002 · [ ] `ApplyResult` once · [ ] buffered unchanged.
**Dependencies:** T-010, T-012. **Parallelizable:** No (shared orchestrator file with the streaming delivery and failure tasks).
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 15: Per-update token delivery + INV-1 grep gate (DR-3)
**Description:** Emit each non-empty `update.Text` to `handler.OnTokenReceivedAsync(text, workflowId, stepName, ct)` in order, then one `OnResponseCompletedAsync(fullText, ...)`. `workflowId`/`stepName` from `StepContext`. No `IProgressEventStore` / `StreamingTokenReceived` in the path.
**Phase:** RED → GREEN · **Test Layer:** integration · **Implements:** DR-3

1. [RED] Write tests: `Streaming_Tokens_DeliveredInOrderThenCompletion`, `Streaming_WorkflowAndStep_SourcedFromContext`, and a grep-gate unit test `StreamingPath_DoesNotReference_ProgressEventStoreOrStreamingTokenReceived`.
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseStreamingTests.cs`
   - Expected failure: no callbacks fire.
2. [GREEN] Wire the handler calls inside the streaming enumeration in `AgentStepBase`.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] ordered tokens + single completion · [ ] grep gate green (INV-1).
**Dependencies:** T-014. **Parallelizable:** No.
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 16: Streaming failure & cancellation modes (DR-4)
**Description:** Cancellation propagates unwrapped from mid-stream. A handler that throws → `AgentStreamingException` (AGAG009), fail-loud, input `TState` reference-equal after throw. Zero-update stream → `AGAG006`. No bare `catch`.
**Phase:** RED → GREEN · **Test Layer:** integration · **Implements:** DR-4, DR-11

1. [RED] Write tests: `Streaming_StreamCancelled_PropagatesUnwrapped`, `Streaming_HandlerThrows_RaisesAGAG009StatePreserved`, `Streaming_ZeroUpdates_ThrowsAGAG006`.
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseStreamingTests.cs`
   - Expected failure: paths absent.
2. [GREEN] Add explicit-type catches in `AgentStepBase` streaming path; wrap handler failure as `AgentStreamingException`.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] OCE unwrapped · [ ] AGAG009 + ref-equal state · [ ] `grep -rEn 'catch *\(' src/Strategos.Agents/ | grep -v 'catch (Exception\|catch (Operation\|catch (Argument'` shows no new bare catch.
**Dependencies:** T-014, T-003. **Parallelizable:** No.
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 17: Tool-source migration & docs sweep (DR-10)
**Description:** CHANGELOG `### Changed (BREAKING) — Tool source port` with before/after recipe + MCP version pin (INV-3); README replaces the MCP-only example with one `.WithToolSource(...)` showing both `AgentToolSource` and `McpToolSource`; note closing #91 (MCP is one adapter). Grep gate: no `IMcpToolSource`/`WithMcpToolSource` outside CHANGELOG.
**Phase:** GREEN · **Test Layer:** unit (doc/grep gate) · **Implements:** DR-10, DR-7

1. [RED] Write/extend `DocumentationAndProseGateTests`: `Readme_UsesWithToolSource_NotWithMcpToolSource`, `Changelog_HasToolSourceBreakingSection`.
   - File: `src/Strategos.Agents.Tests/Unit/DocumentationAndProseGateTests.cs`
   - Expected failure: docs not updated.
2. [GREEN] Edit `CHANGELOG.md`, `src/Strategos.Agents/README.md`.
3. [REFACTOR] Prose-gate pass.

**Verification:** [ ] fail witnessed · [ ] BREAKING recipe present · [ ] README dual example ≤ guidance · [ ] grep gate green.
**Dependencies:** T-005, T-011, T-013. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 18: Streaming docs + legacy-type reconciliation (DR-5)
**Description:** Add a ≤15-line README streaming example. Audit `StreamingTokenReceived`, `StreamingExecutionMode`, `IStreamingHandler` consumers; delete those with zero production consumers after this work (DIM-5) or annotate as specialist-only. Record the audit in the PR description.
**Phase:** GREEN · **Test Layer:** unit (doc/prose gate) · **Implements:** DR-5

1. [RED] Extend `DocumentationAndProseGateTests`: `Readme_HasStreamingExample_Within15Lines`.
   - File: `src/Strategos.Agents.Tests/Unit/DocumentationAndProseGateTests.cs`
   - Expected failure: example absent.
2. [GREEN] Edit README; run the consumer audit; delete/annotate orphaned types.
3. [REFACTOR] Prose-gate pass on all new summaries.

**Verification:** [ ] fail witnessed · [ ] ≤15-line example · [ ] audit recorded · [ ] orphans resolved.
**Dependencies:** T-015. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 19: Diagnostics README + AGAG grep gate + hierarchy reflection (DR-11)
**Description:** README Diagnostics section enumerates AGAG007/009 (and the AGAG008 reservation) with one-line remediations; grep gate confines `AGAG00[0-9]` literals to `AgentDiagnostics.cs` + exception sites; final reflection assertion over all `AgentException` subclasses.
**Phase:** GREEN · **Test Layer:** unit · **Implements:** DR-11

1. [RED] Extend tests: `Readme_Diagnostics_EnumeratesAGAG007And009`, `AgagLiterals_OnlyInDiagnosticsAndExceptionSites`.
   - Files: `DocumentationAndProseGateTests.cs`, `AgentDiagnosticsTests.cs`
   - Expected failure: docs/gate absent.
2. [GREEN] Edit README; the reflection test reuses T-003's hierarchy assertion.
3. [REFACTOR] None.

**Verification:** [ ] fail witnessed · [ ] README enumerates · [ ] grep gate green.
**Dependencies:** T-003, T-008, T-016. **Parallelizable:** No.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 20: Complete the real-chain acceptance test (turns T-001 GREEN) (DR-12)
**Description:** Fill in `FullChain_StreamingWithToolSourceAndMcp_RoundTripsThroughPipeline`: streaming materializes typed output and handler tokens fire *before* `ApplyResult`; an `AgentToolSource` tool is actually invoked via `UseFunctionInvocation`; an in-process MCP `IToolSource` fake resolves; two sources merge by precedence. Fake `IChatClient` at the bottom; mock only there.
**Phase:** GREEN · **Test Layer:** integration · **Implements:** DR-12

1. [RED] Already RED from T-001.
2. [GREEN] Implement the assertions against the now-complete chain; reuse the in-process `InProcessTestToolSource` fixture (an `IToolSource` fake).
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseIntegrationTests.cs`
3. [REFACTOR] Deduplicate fixture wiring with existing integration helpers.

**Verification:** [ ] T-001 now GREEN · [ ] handler-before-apply ordering asserted · [ ] AgentToolSource invoked (round-trip) · [ ] no category-skip · [ ] mock only at IChatClient.
**Dependencies:** T-005, T-008, T-013, T-014, T-015. **Parallelizable:** No (gate).
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 21: Cross-product `basileus-smoke` bump (Testing Strategy)
**Description:** Bump the local-feed package version consumed by `tests/basileus-smoke` and assert the basileus-consumed surface still compiles after the clean break (no `IMcpToolSource` dependency surfaced).
**Phase:** GREEN · **Test Layer:** integration (smoke) · **Implements:** Testing Strategy (cross-product)

1. [RED] Update the smoke csproj/version reference; build MUST initially fail if the surface regressed.
   - File: `tests/basileus-smoke/Basileus.Smoke.Tests/Basileus.Smoke.Tests.csproj`
2. [GREEN] Confirm compile against the new package; no source change expected in basileus surface.
3. [REFACTOR] None.

**Verification:** [ ] smoke builds against new version · [ ] no `IMcpToolSource` in consumed surface.
**Dependencies:** T-017. **Parallelizable:** No (last).
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

## Parallelization Plan

| Wave | Tasks | Rationale |
|---|---|---|
| 0 | T-001 | Pin acceptance spec (RED) first |
| 1 | T-002, T-004, T-006 | Independent files: diagnostics, port, attribute |
| 2 | T-003, T-005, T-007, T-009 | After their foundations (T-002/T-004/T-006) |
| 3 | T-008, T-010, T-011 | After T-007/T-003, T-009 |
| 4 | T-012, T-013, T-014 | After builder/config/runtime foundations |
| 5 | T-015, T-016, T-017 | Streaming delivery/failure + tool-source docs |
| 6 | T-018, T-019, T-020, T-021 | Docs, diagnostics gate, acceptance GREEN, smoke |

Within a wave, no two tasks edit the same file (verified against the file targets above) — safe for worktree isolation.

## Deferred Decisions (Open Questions)
1. **AGAG008** — reserved, not assigned (T-002). Assign only if a build-time validation case materializes during T-012/T-013.
2. **Legacy streaming types** — audited in T-018; delete if orphaned.
3. **Version/milestone** — second BREAKING agent-surface change; flag at plan-review whether to cut 2.7.1 vs 2.8.0 (the "2.8.0 schema substrate" milestone is themed differently).
