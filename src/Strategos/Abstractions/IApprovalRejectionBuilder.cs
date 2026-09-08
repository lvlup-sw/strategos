// =============================================================================
// <copyright file="IApprovalRejectionBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Fluent builder interface for constructing approval rejection handlers.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// Rejection builders allow defining what happens when an approver rejects:
/// <code>
/// .OnRejection(rejection => rejection
///     .Then&lt;LogRejection&gt;()
///     .Then&lt;NotifyRequester&gt;()
///     .Complete())
/// </code>
/// </para>
/// <para>
/// Rejection handlers can:
/// <list type="bullet">
///   <item><description>Execute cleanup or notification steps</description></item>
///   <item><description>Terminate the workflow (Complete())</description></item>
///   <item><description>Rejoin the main flow for alternative processing</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IApprovalRejectionBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Gets the steps configured for this rejection handler.
    /// </summary>
    IReadOnlyList<StepDefinition> Steps { get; }

    /// <summary>
    /// Gets a value indicating whether this rejection terminates the workflow.
    /// </summary>
    bool IsTerminal { get; }

    /// <summary>
    /// Adds a step to the rejection path.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    IApprovalRejectionBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a configured step to the rejection path.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="configure">Action to configure the step occurrence.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is null.
    /// </exception>
    IApprovalRejectionBuilder<TState> Then<TStep>(
        Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Marks this rejection handler as terminal (workflow fails on rejection).
    /// </summary>
    /// <remarks>
    /// When Complete() is called, the workflow will terminate after
    /// executing the rejection steps instead of rejoining the main flow.
    /// </remarks>
    void Complete();

    /// <summary>
    /// Builds the immutable rejection definition.
    /// </summary>
    /// <returns>The configured rejection definition.</returns>
    ApprovalRejectionDefinition Build();
}
