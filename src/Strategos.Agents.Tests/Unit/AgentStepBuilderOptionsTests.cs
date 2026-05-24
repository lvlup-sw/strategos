// =============================================================================
// <copyright file="AgentStepBuilderOptionsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using NSubstitute;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Tests.Fixtures;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// T-014: optional <c>AgentStepBuilder</c> setters — <c>WithChatOptions</c> (DR-2),
/// <c>WithToolSource</c> (DR-9), and <c>WithMaxToolIterations</c> (DR-8).
/// </summary>
[Property("Category", "Unit")]
public sealed class AgentStepBuilderOptionsTests
{
    [Test]
    public async Task WithChatOptions_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithChatOptions(new ChatOptions());

        var ex = Assert.Throws<InvalidOperationException>(() => builder.WithChatOptions(new ChatOptions()));

        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task WithToolSource_StoresPortInConfiguration()
    {
        var port = new InProcessTestToolSource(Array.Empty<AIFunction>());

        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((state, _, _) => Task.FromResult(new StepResult<TestState>(state)));
        builder.WithToolSource(port);

        var step = (AgentStepBase<TestState, string>)builder.Build(FakeChatClient());
        var configuration = step.Configuration;

        await Assert.That(configuration.ToolSources.Count).IsEqualTo(1);
        await Assert.That(configuration.ToolSources[0]).IsSameReferenceAs((IToolSource)port);
    }

    [Test]
    public async Task WithMaxToolIterations_Zero_ThrowsArgumentOutOfRangeException()
    {
        var builder = new AgentStepBuilder<TestState, string>();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxToolIterations(0));

        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task WithMaxToolIterations_NegativeNumber_ThrowsArgumentOutOfRangeException()
    {
        var builder = new AgentStepBuilder<TestState, string>();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithMaxToolIterations(-1));

        await Assert.That(ex).IsNotNull();
    }

    private static IChatClient FakeChatClient()
        => Substitute.For<IChatClient>();

    private sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }
}
