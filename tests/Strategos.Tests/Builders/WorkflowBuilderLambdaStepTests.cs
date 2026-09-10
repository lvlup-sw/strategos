// =============================================================================
// <copyright file="WorkflowBuilderLambdaStepTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Steps;
using Strategos.Tests.Fixtures;

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for lambda step support in <see cref="WorkflowBuilder{TState}"/>.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Then with lambda delegate works correctly</description></item>
///   <item><description>Lambda steps are included in workflow definition</description></item>
///   <item><description>Lambda steps can be named explicitly</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowBuilderLambdaStepTests
{
    // =============================================================================
    // A. Basic Lambda Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then with lambda returns builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task Then_WithLambda_ReturnsBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act
        var result = builder.Then("ProcessData", (state, context, ct) =>
        {
            return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
        });

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that lambda step is included in workflow steps.
    /// </summary>
    [Test]
    public async Task Finally_WithLambdaStep_IncludesLambdaInSteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then("ProcessData", (state, context, ct) =>
            {
                return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
            })
            .Finally<CompleteStep>();

        // Assert - 3 steps: Validate, ProcessData (lambda), Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(3);
    }

    /// <summary>
    /// Verifies that lambda step has correct step name.
    /// </summary>
    [Test]
    public async Task Finally_WithLambdaStep_HasCorrectStepName()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then("ProcessData", (state, context, ct) =>
            {
                return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
            })
            .Finally<CompleteStep>();

        // Assert - second step (index 1) should be the lambda
        await Assert.That(workflow.Steps[1].StepName).IsEqualTo("ProcessData");
    }

    /// <summary>
    /// Verifies that lambda step is marked as lambda type.
    /// </summary>
    [Test]
    public async Task Finally_WithLambdaStep_IsMarkedAsLambda()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then("ProcessData", (state, context, ct) =>
            {
                return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
            })
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(workflow.Steps[1].IsLambdaStep).IsTrue();
    }

    // =============================================================================
    // B. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then with null step name throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Then_WithNullStepName_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.Then(null!, (state, context, ct) =>
        {
            return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
        })).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Then with null lambda throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Then_WithNullLambda_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.Then("ProcessData", (StepDelegate<TestWorkflowState>)null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Then with lambda before StartWith throws InvalidOperationException.
    /// </summary>
    [Test]
    public async Task Then_WithLambdaBeforeStartWith_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.Then("ProcessData", (state, context, ct) =>
        {
            return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
        })).Throws<InvalidOperationException>();
    }

    // =============================================================================
    // C. Multiple Lambda Steps Tests
    // =============================================================================

    /// <summary>
    /// Verifies that multiple lambda steps can be chained.
    /// </summary>
    [Test]
    public async Task Finally_WithMultipleLambdaSteps_IncludesAllSteps()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then("Step1", (state, context, ct) =>
            {
                return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
            })
            .Then("Step2", (state, context, ct) =>
            {
                return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
            })
            .Finally<CompleteStep>();

        // Assert - 4 steps: Validate, Step1, Step2, Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(4);
        await Assert.That(workflow.Steps[1].StepName).IsEqualTo("Step1");
        await Assert.That(workflow.Steps[2].StepName).IsEqualTo("Step2");
    }

    /// <summary>
    /// Verifies that lambda steps can be mixed with class-based steps.
    /// </summary>
    [Test]
    public async Task Finally_WithMixedSteps_WorksCorrectly()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Then("CustomLogic", (state, context, ct) =>
            {
                return Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
            })
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert - 5 steps: Validate, Process, CustomLogic, Process, Complete
        await Assert.That(workflow.Steps).HasCount().EqualTo(5);
        await Assert.That(workflow.Steps[0].IsLambdaStep).IsFalse();
        await Assert.That(workflow.Steps[2].IsLambdaStep).IsTrue();
        await Assert.That(workflow.Steps[2].StepName).IsEqualTo("CustomLogic");
    }
}
