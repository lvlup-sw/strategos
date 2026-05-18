// =============================================================================
// <copyright file="AgentStepBuilderValidationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Exceptions;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Unit;

[Property("Category", "Unit")]
public sealed class AgentStepBuilderValidationTests
{
    [Test]
    public async Task Build_WithoutSystemPromptHook_ThrowsAgentBuilderValidationExceptionWithAGAG001AndNamesMissingHook()
    {
        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((state, _, _) => Task.FromResult(new StepResult<TestState>(state)));

        var ex = Assert.Throws<AgentBuilderValidationException>(() => builder.Build(FakeChatClient()));

        await Assert.That(ex.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG001);
        await Assert.That(ex.MissingHook).IsEqualTo("SystemPrompt");
        await Assert.That(ex.Message.Contains("SystemPrompt")).IsTrue();
    }

    [Test]
    public async Task Build_WithoutUserPromptHook_ThrowsAgentBuilderValidationExceptionWithAGAG001AndNamesMissingHook()
    {
        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithApplyResult((state, _, _) => Task.FromResult(new StepResult<TestState>(state)));

        var ex = Assert.Throws<AgentBuilderValidationException>(() => builder.Build(FakeChatClient()));

        await Assert.That(ex.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG001);
        await Assert.That(ex.MissingHook).IsEqualTo("UserPrompt");
        await Assert.That(ex.Message.Contains("UserPrompt")).IsTrue();
    }

    [Test]
    public async Task Build_WithoutApplyResultHook_ThrowsAgentBuilderValidationExceptionWithAGAG001AndNamesMissingHook()
    {
        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");

        var ex = Assert.Throws<AgentBuilderValidationException>(() => builder.Build(FakeChatClient()));

        await Assert.That(ex.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG001);
        await Assert.That(ex.MissingHook).IsEqualTo("ApplyResult");
        await Assert.That(ex.Message.Contains("ApplyResult")).IsTrue();
    }

    private static Microsoft.Extensions.AI.IChatClient FakeChatClient()
        => NSubstitute.Substitute.For<Microsoft.Extensions.AI.IChatClient>();

    private sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }
}
