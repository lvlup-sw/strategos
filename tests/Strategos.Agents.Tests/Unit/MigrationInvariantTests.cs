// =============================================================================
// <copyright file="MigrationInvariantTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Linq;
using System.Reflection;
using Strategos.Agents;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// DR-11 migration invariants: reflection guards that prevent the deleted
/// single-arity <c>IAgentStep&lt;TState&gt;</c> / <c>AgentStepBase&lt;TState&gt;</c>
/// types — and the legacy <c>TestAgentStep</c> fixture — from reappearing in
/// the production or test assemblies. If any of these tests starts failing,
/// a contributor has resurrected a contract that DR-11 closed; redirect them
/// to the two-arity <c>IAgentStep&lt;TState, TResult&gt;</c> /
/// <c>AgentStepBase&lt;TState, TResult&gt;</c> contract.
/// </summary>
[Property("Category", "Unit")]
public sealed class MigrationInvariantTests
{
    [Test]
    public async Task Strategos_Agents_Assembly_HasNoSingleArityIAgentStepOrAgentStepBase()
    {
        // Use a post-T-007 type to grab the production assembly without
        // referencing any of the to-be-deleted single-arity types directly.
        var assembly = typeof(Strategos.Agents.AgentStepBase<,>).Assembly;

        var singleArityInterfaces = assembly.GetTypes()
            .Where(t => t.IsGenericTypeDefinition
                && t.Name.StartsWith("IAgentStep", StringComparison.Ordinal)
                && t.GetGenericArguments().Length == 1)
            .ToArray();
        await Assert.That(singleArityInterfaces).IsEmpty();

        var singleArityBases = assembly.GetTypes()
            .Where(t => t.IsGenericTypeDefinition
                && t.Name.StartsWith("AgentStepBase", StringComparison.Ordinal)
                && t.GetGenericArguments().Length == 1)
            .ToArray();
        await Assert.That(singleArityBases).IsEmpty();
    }

    [Test]
    public async Task Strategos_Agents_Tests_Assembly_HasNoTestAgentStepFixture()
    {
        var testAssembly = typeof(MigrationInvariantTests).Assembly;

        var orphanedFixtures = testAssembly.GetTypes()
            .Where(t => t.Name == "TestAgentStep")
            .ToArray();
        await Assert.That(orphanedFixtures).IsEmpty();
    }
}
