// -----------------------------------------------------------------------
// <copyright file="RetryWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable state for the retry-proof fixture workflow.
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by the saga to fold each step's returned state.
/// </remarks>
[WorkflowState]
public sealed record RetryState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }
}

/// <summary>
/// Transient failure raised by <see cref="RetryFlakyStep"/> on its first two
/// invocations to exercise the generated Wolverine per-handler retry policy.
/// </summary>
public sealed class TransientStepException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransientStepException"/>
    /// class with the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TransientStepException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Entry step of the retry-proof fixture workflow. Deterministic (never throws);
/// records its invocation so the test can confirm the saga actually started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RetryPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<RetryState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<RetryState>> ExecuteAsync(
        RetryState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RetryPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<RetryState>.FromState(updated));
    }
}

/// <summary>
/// A deliberately flaky instrumented step. It records every invocation in the
/// shared <see cref="WorkflowInvocationLog"/> and throws a transient exception
/// on attempts 1 and 2, succeeding only on attempt 3. The recorded invocation
/// count therefore equals the number of attempts Wolverine made, which the
/// behavioral test asserts to prove the generated retry policy actually retried.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RetryFlakyStep(WorkflowInvocationLog log) : IWorkflowStep<RetryState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<RetryState>> ExecuteAsync(
        RetryState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        // Record FIRST, then decide based on this invocation's attempt number.
        // CountFor reflects all prior recordings plus this one.
        this.log.Record(nameof(RetryFlakyStep));
        var attempt = this.log.CountFor(nameof(RetryFlakyStep));

        if (attempt < 3)
        {
            throw new TransientStepException(
                $"RetryFlakyStep transient failure on attempt {attempt}.");
        }

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<RetryState>.FromState(updated));
    }
}

/// <summary>
/// Terminal step of the retry-proof fixture workflow. Deterministic (never
/// throws); records its invocation. As the workflow's <c>Finally</c> step, its
/// completion drives the saga to its terminal phase and <c>MarkCompleted()</c>,
/// so the test only observes it once the flaky step has finally succeeded.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RetryFinishStep(WorkflowInvocationLog log) : IWorkflowStep<RetryState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<RetryState>> ExecuteAsync(
        RetryState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RetryFinishStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<RetryState>.FromState(updated));
    }
}

/// <summary>
/// The retry-proof fixture workflow definition. The
/// <see cref="WorkflowAttribute"/> drives the Strategos source generator to
/// emit, at build time, the Wolverine saga (<c>RetryProofSaga</c>), the worker
/// handler for <see cref="RetryFlakyStep"/> (carrying the generated
/// <c>Configure(HandlerChain)</c> retry policy from DR-2 T007), the start
/// command (<c>StartRetryProofCommand</c>), and the
/// <c>AddRetryProofWorkflow()</c> DI extension consumed by
/// <see cref="WolverineHostFixture"/>.
/// </summary>
[Workflow("retry-proof")]
public static partial class RetryProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: a prepare step, a flaky middle step
    /// declaring <c>.WithRetry(2)</c> (two retries after the initial attempt,
    /// i.e. up to three total invocations), and a terminal finish step whose
    /// completion the test waits on.
    /// </summary>
    public static WorkflowDefinition<RetryState> Definition => Workflow<RetryState>
        .Create("retry-proof")
        .StartWith<RetryPrepareStep>()
        .Then<RetryFlakyStep>(step => step.WithRetry(2))
        .Finally<RetryFinishStep>();
}
