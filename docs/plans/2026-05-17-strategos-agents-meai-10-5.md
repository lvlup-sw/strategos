# Implementation Plan: Strategos.Agents — MEAI 10.5 adoption

## Source Design
Link: [`docs/designs/2026-05-17-strategos-agents-meai-10-5.md`](../designs/2026-05-17-strategos-agents-meai-10-5.md)

## Scope
**Target:** Full design (all 11 DRs).
**Excluded:** None. Open questions (§Open Questions of design) are not in scope but are deferred decisions, not deferred work.

## Summary
- **Total tasks:** 23
- **Parallel groups:** 4
- **Estimated test count:** 28+ (one acceptance + ~22 unit/integration + cross-product smoke)
- **Design coverage:** 12 of 12 sections (Problem Statement, Chosen Approach, Technical Design, Requirements (DR-1..DR-11), Integration Points, Out of scope, Risks, Testing Strategy, Open Questions, Alternatives, Implementation handoff, References)

**Toolchain reminders (per Strategos conventions):**
- Test runner: TUnit. Test invocation MUST use `dotnet test --project <proj> -- --treenode-filter "/*/*/*/Name"` (NOT `dotnet test --filter`). See [[feedback_tunit_test_invocation]].
- Mocking allowed only at the `IChatClient` boundary (NSubstitute). Internal collaborators are constructed real. See [[feedback_implementer_no_exarchos_mcp]] for the past-incident pattern this guards against.
- All new public types sealed by default ([[INV-6]]); records-by-default for value types ([[INV-7]]).

## Spec Traceability

| Design section / DR | Acceptance criteria addressed | Task(s) |
|---|---|---|
| DR-1 Sealed `AgentStepBase<TState, TResult>` + contract | Interface declared; `AgentStepBase` is sealed; reflection asserts; `GetOutputSchemaType()` deleted | T-004, T-007, T-021 |
| DR-2 `AgentStepBuilder<TState, TResult>` fluent API | Builder sealed; required-hook AGAG001; option-redundancy throws; `Build()` returns interface | T-006, T-012, T-014, T-016 |
| DR-3 Structured output via `GetResponseAsync<TResult>` | Happy-path typed delivery; AGAG002 on `TryGetResult` false; string-TResult works; no silent fallback | T-008, T-009 |
| DR-4 `AIFunction` + `UseFunctionInvocation` | `WithTool` accumulation; duplicate-name AGAG003; integration round-trip | T-013, T-019 |
| DR-5 MCP-client-as-tools | `IMcpToolSource` port; `Strategos.Agents.Mcp` sub-package; AGAG004 handshake failure; `IAsyncDisposable` adapter; INV-3 spec pin | T-005, T-017, T-018, T-020 |
| DR-6 `ConfigureChatClient` escape hatch | Middleware order verified by integration test; cached responses test | T-015, T-020 |
| DR-7 Diagnostic ID family AGAG001–006 | Const declarations; reflection over `AgentException` subclasses; grep gate | T-002, T-023 |
| DR-8 Bounded tool-iteration | Default 8; AGAG005 on overflow; mechanical enforcement; rejection of 0 | T-011, T-014 |
| DR-9 Real-chain integration test | Full pipeline; fake at bottom; ≥4 assertions; no category-skip | T-001, T-019, T-020 |
| DR-10 Error-path sweep | Every exception has Diagnostic; no parameterless catches; failure tests per code; cancellation propagated unwrapped | T-003, T-009, T-010, T-011, T-013, T-018, T-023 |
| DR-11 Migration & documentation | Old types deleted; README rewritten; CHANGELOG BREAKING; cross-product smoke; prose grep gate | T-021, T-022, T-023 |
| Testing Strategy (3-layer) | Unit + integration + cross-product smoke wired | T-001, T-019, T-020, T-022 |

Every DR → ≥1 task. Provenance chain complete.

## Task Breakdown

### Task 1: Acceptance test scaffold (stays RED until T-019+ land)

**Description:** Pin the DR-9 real-chain acceptance test up front — a single end-to-end test that exercises structured output, AIFunction tool invocation, MCP tool resolution, and middleware ordering through the full ChatClientBuilder pipeline. It stays RED until the orchestrator + builder + MCP adapter all land. Compile failures from missing types are the first proof of TDD discipline.

**Phase:** RED · **Test Layer:** acceptance · **Implements:** DR-9, DR-3, DR-4, DR-5, DR-6

**TDD Steps:**
1. [RED] Write test: `MeaiPipeline_StructuredOutputWithToolAndMcp_RoundTripsThroughChain`
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseIntegrationTests.cs`
   - Expected failure: types `AgentStepBuilder<,>`, `IMcpToolSource`, `AgentDiagnostics` do not exist; compile error.
   - Run: `dotnet build` — MUST FAIL with `CS0246`.
2. [GREEN] Deferred — this test passes only after T-019 + T-020 finish wiring the full chain.
3. [REFACTOR] N/A — acceptance tests stay shaped by the spec.

**Verification:**
- [ ] Compile failure observed before any production code lands.
- [ ] Test name follows `Method_Scenario_Outcome`.
- [ ] Test is NOT marked `[Skip]` or behind a category filter (DR-9 acceptance criterion).

**Dependencies:** None.
**Parallelizable:** No — first task; pins the spec.
**testingStrategy:** `{ layer: "acceptance", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 2: `AgentDiagnostics` const class with AGAG001–006

**Description:** Declare the six AGAG diagnostic codes per DR-7 as `public const string` literals on a static `AgentDiagnostics` class, mirroring the AGWF / AONT catalog pattern. Allocates the diagnostic identity that every downstream exception (T-003) and failure path will reference. Reflection-asserted to prevent ad-hoc additions.

**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-7

**TDD Steps:**
1. [RED] Write test: `AgentDiagnostics_AllSixCodes_DeclaredAsConstStringMatchingPattern`
   - File: `src/Strategos.Agents.Tests/Unit/Diagnostics/AgentDiagnosticsTests.cs`
   - Behavior: reflection finds 6 `public const string` fields named `AGAG001`..`AGAG006`; each value matches regex `^AGAG\d{3}$`.
   - Expected failure: `AgentDiagnostics` type does not exist.
2. [GREEN] Implement minimum code.
   - File: `src/Strategos.Agents.Abstractions/Diagnostics/AgentDiagnostics.cs` (if Abstractions project exists) OR `src/Strategos.Agents/Abstractions/Diagnostics/AgentDiagnostics.cs`.
   - Declare `public static class AgentDiagnostics` with six `const string` fields.
3. [REFACTOR] Add XML doc per code (one line, domain voice — no AI clichés; will be grep-gated in T-023).

**Verification:**
- [ ] Witnessed test fail (`CS0246` or null reference).
- [ ] Reflection test green.
- [ ] No additional symbols beyond the six.

**Dependencies:** None.
**Parallelizable:** Yes — pure declaration; no other task touches this file.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 3: `AgentException` base + 6 sealed subclasses

**Description:** Establish the exception hierarchy per DR-10: one abstract `AgentException` base with a non-null `Diagnostic` property, plus six sealed subclasses each pinned to its AGAG code (AGAG001 builder validation, AGAG002 structured output, AGAG003 duplicate tool, AGAG004 MCP handshake, AGAG005 tool-loop limit, AGAG006 chat-response null). Reflection-asserted so no new exception type can drift without a code allocation.

**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-10, DR-2, DR-3, DR-4, DR-5, DR-8

**TDD Steps:**
1. [RED] Write test: `AgentExceptionHierarchy_AllSubclasses_DeclareDiagnosticProperty`
   - File: `src/Strategos.Agents.Tests/Unit/Exceptions/AgentExceptionHierarchyTests.cs`
   - Behavior: reflection over assembly finds `AgentException` (abstract) + 6 sealed subclasses: `AgentBuilderValidationException` (AGAG001), `AgentStructuredOutputException` (AGAG002), `AgentDuplicateToolException` (AGAG003), `AgentMcpException` (AGAG004), `AgentToolLoopException` (AGAG005), `AgentChatResponseException` (AGAG006). Every subclass has `string Diagnostic { get; }` returning the matching AGAG code in every public constructor.
   - Expected failure: types do not exist.
2. [GREEN] Implement minimum code.
   - File: `src/Strategos.Agents/Exceptions/AgentException.cs` (base) + one file per subclass under `src/Strategos.Agents/Exceptions/`.
   - Each subclass sets `Diagnostic` to its AGAG code; payload-bearing classes (e.g. `AgentStructuredOutputException`) carry `RawPayload` (≤4 KB truncation, asserted in T-009).
3. [REFACTOR] Extract truncation helper for `RawPayload` if duplicated across classes.

**Verification:**
- [ ] Reflection test green.
- [ ] All exception classes are `sealed` ([[INV-6]]).
- [ ] No constructor allows `Diagnostic = null`.

**Dependencies:** T-002.
**Parallelizable:** Yes (with T-004, T-005, T-006 once T-002 lands).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 4: `IAgentStep<TState, TResult>` interface declaration

**Description:** Introduce the two-arity port for DR-1 — `IAgentStep<TState, TResult> : IWorkflowStep<TState>`. The interface adds no methods; it is a generic refinement that lets `TResult` flow through the pipeline. The old single-arity interface stays in place temporarily and is deleted in T-021 to avoid an early compile break.

**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-1

**TDD Steps:**
1. [RED] Write test: `IAgentStep_GenericContract_ExtendsIWorkflowStepWithTwoTypeParameters`
   - File: `src/Strategos.Agents.Tests/Unit/Abstractions/IAgentStepContractTests.cs`
   - Behavior: reflection asserts `IAgentStep<,>` is an interface with two type parameters; first param has `class, IWorkflowState` constraints; the open generic extends `IWorkflowStep<>` over the first param. Confirms no `GetOutputSchemaType()` or `GetSystemPrompt()` methods exist on the new interface.
   - Expected failure: type does not exist.
2. [GREEN] Declare interface.
   - File: `src/Strategos.Agents/Abstractions/IAgentStep.cs` (replace existing single-arity definition in T-021; for now leave the old one untouched so it doesn't compile-break before T-021).
   - Actually: introduce in a *new file* `src/Strategos.Agents/Abstractions/IAgentStepT2.cs` to avoid colliding with T-021's deletion. Final consolidation in T-021.

**Verification:**
- [ ] Reflection assertions green.
- [ ] Type parameter constraints match design.
- [ ] No surface method declared — the interface is purely a generic refinement of `IWorkflowStep<TState>`.

**Dependencies:** T-002 (for AGAG references in XML doc cross-links, soft dep).
**Parallelizable:** Yes (with T-003, T-005, T-006).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 5: `IMcpToolSource` port in Abstractions

**Description:** Declare the DR-5 hexagonal port — `IMcpToolSource` — in `Strategos.Agents.Abstractions`, returning `IReadOnlyList<AIFunction>`. The port has zero `ModelContextProtocol.*` dependencies; that concrete adapter lives in the separate `Strategos.Agents.Mcp` sub-package (T-017). Assembly-reference reflection enforces the dependency direction.

**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-5

**TDD Steps:**
1. [RED] Write test: `IMcpToolSource_PortShape_NoModelContextProtocolDependency`
   - File: `src/Strategos.Agents.Tests/Unit/Abstractions/IMcpToolSourceContractTests.cs`
   - Behavior: reflection asserts `IMcpToolSource` exists in `Strategos.Agents.Abstractions` namespace, declares `Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken)`, and the **assembly** containing it has no reference to `ModelContextProtocol.*` packages (asserted via `Assembly.GetReferencedAssemblies()`).
   - Expected failure: type does not exist.
2. [GREEN] Declare port.
   - File: `src/Strategos.Agents/Abstractions/IMcpToolSource.cs`.
   - Single method; uses `Microsoft.Extensions.AI.AIFunction` (already in `Strategos.Agents` deps).
3. [REFACTOR] Add XML doc anchored to design DR-5.

**Verification:**
- [ ] Port shape matches design exactly.
- [ ] Strategos.Agents assembly has zero `ModelContextProtocol.*` references after this task.

**Dependencies:** None.
**Parallelizable:** Yes.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 6: `AgentStepConfiguration<TState, TResult>` sealed record

**Description:** The configuration object that `AgentStepBuilder` produces and `AgentStepBase` consumes. Sealed record per INV-7 immutability, internal-construction-only so only the builder can instantiate. Carries the three required hook delegates (system prompt, user prompt, apply result) plus optional tools, MCP source, chat options, configurator, and max-iterations.

**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-2 (supports DR-1)

**TDD Steps:**
1. [RED] Write test: `AgentStepConfiguration_SealedRecord_CarriesAllHookDelegates`
   - File: `src/Strategos.Agents.Tests/Unit/Configuration/AgentStepConfigurationTests.cs`
   - Behavior: reflection asserts `AgentStepConfiguration<,>` is `sealed` and a `record`; constructor takes `Func<TState, string> systemPrompt`, `Func<TState, string> userPrompt`, `Func<TState, TResult, CancellationToken, Task<StepResult<TState>>> applyResult`, plus optional collections for tools, MCP source, chat options, configurator, max-iterations.
   - Expected failure: type does not exist.
2. [GREEN] Declare record.
   - File: `src/Strategos.Agents/Configuration/AgentStepConfiguration.cs`.
   - Internal constructor (only `AgentStepBuilder` may construct).
3. [REFACTOR] N/A.

**Verification:**
- [ ] `sealed record` confirmed by reflection.
- [ ] Constructor accessibility is `internal`.

**Dependencies:** None (uses `Strategos.Steps.StepResult<TState>` already in `Strategos.csproj`).
**Parallelizable:** Yes.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 7: `AgentStepBase<TState, TResult>` sealed orchestrator scaffold

**Description:** The DR-1 orchestrator type — sealed, two-arity generic, implements `IAgentStep<TState, TResult>`, takes `IChatClient` and `AgentStepConfiguration` via internal constructor. This task lays only the shell; `ExecuteAsync` throws `NotImplementedException` until T-008 wires the structured-output path. Subclassing is mechanically rejected (Roslyn negative compile test).

**Phase:** RED → GREEN · **Test Layer:** unit · **Acceptance Test Ref:** T-001 · **Implements:** DR-1

**TDD Steps:**
1. [RED] Write test: `AgentStepBase_TypeShape_IsSealedAndImplementsGenericInterface`
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBaseTypeShapeTests.cs`
   - Behavior: reflection asserts `AgentStepBase<,>` is `sealed`, has `internal` constructor taking `IChatClient` and `AgentStepConfiguration<TState, TResult>`, and implements `IAgentStep<TState, TResult>`. Compile-time check: a test attempting to subclass it produces a compiler error (verified via Roslyn `SyntaxFactory` snippet that we compile in-test using `CSharpCompilation`).
   - Expected failure: type does not exist.
2. [GREEN] Declare scaffold class with no logic yet.
   - File: `src/Strategos.Agents/AgentStepBase.cs` — REPLACES current content (the existing abstract `AgentStepBase<TState>` is renamed in T-021 to clean break; for this task we add the new type *alongside* the old until T-021, by writing to `AgentStepBaseT2.cs`).
   - `ExecuteAsync` returns `throw new NotImplementedException()` — actual implementation in T-008.

**Verification:**
- [ ] `sealed` confirmed.
- [ ] Constructor accessibility `internal`.
- [ ] Negative compile test (Roslyn snippet) green: subclassing fails with `CS0509`.

**Dependencies:** T-003, T-004, T-006.
**Parallelizable:** No (foundational; T-008..T-011 depend on it).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 8: `ExecuteAsync` happy-path structured output via `GetResponseAsync<TResult>`

**Description:** Wire the DR-3 happy path — orchestrator builds messages from the hook delegates, calls `_chatClient.GetResponseAsync<TResult>(messages, options, ct)`, and on `TryGetResult(out var typed)` invokes the apply-result hook with the typed payload. Tests the structured-output unlock that motivated the whole adoption.

**Phase:** RED → GREEN · **Test Layer:** integration · **Acceptance Test Ref:** T-001 · **Implements:** DR-3

**TDD Steps:**
1. [RED] Write test: `ExecuteAsync_TypedResponse_InvokesApplyResultWithTypedPayload`
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseExecuteTests.cs`
   - Behavior: build an `AgentStepBase<TestState, MyDto>` via direct construction (`AgentStepConfiguration` real). `IChatClient` is `NSubstitute.Substitute.For<IChatClient>()` returning a `ChatResponse<MyDto>` whose `TryGetResult` yields a populated `MyDto`. Assert `ApplyResult` hook is invoked with the typed `MyDto` instance (not a string).
   - Expected failure: `ExecuteAsync` throws `NotImplementedException`.
2. [GREEN] Implement `ExecuteAsync`.
   - File: `src/Strategos.Agents/AgentStepBase.cs` (or `AgentStepBaseT2.cs` per T-007 split).
   - Build messages from system/user-prompt hooks; call `_chatClient.GetResponseAsync<TResult>(messages, options, ct)`; on `TryGetResult` true, invoke `applyResult` hook.
3. [REFACTOR] Extract `BuildMessages` helper internal to the orchestrator if it grows past 10 lines.

**Verification:**
- [ ] Witnessed test fail with `NotImplementedException`.
- [ ] Typed payload flows end-to-end.
- [ ] No silent fallback path (if `TryGetResult` is false the test in T-009 will catch it).

**Dependencies:** T-007.
**Parallelizable:** No (extends T-007 file).
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 9: Structured output failure → `AgentStructuredOutputException` (AGAG002)

**Description:** Close the DR-3 / DR-10 no-silent-fallback path. When `ChatResponse<TResult>.TryGetResult` returns false, the orchestrator throws `AgentStructuredOutputException` with `Diagnostic=AGAG002` and a 4 KB-truncated `RawPayload`. The apply-result hook is never invoked on this path — verified by NSubstitute `DidNotReceive()`.

**Phase:** RED → GREEN · **Test Layer:** integration · **Acceptance Test Ref:** T-001 · **Implements:** DR-3, DR-10

**TDD Steps:**
1. [RED] Write test: `ExecuteAsync_TryGetResultFalse_ThrowsAgentStructuredOutputExceptionWithAGAG002`
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBaseExecuteTests.cs` (same file as T-008).
   - Behavior: `IChatClient` returns `ChatResponse<MyDto>` whose `TryGetResult` yields `false`; raw text is `"{bad}"`. Assert `AgentStructuredOutputException` is thrown with `Diagnostic == "AGAG002"`, `RawPayload != null`, and `RawPayload.Length <= 4096`. Also assert `applyResult` hook was NEVER invoked (no silent fallback).
   - Expected failure: orchestrator currently returns default(TResult) or throws something else.
2. [GREEN] Modify `ExecuteAsync`.
   - On `TryGetResult` false, throw `new AgentStructuredOutputException(response.Text?.Substring(0, Math.Min(4096, response.Text.Length)))`.

**Verification:**
- [ ] Exception thrown with correct diagnostic.
- [ ] `RawPayload` truncated at 4 KB.
- [ ] `applyResult` not invoked (verified via NSubstitute `DidNotReceive()`).

**Dependencies:** T-008, T-003.
**Parallelizable:** No (same file as T-008).
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 10: Null/empty response → `AgentChatResponseException` (AGAG006)

**Description:** Guard the DR-10 boundary where the IChatClient returns null or an empty `ChatResponse<TResult>`. Throw `AgentChatResponseException` with `AGAG006`. State must be reference-equal pre- and post-throw (no partial mutation). A negative sub-test asserts that `OperationCanceledException` from the chat client propagates **unwrapped** — cancellation isn't a domain failure.

**Phase:** RED → GREEN · **Test Layer:** integration · **Acceptance Test Ref:** T-001 · **Implements:** DR-10
**Files:** `src/Strategos.Agents/AgentStepBase.cs`, `src/Strategos.Agents.Tests/Integration/AgentStepBaseExecuteTests.cs`

**TDD Steps:**
1. [RED] Write test: `ExecuteAsync_NullChatResponse_ThrowsAgentChatResponseExceptionWithAGAG006`
   - File: same as T-008.
   - Behavior: `IChatClient.GetResponseAsync<TResult>(...)` returns null OR a `ChatResponse<TResult>` with null/empty `Text` and no `Result`. Assert `AgentChatResponseException` thrown with `Diagnostic == "AGAG006"`. State passed in is reference-equal to state observable after the throw (DR-10 acceptance criterion).
   - Expected failure: orchestrator likely throws `NullReferenceException` instead.
2. [GREEN] Add null/empty guard at top of `ExecuteAsync` after the chat call.

**Verification:**
- [ ] Exception thrown with `AGAG006`.
- [ ] State unchanged after throw.
- [ ] Cancellation: a separate sub-test asserts `OperationCanceledException` from the chat client propagates **unwrapped** (DR-10 negative test).

**Dependencies:** T-008, T-003.
**Parallelizable:** No.
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 11: Bounded tool-iteration → `AgentToolLoopException` (AGAG005)

**Description:** The DR-8 mechanical bound on tool-call iteration. `UseFunctionInvocation(opts => opts.MaximumIterationsPerRequest = 8)` is configured at builder build-time; orchestrator catches the overflow and rethrows as `AgentToolLoopException` with `AGAG005` and a `PartialTrace`. The exact-8 limit is asserted by counting fake IChatClient invocations — no documented contract, mechanical enforcement (per [[feedback_no_handwavy_mitigations]]).

**Phase:** RED → GREEN · **Test Layer:** integration · **Acceptance Test Ref:** T-001 · **Implements:** DR-8, DR-10
**Files:** `src/Strategos.Agents/AgentStepBase.cs`, `src/Strategos.Agents.Tests/Integration/AgentStepBaseToolLoopTests.cs`

**TDD Steps:**
1. [RED] Write test: `ExecuteAsync_ToolCallsExceedMaxIterations_ThrowsAgentToolLoopExceptionAtExactlyEight`
   - File: same as T-008.
   - Behavior: `IChatClient` is composed via `ChatClientBuilder().Use(fake).UseFunctionInvocation(opts => opts.MaximumIterationsPerRequest = 8)`; fake always responds with a tool-call message (never terminal). Assert exactly 8 invocations before `AgentToolLoopException` with `Diagnostic == "AGAG005"`. The `PartialTrace` property contains 8 tool-call records.
   - Expected failure: tests currently rely on default MEAI iteration limit (unbounded or different).
2. [GREEN] Wire orchestrator to pass `config.MaxToolIterations ?? AgentStepBase.DefaultMaxToolIterations` (= 8) when building the inner pipeline. Catch MEAI's iteration-limit exception (or count iterations explicitly) and rethrow as `AgentToolLoopException`.

**Verification:**
- [ ] `DefaultMaxToolIterations == 8` const declared.
- [ ] Limit mechanically enforced — verified by counting fake IChatClient invocations.
- [ ] `PartialTrace` payload present.

**Dependencies:** T-008, T-003.
**Parallelizable:** No.
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 12: `AgentStepBuilder<TState, TResult>` — required-hook validation (AGAG001)

**Description:** The DR-2 fluent builder scaffold with required-hook validation. Three internal nullable hook fields; `Build()` raises `AgentBuilderValidationException` (AGAG001) naming the missing hook in the message. Three sub-tests, one per required hook.

**Phase:** RED → GREEN · **Test Layer:** unit · **Acceptance Test Ref:** T-001 · **Implements:** DR-2

**TDD Steps:**
1. [RED] Write tests (three sub-tests in one task — each is one TDD micro-cycle):
   - `Build_WithoutSystemPromptHook_ThrowsAgentBuilderValidationExceptionWithAGAG001AndNamingMissingHook`
   - `Build_WithoutUserPromptHook_ThrowsAgentBuilderValidationExceptionWithAGAG001AndNamingMissingHook`
   - `Build_WithoutApplyResultHook_ThrowsAgentBuilderValidationExceptionWithAGAG001AndNamingMissingHook`
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBuilderValidationTests.cs`.
   - Expected failure: `AgentStepBuilder<,>` type does not exist.
2. [GREEN] Implement builder skeleton with three internal nullable hook fields. `Build()` checks each and throws `AgentBuilderValidationException(missingHookName)`. The exception's `Message` includes the missing hook name verbatim ("SystemPrompt"/"UserPrompt"/"ApplyResult").

**Verification:**
- [ ] All 3 missing-hook variants throw AGAG001.
- [ ] Message text names the hook (remediation guidance).
- [ ] Builder type is `sealed`.

**Dependencies:** T-003, T-006, T-007 (returns interface).
**Parallelizable:** No (subsequent builder tasks extend this file).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 13: Builder — `WithTool` accumulation + duplicate-name AGAG003

**Description:** DR-4 tool registration via the builder. Multiple `WithTool(AIFunction)` calls accumulate; collision detection by name happens at `.Build()` time (not at registration time, allowing fluent reuse). Duplicate throws `AgentDuplicateToolException` with `AGAG003`.

**Phase:** RED → GREEN · **Test Layer:** unit · **Acceptance Test Ref:** T-001 · **Implements:** DR-4, DR-10

**TDD Steps:**
1. [RED] Write tests:
   - `WithTool_MultipleDistinctTools_AccumulatesAllInConfiguration`
   - `WithTool_DuplicateToolName_ThrowsAgentDuplicateToolExceptionWithAGAG003AtBuildTime`
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBuilderToolsTests.cs`.
   - Behavior: register two distinct `AIFunctionFactory.Create(...)` tools; `Build()` produces config whose `Tools` collection contains both. Then add a tool with a name colliding with one already registered → expect `AgentDuplicateToolException` with `Diagnostic == "AGAG003"`. Detection is at `.Build()` time, not at registration time (allows fluent reuse).
   - Expected failure: `WithTool` does not exist.
2. [GREEN] Implement `WithTool(AIFunction)`; accumulate into private list; in `Build()`, check for name collisions via `tools.GroupBy(t => t.Name)`.

**Verification:**
- [ ] Accumulation works for ≥3 tools.
- [ ] Collision detected at `.Build()`, not at `.WithTool()`.
- [ ] AGAG003 carried.

**Dependencies:** T-012.
**Parallelizable:** No (same file).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 14: Builder — `WithChatOptions` / `WithMcpToolSource` / `WithMaxToolIterations`

**Description:** Three optional builder setters across DR-2 (`WithChatOptions` — redundancy throws `InvalidOperationException`), DR-5 (`WithMcpToolSource` — stores the port), DR-8 (`WithMaxToolIterations` — rejects 0 and negatives via `ArgumentOutOfRangeException`).

**Phase:** RED → GREEN · **Test Layer:** unit · **Acceptance Test Ref:** T-001 · **Implements:** DR-2, DR-5, DR-8

**TDD Steps:**
1. [RED] Write tests:
   - `WithChatOptions_CalledTwice_ThrowsInvalidOperationException`
   - `WithMcpToolSource_StoresPortInConfiguration`
   - `WithMaxToolIterations_Zero_ThrowsArgumentOutOfRangeException`
   - `WithMaxToolIterations_NegativeNumber_ThrowsArgumentOutOfRangeException`
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBuilderOptionsTests.cs`.
   - Expected failure: methods do not exist.
2. [GREEN] Implement the three setters with the asserted guards.

**Verification:**
- [ ] Each setter validated.
- [ ] Redundant `WithChatOptions` rejected.
- [ ] `WithMaxToolIterations(0)` and `(-1)` rejected.

**Dependencies:** T-012.
**Parallelizable:** No (same file family).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 15: Builder — `ConfigureChatClient` escape hatch + middleware ordering

**Description:** DR-6 host-composition escape hatch. `ConfigureChatClient(Action<ChatClientBuilder>)` lets the host wire `UseLogging`, `UseOpenTelemetry`, `UseDistributedCache`. The builder applies the configurator in fixed order: `[host configurator] → UseStrategosFunctions(tools) → UseFunctionInvocation(opts)`. Order is mechanically asserted by inspecting handler call sequence with a real `LoggerFactory`.

**Phase:** RED → GREEN · **Test Layer:** integration · **Acceptance Test Ref:** T-001 · **Implements:** DR-6

**TDD Steps:**
1. [RED] Write test: `Build_ConfigureChatClientHook_AppliesHostConfiguratorBeforeStrategosFunctionsAndFunctionInvocation`
   - File: `src/Strategos.Agents.Tests/Integration/AgentStepBuilderConfiguratorTests.cs`.
   - Behavior: pass a host configurator that calls `.UseLogging(loggerFactory)`. Call `.WithTool(...)` once. Build. Inspect the resulting `IChatClient` chain (via a custom probing delegating handler at each layer) — assert logging-middleware sits *before* function-invocation in the call order. Use a real `LoggerFactory.Create(b => b.AddProvider(testProvider))` where `testProvider` records calls; assert it received logs *before* the function-invocation tool call.
   - Expected failure: `ConfigureChatClient` does not exist.
2. [GREEN] Implement `ConfigureChatClient(Action<ChatClientBuilder>)`. In `Build()`, instantiate `ChatClientBuilder(baseChatClient)`, apply host configurator first, then `UseStrategosFunctions(tools)`, then `UseFunctionInvocation(opts => opts.MaximumIterationsPerRequest = maxIterations)`.

**Verification:**
- [ ] Real logger (not NSubstitute) captures pre-function-invocation events.
- [ ] Order is mechanically enforced (no documentation-only contract per [[feedback_no_handwavy_mitigations]]).

**Dependencies:** T-013, T-014.
**Parallelizable:** No.
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 16: Builder — `Build()` returns interface, not concrete

**Description:** Tighten the DR-2 surface — `AgentStepBuilder<,>.Build()` declares return type `IAgentStep<TState, TResult>` (interface) rather than the concrete `AgentStepBase<,>`. Forces callers to program against the abstraction. Reflection-asserted; builder constructor is parameterless.

**Phase:** RED → GREEN · **Test Layer:** unit · **Acceptance Test Ref:** T-001 · **Implements:** DR-2

**TDD Steps:**
1. [RED] Write test: `Build_ReturnType_IsIAgentStepInterfaceNotAgentStepBaseConcrete`
   - File: `src/Strategos.Agents.Tests/Unit/AgentStepBuilderReturnTypeTests.cs`.
   - Behavior: reflection asserts `AgentStepBuilder<,>.Build()` declared return type is `IAgentStep<TState, TResult>`. Verify caller is forced to use the interface (a test with a `var built = builder.Build();` followed by `typeof(IAgentStep<,>).IsInstanceOfType(built)` — and `built.GetType()` returns `AgentStepBase<,>` but the *declared* return is the interface).
   - Expected failure: `Build()` returns the concrete type.
2. [GREEN] Set return type to `IAgentStep<TState, TResult>`.
3. [REFACTOR] Builder has zero public constructor parameters (reflection-checked).

**Verification:**
- [ ] Declared return type is the interface.
- [ ] Builder has parameterless default constructor only.

**Dependencies:** T-012.
**Parallelizable:** No (extends builder).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 17: `Strategos.Agents.Mcp` csproj scaffold

**Description:** Scaffold the new DR-5 sub-package `LevelUp.Strategos.Agents.Mcp`. Carries the concrete `McpToolSource` adapter so the dependency on `ModelContextProtocol.Client` stays out of the core `Strategos.Agents` assembly. Pin to the latest 2025-11-25-spec-conformant `ModelContextProtocol` release at PR time per [[INV-3]].

**Phase:** GREEN-only (scaffolding) · **Test Layer:** unit (smoke) · **Implements:** DR-5

**TDD Steps:**
1. [RED] Write test: `StrategosAgentsMcp_Package_HasCorrectPackageMetadataAndDependsOnModelContextProtocolClient`
   - File: `src/Strategos.Agents.Mcp.Tests/PackageMetadataTests.cs` (new test project).
   - Behavior: assembly-level reflection: `LevelUp.Strategos.Agents.Mcp` package metadata present (read from MSBuild props); `ModelContextProtocol.Client` is a direct PackageReference; pinned version is the latest 2025-11-25-spec-conformant release at PR time.
   - Expected failure: project does not exist.
2. [GREEN] Create `src/Strategos.Agents.Mcp/Strategos.Agents.Mcp.csproj` with `PackageId = LevelUp.Strategos.Agents.Mcp`, version stub `2.7.0-preview.2`, `<PackageReference Include="ModelContextProtocol.Client" />`. Create matching test project `src/Strategos.Agents.Mcp.Tests/`. Add to `src/strategos.sln`. Wire `Directory.Packages.props` for centralized version management.

**Verification:**
- [ ] `dotnet restore` succeeds.
- [ ] Test project depends only on `Strategos.Agents.Mcp` + `TUnit` + `NSubstitute`.
- [ ] `Strategos.Agents.csproj` does NOT gain a `ModelContextProtocol` reference.

**Dependencies:** T-005.
**Parallelizable:** Yes — pure scaffolding.
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 18: `McpToolSource` default adapter + AGAG004 handshake failure

**Description:** The DR-5 concrete adapter — `McpToolSource : IMcpToolSource, IAsyncDisposable` wraps `McpClientFactory.CreateAsync` + `client.ListToolsAsync`. Handshake failure throws `AgentMcpException` (AGAG004) with a redacted endpoint (no credentials). `IAsyncDisposable` disposes the underlying MCP client deterministically.

**Phase:** RED → GREEN · **Test Layer:** integration · **Acceptance Test Ref:** T-001 · **Implements:** DR-5, DR-10

**TDD Steps:**
1. [RED] Write tests:
   - `GetToolsAsync_ValidMcpServer_ReturnsListedTools`
   - `GetToolsAsync_HandshakeFails_ThrowsAgentMcpExceptionWithAGAG004AndRedactedEndpoint`
   - `McpToolSource_ImplementsIAsyncDisposable_DisposesUnderlyingClient`
   - File: `src/Strategos.Agents.Mcp.Tests/McpToolSourceTests.cs`.
   - Use an in-process MCP test server stub (or `McpClientFactory` with `StdioClientTransport` pointed at a no-op echo binary in CI) for happy path; for failure path point at a closed port to trigger handshake failure.
   - Expected failure: `McpToolSource` does not exist.
2. [GREEN] Implement `McpToolSource : IMcpToolSource, IAsyncDisposable`. Wrap `McpClientFactory.CreateAsync(...)` + `client.ListToolsAsync(...)`. On handshake failure, catch `McpException` (or generic timeout) and rethrow `AgentMcpException` with `Diagnostic = "AGAG004"`, endpoint passed through a redaction helper (strip user-info from URI, mask any Authorization-like properties).

**Verification:**
- [ ] Happy path returns ≥1 tool.
- [ ] Handshake failure carries AGAG004; endpoint property has no credentials.
- [ ] `IAsyncDisposable` disposes the underlying MCP client.
- [ ] Strategos.Agents has zero `ModelContextProtocol.*` references (re-verified).

**Dependencies:** T-005, T-017.
**Parallelizable:** No.
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 19: Real-chain integration — structured output + `AIFunction` round-trip

**Description:** First half of the DR-9 acceptance assertions move from skeleton to live behavior. Builder configured with one `AIFunction` adder; fake `IChatClient` (at bottom of the chain) emits a tool-call then a structured response; assert tool invoked, typed payload arrived at apply-result hook, fake's chain position is bottom-most.

**Phase:** RED → GREEN · **Test Layer:** acceptance · **Acceptance Test Ref:** T-001 · **Implements:** DR-9 (i)(ii), DR-3, DR-4
**Files:** `src/Strategos.Agents.Tests/Integration/AgentStepBaseIntegrationTests.cs`

**TDD Steps:**
1. [RED] The portion of T-001's acceptance test covering structured output + tool round-trip is moved from skeleton to assertion.
   - Behavior: builder configured with one `AIFunction` (a `(int, int) → int` adder). Fake `IChatClient` (composed at the *bottom* of the pipeline) emits a tool-call message on first call, a final structured response on second. Assert: (a) fake received both calls; (b) tool was invoked; (c) `ApplyResult` got the typed result; (d) fake's position in the chain is bottom-most (assert via a probing handler stack inspection).
   - Expected failure: previously RED in T-001 due to missing types; now RED for behavior mismatch.
2. [GREEN] No new production code — wiring already exists from T-08..T-15. Adjust integration test scaffold to actually invoke the orchestrator.

**Verification:**
- [ ] Tool round-trip observed.
- [ ] Fake at bottom of chain (per DR-9 acceptance criterion).
- [ ] No category filter.

**Dependencies:** T-001, T-008, T-013, T-015.
**Parallelizable:** No.
**testingStrategy:** `{ layer: "acceptance", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 20: Real-chain integration — MCP tool resolution + middleware ordering

**Description:** Second half of the DR-9 acceptance. In-process `IMcpToolSource` test adapter yields a known `AIFunction`; host configurator calls `.UseLogging(...)`. Assert MCP path is exercised and logging fires before function invocation. With T-019 + T-020 green, the T-001 acceptance test goes GREEN.

**Phase:** RED → GREEN · **Test Layer:** acceptance · **Acceptance Test Ref:** T-001 · **Implements:** DR-9 (iii)(iv), DR-5, DR-6
**Files:** `src/Strategos.Agents.Tests/Integration/AgentStepBaseIntegrationTests.cs`

**TDD Steps:**
1. [RED] Extend T-001's acceptance test:
   - In-process `IMcpToolSource` test adapter (NOT `McpToolSource` — a hand-rolled in-test implementation that yields a known `AIFunction`).
   - Host configurator calls `.UseLogging(...)`.
   - Assert: MCP tool resolved into the pipeline tools; logging fired before function invocation; tool is invoked via the MCP path.
2. [GREEN] No new production code expected — wiring already in place.

**Verification:**
- [ ] MCP path exercised through real builder chain.
- [ ] Logging assertion captured pre-tool-invocation.

**Dependencies:** T-001, T-005, T-015, T-019.
**Parallelizable:** No.
**testingStrategy:** `{ layer: "acceptance", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 21: DR-11 Migration sweep — delete old `IAgentStep<TState>` + `AgentStepBase<TState>` + test fixture

**Description:** The DR-11 migration & documentation sweep step that closes the contract break. Delete the old single-arity `IAgentStep<TState>` interface, the abstract `AgentStepBase<TState>` class, and the `AgentStepBaseTests.TestAgentStep` subclass fixture; rename the new files to canonical names. Reflection invariant guards against either type reappearing. Clean break, no `[Obsolete]` shim — there are zero production consumers to protect.

**Phase:** RED → GREEN · **Test Layer:** unit · **Implements:** DR-1, DR-11

**TDD Steps:**
1. [RED] Write test: `Strategos_Agents_Assembly_HasNoSingleAritIAgentStepOrAgentStepBase`
   - File: `src/Strategos.Agents.Tests/Unit/MigrationInvariantTests.cs`.
   - Behavior: reflection over the assembly: `typeof(IAgentStep<>).IsAssignableFrom(*)` returns no types; `typeof(AgentStepBase<>)` does not exist; `AgentStepBaseTests.TestAgentStep` does not exist.
   - Expected failure: those types still exist.
2. [GREEN] Delete:
   - `src/Strategos.Agents/Abstractions/IAgentStep.cs` (old single-arity).
   - The old single-arity `AgentStepBase<TState>` class declaration in `src/Strategos.Agents/AgentStepBase.cs`.
   - `src/Strategos.Agents.Tests/AgentStepBaseTests.cs` (the old test file; its successor tests are already in `src/Strategos.Agents.Tests/Unit/` and `src/Strategos.Agents.Tests/Integration/`).
   - Rename the new `IAgentStepT2.cs` → `IAgentStep.cs` (consolidate file naming).
3. [REFACTOR] Confirm no other source files reference the old types via `dotnet build`.

**Verification:**
- [ ] Reflection-based migration invariant test green.
- [ ] `dotnet build src/strategos.sln` clean (0 warnings, 0 errors).
- [ ] grep: `grep -rn 'IAgentStep<[A-Z][^,]*>$\|class AgentStepBase<[A-Z][^,]*>$' src/Strategos.Agents/ --include='*.cs' | grep -v IAgentStep.cs` returns zero hits.

**Dependencies:** T-007, T-019, T-020.
**Parallelizable:** No (gated by all builder + integration tests landing first).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: true }` — characterizes old behavior (none in production) and asserts deletion.

---

### Task 22: DR-11 Migration sweep — cross-product basileus-smoke csproj

**Description:** Validation step for the DR-11 migration. A smoke csproj pulls `LevelUp.Strategos.Agents` at `2.7.0-preview.2` from a local feed and asserts the basileus-consumed surface (`IConversationThreadManager`, `IWorkflowAgentFactory`, `IStreamingHandler`) still compiles. Reflection invariant also asserts old `IAgentStep<>` / `AgentStepBase<>` (single-arity) are absent. CI runs this in the release-readiness job.

**Phase:** RED → GREEN · **Test Layer:** integration · **Implements:** DR-11 (testing strategy)

**TDD Steps:**
1. [RED] Write test: `BasileusConsumedSurface_AfterMeai105Adoption_StillCompiles`
   - File: `tests/basileus-smoke/Basileus.Smoke.Tests/BasileusConsumedSurfaceTests.cs`.
   - Behavior: a smoke csproj that references `LevelUp.Strategos.Agents` via local feed at `2.7.0-preview.2` and pulls only the types basileus consumes today: `IConversationThreadManager`, `IWorkflowAgentFactory`, `IStreamingHandler`. Compile-time assertion via mock-instantiation in test bodies. Reflection assertion that `IAgentStep<>` and `AgentStepBase<>` (single-arity) are absent.
   - Expected failure: package not yet bumped to preview.2.
2. [GREEN] Bump `Strategos.Agents.csproj` version to `2.7.0-preview.2`. Local-feed packaging script in `scripts/`.

**Verification:**
- [ ] Smoke project compiles against the new package.
- [ ] CI runs this as part of the release-readiness job.

**Dependencies:** T-021.
**Parallelizable:** No (gated by deletion).
**testingStrategy:** `{ layer: "integration", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

### Task 23: DR-11 Migration sweep — README rewrite + CHANGELOG breaking entry + grep-gate scripts

**Description:** Closes the DR-11 migration & documentation sweep. Rewrites `src/Strategos.Agents/README.md` with a ≤15-line builder example, adds `## [2.7.0] ### Changed (BREAKING) — Agent step contract` to root CHANGELOG with before/after migration recipe, and ships three CI grep-gate scripts: `scripts/check-agag-hygiene.sh` (DR-7 AGAG literal containment), `scripts/check-catch-discipline.sh` (DR-10 no parameterless catches), `scripts/check-prose.sh` (DIM-8 AI-vocabulary blocklist).

**Phase:** GREEN-only (docs + tooling) · **Test Layer:** unit (grep) · **Implements:** DR-7, DR-10, DR-11

**TDD Steps:**
1. [RED] Write tests:
   - `Readme_TrivialExample_FitsInFifteenLinesIncludingUsings`
   - `Changelog_270Section_HasBreakingAgentStepContractEntry`
   - `Source_AgentDiagnosticsLiterals_OnlyAppearInDiagnosticsAndExceptionFiles`
   - `Source_NoParameterlessCatch_ExistsInStrategosAgents`
   - `Prose_NoAIVocabularyClusters_InStrategosAgentsSourceAndDocs`
   - File: `src/Strategos.Agents.Tests/Unit/DocumentationAndProseGateTests.cs`.
   - Each test runs the corresponding shell gate as a subprocess (`scripts/check-prose.sh`, `scripts/check-agag-hygiene.sh`, etc.) and asserts exit code 0.
   - Expected failure: scripts don't exist; README is stale; CHANGELOG missing.
2. [GREEN] (parallelizable substeps):
   - Rewrite `src/Strategos.Agents/README.md` with a ≤15-line canonical builder example (per Risks §7).
   - Add `## [2.7.0] - 2026-MM-DD` `### Changed (BREAKING) — Agent step contract` to root `CHANGELOG.md` with 5–10 line migration recipe (before/after code blocks).
   - Write `scripts/check-agag-hygiene.sh` (grep AGAG literals outside Diagnostics + Exceptions files; exit non-zero on hit).
   - Write `scripts/check-catch-discipline.sh` (grep `catch *(` then exclude `catch (NamedType` patterns; exit non-zero on `catch` / `catch (Exception)` without rethrow).
   - Write `scripts/check-prose.sh` (grep AI-vocabulary cluster terms from `references/ai-prose-blocklist.txt`; exit non-zero on ≥3 hits per paragraph).
   - Wire all three scripts to the GitHub Actions workflow's `quality-gates` job.
3. [REFACTOR] Move shared shell helpers into `scripts/lib/`.

**Verification:**
- [ ] README trivial example compiles when extracted as `examples/trivial-agent-step.cs`.
- [ ] CHANGELOG present and structured.
- [ ] All three grep scripts exit 0 in CI on the post-merge tree.

**Dependencies:** T-018, T-021.
**Parallelizable:** Substeps within the task are parallel-safe (4 independent files).
**testingStrategy:** `{ layer: "unit", propertyTests: false, benchmarks: false, characterizationRequired: false }`

---

## Parallelization Strategy

```
                                      ┌─→ T-002 (AgentDiagnostics) ─┐
                                      │                              ├─→ T-003 (Exceptions)
                                      ├─→ T-004 (IAgentStep<,>) ─────┤
                       T-001          ├─→ T-005 (IMcpToolSource) ────┤
                  (acceptance RED) ───┤                              ├─→ T-007 (AgentStepBase scaffold)
                                      └─→ T-006 (Configuration) ─────┘
                                                                      │
                            ┌────────────────────────────────┬────────┴────────┐
                            ▼                                ▼                 ▼
                  T-008 (happy path)               T-012 (builder validate) T-017 (Mcp csproj)
                            │                                │                 │
                  T-009 (AGAG002) ──→ T-010 ──→ T-011    T-013 (WithTool)   T-018 (McpToolSource)
                                                              │
                                                          T-014 (options)
                                                              │
                                                          T-015 (Configurator)
                                                              │
                                                          T-016 (Build returns interface)
                                                              │
                                                              ▼
                                              T-019 (struct + tool round-trip)
                                                              │
                                              T-020 (MCP + middleware order)
                                                              │
                                              T-021 (delete old surface)
                                                              │
                                              T-022 (basileus smoke)
                                                              │
                                              T-023 (README + CHANGELOG + grep gates)
```

**Parallel-safe groups (run in worktrees):**

- **Group P1 (foundation, post T-001):** T-002, T-004, T-005, T-006 — pure type/const declarations, no shared files. T-003 depends on T-002 but can run alongside the others once T-002 lands. T-017 (MCP csproj scaffold) is also parallel — only touches new files.
- **Group P2 (orchestrator behaviors, sequential within group):** T-008 → T-009 → T-010 → T-011 — all edit `AgentStepBase.cs`. Sequential.
- **Group P3 (builder behaviors, sequential within group):** T-012 → T-013 → T-014 → T-015 → T-016 — all edit builder family. Sequential.
- **Group P4 (Mcp adapter, sequential within group):** T-017 → T-018 — distinct project, but T-018 depends on T-017's scaffold.

P2, P3, P4 can all execute concurrently (different files).

**Sequential tail:** T-019 → T-020 → T-021 → T-022 → T-023.

## Deferred Items

From the design's `Out of scope` and `Open Questions` sections, intentionally not addressed in this plan:

1. **Streaming responses** — `Strategos.Agents` already exposes `IStreamingHandler`. Adopting `GetStreamingResponseAsync<T>` is additive on top of DR-3 but deferred to v2.8.0+. No task allocated.
2. **Workflow primitives auto-reflected as `AIFunction`** — DR-4 provides the seam via `WithTool(AIFunction)`; the catalog of which workflow primitives become `AIFunctions` is a separate design exercise. Deferred.
3. **`Strategos.Agents.Mcp` versioning cadence** — Open Question #1. Plan defaults to "version with parent package" (`2.7.0-preview.2` for the cohesive cut) but does not commit to long-term cadence; revisit at v2.7.0 GA.
4. **Default `maximumIterationsPerRequest` value** — Open Question #2. Plan pins to 8 in T-011 / T-014 per design. If MEAI 10.6+ documents a recommendation, raise as a follow-up issue.
5. **`IAgentStep<TState, TResult>` placement** — Open Question #3. Plan keeps it in `Strategos.Agents.Abstractions` (current location); a future split into `Strategos.Agents.Contracts` is not in scope.
6. **Convenience builder overloads** — Open Question #4. Plan adds the minimum API surface needed for the design's DRs. Convenience overloads (`WithStaticSystemPrompt(string)`, etc.) deferred to a follow-up issue if the README example exceeds 15 lines in T-023.
7. **`AgentToolLoopException.PartialTrace` payload shape** — Open Question #5. Plan stores it as `IReadOnlyList<ChatMessage>` in T-011 as the prior; the OTel-semantic-conventions review is a follow-up.

## Completion Checklist
- [ ] All 23 tasks decomposed to 2-5 min TDD micro-cycles
- [ ] Every DR (DR-1..DR-11) traces to ≥1 task via `**Implements:**` field
- [ ] All test names follow `MethodName_Scenario_ExpectedOutcome`
- [ ] Acceptance test T-001 stays RED until T-019/T-020 land
- [ ] Sequential tail (T-019..T-023) cannot start until parallel groups complete
- [ ] `dotnet build src/strategos.sln` returns 0 warnings, 0 errors after T-021
- [ ] Cross-product smoke (T-022) compiles against `2.7.0-preview.2`
- [ ] Grep gates (T-023) green in CI
- [ ] Ready for plan-review delta analysis
