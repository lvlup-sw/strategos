// -----------------------------------------------------------------------
// <copyright file="StepModelResilienceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests verifying that <see cref="StepModel"/> carries the resilience IR
/// (<see cref="RetryModel"/>, <see cref="TimeoutModel"/>, <see cref="CompensationModel"/>,
/// <see cref="ConfidenceModel"/>) from construction through to emit-time.
/// </summary>
[Property("Category", "Unit")]
public sealed class StepModelResilienceTests
{
    /// <summary>
    /// Verifies that a <see cref="StepModel"/> created with resilience configuration
    /// carries back the retry, timeout, compensation, and confidence models intact.
    /// </summary>
    [Test]
    public async Task StepModel_WithResilienceConfig_CarriesRetryTimeoutCompensationConfidence()
    {
        // Arrange
        var retry = new RetryModel(
            MaxAttempts: 3,
            InitialDelay: TimeSpan.FromSeconds(2),
            BackoffMultiplier: 2.0,
            MaxDelay: TimeSpan.FromMinutes(1),
            UseJitter: true);
        var timeout = new TimeoutModel(TimeSpan.FromSeconds(30));
        var compensation = new CompensationModel(
            CompensationStepTypeName: "MyApp.Steps.RefundPayment",
            RequiredOnFailure: true);
        var confidence = new ConfidenceModel(
            Threshold: 0.8,
            OnLowConfidenceHandlerId: "EscalateToHuman");

        // Act
        var model = StepModel.Create(
            stepName: "ProcessPayment",
            stepTypeName: "MyApp.Steps.ProcessPayment",
            retry: retry,
            timeout: timeout,
            compensation: compensation,
            confidence: confidence);

        // Assert
        await Assert.That(model.Retry).IsEqualTo(retry);
        await Assert.That(model.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(model.Retry!.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(2));
        await Assert.That(model.Retry!.BackoffMultiplier).IsEqualTo(2.0);
        await Assert.That(model.Retry!.MaxDelay).IsEqualTo(TimeSpan.FromMinutes(1));
        await Assert.That(model.Retry!.UseJitter).IsTrue();

        await Assert.That(model.Timeout).IsEqualTo(timeout);
        await Assert.That(model.Timeout!.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));

        await Assert.That(model.Compensation).IsEqualTo(compensation);
        await Assert.That(model.Compensation!.CompensationStepTypeName).IsEqualTo("MyApp.Steps.RefundPayment");
        await Assert.That(model.Compensation!.RequiredOnFailure).IsTrue();

        await Assert.That(model.Confidence).IsEqualTo(confidence);
        await Assert.That(model.Confidence!.Threshold).IsEqualTo(0.8);
        await Assert.That(model.Confidence!.OnLowConfidenceHandlerId).IsEqualTo("EscalateToHuman");
    }

    /// <summary>
    /// Verifies that the four resilience fields default to null when not supplied,
    /// so existing call sites that omit resilience config are unaffected.
    /// </summary>
    [Test]
    public async Task StepModel_WithoutResilienceConfig_DefaultsToNull()
    {
        // Arrange & Act
        var model = StepModel.Create(
            stepName: "ProcessPayment",
            stepTypeName: "MyApp.Steps.ProcessPayment");

        // Assert
        await Assert.That(model.Retry).IsNull();
        await Assert.That(model.Timeout).IsNull();
        await Assert.That(model.Compensation).IsNull();
        await Assert.That(model.Confidence).IsNull();
    }
}
