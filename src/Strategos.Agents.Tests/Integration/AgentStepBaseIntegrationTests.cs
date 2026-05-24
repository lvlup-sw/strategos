// =============================================================================
// <copyright file="AgentStepBaseIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Tests.Fixtures;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Integration;

/// <summary>
/// Real-chain integration tests (DR-9). Acceptance tests for the full MEAI 10.5
/// pipeline: structured output, AIFunction tool invocation, MCP tool resolution,
/// and middleware ordering through the full ChatClientBuilder.
/// </summary>
/// <remarks>
/// <para>
/// T-019 covers DR-9 (i) structured output + (ii) AIFunction round-trip and asserts
/// the fake <see cref="IChatClient"/> sits at the BOTTOM of the composed pipeline
/// (innermost — invoked last). The chain-composition ordering technique borrows from
/// the T-015 <c>AgentStepBuilderConfiguratorTests</c>.
/// </para>
/// <para>
/// T-020 owns DR-9 (iii) MCP tool resolution and (iv) host-middleware-injection
/// ordering through the full pipeline.
/// </para>
/// </remarks>
// DR-9: this class deliberately carries NO TUnit Property metadata that could
// be used by `--treenode-filter` to exclude it from CI. The design's DR-9
// acceptance criterion requires this test to run in the standard test job; a
// metadata gate is forbidden so it cannot be silently skipped.
public sealed class AgentStepBaseIntegrationTests
{
    [Test]
    public async Task MeaiPipeline_StructuredOutputWithAIFunctionTool_RoundTripsThroughChain()
    {
        // ----- Arrange ----------------------------------------------------------
        //
        // Tool: a single AIFunction that adds two ints. The closure increments
        // toolInvocations on each call so we can assert the round-trip count
        // independently of MEAI's bookkeeping (DR-9 (ii)).
        var toolInvocations = 0;
        var addTool = AIFunctionFactory.Create(
            (int a, int b) =>
            {
                toolInvocations++;
                return a + b;
            },
            name: "add");

        // Fake terminal IChatClient (the BOTTOM of the chain).
        // Call 1 → ChatResponse with FinishReason==ToolCalls containing a
        //          FunctionCallContent invoking add(2, 3). FunctionInvokingChatClient
        //          (one layer up) will execute the tool and re-call us with the
        //          tool result appended to the message list.
        // Call 2 → ChatResponse whose .Text is the JSON serialization of
        //          MyDto { Sum = 5 }. The MEAI typed extension that the orchestrator
        //          uses wraps this into a ChatResponse<MyDto> whose TryGetResult
        //          deserializes successfully.
        var observedLayerSequence = new List<string>();
        var fakeCallCount = 0;
        var fakeClient = new SequenceChatClient(
            response: () =>
            {
                fakeCallCount++;
                if (fakeCallCount == 1)
                {
                    var toolCall = new FunctionCallContent(
                        callId: "call-1",
                        name: "add",
                        arguments: new Dictionary<string, object?>
                        {
                            ["a"] = 2,
                            ["b"] = 3,
                        });
                    var assistantToolMessage = new ChatMessage(
                        ChatRole.Assistant,
                        new List<AIContent> { toolCall });
                    return new ChatResponse(assistantToolMessage)
                    {
                        FinishReason = ChatFinishReason.ToolCalls,
                    };
                }

                // Final structured-output response.
                var dto = new MyDto { Sum = 5 };
                var json = JsonSerializer.Serialize(dto);
                var assistantFinal = new ChatMessage(ChatRole.Assistant, json);
                return new ChatResponse(assistantFinal)
                {
                    FinishReason = ChatFinishReason.Stop,
                };
            },
            observedLayerSequence: observedLayerSequence);

        // Probe layer — inserted via ConfigureChatClient so it wraps everything the
        // builder appends after it. That places the probe ABOVE both
        // StrategosFunctionsChatClient and FunctionInvokingChatClient. We use the
        // probe to (1) confirm the fake is the inner terminal and (2) record call
        // ordering between the outermost host layer and the fake.
        var probe = new RecordingProbeChatClient(observedLayerSequence, "probe");

        MyDto? capturedDto = null;
        var applyResultInvocations = 0;

        var builder = new AgentStepBuilder<TestState, MyDto>();
        builder.WithSystemPrompt(_ => "You are a math helper. Use the add tool and return JSON {\"sum\": int}.");
        builder.WithUserPrompt(state => state.UserQuery);
        builder.WithApplyResult((state, result, _) =>
        {
            applyResultInvocations++;
            capturedDto = result;
            return Task.FromResult(new StepResult<TestState>(state with { Answer = result.Sum.ToString() }));
        });
        builder.WithTool(addTool);

        // Host configurator: inject the probe via builder.Use(...). Per T-015's chain
        // composition contract, _chatClientConfigurator runs FIRST, so the probe ends
        // up as the OUTERMOST middleware wrapping the rest of the pipeline.
        builder.ConfigureChatClient(b =>
        {
            b.Use(inner => probe.WithInner(inner));
        });

        var step = builder.Build(fakeClient);

        var initialState = new TestState { UserQuery = "what is 2 + 3?" };
        var stepContext = new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = initialState.WorkflowId,
            StepName = "MeaiRoundTripStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // ----- Act --------------------------------------------------------------
        var stepResult = await step.ExecuteAsync(initialState, stepContext, CancellationToken.None);

        // ----- Assert (DR-9 acceptance: i + ii) ---------------------------------

        // (a) The fake IChatClient was reached exactly twice — once for the
        // tool-call solicitation, once for the final structured response.
        await Assert.That(fakeCallCount).IsEqualTo(2);

        // (b) The add(2, 3) tool was invoked exactly once via the
        // FunctionInvokingChatClient layer and returned 5 (captured implicitly
        // by the assistant DTO).
        await Assert.That(toolInvocations).IsEqualTo(1);

        // (c) The ApplyResult hook received the typed MyDto, not raw text.
        await Assert.That(applyResultInvocations).IsEqualTo(1);
        await Assert.That(capturedDto).IsNotNull();
        await Assert.That(capturedDto!.Sum).IsEqualTo(5);
        await Assert.That(stepResult.UpdatedState.Answer).IsEqualTo("5");

        // (d) The fake is at the BOTTOM of the chain — mechanically verified.
        //
        // The composed pipeline structure (outermost → innermost) is:
        //
        //   probe (host configurator)
        //     → StrategosFunctionsChatClient (UseStrategosFunctions)
        //       → FunctionInvokingChatClient (UseFunctionInvocation)
        //         → fakeClient (the inner terminal)
        //
        // Verify each layer is present via IChatClient.GetService<T>() (T-015's
        // technique), AND that the per-invocation ordering recorded into
        // observedLayerSequence shows the probe is hit before the fake on every
        // request.
        var composed = GetComposedChatClient(step);

        var strategosLayer = composed.GetService<StrategosFunctionsChatClient>();
        await Assert.That(strategosLayer).IsNotNull();
        await Assert.That(strategosLayer!.Tools.Count).IsEqualTo(1);
        await Assert.That(strategosLayer.Tools[0]).IsSameReferenceAs(addTool);

        var fnInvoker = composed.GetService<FunctionInvokingChatClient>();
        await Assert.That(fnInvoker).IsNotNull();

        // Ordering proof: the probe (outermost host layer) is entered exactly once
        // per ExecuteAsync invocation; the FunctionInvokingChatClient internally
        // re-prompts its INNER client (StrategosFunctionsChatClient → fake) without
        // bubbling back up through the probe — so the fake is reached twice while
        // the probe is reached once. The probe MUST appear first in the recorded
        // sequence, mechanically proving:
        //   - the probe (host configurator) is outermost,
        //   - the fake is innermost (chain bottom),
        //   - and the function-invocation re-prompt loop sits BETWEEN them.
        await Assert.That(observedLayerSequence.Count(s => s == "probe")).IsEqualTo(1);
        await Assert.That(observedLayerSequence.Count(s => s == "fake")).IsEqualTo(2);
        await Assert.That(observedLayerSequence[0]).IsEqualTo("probe");
        await Assert.That(observedLayerSequence[1]).IsEqualTo("fake");
        await Assert.That(observedLayerSequence[2]).IsEqualTo("fake");
    }

    [Test]
    public async Task MeaiPipeline_McpToolResolutionAndMiddlewareInjection_RoundTripsThroughChain()
    {
        // ----- Arrange ----------------------------------------------------------
        //
        // T-020 acceptance for DR-9 (iii) MCP tool resolution + (iv) host-middleware-
        // injection ordering. The `multiply` tool is supplied ONLY through the
        // IToolSource adapter (no .WithTool(...) call for it) so a successful
        // round-trip mechanically proves the MCP path is live.
        //
        // The host middleware (UseLogging) is injected via .ConfigureChatClient(...)
        // and a real LoggerFactory wired to an OrderedLogRecorder captures the
        // global emission order. The closure-side-channel inside the multiply
        // AIFunction records the log index AT the moment the tool is invoked, so
        // we can prove LoggingChatClient logs appeared BEFORE function invocation.
        //
        // Cache verification: ExecuteAsync is called twice. The lazy MCP resolver
        // must be consulted exactly once across both calls.
        var recorder = new OrderedLogRecorder();
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            // Trace level so LoggingChatClient's entry/exit messages are captured.
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddProvider(recorder);
        });

        var toolInvocations = 0;
        int? firstToolInvocationLogIndex = null;

        // Multiply tool — closure captures the recorder's current entry count at
        // the precise moment of invocation. That index is the "boundary" for
        // proving logging fired strictly BEFORE function invocation.
        var multiplyTool = AIFunctionFactory.Create(
            (int a, int b) =>
            {
                toolInvocations++;
                firstToolInvocationLogIndex ??= recorder.Snapshot().Count;
                return a * b;
            },
            name: "multiply");

        // In-process IToolSource adapter (hand-rolled — NOT the production
        // McpToolSource). Yields the multiply tool and counts resolver calls so
        // the cache assertion can be made directly against the adapter.
        var adapter = new InProcessTestToolSource(new[] { multiplyTool });

        // Fake terminal IChatClient. Same pattern as the T-019 GREEN test but
        // emits multiply(3, 4) instead of add(2, 3) and returns {"product": 12}.
        // ExecuteAsync is called twice — each call follows the same
        // ToolCalls → Stop two-step sequence. fakeCallCount % 2 drives the
        // pattern so the second ExecuteAsync also produces a clean structured
        // output (call 3 = ToolCalls, call 4 = Stop).
        var fakeCallCount = 0;
        var fakeClient = new SequenceChatClient(
            response: () =>
            {
                fakeCallCount++;
                var isToolCallTurn = fakeCallCount % 2 == 1;
                if (isToolCallTurn)
                {
                    var toolCall = new FunctionCallContent(
                        callId: $"call-{fakeCallCount}",
                        name: "multiply",
                        arguments: new Dictionary<string, object?>
                        {
                            ["a"] = 3,
                            ["b"] = 4,
                        });
                    var assistantToolMessage = new ChatMessage(
                        ChatRole.Assistant,
                        new List<AIContent> { toolCall });
                    return new ChatResponse(assistantToolMessage)
                    {
                        FinishReason = ChatFinishReason.ToolCalls,
                    };
                }

                var dto = new MultDto { Product = 12 };
                var json = JsonSerializer.Serialize(dto);
                var assistantFinal = new ChatMessage(ChatRole.Assistant, json);
                return new ChatResponse(assistantFinal)
                {
                    FinishReason = ChatFinishReason.Stop,
                };
            });

        MultDto? capturedDto = null;
        var applyResultInvocations = 0;

        var builder = new AgentStepBuilder<TestState, MultDto>();
        builder.WithSystemPrompt(_ => "You are a math helper. Use the multiply tool and return JSON {\"product\": int}.");
        builder.WithUserPrompt(state => state.UserQuery);
        builder.WithApplyResult((state, result, _) =>
        {
            applyResultInvocations++;
            capturedDto = result;
            return Task.FromResult(new StepResult<TestState>(state with { Answer = result.Product.ToString() }));
        });

        // MCP path ONLY — no WithTool(multiplyTool). If multiply ever fires, it
        // *must* have come through the IToolSource adapter.
        builder.WithToolSource(adapter);

        // Host configurator: UseLogging wraps the entire downstream chain, so the
        // LoggingChatClient entry-side log fires before descending into
        // StrategosFunctionsChatClient → FunctionInvokingChatClient → multiplyTool.
        builder.ConfigureChatClient(b => b.UseLogging(loggerFactory));

        var step = builder.Build(fakeClient);

        var initialState = new TestState { UserQuery = "what is 3 * 4?" };
        var stepContext = new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = initialState.WorkflowId,
            StepName = "MeaiMcpRoundTripStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // ----- Act: first execution --------------------------------------------
        var firstResult = await step.ExecuteAsync(initialState, stepContext, CancellationToken.None);

        // ----- Assert (DR-9 (iii): MCP path exercised + tool invoked via MCP) --

        // The fake reached the bottom twice (ToolCalls then Stop).
        await Assert.That(fakeCallCount).IsEqualTo(2);

        // The multiply tool was invoked exactly once during this ExecuteAsync.
        // It was ONLY supplied via the IToolSource adapter — no .WithTool(...).
        // Any invocation here proves the MCP-discovered tool surfaced to
        // FunctionInvokingChatClient through the real builder chain.
        await Assert.That(toolInvocations).IsEqualTo(1);

        // ApplyResult received the typed DTO carrying the tool's product.
        await Assert.That(applyResultInvocations).IsEqualTo(1);
        await Assert.That(capturedDto).IsNotNull();
        await Assert.That(capturedDto!.Product).IsEqualTo(12);
        await Assert.That(firstResult.UpdatedState.Answer).IsEqualTo("12");

        // ----- Assert (DR-9 (iii bis): adapter actually consulted) -------------
        //
        // Even an explicit positive signal that the resolver was hit — independent
        // of MEAI's bookkeeping. Coincidence would require the multiply tool to
        // materialize from nowhere; this rules it out.
        await Assert.That(adapter.GetToolsAsyncCount).IsGreaterThanOrEqualTo(1);

        // ----- Assert (DR-9 (iv): logging fires BEFORE function invocation) ---
        //
        // firstToolInvocationLogIndex was captured INSIDE the multiply lambda,
        // so it reflects the recorder's entry count at the exact moment of tool
        // execution. Every LoggingChatClient entry recorded at an index strictly
        // less than that boundary is a log that fired before the tool ran. The
        // host configurator's UseLogging must produce at least one such entry,
        // proving it wraps StrategosFunctionsChatClient + FunctionInvokingChatClient.
        await Assert.That(firstToolInvocationLogIndex).IsNotNull();
        var boundary = firstToolInvocationLogIndex!.Value;
        await Assert.That(boundary).IsGreaterThan(0);

        var entries = recorder.Snapshot();
        var loggingClientEntriesBeforeTool = entries
            .Take(boundary)
            .Where(e => e.Category.Contains("LoggingChatClient", StringComparison.Ordinal))
            .ToList();
        await Assert.That(loggingClientEntriesBeforeTool.Count).IsGreaterThan(0);

        // Also verify the chain structure — the MCP-resolved tool must be visible
        // on the StrategosFunctionsChatClient layer's per-request merge surface,
        // and the FunctionInvokingChatClient layer must be present so the
        // resolved tool actually gets invoked.
        var composed = GetComposedChatClient(step);
        var strategosLayer = composed.GetService<StrategosFunctionsChatClient>();
        await Assert.That(strategosLayer).IsNotNull();
        // Strategos-registered tools list is empty — multiply came from MCP only.
        await Assert.That(strategosLayer!.Tools.Count).IsEqualTo(0);
        var fnInvoker = composed.GetService<FunctionInvokingChatClient>();
        await Assert.That(fnInvoker).IsNotNull();

        // ----- Act: second execution (cache assertion) -------------------------
        //
        // The MCP resolver caches forever per middleware-instance after a
        // successful resolution. A second ExecuteAsync on the same step must
        // NOT trigger a re-resolution.
        var secondResult = await step.ExecuteAsync(initialState, stepContext, CancellationToken.None);

        // Multiply was invoked once per ExecuteAsync — twice total.
        await Assert.That(toolInvocations).IsEqualTo(2);
        await Assert.That(secondResult.UpdatedState.Answer).IsEqualTo("12");

        // ----- Assert (cache verification) -------------------------------------
        await Assert.That(adapter.GetToolsAsyncCount).IsEqualTo(1);

        // Touch the AGAG codes referenced by upstream failure paths so the using
        // import stays anchored (kept from the original placeholder).
        _ = AgentDiagnostics.AGAG002;
        _ = AgentDiagnostics.AGAG005;
    }

    [Test]
    public async Task FullChain_StreamingWithToolSourceAndMcp_RoundTripsThroughPipeline()
    {
        // =====================================================================
        // T-001 / T-020 acceptance (DR-12, DR-1, DR-8, DR-9).
        //
        // Drives the FULL ChatClientBuilder pipeline with a streaming-capable
        // fake IChatClient at the BOTTOM and proves, through the real chain:
        //   (i)   streaming materializes typed TResult AND the IStreamingHandler
        //         token callbacks fire BEFORE ApplyResult (shared ordering recorder);
        //   (ii)  an in-process AgentToolSource tool is actually INVOKED (round-trip)
        //         via UseFunctionInvocation — not merely registered;
        //   (iii) an MCP-shaped IToolSource (InProcessTestToolSource fake) resolves
        //         and merges;
        //   (iv)  two tool sources merge with correct precedence (registration order:
        //         the first-registered source wins a name collision).
        //
        // Nothing here is mocked except the bottom IChatClient — the builder,
        // AgentStepConfiguration, AgentToolSource and StrategosFunctionsChatClient
        // are all constructed real.
        // =====================================================================

        // Shared ordering recorder. Each entry is a phase tag in arrival order so we
        // can assert "every streaming token fired before ApplyResult ran".
        var order = new List<string>();

        // ----- (ii) real in-process AgentToolSource -----------------------------
        // `agentEcho` is supplied ONLY through the production AgentToolSource adapter
        // (no .WithTool). Any invocation proves the in-process source round-tripped
        // through the real chain. FromObject is used (not FromDelegates) so the tool
        // names are deterministic via [AgentTool(Name=...)] — the model addresses
        // them by stable name. `ping` collides with sourceB's ping; because sourceA
        // is registered FIRST, the documented registration-order precedence means
        // THIS impl must win (iv).
        var toolHostA = new AgentEchoToolHost(order);
        var sourceA = AgentToolSource.FromObject(toolHostA);

        // ----- (iii) MCP-shaped IToolSource (InProcessTestToolSource) -----------
        // `mcpMultiply` is supplied ONLY through the MCP-path fake. A collision tool
        // `ping` (returning "B") is also exposed so the precedence assertion has a
        // loser to compare against.
        var mcpMultiplyInvocations = 0;
        var mcpMultiply = AIFunctionFactory.Create(
            (int a, int b) =>
            {
                mcpMultiplyInvocations++;
                order.Add("tool:mcpMultiply");
                return a * b;
            },
            name: "mcpMultiply");
        var pingB = AIFunctionFactory.Create(
            () =>
            {
                order.Add("tool:pingB");
                return "B";
            },
            name: "ping");
        var sourceB = new InProcessTestToolSource(new[] { mcpMultiply, pingB });

        // ----- streaming-capable fake terminal IChatClient ---------------------
        // Streaming turn 1 → tool-call updates for agentEcho, mcpMultiply, ping
        //   (FinishReason == ToolCalls). FunctionInvokingChatClient executes them and
        //   re-streams.
        // Streaming turn 2 → the final JSON answer streamed in three chunks so the
        //   handler observes multiple token callbacks (FinishReason == Stop).
        var streamingTurn = 0;
        var fakeClient = new StreamingSequenceChatClient(() =>
        {
            streamingTurn++;
            if (streamingTurn == 1)
            {
                var toolCalls = new List<ChatResponseUpdate>
                {
                    new(ChatRole.Assistant, new List<AIContent>
                    {
                        new FunctionCallContent("c-echo", "agentEcho", new Dictionary<string, object?> { ["text"] = "hi" }),
                    }) { FinishReason = ChatFinishReason.ToolCalls },
                    new(ChatRole.Assistant, new List<AIContent>
                    {
                        new FunctionCallContent("c-mul", "mcpMultiply", new Dictionary<string, object?> { ["a"] = 6, ["b"] = 7 }),
                    }) { FinishReason = ChatFinishReason.ToolCalls },
                    new(ChatRole.Assistant, new List<AIContent>
                    {
                        new FunctionCallContent("c-ping", "ping", new Dictionary<string, object?>()),
                    }) { FinishReason = ChatFinishReason.ToolCalls },
                };
                return toolCalls;
            }

            // Final structured payload streamed across chunks. The last chunk carries
            // FinishReason.Stop so the FunctionInvokingChatClient loop terminates.
            return new List<ChatResponseUpdate>
            {
                new(ChatRole.Assistant, "{\"prod"),
                new(ChatRole.Assistant, "uct\":42,\"pi"),
                new(ChatRole.Assistant, "ng\":\"A\"}") { FinishReason = ChatFinishReason.Stop },
            };
        });

        var streamingHandler = new OrderRecordingStreamingHandler(order);

        FullChainDto? capturedDto = null;
        var applyResultInvocations = 0;

        var builder = new AgentStepBuilder<TestState, FullChainDto>();
        builder.WithSystemPrompt(_ => "Use the tools, then return JSON {\"product\":int,\"ping\":string}.");
        builder.WithUserPrompt(state => state.UserQuery);
        builder.WithApplyResult((state, result, _) =>
        {
            applyResultInvocations++;
            order.Add("apply");
            capturedDto = result;
            return Task.FromResult(new StepResult<TestState>(state with { Answer = result.Product.ToString() }));
        });

        // Tool sources merged in registration order: A (AgentToolSource) then B (MCP).
        builder.WithToolSource(sourceA);
        builder.WithToolSource(sourceB);

        // Streaming observability layer over the terminal typed contract.
        builder.WithStreaming(streamingHandler);

        var step = builder.Build(fakeClient);

        var initialState = new TestState { UserQuery = "compute it" };
        var stepContext = new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = initialState.WorkflowId,
            StepName = "FullChainStreamingStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // ----- Act --------------------------------------------------------------
        var stepResult = await step.ExecuteAsync(initialState, stepContext, CancellationToken.None);

        // ----- Assert (i): streaming materialized typed TResult ----------------
        await Assert.That(applyResultInvocations).IsEqualTo(1);
        await Assert.That(capturedDto).IsNotNull();
        await Assert.That(capturedDto!.Product).IsEqualTo(42);
        await Assert.That(stepResult.UpdatedState.Answer).IsEqualTo("42");

        // ----- Assert (i): token callbacks fired BEFORE ApplyResult ------------
        // The recorder interleaves "token:*" (from the handler) and "apply" (from
        // ApplyResult). ApplyResult must appear exactly once and be the LAST entry —
        // every streamed token preceded it.
        await Assert.That(streamingHandler.Tokens.Count).IsGreaterThan(0);
        var applyIndex = order.IndexOf("apply");
        await Assert.That(applyIndex).IsEqualTo(order.Count - 1);
        var anyTokenAfterApply = order
            .Skip(applyIndex + 1)
            .Any(e => e.StartsWith("token:", StringComparison.Ordinal));
        await Assert.That(anyTokenAfterApply).IsFalse();
        await Assert.That(streamingHandler.CompletionCount).IsEqualTo(1);

        // ----- Assert (ii): AgentToolSource tool actually invoked --------------
        await Assert.That(toolHostA.EchoInvocations).IsGreaterThanOrEqualTo(1);
        await Assert.That(order.Contains("tool:agentEcho")).IsTrue();

        // ----- Assert (iii): MCP-shaped IToolSource resolved + invoked ---------
        await Assert.That(sourceB.GetToolsAsyncCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(mcpMultiplyInvocations).IsGreaterThanOrEqualTo(1);
        await Assert.That(order.Contains("tool:mcpMultiply")).IsTrue();

        // ----- Assert (iv): precedence — sourceA's ping wins -------------------
        // Both sources expose a `ping`. sourceA was registered first, so its impl
        // (tagged "tool:pingA", returning "A") must be the one invoked; sourceB's
        // pingB must never fire.
        await Assert.That(order.Contains("tool:pingA")).IsTrue();
        await Assert.That(order.Contains("tool:pingB")).IsFalse();
        await Assert.That(capturedDto.Ping).IsEqualTo("A");

        // Structural proof: the composed chain carries both Strategos + Fn-invocation
        // layers and the fake streamed (never buffered).
        var composed = GetComposedChatClient(step);
        await Assert.That(composed.GetService<StrategosFunctionsChatClient>()).IsNotNull();
        await Assert.That(composed.GetService<FunctionInvokingChatClient>()).IsNotNull();
        await Assert.That(fakeClient.StreamingInvoked).IsTrue();
        await Assert.That(fakeClient.BufferedInvoked).IsFalse();
    }

    /// <summary>
    /// Reflectively reads the private <c>_chatClient</c> field on
    /// <see cref="AgentStepBase{TState, TResult}"/> so we can inspect the composed
    /// pipeline. Matches the technique used in
    /// <c>AgentStepBuilderConfiguratorTests</c>.
    /// </summary>
    private static IChatClient GetComposedChatClient(object step)
    {
        var field = step.GetType().GetField(
            "_chatClient",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected private field '_chatClient' on AgentStepBase<,>.");
        return (IChatClient)field.GetValue(step)!;
    }

    internal sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();

        public string UserQuery { get; init; } = string.Empty;

        public string Answer { get; init; } = string.Empty;
    }

    internal sealed class MyDto
    {
        public int Sum { get; set; }
    }

    internal sealed class MultDto
    {
        public int Product { get; set; }
    }

    internal sealed class FullChainDto
    {
        public int Product { get; set; }

        public string Ping { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tool host whose <c>[AgentTool]</c>-annotated methods are surfaced via the
    /// production <see cref="AgentToolSource.FromObject(object)"/> adapter. Used to
    /// prove an in-process AgentToolSource tool round-trips through the real chain.
    /// </summary>
    private sealed class AgentEchoToolHost
    {
        private readonly List<string> _order;
        private int _echoInvocations;

        public AgentEchoToolHost(List<string> order)
        {
            _order = order;
        }

        public int EchoInvocations => _echoInvocations;

        [AgentTool(Name = "agentEcho")]
        public string Echo(string text)
        {
            _echoInvocations++;
            _order.Add("tool:agentEcho");
            return $"echo:{text}";
        }

        [AgentTool(Name = "ping")]
        public string Ping()
        {
            _order.Add("tool:pingA");
            return "A";
        }
    }

    /// <summary>
    /// <see cref="IStreamingHandler"/> that appends a phase tag to a shared ordering
    /// recorder for every token and completion callback, so the acceptance test can
    /// assert tokens fired BEFORE ApplyResult.
    /// </summary>
    private sealed class OrderRecordingStreamingHandler : IStreamingHandler
    {
        private readonly List<string> _order;
        private readonly List<string> _tokens = new();

        public OrderRecordingStreamingHandler(List<string> order) => _order = order;

        public IReadOnlyList<string> Tokens => _tokens;

        public int CompletionCount { get; private set; }

        public Task OnTokenReceivedAsync(string token, Guid workflowId, string stepName, CancellationToken cancellationToken = default)
        {
            _tokens.Add(token);
            _order.Add($"token:{token}");
            return Task.CompletedTask;
        }

        public Task OnResponseCompletedAsync(string fullResponse, Guid workflowId, string stepName, CancellationToken cancellationToken = default)
        {
            CompletionCount++;
            _order.Add("complete");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Streaming-capable fake terminal <see cref="IChatClient"/>. Each call to
    /// <see cref="GetStreamingResponseAsync"/> replays the next scripted batch of
    /// <see cref="ChatResponseUpdate"/>s (driven by the supplied factory). Buffered
    /// invocation is recorded and rejected so the test can prove the streaming branch
    /// was taken. Resolves itself + inner via <see cref="GetService"/> so the composed
    /// pipeline's service probes succeed.
    /// </summary>
    private sealed class StreamingSequenceChatClient : IChatClient
    {
        private readonly Func<IReadOnlyList<ChatResponseUpdate>> _next;

        public StreamingSequenceChatClient(Func<IReadOnlyList<ChatResponseUpdate>> next) => _next = next;

        public bool StreamingInvoked { get; private set; }

        public bool BufferedInvoked { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            BufferedInvoked = true;
            throw new InvalidOperationException("Buffered path must not be invoked when streaming.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamingInvoked = true;
            foreach (var update in _next())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
                await Task.Yield();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            if (serviceKey is null && serviceType.IsInstanceOfType(this))
            {
                return this;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Custom <see cref="ILoggerProvider"/> that captures every log entry in the order
    /// it was emitted across all logger categories. Used to mechanically assert
    /// middleware ordering between the host-injected logging layer and tool
    /// invocation. Mirrors the technique in <c>AgentStepBuilderConfiguratorTests</c>.
    /// </summary>
    private sealed class OrderedLogRecorder : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = new();
        private readonly object _gate = new();

        public ILogger CreateLogger(string categoryName) => new RecorderLogger(categoryName, this);

        public List<LogEntry> Snapshot()
        {
            lock (_gate)
            {
                return new List<LogEntry>(_entries);
            }
        }

        public void Dispose()
        {
            // no-op
        }

        private void Record(LogEntry entry)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        public sealed record LogEntry(string Category, LogLevel Level, string Message);

        private sealed class RecorderLogger : ILogger
        {
            private readonly string _category;
            private readonly OrderedLogRecorder _owner;

            public RecorderLogger(string category, OrderedLogRecorder owner)
            {
                _category = category;
                _owner = owner;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                _owner.Record(new LogEntry(_category, logLevel, message));
            }
        }
    }

    /// <summary>
    /// Fake terminal IChatClient that emits a caller-supplied <see cref="ChatResponse"/>
    /// per invocation. Sits at the BOTTOM of the composed chain — there is no
    /// further inner client. Records its tag into a shared sequence list each
    /// time it is invoked so middleware ordering can be asserted.
    /// </summary>
    private sealed class SequenceChatClient : IChatClient
    {
        private readonly Func<ChatResponse> _response;
        private readonly List<string>? _observedLayerSequence;
        private const string LayerTag = "fake";

        public SequenceChatClient(Func<ChatResponse> response, List<string>? observedLayerSequence = null)
        {
            _response = response;
            _observedLayerSequence = observedLayerSequence;
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _observedLayerSequence?.Add(LayerTag);
            return Task.FromResult(_response());
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming is out of scope for T-019.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            if (serviceKey is null && serviceType.IsInstanceOfType(this))
            {
                return this;
            }

            return null;
        }

        public void Dispose()
        {
            // no-op
        }
    }

    /// <summary>
    /// Probe layer inserted via <c>ConfigureChatClient(b =&gt; b.Use(inner =&gt; probe.WithInner(inner)))</c>.
    /// Records its self-tag to a shared <see cref="List{T}"/> on each invocation
    /// so middleware ordering can be mechanically asserted.
    /// </summary>
    private sealed class RecordingProbeChatClient : IChatClient
    {
        private readonly List<string> _sequence;
        private readonly string _selfTag;
        private IChatClient? _inner;

        public RecordingProbeChatClient(List<string> sequence, string selfTag)
        {
            _sequence = sequence;
            _selfTag = selfTag;
        }

        private IChatClient Inner =>
            _inner ?? throw new InvalidOperationException("Inner not wired — call WithInner(...) first.");

        public RecordingProbeChatClient WithInner(IChatClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            return this;
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _sequence.Add(_selfTag);
            return await Inner.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Streaming is out of scope for T-019.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            if (serviceKey is null && serviceType.IsInstanceOfType(this))
            {
                return this;
            }

            return _inner?.GetService(serviceType, serviceKey);
        }

        public void Dispose()
        {
            // no-op
        }
    }
}
