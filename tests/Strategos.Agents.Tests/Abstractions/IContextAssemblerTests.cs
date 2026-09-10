// =============================================================================
// <copyright file="IContextAssemblerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Abstractions;

/// <summary>
/// Unit tests for the <see cref="IContextAssembler{TState}"/> interface.
/// </summary>
[Property("Category", "Unit")]
public class IContextAssemblerTests
{
    /// <summary>
    /// Verifies that the interface exists with the correct signature.
    /// </summary>
    [Test]
    public async Task IContextAssembler_Interface_ExistsWithCorrectSignature()
    {
        // Arrange
        var interfaceType = typeof(IContextAssembler<>);

        // Act
        var methods = interfaceType.GetMethods();
        var assembleMethod = methods.FirstOrDefault(m => m.Name == "AssembleAsync");

        // Assert
        await Assert.That(interfaceType.IsInterface).IsTrue();
        await Assert.That(interfaceType.IsGenericTypeDefinition).IsTrue();
        await Assert.That(assembleMethod).IsNotNull();

        // Verify method parameters
        var parameters = assembleMethod!.GetParameters();
        await Assert.That(parameters).HasCount(3);
        await Assert.That(parameters[0].Name).IsEqualTo("state");
        await Assert.That(parameters[1].Name).IsEqualTo("stepContext");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(StepContext));
        await Assert.That(parameters[2].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[2].ParameterType).IsEqualTo(typeof(CancellationToken));

        // Verify return type
        var returnType = assembleMethod.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(Task<>));
        await Assert.That(returnType.GetGenericArguments()[0]).IsEqualTo(typeof(AssembledContext));
    }

    /// <summary>
    /// Verifies that the interface has the correct generic constraint.
    /// </summary>
    [Test]
    public async Task IContextAssembler_GenericConstraint_RequiresWorkflowState()
    {
        // Arrange
        var interfaceType = typeof(IContextAssembler<>);
        var genericParameter = interfaceType.GetGenericArguments()[0];
        var constraints = genericParameter.GetGenericParameterConstraints();

        // Assert
        await Assert.That(constraints).Contains(typeof(IWorkflowState));
    }
}
