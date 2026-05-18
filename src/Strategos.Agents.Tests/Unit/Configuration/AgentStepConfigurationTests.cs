// =============================================================================
// <copyright file="AgentStepConfigurationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Unit.Configuration;

[Property("Category", "Unit")]
public sealed class AgentStepConfigurationTests
{
    [Test]
    public async Task AgentStepConfiguration_SealedRecord_CarriesAllHookDelegates()
    {
        var openGeneric = typeof(AgentStepConfiguration<,>);
        await Assert.That(openGeneric.IsSealed).IsTrue();

        // record check — records have a generated `EqualityContract` property
        var equalityContract = openGeneric.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        await Assert.That(equalityContract).IsNotNull();

        // Closed instance test
        Func<DummyState, string> systemPrompt = _ => "sys";
        Func<DummyState, string> userPrompt = _ => "user";
        Func<DummyState, DummyResult, CancellationToken, Task<StepResult<DummyState>>> apply =
            (s, _, _) => Task.FromResult(new StepResult<DummyState>(s));

        var config = new AgentStepConfiguration<DummyState, DummyResult>(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            ApplyResult: apply,
            Tools: Array.Empty<AIFunction>(),
            McpToolSource: null,
            ChatOptions: null,
            ChatClientConfigurator: null,
            MaxToolIterations: null);

        await Assert.That(config.SystemPrompt).IsEqualTo(systemPrompt);
        await Assert.That(config.UserPrompt).IsEqualTo(userPrompt);
        await Assert.That(config.ApplyResult).IsEqualTo(apply);
        await Assert.That(config.Tools.Count).IsEqualTo(0);
        await Assert.That(config.McpToolSource).IsNull();
        await Assert.That(config.ChatOptions).IsNull();
        await Assert.That(config.ChatClientConfigurator).IsNull();
        await Assert.That(config.MaxToolIterations).IsNull();

        // Constructor accessibility — internal (the builder is the only sanctioned creator)
        var ctors = openGeneric.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(ctors.Length).IsEqualTo(0); // no public constructors
        var internalCtors = openGeneric.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(internalCtors.Length).IsGreaterThan(0);
    }

    private sealed record DummyState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }

    private sealed record DummyResult(string Value);
}
