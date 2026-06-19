// -----------------------------------------------------------------------
// <copyright file="ValidationWorkflow.cs" company="Levelup Software">
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
/// Immutable state for the validation-guard behavioral fixture (#143, G-6 6.1
/// backfill). The guarded step's <c>.ValidateState(...)</c> predicate reads
/// <see cref="IsAuthorized"/>, which is left at its default <see langword="false"/>
/// so the guard fails at runtime and the saga routes to <c>ValidationFailed</c>.
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by the saga to fold each step's returned state.
/// </remarks>
[WorkflowState]
public sealed record ValidationState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workflow is authorized to run the guarded
    /// step. Left at its default <see langword="false"/> by the prepare step, so the
    /// guarded step's <c>.ValidateState(s =&gt; s.IsAuthorized, ...)</c> predicate
    /// fails and the lowered Guard-Then-Dispatch short-circuits the worker dispatch.
    /// </summary>
    public bool IsAuthorized { get; init; }
}

/// <summary>
/// Entry step of the validation fixture workflow. Deterministic (never throws);
/// records its invocation and folds state WITHOUT setting
/// <see cref="ValidationState.IsAuthorized"/>, so the downstream guard fails.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ValidationPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ValidationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ValidationState>> ExecuteAsync(
        ValidationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ValidationPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ValidationState>.FromState(updated));
    }
}

/// <summary>
/// The validation-guarded middle step. It declares
/// <c>.ValidateState(s =&gt; s.IsAuthorized, "...")</c>. Because the prepare step
/// leaves <see cref="ValidationState.IsAuthorized"/> at <see langword="false"/>,
/// the generated saga's Guard-Then-Dispatch guard fails BEFORE this worker is
/// dispatched, so this step's <see cref="ExecuteAsync"/> MUST NOT run. Recording
/// any invocation here is the failure signal: it would mean the guard did not
/// short-circuit (the lowering regressed to a standard dispatch).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ValidationGuardedStep(WorkflowInvocationLog log) : IWorkflowStep<ValidationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ValidationState>> ExecuteAsync(
        ValidationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ValidationGuardedStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ValidationState>.FromState(updated));
    }
}

/// <summary>
/// Terminal step that must NOT run: once the guard fails the saga transitions to
/// <c>ValidationFailed</c> and <c>yield break</c>s without cascading further start
/// commands, so this step is never reached. Its invocation count being zero is the
/// observable proof that the failed guard short-circuited the rest of the flow.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ValidationNeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<ValidationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ValidationState>> ExecuteAsync(
        ValidationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ValidationNeverReachedStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<ValidationState>.FromState(updated));
    }
}

/// <summary>
/// The validation-proof fixture workflow definition. The
/// <see cref="WorkflowAttribute"/> drives the Strategos source generator to emit,
/// at build time, the Wolverine saga (<c>ValidationProofSaga</c>), the yield-based
/// Guard-Then-Dispatch start handler for <see cref="ValidationGuardedStep"/> lowered
/// from <c>.ValidateState(...)</c>, the <c>ValidationProofValidationFailed</c> event,
/// the <c>ValidationFailed</c> phase, the start command
/// (<c>StartValidationProofCommand</c>), and the
/// <c>AddValidationProofWorkflow()</c> DI extension consumed by the host fixture.
/// </summary>
[Workflow("validation-proof")]
public static partial class ValidationProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: a prepare step, a guarded middle step
    /// declaring <c>.ValidateState(s =&gt; s.IsAuthorized, "...")</c> whose predicate
    /// is FALSE at runtime, and a terminal step that must never be reached.
    /// </summary>
    public static WorkflowDefinition<ValidationState> Definition => Workflow<ValidationState>
        .Create("validation-proof")
        .StartWith<ValidationPrepareStep>()
        .Then<ValidationGuardedStep>(step => step
            .ValidateState(s => s.IsAuthorized, "Workflow is not authorized to run the guarded step."))
        .Finally<ValidationNeverReachedStep>();
}
