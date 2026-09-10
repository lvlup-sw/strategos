// -----------------------------------------------------------------------
// <copyright file="HappyPathWorkflow.cs" company="Levelup Software">
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
/// Immutable state for the happy-path fixture workflow.
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer (<c>HappyPathStateReducer</c>) used by the saga to fold each step's
/// returned state. <see cref="StepCount"/> is appended to by each step so a
/// test can confirm state actually flows through the generated saga.
/// </remarks>
[WorkflowState]
public sealed record HappyPathState : IWorkflowState
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
/// First instrumented step of the happy-path fixture workflow. Deterministic
/// (never throws) for this happy-path task; records its invocation and
/// increments the state step counter.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RecordFirstStep(WorkflowInvocationLog log) : IWorkflowStep<HappyPathState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<HappyPathState>> ExecuteAsync(
        HappyPathState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RecordFirstStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<HappyPathState>.FromState(updated));
    }
}

/// <summary>
/// Final instrumented step of the happy-path fixture workflow. Deterministic
/// (never throws); records its invocation and increments the state step
/// counter. As the workflow's <c>Finally</c> step, its completion drives the
/// saga to its terminal <c>Completed</c> phase and <c>MarkCompleted()</c>.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RecordSecondStep(WorkflowInvocationLog log) : IWorkflowStep<HappyPathState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<HappyPathState>> ExecuteAsync(
        HappyPathState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RecordSecondStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<HappyPathState>.FromState(updated));
    }
}

/// <summary>
/// The happy-path fixture workflow definition. The
/// <see cref="WorkflowAttribute"/> drives the Strategos source generator to
/// emit, at build time, the Wolverine saga (<c>HappyPathSaga</c>), the worker
/// handlers, the command records (notably <c>StartHappyPathCommand</c>), the
/// <c>HappyPathPhase</c> enum, and the <c>AddHappyPathWorkflow()</c> DI
/// extension consumed by <see cref="WolverineHostFixture"/>.
/// </summary>
[Workflow("happy-path")]
public static partial class HappyPathWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: a single linear chain of two
    /// deterministic, instrumented steps.
    /// </summary>
    public static WorkflowDefinition<HappyPathState> Definition => Workflow<HappyPathState>
        .Create("happy-path")
        .StartWith<RecordFirstStep>()
        .Finally<RecordSecondStep>();
}
