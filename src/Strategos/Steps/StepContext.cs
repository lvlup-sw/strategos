// =============================================================================
// <copyright file="StepContext.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Steps;

/// <summary>
/// Immutable context provided to workflow steps during execution.
/// </summary>
/// <remarks>
/// <para>
/// Step context provides execution metadata to steps:
/// <list type="bullet">
///   <item><description>CorrelationId: Distributed tracing correlation</description></item>
///   <item><description>WorkflowId: Parent workflow identifier</description></item>
///   <item><description>StepName: Current step being executed</description></item>
///   <item><description>CurrentPhase: Generated phase enum value</description></item>
///   <item><description>Timestamp: Execution start time</description></item>
///   <item><description>RetryCount: Number of retry attempts</description></item>
///   <item><description>IsCompensation: Whether this is an inverse execution</description></item>
///   <item><description>RollbackId: Stable identity shared by retries of one inverse execution</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed record StepContext
{
    /// <summary>
    /// Gets the correlation ID for distributed tracing.
    /// </summary>
    /// <remarks>
    /// Generated compensation handlers retain the rollback identifier's N-format
    /// representation here for compatibility. Use <see cref="RollbackId"/> rather than
    /// parsing this tracing value when implementing compensation idempotency.
    /// </remarks>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the parent workflow identifier.
    /// </summary>
    public required Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the name of the step being executed.
    /// </summary>
    public required string StepName { get; init; }

    /// <summary>
    /// Gets the timestamp when step execution started.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the current workflow phase (maps to generated Phase enum).
    /// </summary>
    public required string CurrentPhase { get; init; }

    /// <summary>
    /// Gets the number of retry attempts for this step.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether this execution is compensating a completed
    /// forward step.
    /// </summary>
    /// <remarks>
    /// The value defaults to <see langword="false"/> for ordinary forward execution.
    /// </remarks>
    public bool IsCompensation { get; init; }

    /// <summary>
    /// Gets the stable identifier for this rollback execution, or <see langword="null"/>
    /// during ordinary forward execution.
    /// </summary>
    /// <remarks>
    /// Retries and redeliveries of the same inverse execution carry the same value.
    /// Compensation steps that produce external effects should use it as their durable
    /// idempotency key when <see cref="IsCompensation"/> is <see langword="true"/>.
    /// </remarks>
    public Guid? RollbackId { get; init; }

    /// <summary>
    /// Creates a new step context with auto-generated correlation ID and timestamp.
    /// </summary>
    /// <param name="workflowId">The parent workflow identifier.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="currentPhase">The current workflow phase.</param>
    /// <returns>A new step context.</returns>
    public static StepContext Create(Guid workflowId, string stepName, string currentPhase)
    {
        return new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            WorkflowId = workflowId,
            StepName = stepName,
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = currentPhase,
        };
    }
}
