// -----------------------------------------------------------------------
// <copyright file="BranchFailureConfigExpressibilityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Builders;

/// <summary>
/// Tests that resilience configuration declared via the
/// <c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(...))</c> configure-lambda overload on the
/// <see cref="Strategos.Builders.IBranchBuilder{TState}"/> branch builder and the
/// <see cref="Strategos.Builders.IFailureBuilder{TState}"/> failure-handler builder reaches the
/// generator's <see cref="StepModel"/> IR (epic #135, DR-7).
/// </summary>
/// <remarks>
/// <para>
/// The configure overload (carrying <c>.WithRetry/.WithTimeout/.Compensate/.RequireConfidence/
/// .OnLowConfidence/.ValidateState/.WithContext</c>) already exists on the top-level
/// <see cref="Strategos.Builders.IWorkflowBuilder{TState}"/>, the loop-body
/// <see cref="Strategos.Builders.ILoopBuilder{TState}"/>, and (via #134) the fork-path
/// <see cref="Strategos.Builders.IForkPathBuilder{TState}"/> builder. DR-7 closes the
/// expressibility gap by adding it to the branch and failure-handler builders too.
/// </para>
/// <para>
/// These tests drive a workflow snippet through <see cref="ParserTestHelper.ExtractStepModels"/>,
/// which routes through <c>FluentDslParser.ExtractStepModels</c> →
/// <see cref="StepExtractor.ExtractStepModels"/>. The shared <c>TryGetStepModel</c> path invokes
/// <c>ExtractConfiguredResilience</c> on each <c>Then</c> invocation, so config declared in any
/// context the walker descends into is captured as a populated <see cref="RetryModel"/> on the
/// step's <see cref="StepModel"/>.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class BranchFailureConfigExpressibilityTests
{
    /// <summary>
    /// Verifies that <c>.WithRetry(2)</c> declared via the new branch-builder configure overload
    /// (<c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(2))</c>) populates the branch step's
    /// <see cref="StepModel.Retry"/>.
    /// </summary>
    [Test]
    public async Task BranchBuilder_ThenWithConfig_StepModelCarriesRetry()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(BranchConfigWorkflow);

        // Act
        var branchStep = stepModels.Single(s => s.StepName == "EscalateClaim");

        // Assert
        await Assert.That(branchStep.Retry).IsNotNull();
        await Assert.That(branchStep.Retry!.MaxAttempts).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that <c>.WithRetry(2)</c> declared via the new failure-handler-builder configure
    /// overload (<c>OnFailure(f =&gt; f.Then&lt;TStep&gt;(step =&gt; step.WithRetry(2)))</c>) populates
    /// the failure-handler step's <see cref="StepModel.Retry"/>.
    /// </summary>
    [Test]
    public async Task FailureHandlerBuilder_ThenWithConfig_StepModelCarriesRetry()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(FailureHandlerConfigWorkflow);

        // Act
        var recoveryStep = stepModels.Single(s => s.StepName == "RefundPayment");

        // Assert
        await Assert.That(recoveryStep.Retry).IsNotNull();
        await Assert.That(recoveryStep.Retry!.MaxAttempts).IsEqualTo(2);
    }

    // =========================================================================
    // Test source workflows
    // =========================================================================

    /// <summary>
    /// A workflow whose branch case declares a step's retry via the configure lambda,
    /// exercising the new branch-builder <c>Then&lt;TStep&gt;(Action&lt;IStepConfiguration&lt;TState&gt;&gt;)</c>
    /// overload.
    /// </summary>
    private const string BranchConfigWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool RequiresEscalation { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class EscalateClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AutoApproveClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("branch-config")]
        public static partial class BranchConfigWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("branch-config")
                .StartWith<IntakeClaim>()
                .Branch(state => state.RequiresEscalation,
                    BranchCase.When(true, path => path
                        .Then<EscalateClaim>(step => step.WithRetry(2))),
                    BranchCase.Otherwise(path => path
                        .Then<AutoApproveClaim>()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A workflow whose fork-path failure handler declares a recovery step's retry via the
    /// configure lambda, exercising the new failure-handler-builder
    /// <c>Then&lt;TStep&gt;(Action&lt;IStepConfiguration&lt;TState&gt;&gt;)</c> overload.
    /// </summary>
    private const string FailureHandlerConfigWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record CheckoutState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateCart : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class ChargeCard : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class RefundPayment : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class ReserveStock : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class FinalizeOrder : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class CompleteCheckout : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        [Workflow("failure-config")]
        public static partial class FailureHandlerConfigWorkflow
        {
            public static WorkflowDefinition<CheckoutState> Definition => Workflow<CheckoutState>
                .Create("failure-config")
                .StartWith<ValidateCart>()
                .Fork(
                    path => path.Then<ChargeCard>()
                        .OnFailure(f => f.Then<RefundPayment>(step => step.WithRetry(2))),
                    path => path.Then<ReserveStock>())
                .Join<FinalizeOrder>()
                .Finally<CompleteCheckout>();
        }
        """;
}
