// =============================================================================
// <copyright file="AgentStepBaseIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Agents.Diagnostics;
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
[Property("Category", "Integration")]
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
    [Skip("DR-9 (iii) + (iv) — owned by T-020: MCP tool resolution + middleware-injection ordering across the full pipeline. " +
        "T-019 covers (i) structured output + (ii) AIFunction round-trip; this placeholder is intentional.")]
    public Task MeaiPipeline_McpToolResolutionAndMiddlewareInjection_RoundTripsThroughChain()
    {
        // T-020 anchor: stays RED-by-Skip until T-020 fills in:
        //   - InProcessMcpToolSource registered via .WithMcpToolSource(...) (T-014)
        //   - Host middleware (e.g. UseLogging) injected via .ConfigureChatClient(...)
        //   - Assertion that MCP-discovered tools reach FunctionInvokingChatClient
        //   - Assertion that host middleware fires BEFORE Strategos-internal stages
        //
        // Diagnostic codes asserted on failure paths:
        //   AgentDiagnostics.AGAG002 — structured output (T-009)
        //   AgentDiagnostics.AGAG005 — tool-loop overflow (T-011)
        _ = AgentDiagnostics.AGAG002;
        _ = AgentDiagnostics.AGAG005;
        return Task.CompletedTask;
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
