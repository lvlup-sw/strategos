# Strategos.Agents — Microsoft.Extensions.AI 10.5 adoption

**Date:** 2026-05-17 · **Milestone:** [Strategos 2.7.0 — Agent Capabilities](https://github.com/lvlup-sw/strategos/milestone/3) · **Tracks:** [#45](https://github.com/lvlup-sw/strategos/issues/45)

## Problem Statement

`Strategos.Agents` 2.6.x adopted MEAI 10.0.1 as a behavior-preserving refactor. The 10.5 surface introduces five capabilities that are now live in our dependency graph but unwired:

1. **Structured output** via `IChatClient.GetResponseAsync<T>(...)` and `ChatResponse<T>.TryGetResult(out T)`.
2. **Tool-use loop** as middleware via `ChatClientBuilder.UseFunctionInvocation()`.
3. **`AIFunction` reflection** via `AIFunctionFactory.Create(...)` to expose workflow primitives as tools.
4. **MCP-client-as-tools** via `McpClientFactory.CreateAsync(...) → ListToolsAsync() → IList<AIFunction>`.
5. **Composition middleware** (`UseLogging`, `UseOpenTelemetry`, `UseDistributedCache`) on top of `UseFunctionInvocation`.

The current `AgentStepBase<TState>` makes a single `_chatClient.GetResponseAsync(messages, options: null, ct)` call, reads `response.Text`, and hands a raw string to an abstract `ApplyResultAsync`. `GetOutputSchemaType()` is declared on `IAgentStep<TState>` but unused by the runtime. Every subclass parses strings by hand — DIM-3 contracts gap and a perennial source of brittle deserialization.

**Greenfield affordance:** a repo-wide grep for `AgentStepBase` / `IAgentStep<` returns *zero* production subclasses anywhere — the only consumer is one in-repo test fixture (`AgentStepBaseTests.TestAgentStep`). Basileus references `LevelUp.Strategos.Agents` for factories and conversation infrastructure but its production `ThinkStep`/`ActStep`/etc. implement `IWorkflowStep<TState>` directly. We can break the agent-step contract freely.

## Chosen Approach

**Composition over inheritance.**


`AgentStepBase` becomes a **sealed** orchestrator parameterized on `(TState, TResult)`. Subclassing is deleted as an extension model. Step authors compose instances through a fluent `AgentStepBuilder<TState, TResult>` that mirrors the [[INV-4 concrete-DSL-nomenclature]] family already established by `IWorkflowBuilder<TState>`. Hook delegates provide the per-step behavior (system prompt, user prompt, result-application). Tool composition, MCP federation, and middleware all live at the builder; the orchestrator does one job — execute MEAI 10.5's chat loop with strategy injected from the builder.

Alternatives A (keep abstract base, add generic) and B (parallel base classes) are documented in §8 and rejected on hygiene (DIM-5 / [[INV-2]]) and architecture (DIM-6 / [[INV-6]]) grounds.

## Technical Design

```
                    ┌──────────────────────────────────────────────────────┐
                    │  AgentStepBuilder<TState, TResult>                   │
                    │  ─────────────────────────────────                   │
                    │   .WithSystemPrompt(Func<TState, string>)            │
                    │   .WithUserPrompt(Func<TState, string>)              │
                    │   .WithApplyResult(Func<TState, TResult, CT, Task<StepResult<TState>>>)
                    │   .WithTool(AIFunction)                              │
                    │   .WithToolSource(IToolSource)                       │
                    │   .WithChatOptions(ChatOptions)                      │
                    │   .ConfigureChatClient(Action<ChatClientBuilder>)    │
                    │   .Build(IChatClient) → IAgentStep<TState, TResult>  │
                    └──────────────────────────────────────────────────────┘
                                            │
                                            ▼
                    ┌──────────────────────────────────────────────────────┐
                    │  AgentStepBase<TState, TResult>  (sealed)            │
                    │  ─────────────────────────────────                   │
                    │  ctor(IChatClient, AgentStepConfiguration<TState, TResult>)
                    │  ExecuteAsync ─→                                     │
                    │    1. assemble context (optional IContextAssembler)  │
                    │    2. build messages from hook delegates             │
                    │    3. chatClient.GetResponseAsync<TResult>(messages, options, ct)
                    │    4. invoke applyResult hook with TResult           │
                    └──────────────────────────────────────────────────────┘
                                            │
                                            ▼
                    ┌──────────────────────────────────────────────────────┐
                    │  IChatClient   (composed via ChatClientBuilder at DI)│
                    │   .UseStrategosFunctions(tools)                      │
                    │   .UseStrategosOntologyMcp(options)                  │
                    │   .UseFunctionInvocation()                           │
                    │   .UseLogging() / .UseOpenTelemetry() / .UseDistributedCache()
                    └──────────────────────────────────────────────────────┘
```

Dependencies point inward: `Strategos.Agents` depends only on `Microsoft.Extensions.AI.Abstractions` + `ModelContextProtocol.Abstractions`; MCP client implementations and middleware live at host composition (Basileus.AgentHost, or strategos hosting extensions).

## Requirements

### DR-1 — Sealed `AgentStepBase<TState, TResult>` + contract evolution

`IAgentStep<TState>` is replaced by `IAgentStep<TState, TResult> : IWorkflowStep<TState>`. The interface drops `GetOutputSchemaType()` (type parameter subsumes it) and drops `GetSystemPrompt()` (now a closed-over delegate). `AgentStepBase<TState, TResult>` is the **only** implementation and is `sealed`. Its constructor takes `IChatClient` and `AgentStepConfiguration<TState, TResult>` (a sealed record carrying the hook delegates and configured options).

**Acceptance criteria:**
- `IAgentStep<TState, TResult>` declared in `Strategos.Agents.Abstractions`; previous `IAgentStep<TState>` and `GetOutputSchemaType()` deleted in same PR.
- `AgentStepBase<TState, TResult>` is `sealed`.
- A consumer attempting to subclass `AgentStepBase` fails to compile (asserted by a negative test in `Strategos.Agents.Tests` referencing `[Fact(Skip = "regression net — uncomment to verify")]` ❌ — instead **asserted via reflection**: `typeof(AgentStepBase<,>).IsSealed` is `true`).
- Reflection test confirms `IAgentStep<,>` extends `IWorkflowStep<>` (workflow lowering remains unchanged).
- Existing `AgentStepBaseTests.TestAgentStep` subclass is deleted; its tests rewrite to build instances via `AgentStepBuilder` (DR-2).

### DR-2 — `AgentStepBuilder<TState, TResult>` fluent API

A sealed builder produces configured `AgentStepBase` instances. The builder is the only public construction path; `AgentStepBase`'s constructor is `internal`. Methods follow the strategos fluent convention (chainable returns, terminal `.Build()` returning the interface, not the concrete type).

**Acceptance criteria:**
- `AgentStepBuilder<TState, TResult>` is `sealed`.
- Required hooks (`WithSystemPrompt`, `WithUserPrompt`, `WithApplyResult`) — calling `.Build()` without any one throws `InvalidOperationException` with diagnostic `AGAG001` and a remediation message naming the missing hook.
- Optional hooks (`WithTool`, `WithMcpToolSource`, `WithChatOptions`, `ConfigureChatClient`) are additive — multiple `WithTool(...)` calls accumulate; redundant `WithChatOptions(...)` calls throw `InvalidOperationException`.
- `Build()` returns `IAgentStep<TState, TResult>` (interface, not concrete).
- Reflection test: builder has no public constructor parameters (the parameterless default constructor is the only way in).

### DR-3 — Structured output via `GetResponseAsync<TResult>`

The orchestrator calls `_chatClient.GetResponseAsync<TResult>(messages, options, ct)`. On `ChatResponse<TResult>.TryGetResult(out var result)` returning `false`, the orchestrator throws an `AgentStructuredOutputException` carrying diagnostic `AGAG002` with the failed JSON payload truncated to ≤4 KB.

**Acceptance criteria:**
- Happy-path test: a builder configured with `TResult = MyDto` receives an `IChatClient` returning a valid `ChatResponse<MyDto>`; `ApplyResult` is invoked with the typed `MyDto` instance.
- Failure-path test: `IChatClient` returns a `ChatResponse<MyDto>` whose `TryGetResult` returns `false`; `AgentStructuredOutputException` is thrown with `Diagnostic == "AGAG002"` and `RawPayload != null`.
- Unstructured case: a builder with `TResult = string` works — `ChatResponse<string>` always yields the text.
- No fallback path: structured output failure never silently returns `default(TResult)` (DIM-2 / DIM-5 guard).

### DR-4 — `AIFunction` registration via builder + `UseFunctionInvocation` middleware

`AgentStepBuilder.WithTool(AIFunction)` accumulates tools into a private list. The builder injects them into the final `ChatOptions.Tools` and *also* registers a `UseStrategosFunctions(IEnumerable<AIFunction>)` extension on the underlying `ChatClientBuilder` so the tool-invocation middleware sees them. The orchestrator never invokes tools directly — `UseFunctionInvocation()` does, configured by the host.

**Acceptance criteria:**
- Integration test using `ChatClientBuilder.UseFunctionInvocation()` + a fake `IChatClient` that emits a tool-call message; the registered `AIFunction` is invoked and the result is fed back into the loop.
- Builder rejects duplicate tool names with `InvalidOperationException` carrying `AGAG003` (collision detection at `.Build()` time, not runtime).
- A `WithTool(AIFunctionFactory.Create(...))` example is added to README replacing the current string-parsing example.

### DR-5 — MCP-client-as-tools via `IMcpToolSource` port

A port — `IMcpToolSource` — exposes `Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken)`. The builder accepts an `IMcpToolSource`; at `.Build()` time it composes a deferred `Func<CancellationToken, Task<IReadOnlyList<AIFunction>>>` that gets folded into the tool list lazily on first execution. The default implementation (shipped in `Strategos.Agents.Mcp`, a new sub-package) wraps `ModelContextProtocol.Client.McpClientFactory`. Other adapter implementations (e.g. an in-process port for tests) plug in via DI.

**Acceptance criteria:**
- `IMcpToolSource` is a port in `Strategos.Agents.Abstractions` — no `ModelContextProtocol` dependency on the abstraction.
- Default adapter `McpToolSource` ships in `Strategos.Agents.Mcp` (separate package) referencing `ModelContextProtocol.Client`.
- MCP handshake failure during first-execution resolution throws `AgentMcpException` with diagnostic `AGAG004` and the server endpoint redacted of any credentials.
- MCP client lifecycle: the adapter implements `IAsyncDisposable`; the orchestrator does not own its lifetime (host owns it).
- Per [[INV-3 mcp-first-class-latest-spec]]: package version of `ModelContextProtocol.Client` pinned to the latest 2025-11-25-spec-conformant release at PR time, documented in CHANGELOG.

### DR-6 — Composition middleware via `ConfigureChatClient` escape hatch

`AgentStepBuilder.ConfigureChatClient(Action<ChatClientBuilder>)` exposes the underlying builder so host code can layer `UseLogging()`, `UseOpenTelemetry()`, `UseDistributedCache()` etc. The orchestrator does NOT prescribe middleware — host composition does. The builder applies the configurator in a fixed order: `[host configurator] → UseStrategosFunctions(tools) → UseFunctionInvocation()`. Order is asserted by integration test.

**Acceptance criteria:**
- Integration test: a host configurator that calls `.UseLogging()` produces a `ChatClient` whose pipeline includes logging *before* function invocation.
- A consumer calling `.ConfigureChatClient(b => b.UseDistributedCache(cache))` sees cached responses on repeat calls (real `MemoryCache`, not a mock — DIM-4 wiring parity).
- Documented in README; the demo example wires logging + OTel + tool invocation in the canonical order.

### DR-7 — Diagnostic ID family `AGAG001`–`AGAG006`

[[INV-5 stable-diagnostic-ids]] requires AGAG codes. Allocated in this PR:
- **AGAG001** — `AgentStepBuilder.Build()` invoked with a required hook missing.
- **AGAG002** — Structured-output deserialization failure (DR-3).
- **AGAG003** — Duplicate tool name registered (DR-4).
- **AGAG004** — MCP handshake / discovery failure (DR-5).
- **AGAG005** — Tool-iteration count exceeded (DR-8).
- **AGAG006** — `IChatClient` returned `null` or empty `ChatResponse<TResult>` text (DIM-2 guard).

Each code is a `const string` on a `public static class AgentDiagnostics` in `Strategos.Agents.Abstractions`, mirroring the AGWF/AONT catalogs. The `Strategos.Agents` README has a Diagnostics section with a 1-line summary per code and a remediation pointer.

**Acceptance criteria:**
- `AgentDiagnostics.AGAG001`–`AGAG006` declared as `const string` literals in the form `"AGAG###"`.
- Every exception raised by the new agent runtime carries a `Diagnostic` property (`string`) populated with one of these codes — asserted by reflection over `AgentException` subclasses.
- Grep test: `grep -rn 'AGAG0[0-9]\{2\}' src/Strategos.Agents/ --include='*.cs' | grep -v Generated/` only returns hits in `AgentDiagnostics.cs` and in exception-construction sites (no string-literal proliferation).
- README diagnostics section enumerates all six.

### DR-8 — Bounded tool-iteration enforcement

`UseFunctionInvocation()` accepts a `maximumIterationsPerRequest` knob. The orchestrator sets a default of **8** (matching MEAI's documented sensible upper bound) but the builder exposes `.WithMaxToolIterations(int)` for override. Exceeding the limit raises `AgentToolLoopException` carrying `AGAG005` and the partial tool-call trace.

**Acceptance criteria:**
- Default value `8` declared as `const int AgentStepBase.DefaultMaxToolIterations`.
- Integration test: a fake `IChatClient` that always emits a tool call (no terminal response) trips the limit at exactly 8 iterations; `AgentToolLoopException` is thrown with `Diagnostic == "AGAG005"`.
- Per [[feedback_no_handwavy_mitigations]]: the bound is **mechanically enforced** by `UseFunctionInvocation` configuration, NOT documented in XML doc and trusted at runtime.
- `.WithMaxToolIterations(0)` is rejected at builder time with `ArgumentOutOfRangeException`.

### DR-9 — Real-chain integration test gate

A new integration test class `Strategos.Agents.Tests.AgentStepBaseIntegrationTests` constructs the **full** `ChatClientBuilder` chain end-to-end with a fake `IChatClient` at the *bottom* of the pipeline (not substituted at the top). It exercises: typed output, tool invocation (≥1 round-trip), MCP tool resolution (via an in-process `IMcpToolSource` adapter), and middleware ordering. This is the DIM-4 wiring-parity guard.

**Acceptance criteria:**
- Test class lives at `tests/Strategos.Agents.Tests/AgentStepBaseIntegrationTests.cs`.
- The fake `IChatClient` is composed at the bottom of the chain — verified by the integration test interrogating its received calls *after* middleware layers.
- Test covers: (i) structured output success, (ii) one tool round-trip via `AIFunction`, (iii) MCP tool resolved through in-process `IMcpToolSource`, (iv) logging middleware actually logged something (asserted via `ITestOutputHelper`-backed logger).
- CI runs this test in the standard test job — not gated behind an `[Trait("Category","Integration")]` filter — so it cannot be silently skipped.

### DR-10 — Error-path sweep (DIM-2 / failure mode coverage)

Every new error path produces an `AgentException` (or a subclass) with: `Diagnostic` (AGAG###), `Message` (what failed + why + what to do), and contextual properties (failed payload truncated to ≤4 KB, server endpoint redacted, tool name involved, etc.). No `catch { }` swallowing anywhere in the new code. Every `catch (Exception)` either rethrows or wraps with `new AgentXxxException(...)`.

**Acceptance criteria:**
- Reflection: every `public sealed class *Exception : AgentException` declares `Diagnostic` and `Message` with non-default values in every constructor.
- Grep gate: `grep -rEn 'catch *\(' src/Strategos.Agents/ | grep -v 'catch (Exception' | grep -v Generated/` returns zero hits (catches must be explicit type catches; no parameterless catch).
- Failure-path tests for each error code (AGAG001 through AGAG006) exist and assert both `Diagnostic` and the absence of partial state mutation (state passed in equals state observable after the throw).
- Negative test: an `IChatClient` whose `GetResponseAsync<T>` throws `TaskCanceledException` propagates the cancellation — orchestrator does not wrap it as an `AgentException` (cancellation is not a domain failure).

### DR-11 — Migration & documentation sweep

The old `IAgentStep<TState>` and abstract `AgentStepBase<TState>` are deleted in the same PR. The README example is rewritten end-to-end. CHANGELOG documents the break under `## [2.7.0]` `### Changed (BREAKING)` with a migration recipe. `Strategos.Agents.Tests.AgentStepBaseTests.TestAgentStep` is deleted and replaced with builder-based test fixtures.

**Acceptance criteria:**
- `IAgentStep<TState>` (single-arity) does not exist in `Strategos.Agents.Abstractions` after this PR.
- `AgentStepBase<TState>` (single-arity) does not exist after this PR.
- README has zero references to subclassing as an extension model.
- CHANGELOG `## [2.7.0]` includes a section `### Changed (BREAKING) — Agent step contract` with a 5–10 line migration recipe (before/after code blocks).
- No deprecated `[Obsolete]` shims — clean break per the user feedback `[[feedback_no_handwavy_mitigations]]` (we don't carry forward two-ways-to-do-the-same-thing because of compat concerns when nobody is consuming the old way).
- DocFX/XML-doc sweep: every new public type has a `<summary>` written in a domain-expert voice (DIM-8) — no AI-generic tells; grep gate against the AI-vocabulary cluster list lives in `scripts/check-prose.sh` and runs in CI.

## Integration Points

**Basileus** — no source impact. Basileus consumes `LevelUp.Strategos.Agents` for `IConversationThreadManager`, `IWorkflowAgentFactory`, `IStreamingHandler`, and related infrastructure types — none of which touch `IAgentStep<TState>`. The package-version bump is the only consumer-side change. Verified by `grep -r "IAgentStep\b\|AgentStepBase\b" basileus/` returning zero production hits as of 2026-05-17.

**Exarchos** — none. Exarchos has no .NET surface.

**G1 identity** — orthogonal. Agent identity (PR #81's `AgentIdentity` / `WorkflowIdentity`) lives at the saga seam, not the step seam. `AgentStepBase` does not interact with identity directly; if a host wants identity-aware logging it composes it at the `ChatClientBuilder` layer per DR-6.

## Out of scope (deferred)

- **Streaming responses** — `Strategos.Agents` already exposes an `IStreamingHandler` port. Adopting MEAI 10.5's `GetStreamingResponseAsync<T>` shape into the new orchestrator is mechanically additive on top of DR-3 but defers to v2.8.0+. Tracked separately.
- **Workflow primitives auto-reflected as AIFunctions** — issue #45 mentions reflecting "ontology lookups" via `AIFunctionFactory.Create`. Concrete catalog deferred to a follow-up; DR-4 provides the wiring affordance via `WithTool(AIFunction)`.
- **G5 SubagentSpawn OBO token exchange** — separate seam (basileus side); consumes `AgentIdentity` from PR #81, not the agent-step contract.

## Risks & mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| MEAI 10.5 `ChatResponse<T>.TryGetResult` semantics change in a 10.6/11.0 patch | Structured output silently breaks | DR-3 acceptance test pins the `TryGetResult` contract; CI Renovate PR to MEAI 10.6+ runs the test before merge. |
| `UseFunctionInvocation()` default-loop-count drift across MEAI versions | Tool runaway in production | DR-8 sets an explicit `maximumIterationsPerRequest = 8` regardless of MEAI default. |
| ModelContextProtocol package surface still evolving | MCP adapter brittleness | DR-5 puts MCP adapter in a separate `Strategos.Agents.Mcp` sub-package so its version cadence is independent. |
| Tests pass via mocked `IChatClient` but production composition is broken | DIM-4 false confidence — past pattern in [[feedback_implementer_no_exarchos_mcp]] | DR-9 mandates real-chain integration test that constructs the full `ChatClientBuilder` pipeline. |
| Builder ergonomics regress on simple cases | Friction for trivial steps | The README's "trivial example" should be ≤15 lines including using statements. If it isn't, the builder needs convenience overloads (`WithStaticSystemPrompt(string)`, `WithUserPromptFromState(Func<TState, string>)` short forms). |

## Testing Strategy

Three layers, each addressing a distinct DIM-4 concern:

**Unit tests (`Strategos.Agents.Tests/Unit/`):** isolate the orchestrator and the builder. Cover happy-path typed output (DR-3), every failure-path diagnostic AGAG001–AGAG006 (DR-7, DR-10), and builder-validation rejection paths (DR-2). `IChatClient` is faked via a small in-test stub that returns a configured `ChatResponse<TResult>` — no NSubstitute, no Verify. State immutability is asserted by reference equality after the throw (DR-10 acceptance criterion).

**Integration tests (`Strategos.Agents.Tests/Integration/`):** the DR-9 real-chain gate. Constructs the full `ChatClientBuilder` pipeline with the fake `IChatClient` *at the bottom* of the chain. Covers (i) typed output, (ii) one `AIFunction` round-trip via `UseFunctionInvocation`, (iii) MCP tool resolution via an in-process `IMcpToolSource` adapter, (iv) middleware ordering (logging fires *before* function invocation), (v) tool-iteration bound at 8 (DR-8). Runs in the standard test job — no category filter that could be silently skipped.

**Cross-product smoke (`tests/basileus-smoke/`):** new csproj that pulls `LevelUp.Strategos.Agents` from the local feed at preview.2 version and asserts the basileus-consumed surface (`IConversationThreadManager`, `IWorkflowAgentFactory`, etc.) still compiles. Catches accidental SemVer breakage in non-`IAgentStep` parts of the package. Runs in CI as part of the release-readiness job.

**Test fidelity rule (per [[feedback_implementer_no_exarchos_mcp]]):** mocking is only permitted at the `IChatClient` boundary. Internal `Strategos.Agents` collaborators are constructed real — `AgentStepBuilder`, `AgentStepConfiguration`, `AgentDiagnostics`, etc. Internal mocking would trip the past-incident pattern.

## Open Questions

Items deliberately deferred from this design until plan or implementation surfaces a concrete signal:

1. **`Strategos.Agents.Mcp` package versioning cadence.** DR-5 puts the MCP adapter in a separate sub-package. Should it version with `Strategos.Agents` (same SemVer) or independently (track `ModelContextProtocol` package cadence)? Independence is the safer default — confirm in plan-review.
2. **Default `maximumIterationsPerRequest` value.** DR-8 picks 8. MEAI documentation does not pin a recommended bound; 8 is a judgment call. If MEAI 10.6+ ships a documented recommendation, defer to it. Re-evaluate before 2.7.0 GA.
3. **`IAgentStep<TState, TResult>` placement.** Does it stay in `Strategos.Agents.Abstractions` or move to a new `Strategos.Agents.Contracts` parallel to `Strategos.Identity.Abstractions`? Architecturally cleaner to split (port/adapter separation), but doubles the package count. Defer to plan unless v2.8.0 contracts work re-raises the question.
4. **Convenience overloads on the builder.** Risk §7 flags that the README "trivial example" should be ≤15 lines. If `.WithSystemPrompt(Func<TState, string>)` always wrapping a static string is ergonomically painful, add `.WithStaticSystemPrompt(string)`. Defer until plan can prototype the README example.
5. **Telemetry payload format for `AgentToolLoopException`.** DR-8 mandates partial tool-call trace in the exception. Format (JSON? `IReadOnlyList<ChatMessage>`? structured `ToolCallTrace` record?) deferred to plan — record-typed is the prior — but the exact shape needs review against OTel semantic conventions.

## Alternatives Considered

Three options were evaluated against axiom DIM-1..DIM-8 and Strategos invariants INV-3..INV-7 during ideation. Option 3 (composition + builder) was selected — see Chosen Approach / Technical Design above. Options 1 and 2 documented below for provenance.

### Option 3: Composition over inheritance + fluent `AgentStepBuilder` (selected)

See Chosen Approach and Technical Design sections. Sealed `AgentStepBase<TState, TResult>`; subclassing deleted as an extension model; hook delegates injected through a fluent builder that mirrors `IWorkflowBuilder<TState>`. Strongest INV-4 / INV-6 / DIM-5 / DIM-6 alignment. Required fixes: AGAG diagnostic IDs (DR-7), real-chain integration test (DR-9), bounded tool-iteration (DR-8).


The three options below were evaluated against the eight axiom dimensions (DIM-1..DIM-8) and the Strategos invariants (INV-3..INV-7) during ideation. Option C is selected (§2 / §3). Options A and B are documented below for provenance.

### Option 1: Cohesive break, abstract `AgentStepBase<TState, TResult>` (Option A)

`IAgentStep<TState, TResult>` evolves with two type parameters; `AgentStepBase<TState, TResult>` remains an abstract base with `protected abstract` hooks (`GetSystemPrompt`, `GetUserPrompt`, `ApplyResultAsync`). Subclassing remains the extension model; tool composition lives at constructor injection.

**Why rejected:** Equivalent on DIM-3/4/7, but keeps the subclassing seam. Future MEAI evolutions (when 11.x or 12.x adds another capability that needs to be wired into the step) will require another contract break or another `protected virtual` method. Option C's builder model absorbs additive capabilities via additive `.WithFoo(...)` methods without contract change. Strongest difference is on [[INV-4]] (concrete DSL nomenclature) — Option C's builder family mirrors `IWorkflowBuilder<TState>`; Option A's abstract base is a structural pattern that doesn't reflect any Strategos-specific shape.

### Option 2: Layered additive, parallel base classes (Option B)

Keep `IAgentStep<TState>` unchanged; add `IStructuredAgentStep<TState, TResult>` and `StructuredAgentStepBase<TState, TResult>` alongside the existing types. Each MEAI 10.5 capability lands in its own additive PR. No breaking changes.

**Why rejected:** Two parallel base classes for one intent — DIM-5 / [[INV-2]] / "no divergent implementations" violation. `GetOutputSchemaType()` becomes permanently vestigial. README must explain "use this when you want X, that when you want Y" — a bifurcation that AI-generic prose handles badly (DIM-8). Worst of all: with **zero existing production subclasses**, the "backward compatibility" argument that motivates Option B is empty. We are protecting compat for users who don't exist.

## Implementation plan handoff

The plan phase (`/exarchos:plan`) will decompose this into TDD tasks. Anticipated decomposition:

- 1 task per DR (DR-1 through DR-11) → 11 tasks.
- DR-9 (real-chain integration test) likely a 2-task split (test scaffolding + each capability assertion).
- DR-10 (error-path sweep) likely a 3-task split (exception hierarchy + per-code failure tests + grep-gate CI step).
- DR-5 (MCP) spawns a new sub-project `Strategos.Agents.Mcp` — additional csproj scaffold task.
- Release tasks: CHANGELOG, README, package-version bump to 2.7.0-preview.2 (paired with #70 cleanup).

Estimated 15–18 TDD tasks. Detailed ordering, dependencies, and test-list goes in the plan document.

## References

- Issue: [#45 — Adopt Microsoft.Extensions.AI 10.5 capabilities in Strategos.Agents](https://github.com/lvlup-sw/strategos/issues/45)
- Milestone: [Strategos 2.7.0 — Agent Capabilities](https://github.com/lvlup-sw/strategos/milestone/3)
- Predecessor design: [G1 agent-identity seam (2026-05-16)](2026-05-16-g1-agent-identity-seam.md) — orthogonal seam at the saga layer
- MEAI 10.5 release notes: <https://devblogs.microsoft.com/dotnet/announcing-microsoft-extensions-ai-10-5/>
- Function invocation guidance: <https://learn.microsoft.com/dotnet/ai/quickstarts/use-function-calling>
- MCP client docs: <https://modelcontextprotocol.io/clients/dotnet>
- Strategos invariants applied: [[INV-3]] (MCP latest spec), [[INV-4]] (concrete nomenclature), [[INV-5]] (stable diagnostic IDs), [[INV-6]] (sealed-by-default), [[INV-7]] (immutable records)
- Axiom dimensions applied: DIM-1 (Topology), DIM-2 (Observability), DIM-3 (Contracts), DIM-4 (Test Fidelity), DIM-5 (Hygiene), DIM-6 (Architecture), DIM-7 (Resilience), DIM-8 (Prose Quality)
