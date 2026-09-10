// =============================================================================
// <copyright file="TestSteps.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Fixtures;

/// <summary>
/// Test step for validation operations.
/// </summary>
internal sealed class ValidateStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for processing operations.
/// </summary>
internal sealed class ProcessStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for completion/finalization operations.
/// </summary>
internal sealed class CompleteStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for notification operations.
/// </summary>
internal sealed class NotifyStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for auto-processing branch operations.
/// </summary>
internal sealed class AutoProcessStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for manual processing branch operations.
/// </summary>
internal sealed class ManualProcessStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for critique operations in loops.
/// </summary>
internal sealed class CritiqueStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for refine operations in loops.
/// </summary>
internal sealed class RefineStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for logging failures in error handling paths.
/// </summary>
internal sealed class LogFailureStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for notifying administrators in error handling paths.
/// </summary>
internal sealed class NotifyAdminStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}

/// <summary>
/// Test step for refund operations in error recovery.
/// </summary>
internal sealed class RefundStep : IWorkflowStep<TestWorkflowState>
{
    /// <inheritdoc/>
    public Task<StepResult<TestWorkflowState>> ExecuteAsync(
        TestWorkflowState state,
        StepContext context,
        CancellationToken cancellationToken)
        => Task.FromResult(StepResult<TestWorkflowState>.FromState(state));
}
