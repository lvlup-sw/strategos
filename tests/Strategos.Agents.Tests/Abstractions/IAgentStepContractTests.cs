// =============================================================================
// <copyright file="IAgentStepContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Strategos.Abstractions;
using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Abstractions;

[Property("Category", "Unit")]
public sealed class IAgentStepContractTests
{
    [Test]
    public async Task IAgentStep_GenericContract_ExtendsIWorkflowStepWithTwoTypeParameters()
    {
        var openGeneric = typeof(IAgentStep<,>);
        await Assert.That(openGeneric.IsInterface).IsTrue();
        await Assert.That(openGeneric.GetGenericArguments().Length).IsEqualTo(2);

        var tState = openGeneric.GetGenericArguments()[0];
        var tStateConstraints = tState.GetGenericParameterConstraints();
        await Assert.That(tStateConstraints.Any(c => c == typeof(IWorkflowState))).IsTrue();
        await Assert.That((tState.GenericParameterAttributes & System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint) != 0).IsTrue();

        // Extends IWorkflowStep<TState> (open generic)
        var implemented = openGeneric.GetInterfaces();
        await Assert.That(implemented.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IWorkflowStep<>))).IsTrue();

        // No surface methods on the new interface (it is a pure refinement)
        var declaredMethods = openGeneric.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        await Assert.That(declaredMethods.Length).IsEqualTo(0);
    }
}
