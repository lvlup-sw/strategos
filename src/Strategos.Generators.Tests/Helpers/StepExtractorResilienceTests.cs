// -----------------------------------------------------------------------
// <copyright file="StepExtractorResilienceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Tests that <see cref="StepExtractor"/> parses the per-step resilience configuration
/// declared inside the <c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(...).WithTimeout(...)...)</c>
/// configure lambda into the step's <see cref="StepModel"/> IR (epic #135, DR-1).
/// </summary>
/// <remarks>
/// These tests drive a workflow snippet through <see cref="ParserTestHelper.ExtractStepModels"/>,
/// which routes through <c>FluentDslParser.ExtractStepModels</c> →
/// <see cref="StepExtractor.ExtractStepModels"/>, returning the configured
/// <see cref="StepModel"/> list. The resilience models
/// (<see cref="RetryModel"/>, <see cref="TimeoutModel"/>, <see cref="CompensationModel"/>,
/// <see cref="ConfidenceModel"/>) are internal but visible to this test project.
/// </remarks>
[Property("Category", "Unit")]
public sealed class StepExtractorResilienceTests
{
    // =========================================================================
    // Task 002 — WithRetry / WithTimeout
    // =========================================================================

    /// <summary>
    /// Verifies that <c>.WithRetry(int, TimeSpan)</c> and <c>.WithTimeout(TimeSpan)</c>
    /// declared in a step's configure lambda populate the step's
    /// <see cref="RetryModel"/> and <see cref="TimeoutModel"/>.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_StepWithWithRetryAndTimeout_PopulatesRetryAndTimeoutModels()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceLinearWorkflow);

        // Act
        var processStep = stepModels.Single(s => s.StepName == "ProcessPayment");

        // Assert - retry policy
        await Assert.That(processStep.Retry).IsNotNull();
        await Assert.That(processStep.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(processStep.Retry!.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(5));

        // Assert - timeout policy
        await Assert.That(processStep.Timeout).IsNotNull();
        await Assert.That(processStep.Timeout!.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Verifies that the single-argument <c>.WithRetry(int)</c> overload populates
    /// <see cref="RetryModel.MaxAttempts"/> with a null <see cref="RetryModel.InitialDelay"/>.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_StepWithWithRetryNoDelay_PopulatesMaxAttemptsOnly()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceLinearWorkflow);

        // Act
        var auditStep = stepModels.Single(s => s.StepName == "AuditPayment");

        // Assert
        await Assert.That(auditStep.Retry).IsNotNull();
        await Assert.That(auditStep.Retry!.MaxAttempts).IsEqualTo(2);
        await Assert.That(auditStep.Retry!.InitialDelay).IsNull();
        await Assert.That(auditStep.Timeout).IsNull();
    }

    // =========================================================================
    // Test source workflows
    // =========================================================================

    /// <summary>
    /// A linear workflow whose steps declare retry/timeout via the configure lambda.
    /// </summary>
    private const string ResilienceLinearWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PaymentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidatePayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class AuditPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class SendReceipt : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        [Workflow("resilience-linear")]
        public static partial class ResilienceLinearWorkflow
        {
            public static WorkflowDefinition<PaymentState> Definition => Workflow<PaymentState>
                .Create("resilience-linear")
                .StartWith<ValidatePayment>()
                .Then<ProcessPayment>(step => step
                    .WithRetry(3, TimeSpan.FromSeconds(5))
                    .WithTimeout(TimeSpan.FromMinutes(2)))
                .Then<AuditPayment>(step => step.WithRetry(2))
                .Finally<SendReceipt>();
        }
        """;
}
