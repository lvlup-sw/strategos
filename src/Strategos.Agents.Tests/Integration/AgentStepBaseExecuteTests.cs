// =============================================================================
// <copyright file="AgentStepBaseExecuteTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Configuration;
using Strategos.Agents.Exceptions;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Integration;

/// <summary>
/// DR-3 acceptance tests for <see cref="AgentStepBase{TState, TResult}"/>'s happy-path
/// structured-output execution: <c>GetResponseAsync&lt;TResult&gt;</c> →
/// <c>TryGetResult</c> → <c>ApplyResult</c>.
/// </summary>
[Property("Category", "Integration")]
public sealed class AgentStepBaseExecuteTests
{
    [Test]
    public async Task ExecuteAsync_TypedResponse_InvokesApplyResultWithTypedPayload()
    {
        // Arrange — fake IChatClient.GetResponseAsync (untyped) returns a ChatResponse whose
        // .Text is a JSON serialization of TestDto. The MEAI typed extension
        // ChatClientStructuredOutputExtensions.GetResponseAsync<T> wraps that into a
        // ChatResponse<TestDto>; its TryGetResult will JSON-deserialize successfully.
        var expectedDto = new TestDto(Answer: "four", Confidence: 0.9);
        var jsonPayload = JsonSerializer.Serialize(expectedDto);
        var chatMessage = new ChatMessage(ChatRole.Assistant, jsonPayload);
        var chatResponse = new ChatResponse(chatMessage);

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var initialState = new TestState { UserQuery = "what's 2+2?" };

        var applyResultInvocations = 0;
        TestDto? capturedResult = null;

        var configuration = new AgentStepConfiguration<TestState, TestDto>(
            SystemPrompt: _ => "You are a math assistant. Respond as JSON {\"answer\":string, \"confidence\":number}.",
            UserPrompt: state => state.UserQuery,
            ApplyResult: (state, result, ct) =>
            {
                applyResultInvocations++;
                capturedResult = result;
                return Task.FromResult(new StepResult<TestState>(state with { Answer = result.Answer }));
            },
            Tools: Array.Empty<AIFunction>(),
            McpToolSource: null,
            ChatOptions: null,
            ChatClientConfigurator: null,
            MaxToolIterations: null);

        var orchestrator = new AgentStepBase<TestState, TestDto>(chatClient, configuration);
        var context = new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = initialState.WorkflowId,
            StepName = "TestStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // Act
        var result = await orchestrator.ExecuteAsync(initialState, context, CancellationToken.None);

        // Assert
        await Assert.That(applyResultInvocations).IsEqualTo(1);
        await Assert.That(capturedResult).IsNotNull();
        await Assert.That(capturedResult!.Answer).IsEqualTo("four");
        await Assert.That(result.UpdatedState.Answer).IsEqualTo("four");
    }

    [Test]
    public async Task ExecuteAsync_TryGetResultFalse_ThrowsAgentStructuredOutputExceptionWithAGAG002()
    {
        // Arrange — fake IChatClient.GetResponseAsync (untyped) returns a ChatResponse whose
        // .Text is malformed JSON for TestDto. The MEAI typed extension
        // ChatClientStructuredOutputExtensions.GetResponseAsync<T> wraps that into a
        // ChatResponse<TestDto>; its TryGetResult will return false because deserialization
        // of "{bad}" against TestDto fails (DR-3, DR-10 no-silent-fallback path).
        const string rawText = "{bad}";
        var chatMessage = new ChatMessage(ChatRole.Assistant, rawText);
        var chatResponse = new ChatResponse(chatMessage);

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(chatResponse);

        var initialState = new TestState { UserQuery = "what's 2+2?" };

        var applyResult = Substitute.For<Func<TestState, TestDto, CancellationToken, Task<StepResult<TestState>>>>();
        applyResult
            .Invoke(Arg.Any<TestState>(), Arg.Any<TestDto>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new StepResult<TestState>(ci.Arg<TestState>())));

        var configuration = new AgentStepConfiguration<TestState, TestDto>(
            SystemPrompt: _ => "You are a math assistant. Respond as JSON {\"answer\":string, \"confidence\":number}.",
            UserPrompt: state => state.UserQuery,
            ApplyResult: applyResult,
            Tools: Array.Empty<AIFunction>(),
            McpToolSource: null,
            ChatOptions: null,
            ChatClientConfigurator: null,
            MaxToolIterations: null);

        var orchestrator = new AgentStepBase<TestState, TestDto>(chatClient, configuration);
        var context = new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = initialState.WorkflowId,
            StepName = "TestStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // Act + Assert
        var thrown = await Assert
            .That(async () => await orchestrator.ExecuteAsync(initialState, context, CancellationToken.None))
            .Throws<AgentStructuredOutputException>();

        await Assert.That(thrown!.Diagnostic).IsEqualTo("AGAG002");
        await Assert.That(thrown.RawPayload).IsNotNull();
        await Assert.That(thrown.RawPayload!.Length).IsLessThanOrEqualTo(4096);

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

        public string Answer { get; init; } = string.Empty;
    }

    internal sealed record TestDto(string Answer, double Confidence);
}
