// =============================================================================
// <copyright file="WorkflowBuilderStepConfigTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Tests for the <c>StartWith</c>/<c>Finally</c> step-config overloads (#141):
/// <c>StartWith&lt;TStep&gt;(configure)</c>, <c>StartWith&lt;TStep&gt;(instanceName, configure)</c>,
/// and <c>Finally&lt;TStep&gt;(configure)</c>. They mirror the existing
/// <c>Then&lt;TStep&gt;(configure)</c> overload, routing the captured configuration through
/// the same <c>WithConfiguration</c> path so the ENTRY (StartWith) and TERMINAL (Finally)
/// steps can declare per-step resilience inline.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowBuilderStepConfigTests
{
    // =============================================================================
    // A. StartWith(configure)
    // =============================================================================

    /// <summary>
    /// Verifies that <c>StartWith&lt;TStep&gt;(step =&gt; step.WithRetry(2))</c> carries the
    /// retry configuration onto the entry step in the built definition.
    /// </summary>
    [Test]
    public async Task StartWith_WithConfigure_StoresRetryOnEntryStep()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>(step => step.WithRetry(2))
            .Finally<CompleteStep>();

        // Assert
        var entryStep = definition.Steps.First(s => s.StepType == typeof(ValidateStep));
        await Assert.That(entryStep.Configuration).IsNotNull();
        await Assert.That(entryStep.Configuration!.Retry).IsNotNull();
        await Assert.That(entryStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that the entry step keeps its entry identity (the built definition's
    /// EntryStep is the configured step), so configuration does not displace it.
    /// </summary>
    [Test]
    public async Task StartWith_WithConfigure_EntryStepIsConfiguredStep()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>(step => step.WithRetry(2))
            .Finally<CompleteStep>();

        // Assert
        await Assert.That(definition.EntryStep).IsNotNull();
        await Assert.That(definition.EntryStep!.StepType).IsEqualTo(typeof(ValidateStep));
        await Assert.That(definition.EntryStep!.Configuration).IsNotNull();
    }

    /// <summary>
    /// Verifies that <c>StartWith&lt;TStep&gt;((Action&lt;IStepConfiguration&lt;TState&gt;&gt;)null)</c>
    /// throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Test]
    public async Task StartWith_WithNullConfigure_Throws()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.StartWith<ValidateStep>(
            (Action<IStepConfiguration<TestWorkflowState>>)null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. StartWith(instanceName, configure)
    // =============================================================================

    /// <summary>
    /// Verifies that <c>StartWith&lt;TStep&gt;("Entry", step =&gt; step.WithRetry(4))</c>
    /// carries BOTH the instance name AND the retry configuration onto the entry step.
    /// </summary>
    [Test]
    public async Task StartWith_WithInstanceNameAndConfigure_StoresBoth()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>("InitialValidation", step => step.WithRetry(4))
            .Finally<CompleteStep>();

        // Assert
        var entryStep = definition.Steps.First(s => s.StepType == typeof(ValidateStep));
        await Assert.That(entryStep.InstanceName).IsEqualTo("InitialValidation");
        await Assert.That(entryStep.Configuration).IsNotNull();
        await Assert.That(entryStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(4);
    }

    /// <summary>
    /// Verifies the named-instance + configure overload guards a null configure.
    /// </summary>
    [Test]
    public async Task StartWith_WithInstanceNameAndNullConfigure_Throws()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.StartWith<ValidateStep>("Entry", null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // C. Finally(configure)
    // =============================================================================

    /// <summary>
    /// Verifies that <c>Finally&lt;TStep&gt;(step =&gt; step.WithTimeout(...))</c> carries the
    /// timeout configuration onto the terminal step in the built definition and keeps it
    /// marked terminal.
    /// </summary>
    [Test]
    public async Task Finally_WithConfigure_StoresTimeoutOnTerminalStep()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>(step => step.WithTimeout(TimeSpan.FromSeconds(5)));

        // Assert
        var terminalStep = definition.Steps.First(s => s.StepType == typeof(CompleteStep));
        await Assert.That(terminalStep.IsTerminal).IsTrue();
        await Assert.That(terminalStep.Configuration).IsNotNull();
        await Assert.That(terminalStep.Configuration!.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Verifies that the built definition's TerminalStep is the configured terminal step.
    /// </summary>
    [Test]
    public async Task Finally_WithConfigure_TerminalStepIsConfiguredStep()
    {
        // Arrange & Act
        var definition = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>(step => step.WithTimeout(TimeSpan.FromSeconds(5)));

        // Assert
        await Assert.That(definition.TerminalStep).IsNotNull();
        await Assert.That(definition.TerminalStep!.StepType).IsEqualTo(typeof(CompleteStep));
        await Assert.That(definition.TerminalStep!.Configuration).IsNotNull();
    }

    /// <summary>
    /// Verifies that <c>Finally&lt;TStep&gt;(null)</c> throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Test]
    public async Task Finally_WithNullConfigure_Throws()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow")
            .StartWith<ValidateStep>();

        // Act & Assert
        await Assert.That(() => builder.Finally<CompleteStep>(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that <c>Finally&lt;TStep&gt;(configure)</c> before <c>StartWith</c> throws
    /// <see cref="InvalidOperationException"/>, mirroring the parameterless overload's guard.
    /// </summary>
    [Test]
    public async Task Finally_WithConfigureBeforeStartWith_Throws()
    {
        // Arrange
        var builder = Workflow<TestWorkflowState>.Create("test-workflow");

        // Act & Assert
        await Assert.That(() => builder.Finally<CompleteStep>(step => step.WithRetry(2)))
            .Throws<InvalidOperationException>();
    }
}
