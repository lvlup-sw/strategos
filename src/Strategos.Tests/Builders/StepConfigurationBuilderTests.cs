// =============================================================================
// <copyright file="StepConfigurationBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="IStepConfiguration{TState}"/> implementation.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>RequireConfidence sets confidence threshold</description></item>
///   <item><description>OnLowConfidence configures handler</description></item>
///   <item><description>Compensate sets compensation step</description></item>
///   <item><description>WithRetry configures retry policy</description></item>
///   <item><description>WithTimeout sets timeout</description></item>
///   <item><description>Fluent chaining works correctly</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class StepConfigurationBuilderTests
{
    // =============================================================================
    // A. RequireConfidence Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RequireConfidence sets the confidence threshold.
    /// </summary>
    [Test]
    public async Task Then_WithRequireConfidence_SetsThreshold()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.RequireConfidence(0.85))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration).IsNotNull();
        await Assert.That(processStep.Configuration!.ConfidenceThreshold).IsEqualTo(0.85);
    }

    /// <summary>
    /// Verifies that RequireConfidence throws for value below 0.
    /// </summary>
    [Test]
    public async Task Then_WithRequireConfidence_BelowZero_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.RequireConfidence(-0.1))
            .Finally<CompleteStep>())
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that RequireConfidence throws for value above 1.
    /// </summary>
    [Test]
    public async Task Then_WithRequireConfidence_AboveOne_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.RequireConfidence(1.1))
            .Finally<CompleteStep>())
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that RequireConfidence called AFTER other setters preserves them (DR-6, #145).
    /// </summary>
    /// <remarks>
    /// Guards the replace-not-merge regression: RequireConfidence must compose with prior
    /// WithRetry/Compensate/WithTimeout rather than discarding them.
    /// </remarks>
    [Test]
    public async Task Then_RequireConfidenceAfterOtherSetters_PreservesAllConfiguration()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .WithRetry(3)
                .Compensate<RollbackStep>()
                .WithTimeout(TimeSpan.FromMinutes(2))
                .RequireConfidence(0.85))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration!.ConfidenceThreshold).IsEqualTo(0.85);
        await Assert.That(processStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(processStep.Configuration!.Compensation!.CompensationStepType).IsEqualTo(typeof(RollbackStep));
        await Assert.That(processStep.Configuration!.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Verifies that confidence-first and confidence-last chains compose to an equal
    /// configuration via record equality (DR-6, #145).
    /// </summary>
    [Test]
    public async Task Then_RequireConfidenceOrderIndependent_ProducesEqualConfiguration()
    {
        // Act
        var confidenceFirst = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .RequireConfidence(0.85)
                .WithRetry(3)
                .Compensate<RollbackStep>()
                .WithTimeout(TimeSpan.FromMinutes(2)))
            .Finally<CompleteStep>();

        var confidenceLast = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .WithRetry(3)
                .Compensate<RollbackStep>()
                .WithTimeout(TimeSpan.FromMinutes(2))
                .RequireConfidence(0.85))
            .Finally<CompleteStep>();

        // Assert
        var firstConfig = confidenceFirst.Steps.First(s => s.StepType == typeof(ProcessStep)).Configuration;
        var lastConfig = confidenceLast.Steps.First(s => s.StepType == typeof(ProcessStep)).Configuration;
        await Assert.That(lastConfig).IsEqualTo(firstConfig);
    }

    // =============================================================================
    // B. Compensate Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Compensate sets the compensation step type.
    /// </summary>
    [Test]
    public async Task Then_WithCompensate_SetsCompensationStepType()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.Compensate<RollbackStep>())
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration).IsNotNull();
        await Assert.That(processStep.Configuration!.Compensation).IsNotNull();
        await Assert.That(processStep.Configuration!.Compensation!.CompensationStepType).IsEqualTo(typeof(RollbackStep));
    }

    // =============================================================================
    // C. WithRetry Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithRetry sets retry configuration.
    /// </summary>
    [Test]
    public async Task Then_WithRetry_SetsRetryConfiguration()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.WithRetry(3))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration).IsNotNull();
        await Assert.That(processStep.Configuration!.Retry).IsNotNull();
        await Assert.That(processStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
    }

    /// <summary>
    /// Verifies that WithRetry with delay sets both max attempts and initial delay.
    /// </summary>
    [Test]
    public async Task Then_WithRetryAndDelay_SetsBothValues()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.WithRetry(3, TimeSpan.FromSeconds(5)))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(processStep.Configuration!.Retry!.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    // =============================================================================
    // D. WithTimeout Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithTimeout sets the timeout value.
    /// </summary>
    [Test]
    public async Task Then_WithTimeout_SetsTimeout()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg.WithTimeout(TimeSpan.FromMinutes(5)))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration).IsNotNull();
        await Assert.That(processStep.Configuration!.Timeout).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    // =============================================================================
    // E. Fluent Chaining Tests
    // =============================================================================

    /// <summary>
    /// Verifies that multiple configuration methods can be chained.
    /// </summary>
    [Test]
    public async Task Then_WithMultipleConfigurations_ChainsCorrectly()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .RequireConfidence(0.85)
                .Compensate<RollbackStep>()
                .WithRetry(3)
                .WithTimeout(TimeSpan.FromMinutes(2)))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration!.ConfidenceThreshold).IsEqualTo(0.85);
        await Assert.That(processStep.Configuration!.Compensation!.CompensationStepType).IsEqualTo(typeof(RollbackStep));
        await Assert.That(processStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(processStep.Configuration!.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Verifies that steps without configuration have null Configuration.
    /// </summary>
    [Test]
    public async Task Then_WithoutConfiguration_ConfigurationIsNull()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration).IsNull();
    }

    // =============================================================================
    // F. ValidateState Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ValidateState captures predicate and message.
    /// </summary>
    [Test]
    public async Task Then_WithValidateState_CapturesPredicateAndMessage()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .ValidateState(state => state.QualityScore > 0, "Quality score must be positive"))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration).IsNotNull();
        await Assert.That(processStep.Configuration!.Validation).IsNotNull();
        await Assert.That(processStep.Configuration!.Validation!.ErrorMessage).IsEqualTo("Quality score must be positive");
        await Assert.That(processStep.Configuration!.Validation!.PredicateExpression).Contains("QualityScore");
    }

    /// <summary>
    /// Verifies that ValidateState throws for null predicate.
    /// </summary>
    [Test]
    public async Task Then_WithValidateState_NullPredicate_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .ValidateState(null!, "Must have items"))
            .Finally<CompleteStep>())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ValidateState throws for null message.
    /// </summary>
    [Test]
    public async Task Then_WithValidateState_NullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .ValidateState(state => state.QualityScore > 0, null!))
            .Finally<CompleteStep>())
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that ValidateState can chain with other configurations.
    /// </summary>
    [Test]
    public async Task Then_WithValidateState_CanChainWithOtherConfigurations()
    {
        // Act
        var workflow = Workflow<TestWorkflowState>
            .Create("test-workflow")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>(cfg => cfg
                .ValidateState(state => state.QualityScore > 0, "Quality score must be positive")
                .WithRetry(3)
                .WithTimeout(TimeSpan.FromMinutes(5)))
            .Finally<CompleteStep>();

        // Assert
        var processStep = workflow.Steps.First(s => s.StepType == typeof(ProcessStep));
        await Assert.That(processStep.Configuration!.Validation).IsNotNull();
        await Assert.That(processStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(processStep.Configuration!.Timeout).IsEqualTo(TimeSpan.FromMinutes(5));
    }
}

/// <summary>
/// Test step class for rollback/compensation operations.
/// </summary>
internal sealed class RollbackStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}
