# Strategos.Agents — streaming responses + in-process tool sources

**Date:** 2026-05-23 · **Milestone:** Strategos 2.7.0 — Agent Capabilities (completion) · **Tracks:** [#89](https://github.com/lvlup-sw/strategos/issues/89), [#90](https://github.com/lvlup-sw/strategos/issues/90); closes [#91](https://github.com/lvlup-sw/strategos/issues/91)

## Problem Statement

The MEAI 10.5 adoption (PR #82, design `2026-05-17-strategos-agents-meai-10-5.md`) shipped the sealed two-arity `AgentStepBase<TState, TResult>` + `AgentStepBuilder`, structured output, tool-use middleware, the `IMcpToolSource` port, and the `AGAG001`–`AGAG006` diagnostic family. It explicitly deferred two capabilities that are live in the dependency graph but unwired:

1. **Streaming responses (#89).** `AgentStepBase.ExecuteAsync` makes a single `GetResponseAsync<TResult>` call (`AgentStepBase.cs:60`). MEAI 10.5's `GetStreamingResponseAsync` is unused. The legacy `IStreamingHandler`, `StreamingTokenReceived`, and `StreamingExecutionMode` types exist from the pre-2.7.0 specialist-agent era but were never connected to the two-arity orchestrator.
2. **Workflow/ontology lookups as tools (#90).** The 2.7.0 tool-use middleware wired the *mechanism* — `WithTool(AIFunction)` → `StrategosFunctionsChatClient` injection → `UseFunctionInvocation`. What is missing is an ergonomic way to supply an *in-process catalog* of tools (ontology lookups, workflow primitives) without dragging an ontology dependency into `Strategos.Agents`.

Both extend the same orchestrator at orthogonal seams — #89 on the response path, #90 on the request/tool path — so they compose into one unit with two requirement tracks.

## Chosen Approach

**Streaming as an observability layer over a terminal typed contract; tool sources unified behind one domain-named port.**

`IWorkflowStep<TState>.ExecuteAsync` returns exactly one `StepResult<TState>`, and structured `TResult` JSON is not materializable until the response closes. Streaming therefore *cannot* change the step's return shape — it is an ephemeral side-channel that surfaces text deltas for UX while `ApplyResult(TResult)` stays terminal. The accumulated `ChatResponse<TResult>` funnels into the **same** `TryGetResult`/empty-payload checks that exist today, so there is one structured-output validation path, not two.

For tools, the just-shipped `IMcpToolSource` is generalized into a single **`IToolSource`** port (`GetToolsAsync`). MCP becomes one adapter (`McpToolSource`, full spec per [[INV-3 mcp-first-class-latest-spec]]); a new in-process reflection adapter over `AIFunctionFactory.Create` is the second. This is a clean break — `IMcpToolSource`/`WithMcpToolSource` are deleted, not shimmed — justified by zero consumers anywhere outside `Strategos.Agents*` (verified) and the still-preview MCP sub-package (#91).

Alternatives are documented in §Alternatives and rejected on [[INV-1]]/[[INV-4]] and DIM-5 grounds.

## Design rationale — invariants & dimensions applied

This design was audited against `/axiom:design` (DIM-1..8) and `/strategos-design-invariants` (INV-1..8) during ideation.

| Decision | Governing invariant / dimension | Consequence |
|---|---|---|
| Streaming via `IStreamingHandler` only; **no** `IProgressEventStore` token log | **INV-1** (HIGH, durability) | A parallel durable token log would fork durability away from Wolverine+Marten. Tokens are an explicitly non-durable side-channel; the only durable artifact is the terminal `StepResult` the SG lowers into the saga. |
| Accumulated response funnels into the existing `AGAG002`/`AGAG006` checks | **INV-7** (HIGH) · DIM-4 | One structured-output path; `ApplyResult` terminal; `TState` reference-equal until the single evolution. No streaming-specific deserialization fork. |
| `IToolSource` named in "tool" vocabulary, not "function" | **INV-4** (MEDIUM) | The authoring surface already uses `WithTool`; `AIFunction` is MEAI's CLR type and must not leak onto the DSL. |
| Generalize `IMcpToolSource` → `IToolSource` rather than add a sibling | DIM-5 (hygiene) · **INV-6** | Two structurally-identical ports are a divergent duplicate of one behavior. One port, two adapters. |
| MCP-specific handshake/redaction stays in `McpToolSource`, not the port | **INV-3** · DIM-6 | MCP keeps full-spec fidelity as a peer adapter, not a downgraded special case. |
| Every new failure mode gets a stable `AGAG###` ID | **INV-5** (HIGH) | `AGAG007`–`AGAG009` allocated below, never reused/renumbered in a non-major release. |
| All new types `sealed` | **INV-6** (HIGH) | Composition over inheritance; SG-target safety. |

## Technical Design

```
 AgentStepBuilder<TState, TResult>
   .WithSystemPrompt / .WithUserPrompt / .WithApplyResult   (required, unchanged)
   .WithTool(AIFunction)                                    (unchanged)
   .WithToolSource(IToolSource)             ◀── replaces WithMcpToolSource; accumulates
   .WithStreaming(IStreamingHandler)        ◀── NEW; opt-in, default = buffered
   .WithMaxToolIterations(int) / .WithChatOptions / .ConfigureChatClient   (unchanged)
   .Build(IChatClient) → IAgentStep<TState, TResult>
                          │
                          ▼
 AgentStepBase<TState, TResult>  (sealed)
   ExecuteAsync ─→
     buffered (default):  chatClient.GetResponseAsync<TResult>(...)        ── unchanged
     streaming  (opt-in): await foreach (update in GetStreamingResponseAsync(...))
                              handler.OnTokenReceivedAsync(update.Text, wf, step)
                          accumulate → ChatResponse<TResult>               ── ToChatResponse()
                          handler.OnResponseCompletedAsync(fullText, ...)
     ── both paths converge here ──
     FinishReason==ToolCalls → AGAG005 ; TryGetResult / empty → AGAG006/AGAG002   (unchanged)
     ApplyResult(state, typedResult, ct) → StepResult<TState>              (terminal)
                          │
                          ▼
 IChatClient pipeline (ChatClientBuilder, unchanged order):
   [host configurator] → UseStrategosFunctions(tools, toolSources) → UseFunctionInvocation()
                                            │
              StrategosFunctionsChatClient resolves IToolSource[] lazily, caches,
              merges [host > Strategos WithTool > tool-sources] into ChatOptions.Tools

 IToolSource (port, Strategos.Agents.Abstractions)
   Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken)
     ├─ McpToolSource        (Strategos.Agents.Mcp)  full MCP 2025-11-25 spec, IAsyncDisposable
     └─ AgentToolSource      (Strategos.Agents)      AIFunctionFactory.Create over [AgentTool] methods
```

Dependencies point inward: `Strategos.Agents` depends only on MEAI abstractions; the MCP adapter and any ontology catalog live at the host/sub-package boundary (DIM-1).

## Requirements

**Track S — Streaming (#89)**

### DR-1 — Streaming orchestration path in `AgentStepBase`

When a streaming sink is configured, `ExecuteAsync` calls `GetStreamingResponseAsync`, accumulates the `ChatResponseUpdate` sequence into a `ChatResponse<TResult>` (`updates.ToChatResponse()`), and then runs the identical terminal logic as the buffered path. The buffered path (default) is unchanged.

**Acceptance criteria:**
- With no streaming configured, `ExecuteAsync` behavior is byte-for-byte the current `GetResponseAsync<TResult>` path (existing tests stay green).
- With streaming configured, the orchestrator calls `GetStreamingResponseAsync` and never `GetResponseAsync<TResult>` (asserted via a fake `IChatClient` recording which method was invoked).
- The accumulated `ChatResponse<TResult>` passes through the **same** `FinishReason==ToolCalls` (AGAG005), `TryGetResult`, empty-payload (AGAG006), and structured-output (AGAG002) checks — verified by a test that drives a malformed streamed payload and asserts `AgentStructuredOutputException` with `Diagnostic == "AGAG002"`.
- `ApplyResult` is invoked exactly once, after the stream completes, with the materialized `TResult`.

### DR-2 — `AgentStepBuilder.WithStreaming(IStreamingHandler)` opt-in

A new builder method registers the streaming sink. Streaming is opt-in; absence preserves the buffered default. The handler is the existing `IStreamingHandler` (keyed on `workflowId`/`stepName` — domain-aligned, INV-4).

**Acceptance criteria:**
- `WithStreaming(IStreamingHandler)` returns the builder (chainable); passing `null` throws `ArgumentNullException`.
- Calling `WithStreaming` twice throws `InvalidOperationException` (mirrors `WithChatOptions` single-call semantics).
- The handler reaches `AgentStepConfiguration` as a new (nullable) field; `Configuration` exposes it for white-box tests.
- A reflection test confirms `AgentToolSource`, `AgentStepConfiguration`, and any new options type remain `sealed` (INV-6).

### DR-3 — Per-update token delivery; non-durable side-channel (INV-1)

For each `ChatResponseUpdate` with non-empty text, the orchestrator awaits `handler.OnTokenReceivedAsync(update.Text, workflowId, stepName, ct)` in stream order; on completion it awaits `handler.OnResponseCompletedAsync(fullText, workflowId, stepName, ct)`. Tokens are **not** written to `IProgressEventStore` and **not** emitted as `StreamingTokenReceived` events — the durable record is the terminal `StepResult` only.

**Acceptance criteria:**
- An in-test `IStreamingHandler` receives token callbacks in order, then exactly one completion callback, with `fullText` equal to the concatenated deltas.
- `workflowId`/`stepName` are sourced from `StepContext` (no synthesized specialist-world `SpecialistType`/`TaskId`).
- Grep gate: the streaming code path contains no reference to `IProgressEventStore` or `StreamingTokenReceived` (INV-1 — no parallel durable log).
- The legacy `StreamingTokenReceived` record and `StreamingExecutionMode` enum are left untouched (or removed under DR-5 hygiene if confirmed vestigial), but are *not* wired into the new path.

### DR-4 — Streaming failure & cancellation modes (error-path requirement)

Cancellation propagates unwrapped from mid-stream (not a domain failure, per the 2.7.0 cancellation contract). A handler that throws is surfaced, never swallowed (DIM-2): the orchestrator wraps it as `AgentStreamingException` carrying `AGAG009` and the step fails loud. A stream that yields zero updates funnels into the existing empty-payload check (`AGAG006`).

**Acceptance criteria:**
- A fake `IChatClient` whose stream throws `OperationCanceledException` mid-enumeration propagates it unwrapped (no `AgentException` wrap).
- An `IStreamingHandler` whose `OnTokenReceivedAsync` throws produces `AgentStreamingException` with `Diagnostic == "AGAG009"`; the input `TState` is reference-equal to the value observed after the throw (no partial mutation).
- A stream that completes with no updates throws `AgentChatResponseException` (`AGAG006`) — the same code as the buffered empty case.
- There is no `catch { }` or `catch (Exception)` without rethrow/wrap in the streaming path (grep gate, mirrors the 2.7.0 error-path sweep).

### DR-5 — Streaming docs & legacy-type reconciliation (hygiene)

The README gains a ≤15-line streaming example. The pre-2.7.0 `IStreamingHandler` is confirmed free of specialist-era coupling before reuse; `StreamingTokenReceived`/`StreamingExecutionMode`, if confirmed to have no remaining consumer after this work, are removed (DIM-5) or explicitly documented as specialist-only.

**Acceptance criteria:**
- README streaming example compiles and is ≤15 lines including usings.
- A grep/consumer audit of `StreamingTokenReceived`, `StreamingExecutionMode`, and `IStreamingHandler` is recorded in the PR description; anything with zero production consumers after this PR is deleted or annotated.
- DocFX `<summary>` on every new public type passes the `scripts/check-prose.sh` AI-vocabulary gate (DIM-8).

**Track T — Tool sources (#90)**

### DR-6 — `IToolSource` port replaces `IMcpToolSource` (clean break)

`IToolSource` is declared in `Strategos.Agents.Abstractions` with `Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken)`. `IMcpToolSource` is deleted in the same PR. No `[Obsolete]` shim ([[feedback_no_handwavy_mitigations]]).

**Acceptance criteria:**
- `IMcpToolSource` does not exist after this PR (grep gate over `src/`).
- `IToolSource` carries no `ModelContextProtocol` dependency (the abstraction stays MCP-free, INV-3 separation).
- A reflection test asserts `McpToolSource : IToolSource`.

### DR-7 — `McpToolSource` retargeted onto `IToolSource`, full spec preserved (INV-3)

The existing `McpToolSource` (in `Strategos.Agents.Mcp`) implements `IToolSource`. MCP-specific behavior — handshake, endpoint redaction, `AGAG004`, `IAsyncDisposable` lifecycle — stays in the adapter, not the port (DIM-6 single responsibility).

**Acceptance criteria:**
- `McpToolSource` still resolves full MCP 2025-11-25-spec tools; the `ModelContextProtocol.Client` version pin is documented in CHANGELOG (INV-3).
- Handshake failure still throws `AgentMcpException` (`AGAG004`) with the endpoint redacted.
- `McpToolSource` remains `IAsyncDisposable`; the orchestrator does not own its lifetime.

### DR-8 — `AgentToolSource` in-process reflection adapter

A new sealed `AgentToolSource` in `Strategos.Agents` implements `IToolSource` by reflecting `[AgentTool]`-annotated methods on a supplied instance via `AIFunctionFactory.Create`. A `FromDelegates(params Delegate[])` escape hatch is also provided. The `[AgentTool]` attribute (not `[AgentFunction]` — INV-4) is the opt-in discovery marker. No ontology dependency enters `Strategos.Agents` (DIM-1).

**Acceptance criteria:**
- `AgentToolSource.FromObject(instance)` returns an `IToolSource` whose `GetToolsAsync` yields one `AIFunction` per `[AgentTool]` method, names taken from the method (or attribute override).
- A method's `[Description]` flows into the `AIFunction` description (parameter schema generated by `AIFunctionFactory`).
- `FromDelegates(d1, d2)` yields two tools; an empty source yields an empty list (not null).
- A resolution failure (reflection/factory throws) surfaces as `AgentToolSourceException` with `Diagnostic == "AGAG007"` and the source type named — distinct from MCP's `AGAG004`.
- `AgentToolSource` is `sealed` (INV-6) and has no `ModelContextProtocol` reference (grep gate).

### DR-9 — Builder `.WithToolSource(IToolSource)`; multi-source merge

`WithMcpToolSource` is replaced by `WithToolSource(IToolSource)`, which **accumulates** (multiple sources allowed). `AgentStepConfiguration` carries `IReadOnlyList<IToolSource>`. `StrategosFunctionsChatClient` is generalized (`_toolSources`, `ResolveToolsAsync`) to resolve each source lazily-once, cache, and merge in precedence order: host-supplied > Strategos `WithTool` > tool-sources (registration order).

**Acceptance criteria:**
- `WithMcpToolSource` does not exist after this PR.
- Two `WithToolSource(...)` calls accumulate; both sources' tools appear in the merged `ChatOptions.Tools`.
- Merge precedence asserted: a name collision keeps the host tool over a Strategos tool over a tool-source tool (extends the existing `MergeToolsAsync` test).
- Each `IToolSource` is resolved at most once per middleware instance (cached); resolution is lazy on first request (`Build()` stays synchronous — the T-016 invariant).
- Duplicate tool names *across* in-process `WithTool` registrations still throw `AgentDuplicateToolException` (`AGAG003`) at `Build()`.

### DR-10 — Migration & documentation sweep (clean break)

CHANGELOG documents the break; README replaces the MCP-only example with an `IToolSource` example showing both an `AgentToolSource` and `McpToolSource`. Issue #91 is closed (cadence decided: the answer is "MCP is one adapter behind a shared port").

**Acceptance criteria:**
- CHANGELOG `## [Unreleased]` (or the target version) has a `### Changed (BREAKING) — Tool source port` section with a before/after migration recipe.
- README shows registering an in-process catalog and an MCP source through the same `.WithToolSource(...)` method.
- No `IMcpToolSource`/`WithMcpToolSource` references survive outside CHANGELOG history (grep gate).

**Cross-cutting**

### DR-11 — Diagnostic allocation `AGAG007`–`AGAG009` (INV-5)

New stable IDs on `AgentDiagnostics`: `AGAG007` (in-process tool-source resolution failure), `AGAG008` (builder validation — reserved for a future streaming/tool misconfiguration; allocate only if a builder-time case materializes), `AGAG009` (streaming-handler failure, DR-4). Each is a `const string` literal; every raising exception carries a populated `Diagnostic`.

**Acceptance criteria:**
- `AgentDiagnostics.AGAG007` and `AGAG009` exist as `"AGAG00#"` literals; `AGAG008` is allocated only if DR-2/DR-9 surface a build-time validation case (otherwise the number is reserved, not skipped-and-reused).
- Reflection over `AgentException` subclasses confirms every new exception type sets `Diagnostic` in every constructor (extends the existing `AgentExceptionHierarchyTests`).
- README Diagnostics section enumerates the new codes with a one-line remediation each.
- Grep gate: `AGAG00[0-9]` literals appear only in `AgentDiagnostics.cs` and exception-construction sites.

### DR-12 — Real-chain integration test extension (DIM-4 wiring parity)

`AgentStepBaseIntegrationTests` is extended to exercise both new capabilities through the **full** `ChatClientBuilder` pipeline with the fake `IChatClient` at the bottom: (i) streaming materializes typed output and fires handler callbacks before the terminal `ApplyResult`; (ii) an in-process `AgentToolSource` tool is resolved and invoked via `UseFunctionInvocation`; (iii) an MCP `IToolSource` (in-process fake adapter) still resolves; (iv) two tool sources merge with correct precedence.

**Acceptance criteria:**
- The streaming integration test asserts handler token callbacks occurred before `ApplyResult` ran (ordering observable via a shared recorder).
- The tool-source integration test asserts an `AgentToolSource` function was actually invoked (round-trip), not merely registered.
- All assertions run in the standard test job — no `[Trait("Category","Integration")]` filter that could be silently skipped (mirrors the 2.7.0 real-chain gate).
- Mocking is confined to the `IChatClient` boundary; `AgentToolSource`, `AgentStepBuilder`, `StrategosFunctionsChatClient` are constructed real ([[feedback_implementer_no_exarchos_mcp]]).

## Integration Points

- **Basileus** — package-version bump only. Verified: zero references to `IMcpToolSource`/`WithMcpToolSource`/`WithTool` in basileus production code; the clean break costs it nothing today.
- **Ontology** — the in-process `AgentToolSource` is the supported way for a consumer (basileus, or a future `Strategos.Rag` host) to expose ontology lookups as tools. Strategos ships the adapter mechanism, not the ontology catalog (DIM-1).
- **G1 identity** — orthogonal; identity rides the saga seam, not the step seam.

## Out of scope (deferred)

- **Incremental typed streaming** (progressive `TResult` snapshots via partial-JSON) — rejected (§Alternatives, INV-7/DIM-7).
- **Source-generated tool catalog** (`[AgentTool]` + Roslyn emit) — deferred; `AIFunctionFactory.Create` at runtime is sufficient. Revisit only if reflection cost is measured to matter.
- **Streaming through Wolverine/SignalR transport** — the consumer's `IStreamingHandler` implementation owns transport; Strategos defines the port only.
- **`AgentToolLoopException` payload format (#92)** — independent; not touched here.

## Risks & mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `ToChatResponse()` accumulation semantics differ from `GetResponseAsync<T>` for structured output across MEAI patch versions | Streamed typed output silently diverges from buffered | DR-1 asserts both paths converge on identical AGAG002/006 behavior; pin verified against MEAI 10.5.2 at PR time. |
| Clean break strands an unseen `IMcpToolSource` consumer | Compile break downstream | Verified zero consumers in basileus and `src/` outside `Strategos.Agents*`; MCP sub-package still preview (#91). |
| Handler exceptions aborting steps frustrate consumers who want best-effort UX | Streaming step fails on a cosmetic callback error | DR-4 makes the fail-loud contract explicit and documented; a consumer wanting best-effort wraps their own handler. No silent swallow (DIM-2). |
| Multi-source resolution adds unbounded latency on first request | First streamed token delayed by tool discovery | Each `IToolSource` resolved once and cached (DR-9); resolution is lazy and bounded by the source's own contract. |

## Testing Strategy

**Unit** (`Strategos.Agents.Tests/Unit/`): builder validation for `WithStreaming`/`WithToolSource` (DR-2, DR-9); `AgentToolSource` reflection over `[AgentTool]` (DR-8); diagnostic literals (DR-11); failure-path tests for `AGAG007`/`AGAG009` asserting `Diagnostic` and state reference-equality (DR-4, DR-8). `IChatClient` faked via in-test stub; internal collaborators real.

**Integration** (`Strategos.Agents.Tests/Integration/`): the DR-12 real-chain gate — streaming + tool-source through the full pipeline, fake `IChatClient` at the bottom, no category filter.

**Cross-product smoke** (`tests/basileus-smoke/`): the existing surface assertion plus the package-version bump; confirms the clean break compiles against the basileus-consumed surface.

## Open Questions

1. **`AGAG008` allocation.** Reserve vs. assign — depends on whether DR-2/DR-9 surface a genuine *build-time* validation case (vs. runtime). Resolve in plan.
2. **Legacy streaming types (DR-5).** Delete `StreamingTokenReceived`/`StreamingExecutionMode` outright, or retain for a not-yet-deleted specialist path? Needs a consumer audit in plan — deletion preferred if truly orphaned (DIM-5).
3. **Version/milestone placement.** This is a second BREAKING agent-surface change in the 2.7.0 line (tool-source clean break). Cut as `2.7.1`/`2.8.0`? The "2.8.0 Cross-product schema substrate" milestone is themed differently — confirm versioning at plan-review.

## Alternatives Considered

Two axes were evaluated against DIM-1..8 and INV-1..8 during ideation. The selected approach (§Chosen Approach) is streaming-as-observability + unified `IToolSource` clean break. Rejected options below for provenance.

### Option 1: Streaming as an observability layer + unified `IToolSource` (selected)

See Chosen Approach and Technical Design. Streaming funnels into the terminal typed contract via `IStreamingHandler`; `IMcpToolSource` generalized to `IToolSource` with MCP + in-process adapters. Strongest INV-1 / INV-4 / INV-7 and DIM-5 alignment.

### Option 2: Incremental typed streaming (`ApplyPartialResult` via partial JSON)

`ApplyResult` (or a new `ApplyPartialResult`) receives progressively-completed `TResult` snapshots parsed from partial JSON deltas.

**Why rejected:** fragile partial-JSON parsing, a half-built `TResult` is a footgun, and the workflow step contract is terminal regardless — the engine consumes one `StepResult` (INV-7, DIM-7). Consumers need progressive *text*, not progressive typed state.

### Option 3: Dual-method streaming contract (`ExecuteStreamingAsync` on the interface)

Add a second execute method returning `IAsyncEnumerable<...>` parallel to `ExecuteAsync`.

**Why rejected:** breaks `IWorkflowStep` lowering — a streaming execute has no terminal `StepResult` to return to, and the SG-emitted saga drives the terminal contract (INV-1, DIM-6).

### Option 4: Tool source as a sibling port / additive base / source-generated

Three sub-variants for #90, all rejected in favor of the unified clean break:
- **Sibling `IAgentFunctionSource`:** "function" leaks MEAI's CLR type onto the authoring surface (INV-4); a second identical-signature port duplicates one behavior (DIM-5).
- **Additive `IToolSource` base over a retained `IMcpToolSource`:** non-breaking but leaves two registration methods for one behavior (DIM-5) and a vestigial name to retire later; the clean break is cheap given zero consumers.
- **Source-generated `[AgentTool]` + Roslyn emit:** premature; `AIFunctionFactory.Create` already does it in one line at acceptable runtime cost.

## References

- Issues: [#89 streaming](https://github.com/lvlup-sw/strategos/issues/89), [#90 tools-as-AIFunctions](https://github.com/lvlup-sw/strategos/issues/90), [#91 Mcp cadence](https://github.com/lvlup-sw/strategos/issues/91)
- Predecessor design: [MEAI 10.5 adoption (2026-05-17)](2026-05-17-strategos-agents-meai-10-5.md)
- MEAI 10.5: <https://devblogs.microsoft.com/dotnet/announcing-microsoft-extensions-ai-10-5/>
- Strategos invariants: [[INV-1]] (durability), [[INV-3]] (MCP latest spec), [[INV-4]] (concrete nomenclature), [[INV-5]] (stable diagnostic IDs), [[INV-6]] (sealed-by-default), [[INV-7]] (immutable state)
- Axiom dimensions: DIM-1 (Topology), DIM-2 (Observability), DIM-4 (Test Fidelity), DIM-5 (Hygiene), DIM-6 (Architecture), DIM-7 (Resilience), DIM-8 (Prose)
