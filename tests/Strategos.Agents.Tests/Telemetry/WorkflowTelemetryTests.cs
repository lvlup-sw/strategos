// =============================================================================
// <copyright file="WorkflowTelemetryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Telemetry;

namespace Strategos.Agents.Tests.Telemetry;

/// <summary>
/// Unit tests for <see cref="WorkflowTelemetry"/> covering constants and helper methods.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowTelemetryTests
{
    /// <summary>
    /// Verifies that SourceName constant has expected value.
    /// </summary>
    [Test]
    public async Task SourceName_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.SourceName).IsEqualTo("Strategos.Steps");
    }

    /// <summary>
    /// Verifies that MeterName constant has expected value.
    /// </summary>
    [Test]
    public async Task MeterName_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.MeterName).IsEqualTo("Strategos.Steps");
    }

    /// <summary>
    /// Verifies that StepSource is initialized.
    /// </summary>
    [Test]
    public async Task StepSource_IsInitialized()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.StepSource).IsNotNull();
        await Assert.That(WorkflowTelemetry.StepSource.Name).IsEqualTo("Strategos.Steps");
    }

    /// <summary>
    /// Verifies that StepMeter is initialized.
    /// </summary>
    [Test]
    public async Task StepMeter_IsInitialized()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.StepMeter).IsNotNull();
        await Assert.That(WorkflowTelemetry.StepMeter.Name).IsEqualTo("Strategos.Steps");
    }

    /// <summary>
    /// Verifies that StepCompletedCounter is initialized.
    /// </summary>
    [Test]
    public async Task StepCompletedCounter_IsInitialized()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.StepCompletedCounter).IsNotNull();
        await Assert.That(WorkflowTelemetry.StepCompletedCounter.Name).IsEqualTo("agentic.step.completed");
    }

    /// <summary>
    /// Verifies that StepDurationHistogram is initialized.
    /// </summary>
    [Test]
    public async Task StepDurationHistogram_IsInitialized()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.StepDurationHistogram).IsNotNull();
        await Assert.That(WorkflowTelemetry.StepDurationHistogram.Name).IsEqualTo("agentic.step.duration");
    }

    /// <summary>
    /// Verifies that StepFailureCounter is initialized.
    /// </summary>
    [Test]
    public async Task StepFailureCounter_IsInitialized()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.StepFailureCounter).IsNotNull();
        await Assert.That(WorkflowTelemetry.StepFailureCounter.Name).IsEqualTo("agentic.step.failures");
    }

    /// <summary>
    /// Verifies that Attributes class has correct WorkflowId constant.
    /// </summary>
    [Test]
    public async Task Attributes_WorkflowId_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.WorkflowId).IsEqualTo("workflow.id");
    }

    /// <summary>
    /// Verifies that Attributes class has correct StepName constant.
    /// </summary>
    [Test]
    public async Task Attributes_StepName_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.StepName).IsEqualTo("step.name");
    }

    /// <summary>
    /// Verifies that Attributes class has correct StepExecutionId constant.
    /// </summary>
    [Test]
    public async Task Attributes_StepExecutionId_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.StepExecutionId).IsEqualTo("step.execution_id");
    }

    /// <summary>
    /// Verifies that Attributes class has correct StepDurationMs constant.
    /// </summary>
    [Test]
    public async Task Attributes_StepDurationMs_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.StepDurationMs).IsEqualTo("step.duration_ms");
    }

    /// <summary>
    /// Verifies that Attributes class has correct StepConfidence constant.
    /// </summary>
    [Test]
    public async Task Attributes_StepConfidence_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.StepConfidence).IsEqualTo("step.confidence");
    }

    /// <summary>
    /// Verifies that Attributes class has correct ErrorType constant.
    /// </summary>
    [Test]
    public async Task Attributes_ErrorType_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.ErrorType).IsEqualTo("error.type");
    }

    /// <summary>
    /// Verifies that Attributes class has correct ErrorMessage constant.
    /// </summary>
    [Test]
    public async Task Attributes_ErrorMessage_HasExpectedValue()
    {
        // Assert
        await Assert.That(WorkflowTelemetry.Attributes.ErrorMessage).IsEqualTo("error.message");
    }

    /// <summary>
    /// Verifies that StartStepSpan behavior depends on listener presence.
    /// </summary>
    [Test]
    public async Task StartStepSpan_BehaviorDependsOnListenerPresence()
    {
        // Act
        var activity = WorkflowTelemetry.StartStepSpan("TestStep", Guid.NewGuid());

        // Assert - result depends on whether listeners exist in test infrastructure
        // When HasListeners is false, activity is null; when true, activity is non-null
        var hasListeners = WorkflowTelemetry.StepSource.HasListeners();
        if (hasListeners)
        {
            await Assert.That(activity).IsNotNull();
            activity?.Dispose();
        }
        else
        {
            await Assert.That(activity).IsNull();
        }
    }

    /// <summary>
    /// Verifies that RecordStepCompletion does not throw on success.
    /// </summary>
    [Test]
    public async Task RecordStepCompletion_Success_DoesNotThrow()
    {
        // Act & Assert - should not throw
        WorkflowTelemetry.RecordStepCompletion("TestStep", success: true, durationMs: 100.0, confidence: 0.95);
        await Assert.That(true).IsTrue(); // If we got here, no exception was thrown
    }

    /// <summary>
    /// Verifies that RecordStepCompletion does not throw on failure.
    /// </summary>
    [Test]
    public async Task RecordStepCompletion_Failure_DoesNotThrow()
    {
        // Act & Assert - should not throw
        WorkflowTelemetry.RecordStepCompletion("TestStep", success: false, durationMs: 50.0);
        await Assert.That(true).IsTrue(); // If we got here, no exception was thrown
    }

    /// <summary>
    /// Verifies that RecordStepFailure does not throw.
    /// </summary>
    [Test]
    public async Task RecordStepFailure_DoesNotThrow()
    {
        // Act & Assert - should not throw
        WorkflowTelemetry.RecordStepFailure("TestStep", errorType: "TimeoutException", durationMs: 30000.0);
        await Assert.That(true).IsTrue(); // If we got here, no exception was thrown
    }
}

