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
///   <item><description>ExecutionId: Durable identity of this dispatch; the supported idempotency key</description></item>
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
    /// This value exists for tracing only. Its textual shape is not part of the supported
    /// contract and must never be parsed. Use <see cref="ExecutionId"/> as the durable
    /// idempotency key, and <see cref="RollbackId"/> to identify an inverse execution.
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
    /// Gets the durable identity of this dispatch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a forward execution this is the forward execution id the saga pinned in its
    /// dispatch claim; for a compensation execution it is the rollback id. It is the
    /// supported idempotency key: a redelivery of the same command carries the same value,
    /// so a step that produces external effects can key those effects on it safely.
    /// </para>
    /// <para>
    /// Generated derived-runtime handlers always set this explicitly from the dispatched
    /// command. <see cref="Create(System.Guid, string, string)"/> mints a fresh value for
    /// the legacy non-derived path, where no durable dispatch identity exists.
    /// </para>
    /// </remarks>
    public required Guid ExecutionId { get; init; }

    /// <summary>
    /// Gets a value indicating whether this execution is compensating a completed
    /// forward step.
    /// </summary>
    /// <remarks>
    /// The value is derived from <see cref="RollbackId"/>: an execution is a compensation
    /// exactly when it carries a rollback identifier. Forward execution is therefore always
    /// <see langword="false"/>, and the two properties cannot disagree.
    /// </remarks>
    public bool IsCompensation => RollbackId.HasValue;

    /// <summary>
    /// Gets the stable identifier for this rollback execution, or <see langword="null"/>
    /// during ordinary forward execution.
    /// </summary>
    /// <remarks>
    /// Retries and redeliveries of the same inverse execution carry the same value.
    /// Setting it is what makes <see cref="IsCompensation"/> <see langword="true"/>, and it
    /// is the value surfaced as <see cref="ExecutionId"/> for an inverse execution.
    /// </remarks>
    public Guid? RollbackId { get; init; }

    /// <summary>
    /// Creates a new step context with auto-generated correlation ID and timestamp.
    /// </summary>
    /// <param name="workflowId">The parent workflow identifier.</param>
    /// <param name="stepName">The step name.</param>
    /// <param name="currentPhase">The current workflow phase.</param>
    /// <returns>A new step context.</returns>
    /// <remarks>
    /// The minted <see cref="ExecutionId"/> is fresh per call, so it is a durable
    /// idempotency key only on the legacy non-derived path this factory serves. Generated
    /// derived-runtime handlers overwrite it with the dispatched command's execution
    /// identity before the step runs.
    /// </remarks>
    public static StepContext Create(Guid workflowId, string stepName, string currentPhase)
    {
        return new StepContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            ExecutionId = Guid.NewGuid(),
            WorkflowId = workflowId,
            StepName = stepName,
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = currentPhase,
        };
    }
}
