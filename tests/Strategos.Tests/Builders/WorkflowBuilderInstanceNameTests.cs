// =============================================================================
// <copyright file="WorkflowBuilderInstanceNameTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// TDD Cycle 5: Tests for instance name support in workflow builder DSL.
/// Instance names enable reusing the same step type in fork/branch paths
/// with distinct identities for phase tracking and duplicate detection.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowBuilderInstanceNameTests
{
    // =============================================================================
    // A. Then Instance Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Then with instance name stores the name in the step definition.
    /// </summary>
    [Test]
    public async Task Then_WithInstanceName_StoresNameInDefinition()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>("CustomProcessing")
            .Finally<CompleteStep>();

        // Assert
        var processStep = definition.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.InstanceName).IsEqualTo("CustomProcessing");
    }

    /// <summary>
    /// Verifies that Then without instance name leaves InstanceName as null.
    /// </summary>
    [Test]
    public async Task Then_WithoutInstanceName_InstanceNameIsNull()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert
        var processStep = definition.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.InstanceName).IsNull();
    }

    /// <summary>
    /// Verifies that Then with instance name returns builder for fluent chaining.
    /// </summary>
    [Test]
    public async Task Then_WithInstanceName_ReturnsBuilder()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act
        var result = builder.Then<ProcessStep>("Named");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<IWorkflowBuilder<TestWorkflowState>>();
    }

    /// <summary>
    /// Verifies that multiple Then calls can use different instance names.
    /// </summary>
    [Test]
    public async Task Then_MultipleInstanceNames_StoresDistinctNames()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>("FirstProcess")
            .Then<NotifyStep>("NotifyAdmin")
            .Finally<CompleteStep>();

        // Assert
        var processStep = definition.Steps.First(s => s.StepType == typeof(ProcessStep));
        var notifyStep = definition.Steps.First(s => s.StepType == typeof(NotifyStep));

        await Assert.That(processStep.InstanceName).IsEqualTo("FirstProcess");
        await Assert.That(notifyStep.InstanceName).IsEqualTo("NotifyAdmin");
    }

    // =============================================================================
    // B. StartWith Instance Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StartWith with instance name stores the name in the step definition.
    /// </summary>
    [Test]
    public async Task StartWith_WithInstanceName_StoresNameInDefinition()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>("InitialValidation")
            .Finally<CompleteStep>();

        // Assert
        var validateStep = definition.Steps.First(s => s.StepType == typeof(ValidateStep));
        await Assert.That(validateStep.InstanceName).IsEqualTo("InitialValidation");
    }

    /// <summary>
    /// Verifies that StartWith without instance name leaves InstanceName as null.
    /// </summary>
    [Test]
    public async Task StartWith_WithoutInstanceName_InstanceNameIsNull()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        // Assert
        var validateStep = definition.Steps.First(s => s.StepType == typeof(ValidateStep));
        await Assert.That(validateStep.InstanceName).IsNull();
    }

    // =============================================================================
    // C. StepDefinition.Create Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepDefinition.Create with instance name sets the property.
    /// </summary>
    [Test]
    public async Task StepDefinitionCreate_WithInstanceName_SetsProperty()
    {
        // Arrange & Act
        var stepDef = StepDefinition.Create(typeof(ValidateStep), instanceName: "CustomInstance");

        // Assert
        await Assert.That(stepDef.InstanceName).IsEqualTo("CustomInstance");
        await Assert.That(stepDef.StepName).IsEqualTo("Validate"); // Still derives from type
    }

    /// <summary>
    /// Verifies that StepDefinition.Create without instance name leaves it null.
    /// </summary>
    [Test]
    public async Task StepDefinitionCreate_WithoutInstanceName_PropertyIsNull()
    {
        // Arrange & Act
        var stepDef = StepDefinition.Create(typeof(ValidateStep));

        // Assert
        await Assert.That(stepDef.InstanceName).IsNull();
    }
}
