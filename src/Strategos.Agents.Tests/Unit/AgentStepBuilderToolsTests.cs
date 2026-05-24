// =============================================================================
// <copyright file="AgentStepBuilderToolsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Exceptions;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// T-013: <c>AgentStepBuilder.WithTool(AIFunction)</c> accumulates tools, with
/// duplicate-name collision detection deferred until <c>Build()</c> (DR-4, DR-10).
/// </summary>
[Property("Category", "Unit")]
public sealed class AgentStepBuilderToolsTests
{
    [Test]
    public async Task WithTool_MultipleDistinctTools_AccumulatesAllInConfiguration()
    {
        AIFunction toolA = AIFunctionFactory.Create(() => "result-a", name: "tool_a");
        AIFunction toolB = AIFunctionFactory.Create(() => "result-b", name: "tool_b");

        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((state, _, _) => Task.FromResult(new StepResult<TestState>(state)));
        builder.WithTool(toolA);
        builder.WithTool(toolB);

        var step = (AgentStepBase<TestState, string>)builder.Build(FakeChatClient());

        var configuration = step.Configuration;

        await Assert.That(configuration.Tools.Count).IsEqualTo(2);
        await Assert.That(configuration.Tools).Contains(toolA);
        await Assert.That(configuration.Tools).Contains(toolB);
    }

    [Test]
    public async Task WithTool_DuplicateToolName_ThrowsAgentDuplicateToolExceptionWithAGAG003AtBuildTime()
    {
        AIFunction first = AIFunctionFactory.Create(() => "result-1", name: "collide");
        AIFunction second = AIFunctionFactory.Create(() => "result-2", name: "collide");

        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((state, _, _) => Task.FromResult(new StepResult<TestState>(state)));

        // Critical: the second WithTool() must NOT throw — collision detection is deferred to Build().
        builder.WithTool(first);
        builder.WithTool(second);

        var ex = Assert.Throws<AgentDuplicateToolException>(() => builder.Build(FakeChatClient()));

        await Assert.That(ex.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG003);
        await Assert.That(ex.ToolName).IsEqualTo("collide");
    }

    private static IChatClient FakeChatClient()
        => NSubstitute.Substitute.For<IChatClient>();

    private sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }
}
