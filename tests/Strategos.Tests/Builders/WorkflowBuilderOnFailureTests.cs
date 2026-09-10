// =============================================================================
// <copyright file="WorkflowBuilderOnFailureTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="WorkflowBuilder{TState}.OnFailure"/> DSL method.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>OnFailure returns builder for fluent chaining</description></item>
///   <item><description>OnFailure can only be called once</description></item>
///   <item><description>Failure handler steps are included in workflow</description></item>
///   <item><description>Guard clauses validate inputs</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowBuilderOnFailureTests
{
    // =============================================================================
    // A. OnFailure Basic Tests
    // =============================================================================

    /// <summary>
    /// Verifies that OnFailure returns the builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task OnFailure_WithHandler_ReturnsBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act
        var result = builder.OnFailure(f => f.Then<LogFailureStep>());

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that OnFailure called twice throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task OnFailure_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f.Then<LogFailureStep>());

        // Act & Assert
        await Assert.That(() => builder.OnFailure(f => f.Then<NotifyAdminStep>()))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that OnFailure with null handler throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task OnFailure_WithNullHandler_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.OnFailure(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that OnFailure before StartWith throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task OnFailure_BeforeStartWith_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.OnFailure(f => f.Then<LogFailureStep>()))
            .Throws<InvalidOperationException>();
    }

    // =============================================================================
    // B. Workflow Definition Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Finally includes failure handler in definition.
    /// </summary>
    [Test]
    public async Task Finally_WithOnFailure_IncludesFailureHandler()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f
                .Then<LogFailureStep>()
                .Complete())
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.FailureHandlers).HasCount().EqualTo(1);
    }

    /// <summary>
    /// Verifies that failure handler has workflow scope.
    /// </summary>
    [Test]
    public async Task Finally_WithOnFailure_HasWorkflowScope()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f.Then<LogFailureStep>().Complete())
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.FailureHandlers[0].Scope).IsEqualTo(FailureHandlerScope.Workflow);
    }

    /// <summary>
    /// Verifies that failure handler steps are stored correctly.
    /// </summary>
    [Test]
    public async Task Finally_WithOnFailure_StoresHandlerSteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f
                .Then<LogFailureStep>()
                .Then<NotifyAdminStep>()
                .Complete())
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.FailureHandlers[0].Steps).HasCount().EqualTo(2);
        await Assert.That(workflow.FailureHandlers[0].Steps[0].StepType).IsEqualTo(typeof(LogFailureStep));
        await Assert.That(workflow.FailureHandlers[0].Steps[1].StepType).IsEqualTo(typeof(NotifyAdminStep));
    }

    /// <summary>
    /// Verifies that failure handler IsTerminal is set correctly.
    /// </summary>
    [Test]
    public async Task Finally_WithTerminalOnFailure_SetsIsTerminal()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f.Then<LogFailureStep>().Complete())
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.FailureHandlers[0].IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that non-terminal failure handler has IsTerminal false.
    /// </summary>
    [Test]
    public async Task Finally_WithNonTerminalOnFailure_HasIsTerminalFalse()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f.Then<LogFailureStep>())
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.FailureHandlers[0].IsTerminal).IsFalse();
    }

    /// <summary>
    /// Verifies that failure handler steps are included in main steps collection.
    /// </summary>
    [Test]
    public async Task Finally_WithOnFailure_IncludesStepsInMainCollection()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f.Then<LogFailureStep>())
            .Finally<CompleteStep>();

        // Assert - 3 steps: Validate, LogFailure, Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(3);
    }

    // =============================================================================
    // C. Fluent Chaining Tests
    // =============================================================================

    /// <summary>
    /// Verifies that OnFailure can be chained with Then.
    /// </summary>
    [Test]
    public async Task OnFailure_ChainedWithThen_Works()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .OnFailure(f => f.Then<LogFailureStep>().Complete())
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Steps).HasCount().EqualTo(4);
        await Assert.That(workflow.FailureHandlers).HasCount().EqualTo(1);
    }

    /// <summary>
    /// Verifies that OnFailure can be placed after Branch.
    /// </summary>
    [Test]
    public async Task OnFailure_AfterBranch_Works()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Branch(
                state => state.ProcessingMode,
                BranchCase<TestWorkflowState, ProcessingMode>.When(
                    ProcessingMode.Auto,
                    path => path.Then<AutoProcessStep>()))
            .OnFailure(f => f.Then<LogFailureStep>().Complete())
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.FailureHandlers).HasCount().EqualTo(1);
    }
}
