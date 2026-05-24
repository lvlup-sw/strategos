// =============================================================================
// <copyright file="AgentStepBaseToolLoopTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Configuration;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Exceptions;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Integration;

/// <summary>
/// DR-8 acceptance test: tool-call iteration is mechanically bounded by composing
/// <see cref="FunctionInvokingChatClient"/> with <c>MaximumIterationsPerRequest = 8</c>.
/// The orchestrator must surface the cap as <see cref="AgentToolLoopException"/> (AGAG005)
/// rather than silently returning whatever the capped pipeline produced.
/// </summary>
// DR-9: this class deliberately carries NO TUnit Property metadata that could
// be used by `--treenode-filter` to exclude it from CI. The design's DR-9
// acceptance criterion requires this test to run in the standard test job; a
// metadata gate is forbidden so it cannot be silently skipped.
public sealed class AgentStepBaseToolLoopTests
{
    [Test]
    public async Task ExecuteAsync_ToolCallsExceedMaxIterations_ThrowsAgentToolLoopExceptionAtCap()
    {
        // Arrange — the fake inner client never produces a terminal response. Every
        // call returns a ChatResponse containing a single FunctionCallContent that
        // targets our registered AIFunction tool, with FinishReason = ToolCalls.
        // FunctionInvokingChatClient will invoke the tool, append the tool result,
        // and call the inner client again — repeating until MaximumIterationsPerRequest
        // (8) is reached, at which point MEAI logs and returns the latest response
        // without throwing. The orchestrator is responsible for detecting this and
        // raising AGAG005.
        var fakeInvocations = 0;

        // AIFunction whose Name matches the FunctionCallContent.Name we emit below.
        // The implementation is irrelevant — FunctionInvokingChatClient just needs
        // a tool to invoke so the loop keeps going.
        var fakeTool = AIFunctionFactory.Create(
            () => "tool-result",
            name: "fake_tool");

        var innerClient = Substitute.For<IChatClient>();
        innerClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fakeInvocations++;
                var callId = $"call-{fakeInvocations}";
                var toolCall = new FunctionCallContent(
                    callId: callId,
                    name: "fake_tool",
                    arguments: new Dictionary<string, object?>());
                var assistantMessage = new ChatMessage(ChatRole.Assistant, new List<AIContent> { toolCall });
                return new ChatResponse(assistantMessage)
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
            });

        // Compose: FunctionInvokingChatClient(inner) with the iteration bound.
        var composedClient = new ChatClientBuilder(innerClient)
            .UseFunctionInvocation(loggerFactory: null, configure: c => c.MaximumIterationsPerRequest = 8)
            .Build();

        var initialState = new TestState { UserQuery = "loop forever" };

        var applyResult = Substitute.For<Func<TestState, TestDto, CancellationToken, Task<StepResult<TestState>>>>();
        applyResult
            .Invoke(Arg.Any<TestState>(), Arg.Any<TestDto>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new StepResult<TestState>(ci.Arg<TestState>())));

        // Provide the tool via ChatOptions so the composed FunctionInvokingChatClient
        // can resolve and invoke it. (The orchestrator passes ChatOptions through; the
        // builder-level tool wiring is T-015's responsibility.)
        var chatOptions = new ChatOptions
        {
            Tools = new List<AITool> { fakeTool },
        };

        var configuration = new AgentStepConfiguration<TestState, TestDto>(
            SystemPrompt: _ => "system",
            UserPrompt: state => state.UserQuery,
            ApplyResult: applyResult,
            Tools: new[] { fakeTool },
            ToolSources: Array.Empty<IToolSource>(),
            ChatOptions: chatOptions,
            ChatClientConfigurator: null,
            MaxToolIterations: 8);

        var orchestrator = new AgentStepBase<TestState, TestDto>(composedClient, configuration);
        var context = new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = initialState.WorkflowId,
            StepName = "ToolLoopStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // Act + Assert
        var thrown = await Assert
            .That(async () => await orchestrator.ExecuteAsync(initialState, context, CancellationToken.None))
            .Throws<AgentToolLoopException>();

        await Assert.That(thrown!.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG005);
        await Assert.That(thrown.MaxIterations).IsEqualTo(8);
        await Assert.That(thrown.PartialTrace).IsNotNull();
        await Assert.That(thrown.PartialTrace.Count).IsGreaterThan(0);

        // Mechanical enforcement of the iteration cap. The number of inner-client
        // invocations is bounded by FunctionInvokingChatClient itself — there is no
        // orchestrator-side counter and no "documented contract" mitigation.
        //
        // MEAI 10.5.2 semantics for MaximumIterationsPerRequest = N (decompiled from
        // FunctionInvokingChatClient.GetResponseAsync):
        //
        //     int iteration = 0;
        //     while (true) {
        //         if (iteration >= MaximumIterationsPerRequest) {
        //             LogMaximumIterationsReached(N);
        //             PrepareOptionsForLastIteration(ref options); // strips tools
        //         }
        //         response = await inner.GetResponseAsync(messages, options, ct);
        //         bool more = iteration < MaximumIterationsPerRequest
        //                  && CopyFunctionCalls(response.Messages, ref functionCallContents);
        //         if (!more) break;
        //         // …apply tool results, iteration++…
        //     }
        //
        // With N = 8 and a fake that always returns tool-call content:
        //   iterations 0..7  → 8 calls WITH tools, each returns a tool call
        //   iteration  8     → tools stripped + log fires + 1 final call WITHOUT tools
        //
        // Total inner-client calls = 9. This is the empirically-correct mechanical
        // count; it is NOT equal to N. (If a future MEAI release tightens this to
        // "exactly N calls", update this assertion together with the comment.)
        await Assert.That(fakeInvocations).IsEqualTo(9);

        // apply-result hook MUST NEVER be invoked on the failure path (DR-10).
        _ = applyResult.DidNotReceive().Invoke(
            Arg.Any<TestState>(),
            Arg.Any<TestDto>(),
            Arg.Any<CancellationToken>());
    }

    internal sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();

        public string UserQuery { get; init; } = string.Empty;
    }

    internal sealed record TestDto(string Answer);
}
