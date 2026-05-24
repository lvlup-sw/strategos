// =============================================================================
// <copyright file="AgentStepBaseStreamingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Agents.Exceptions;
using Strategos.Agents.Tests.Fixtures;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Integration;

/// <summary>
/// T-014/T-015/T-016 acceptance tests for the streaming branch of
/// <see cref="AgentStepBase{TState, TResult}"/> (DR-1, DR-3, DR-4, DR-11).
/// Streaming is an observability layer over the terminal typed contract: the
/// streaming path funnels into the SAME FinishReason / TryGetResult / empty
/// checks as the buffered path and applies the typed result exactly once.
/// </summary>
public sealed class AgentStepBaseStreamingTests
{
    [Test]
    public async Task Execute_StreamingConfigured_UsesStreamingNotBuffered()
    {
        var payload = JsonSerializer.Serialize(new TestDto("four", 0.9));
        var chatClient = new RecordingChatClient(
            streamingUpdates: ToUpdates(payload),
            throwOnBuffered: true);
        var handler = new RecordingStreamingHandler();
        var orchestrator = BuildOrchestrator(chatClient, handler, out var state, out var context);

        _ = await orchestrator.ExecuteAsync(state, context, CancellationToken.None);

        await Assert.That(chatClient.StreamingInvoked).IsTrue();
        await Assert.That(chatClient.BufferedInvoked).IsFalse();
    }

    [Test]
    public async Task Execute_StreamingMalformedPayload_ThrowsAGAG002()
    {
        var chatClient = new RecordingChatClient(
            streamingUpdates: ToUpdates("{bad}"),
            throwOnBuffered: true);
        var handler = new RecordingStreamingHandler();
        var orchestrator = BuildOrchestrator(chatClient, handler, out var state, out var context);

        var thrown = await Assert
            .That(async () => await orchestrator.ExecuteAsync(state, context, CancellationToken.None))
            .Throws<AgentStructuredOutputException>();

        await Assert.That(thrown!.Diagnostic).IsEqualTo("AGAG002");
    }

    [Test]
    public async Task Execute_StreamingValidPayload_AppliesTypedResultOnce()
    {
        var payload = JsonSerializer.Serialize(new TestDto("four", 0.9));
        var chatClient = new RecordingChatClient(
            streamingUpdates: ToUpdates(payload),
            throwOnBuffered: true);
        var handler = new RecordingStreamingHandler();
        var applyCount = 0;
        TestDto? captured = null;

        var configuration = new AgentStepConfiguration<TestState, TestDto>(
            SystemPrompt: _ => "sys",
            UserPrompt: s => s.UserQuery,
            ApplyResult: (s, result, _) =>
            {
                applyCount++;
                captured = result;
                return Task.FromResult(new StepResult<TestState>(s with { Answer = result.Answer }));
            },
            Tools: Array.Empty<AIFunction>(),
            ToolSources: Array.Empty<IToolSource>(),
            ChatOptions: null,
            MaxToolIterations: null,
            StreamingHandler: handler);

        var state = new TestState { UserQuery = "what's 2+2?" };
        var context = MakeContext(state);
        var orchestrator = new AgentStepBase<TestState, TestDto>(chatClient, configuration);

        var result = await orchestrator.ExecuteAsync(state, context, CancellationToken.None);

        await Assert.That(applyCount).IsEqualTo(1);
        await Assert.That(captured!.Answer).IsEqualTo("four");
        await Assert.That(result.UpdatedState.Answer).IsEqualTo("four");
    }

    [Test]
    public async Task Streaming_Tokens_DeliveredInOrderThenCompletion()
    {
        // Three text chunks that concatenate into valid JSON for TestDto.
        var updates = new[]
        {
            new ChatResponseUpdate(ChatRole.Assistant, "{\"answer\":\"fo"),
            new ChatResponseUpdate(ChatRole.Assistant, "ur\",\"confi"),
            new ChatResponseUpdate(ChatRole.Assistant, "dence\":0.9}"),
        };
        var chatClient = new RecordingChatClient(updates, throwOnBuffered: true);
        var handler = new RecordingStreamingHandler();
        var orchestrator = BuildOrchestrator(chatClient, handler, out var state, out var context);

        _ = await orchestrator.ExecuteAsync(state, context, CancellationToken.None);

        // Tokens delivered in order, then exactly one completion last.
        await Assert.That(handler.Tokens.Count).IsEqualTo(3);
        await Assert.That(handler.Tokens[0]).IsEqualTo("{\"answer\":\"fo");
        await Assert.That(handler.Tokens[1]).IsEqualTo("ur\",\"confi");
        await Assert.That(handler.Tokens[2]).IsEqualTo("dence\":0.9}");
        await Assert.That(handler.CompletionCount).IsEqualTo(1);
        await Assert.That(handler.CompletedResponse).IsEqualTo("{\"answer\":\"four\",\"confidence\":0.9}");
        await Assert.That(handler.CallOrder[^1]).StartsWith("complete:");
    }

    [Test]
    public async Task Streaming_WorkflowAndStep_SourcedFromContext()
    {
        var payload = JsonSerializer.Serialize(new TestDto("four", 0.9));
        var chatClient = new RecordingChatClient(ToUpdates(payload), throwOnBuffered: true);
        var handler = new RecordingStreamingHandler();
        var orchestrator = BuildOrchestrator(chatClient, handler, out var state, out var context);

        _ = await orchestrator.ExecuteAsync(state, context, CancellationToken.None);

        await Assert.That(handler.ObservedWorkflowId).IsEqualTo(context.WorkflowId);
        await Assert.That(handler.ObservedStepName).IsEqualTo(context.StepName);
    }

    [Test]
    public async Task Streaming_StreamCancelled_PropagatesUnwrapped()
    {
        var chatClient = new ThrowingStreamingChatClient(() => throw new OperationCanceledException("test-cancel"));
        var handler = new RecordingStreamingHandler();
        var orchestrator = BuildOrchestrator(chatClient, handler, out var state, out var context);

        var thrown = await Assert
            .That(async () => await orchestrator.ExecuteAsync(state, context, CancellationToken.None))
            .Throws<OperationCanceledException>();

        Exception asException = thrown!;
        await Assert.That(asException is AgentException).IsFalse();
    }

    [Test]
    public async Task Streaming_HandlerThrows_RaisesAGAG009StatePreserved()
    {
        var payload = JsonSerializer.Serialize(new TestDto("four", 0.9));
        var chatClient = new RecordingChatClient(ToUpdates(payload), throwOnBuffered: true);
        var handler = new ThrowingStreamingHandler();

        var configuration = new AgentStepConfiguration<TestState, TestDto>(
            SystemPrompt: _ => "sys",
            UserPrompt: s => s.UserQuery,
            ApplyResult: (s, _, _) => Task.FromResult(new StepResult<TestState>(s)),
            Tools: Array.Empty<AIFunction>(),
            ToolSources: Array.Empty<IToolSource>(),
            ChatOptions: null,
            MaxToolIterations: null,
            StreamingHandler: handler);

        var state = new TestState { UserQuery = "q" };
        var context = MakeContext(state);
        var orchestrator = new AgentStepBase<TestState, TestDto>(chatClient, configuration);

        var thrown = await Assert
            .That(async () => await orchestrator.ExecuteAsync(state, context, CancellationToken.None))
            .Throws<AgentStreamingException>();

        await Assert.That(thrown!.Diagnostic).IsEqualTo("AGAG009");

        // DR-4: input TState reference is preserved (apply-result never ran).
        await Assert.That(state.UserQuery).IsEqualTo("q");
        await Assert.That(state.Answer).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Streaming_ZeroUpdates_ThrowsAGAG006()
    {
        var chatClient = new RecordingChatClient(Array.Empty<ChatResponseUpdate>(), throwOnBuffered: true);
        var handler = new RecordingStreamingHandler();
        var orchestrator = BuildOrchestrator(chatClient, handler, out var state, out var context);

        var thrown = await Assert
            .That(async () => await orchestrator.ExecuteAsync(state, context, CancellationToken.None))
            .Throws<AgentChatResponseException>();

        await Assert.That(thrown!.Diagnostic).IsEqualTo("AGAG006");
    }

    private static AgentStepBase<TestState, TestDto> BuildOrchestrator(
        IChatClient chatClient,
        IStreamingHandler handler,
        out TestState state,
        out StepContext context)
    {
        var configuration = new AgentStepConfiguration<TestState, TestDto>(
            SystemPrompt: _ => "sys",
            UserPrompt: s => s.UserQuery,
            ApplyResult: (s, result, _) => Task.FromResult(new StepResult<TestState>(s with { Answer = result.Answer })),
            Tools: Array.Empty<AIFunction>(),
            ToolSources: Array.Empty<IToolSource>(),
            ChatOptions: null,
            MaxToolIterations: null,
            StreamingHandler: handler);

        state = new TestState { UserQuery = "what's 2+2?" };
        context = MakeContext(state);
        return new AgentStepBase<TestState, TestDto>(chatClient, configuration);
    }

    private static StepContext MakeContext(TestState state) => new()
    {
        CorrelationId = Guid.NewGuid().ToString("N"),
        WorkflowId = state.WorkflowId,
        StepName = "StreamingStep",
        Timestamp = DateTimeOffset.UtcNow,
        CurrentPhase = "Testing",
    };

    private static ChatResponseUpdate[] ToUpdates(string fullText)
        => new[] { new ChatResponseUpdate(ChatRole.Assistant, fullText) };

    internal sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();

        public string UserQuery { get; init; } = string.Empty;

        public string Answer { get; init; } = string.Empty;
    }

    internal sealed record TestDto(string Answer, double Confidence);
}
