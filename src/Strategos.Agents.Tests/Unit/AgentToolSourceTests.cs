// =============================================================================
// <copyright file="AgentToolSourceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.ComponentModel;
using Microsoft.Extensions.AI;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// T-007 / T-008 (DR-8): <see cref="AgentToolSource"/> is the in-process reflection
/// adapter for <see cref="IToolSource"/>. <c>FromObject</c> reflects
/// <c>[AgentTool]</c>-annotated instance methods into <see cref="AIFunction"/>s;
/// <c>FromDelegates</c> wraps explicit delegates. The adapter carries NO
/// ModelContextProtocol dependency and surfaces resolution failures as
/// <see cref="AgentToolSourceException"/> (AGAG007).
/// </summary>
[Property("Category", "Unit")]
public sealed class AgentToolSourceTests
{
    [Test]
    public async Task FromObject_AnnotatedMethods_YieldOneAIFunctionEach()
    {
        var source = AgentToolSource.FromObject(new AnnotatedToolHost());

        var tools = await source.GetToolsAsync(CancellationToken.None);

        await Assert.That(tools.Count).IsEqualTo(2);

        var names = tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        // One uses the method name; the other uses the [AgentTool(Name=...)] override.
        await Assert.That(names).Contains("Add");
        await Assert.That(names).Contains("renamed_multiply");
    }

    [Test]
    public async Task FromObject_MethodDescription_FlowsToAIFunction()
    {
        var source = AgentToolSource.FromObject(new AnnotatedToolHost());

        var tools = await source.GetToolsAsync(CancellationToken.None);

        var add = tools.Single(t => t.Name == "Add");
        await Assert.That(add.Description).IsEqualTo("Adds two integers.");
    }

    [Test]
    public async Task FromDelegates_TwoDelegates_YieldTwoTools()
    {
        var source = AgentToolSource.FromDelegates(
            (int a, int b) => a + b,
            (string s) => s.ToUpperInvariant());

        var tools = await source.GetToolsAsync(CancellationToken.None);

        await Assert.That(tools.Count).IsEqualTo(2);
    }

    [Test]
    public async Task FromObject_EmptyType_YieldsEmptyNotNull()
    {
        var source = AgentToolSource.FromObject(new NoToolsHost());

        var tools = await source.GetToolsAsync(CancellationToken.None);

        await Assert.That(tools).IsNotNull();
        await Assert.That(tools.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetToolsAsync_FactoryThrows_RaisesAGAG007NamingSource()
    {
        // An [AgentTool] applied to a generic method definition cannot be turned into
        // an AIFunction; AIFunctionFactory.Create throws. The adapter must wrap that
        // as AgentToolSourceException (AGAG007) naming the offending source type.
        var instance = new BadToolHost();
        var source = AgentToolSource.FromObject(instance);

        var caught = await Assert.ThrowsAsync<AgentToolSourceException>(async () =>
            await source.GetToolsAsync(CancellationToken.None));

        await Assert.That(caught!.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG007);
        await Assert.That(caught.SourceType).IsNotNull();
        await Assert.That(caught.SourceType!.Contains(nameof(BadToolHost), StringComparison.Ordinal)).IsTrue();
    }

    private sealed class AnnotatedToolHost
    {
        [AgentTool]
        [Description("Adds two integers.")]
        public int Add(int a, int b) => a + b;

        [AgentTool(Name = "renamed_multiply")]
        public int Multiply(int a, int b) => a * b;

        // Not annotated — must be ignored.
        public int Subtract(int a, int b) => a - b;
    }

    private sealed class NoToolsHost
    {
        public int NotATool(int a) => a;
    }

    private sealed class BadToolHost
    {
        // Open generic method — AIFunctionFactory cannot bind it, so Create throws.
        [AgentTool]
        public T Echo<T>(T value) => value;
    }
}
