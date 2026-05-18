// =============================================================================
// <copyright file="AgentStepBaseIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

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
    [Skip("T-019 BLOCKED by prior-wave wiring defect: AIFunctions registered via " +
        "AgentStepBuilder.WithTool() never reach FunctionInvokingChatClient.options.Tools — " +
        "StrategosFunctionsChatClient exposes them only via GetService<T>() and never injects " +
        "them into the per-request ChatOptions.Tools. AgentStepBase<,>.ExecuteAsync forwards " +
        "_configuration.ChatOptions (null here) unchanged. Net result: the fake at the chain " +
        "bottom is invoked twice (FIC sees the tool-call FinishReason and treats the missing " +
        "tool as NotFound), but the AIFunction's closure is never executed. Follow-up ticket " +
        "needed to either (a) override StrategosFunctionsChatClient.GetResponseAsync to clone " +
        "options and merge Tools, or (b) merge AgentStepConfiguration.Tools into ChatOptions " +
        "inside AgentStepBase<,>.ExecuteAsync. T-019 explicitly defers this fix per spec " +
        "(\"stop and report rather than fixing in T-019\"). The test body below is the GREEN " +
        "blueprint — un-skip it after the wiring backfill lands.")]
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
            });

        // Probe layer — inserted via ConfigureChatClient so it wraps everything the
        // builder appends after it. That places the probe ABOVE both
        // StrategosFunctionsChatClient and FunctionInvokingChatClient, but BELOW any
        // host middleware. We use the probe to (1) confirm the fake is the inner
        // terminal (probe sees response identity AFTER inner stages return) and
        // (2) record call ordering between the probe and the fake.
        var observedLayerSequence = new List<string>();
        var probe = new RecordingProbeChatClient(observedLayerSequence, "probe");

        var apply_capturedDto = (MyDto?)null;
        var applyResultInvocations = 0;

        var builder = new AgentStepBuilder<TestState, MyDto>();
        builder.WithSystemPrompt(_ => "You are a math helper. Use the add tool and return JSON {\"sum\": int}.");
        builder.WithUserPrompt(state => state.UserQuery);
        builder.WithApplyResult((state, result, _) =>
        {
            applyResultInvocations++;
            apply_capturedDto = result;
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
        await Assert.That(apply_capturedDto).IsNotNull();
        await Assert.That(apply_capturedDto!.Sum).IsEqualTo(5);
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
        // technique), AND that the probe's observed inner reference (resolved at
        // first invocation) is the FunctionInvokingChatClient — which itself wraps
        // the StrategosFunctionsChatClient → fakeClient chain.
        var composed = GetComposedChatClient(step);

        var strategosLayer = composed.GetService<StrategosFunctionsChatClient>();
        await Assert.That(strategosLayer).IsNotNull();
        await Assert.That(strategosLayer!.Tools.Count).IsEqualTo(1);
        await Assert.That(strategosLayer.Tools[0]).IsSameReferenceAs(addTool);

        var fnInvoker = composed.GetService<FunctionInvokingChatClient>();
        await Assert.That(fnInvoker).IsNotNull();

        // Ordering proof: probe was hit at least once during execution and its
        // recorded order shows it was called BEFORE the fake on every request.
        // (Each ExecuteAsync iteration that reaches the fake also passes through
        // the probe.) The fake was reached twice, the probe was reached twice,
        // and "probe" precedes each "fake" entry in the sequence.
        await Assert.That(observedLayerSequence.Count(s => s == "probe")).IsEqualTo(2);
        await Assert.That(observedLayerSequence.Count(s => s == "fake")).IsEqualTo(2);
        for (var i = 0; i < observedLayerSequence.Count - 1; i += 2)
        {
            await Assert.That(observedLayerSequence[i]).IsEqualTo("probe");
            await Assert.That(observedLayerSequence[i + 1]).IsEqualTo("fake");
        }
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
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
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
    /// further inner client.
    /// </summary>
    private sealed class SequenceChatClient : IChatClient
    {
        private readonly Func<ChatResponse> _response;

        public SequenceChatClient(Func<ChatResponse> response)
        {
            _response = response;
            _layerTag = "fake";
        }

        public SequenceChatClient(Func<ChatResponse> response, List<string>? observedLayerSequence)
            : this(response)
        {
            ObservedLayerSequence = observedLayerSequence;
        }

        // Optional sequence recorder so the fake can self-report when it was hit
        // (only used when a probe layer is wired up alongside this client).
        private readonly string _layerTag;

        public List<string>? ObservedLayerSequence { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ObservedLayerSequence?.Add(_layerTag);
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
    /// Records its own tag plus the tag of its inner client to a shared
    /// <see cref="List{T}"/> so middle-ordering can be mechanically asserted.
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

        public IChatClient Inner =>
            _inner ?? throw new InvalidOperationException("Inner not wired — call WithInner(...) first.");

        public RecordingProbeChatClient WithInner(IChatClient inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            // Tag the inner fake (if present) so it can record its own invocations.
            var fake = (SequenceChatClient?)inner.GetService(typeof(SequenceChatClient));
            if (fake is not null)
            {
                fake.ObservedLayerSequence = _sequence;
            }

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
            return Inner.GetStreamingResponseAsync(messages, options, cancellationToken);
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
