// =============================================================================
// <copyright file="TestWorkflowState.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Fixtures;

/// <summary>
/// Processing mode discriminator for branch testing.
/// </summary>
public enum ProcessingMode
{
    /// <summary>
    /// Automatic processing mode.
    /// </summary>
    Auto,

    /// <summary>
    /// Manual processing mode.
    /// </summary>
    Manual,
}

/// <summary>
/// Test implementation of IWorkflowState for unit testing.
/// </summary>
public sealed record TestWorkflowState : IWorkflowState
{
    /// <inheritdoc/>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets or sets test data for state transitions.
    /// </summary>
    public string TestData { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets an order identifier for testing.
    /// </summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the processing mode for branch testing.
    /// </summary>
    public ProcessingMode ProcessingMode { get; init; } = ProcessingMode.Auto;

    /// <summary>
    /// Gets or sets the quality score for loop testing.
    /// </summary>
    public decimal QualityScore { get; init; }

    /// <summary>
    /// Gets or sets the iteration count for loop testing.
    /// </summary>
    public int IterationCount { get; init; }
}
