// =============================================================================
// <copyright file="AgentStepBuilderReturnTypeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;
using NSubstitute;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Unit;

/// <summary>
/// T-016: Tighten the DR-2 builder surface — <see cref="AgentStepBuilder{TState, TResult}.Build"/>
/// declares its return type as the abstraction <see cref="IAgentStep{TState, TResult}"/>, not
/// the concrete <see cref="AgentStepBase{TState, TResult}"/>. The builder also has exactly one
/// public, parameterless constructor.
/// </summary>
[Property("Category", "Unit")]
public sealed class AgentStepBuilderReturnTypeTests
{
    [Test]
    public async Task Build_ReturnType_IsIAgentStepInterfaceNotAgentStepBaseConcrete()
    {
        // (1) Reflection: declared return type of Build is IAgentStep<,>, not AgentStepBase<,>.
        var builderOpen = typeof(AgentStepBuilder<,>);
        var buildMethod = builderOpen.GetMethod("Build")
            ?? throw new InvalidOperationException("Build method not found on AgentStepBuilder<,>.");

        var returnType = buildMethod.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(IAgentStep<,>));
        await Assert.That(returnType.GetGenericTypeDefinition()).IsNotEqualTo(typeof(AgentStepBase<,>));

        // (2) Closed-form: construct a fully-configured builder and verify the binding still
        //     produces an AgentStepBase<,> (the concrete type) even though the surface is the interface.
        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((state, _, _) => Task.FromResult(new StepResult<TestState>(state)));

        var built = builder.Build(Substitute.For<IChatClient>());

        // (a) Assignable to the abstraction.
        await Assert.That(built).IsAssignableTo<IAgentStep<TestState, string>>();
        // (b) Concrete runtime type is still AgentStepBase<,> — the binding is intact.
        await Assert.That(built.GetType()).IsEqualTo(typeof(AgentStepBase<TestState, string>));
    }

    [Test]
    public async Task AgentStepBuilder_HasOnlyParameterlessConstructor()
    {
        var publicCtors = typeof(AgentStepBuilder<,>).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(publicCtors.Length).IsEqualTo(1);
        await Assert.That(publicCtors[0].GetParameters().Length).IsEqualTo(0);
    }

    private sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }
}
