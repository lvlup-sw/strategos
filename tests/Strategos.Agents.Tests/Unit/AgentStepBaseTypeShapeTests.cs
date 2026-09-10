// =============================================================================
// <copyright file="AgentStepBaseTypeShapeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;

namespace Strategos.Agents.Tests.Unit;

[Property("Category", "Unit")]
public sealed class AgentStepBaseTypeShapeTests
{
    [Test]
    public async Task AgentStepBase_TypeShape_IsSealedAndImplementsGenericInterface()
    {
        // Resolve the new 2-arity orchestrator (NOT the old single-arity AgentStepBase<TState>)
        var openGeneric = typeof(Strategos.Agents.AgentStepBase<,>);
        await Assert.That(openGeneric.IsSealed).IsTrue();
        await Assert.That(openGeneric.GetGenericArguments().Length).IsEqualTo(2);

        // Implements IAgentStep<TState, TResult>
        var implemented = openGeneric.GetInterfaces();
        await Assert.That(implemented.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAgentStep<,>))).IsTrue();

        // Constructor is internal — only the builder may construct
        var publicCtors = openGeneric.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(publicCtors.Length).IsEqualTo(0);
        var internalCtors = openGeneric.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(internalCtors.Length).IsGreaterThan(0);

        // Constructor signature: (IChatClient, AgentStepConfiguration<TState, TResult>)
        var ctor = internalCtors[0];
        var parameters = ctor.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(IChatClient));
        var configType = parameters[1].ParameterType;
        await Assert.That(configType.IsGenericType).IsTrue();
        await Assert.That(configType.GetGenericTypeDefinition()).IsEqualTo(typeof(AgentStepConfiguration<,>));
    }
}
