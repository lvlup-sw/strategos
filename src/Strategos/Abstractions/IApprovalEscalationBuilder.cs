// =============================================================================
// <copyright file="IApprovalEscalationBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Fluent builder interface for constructing approval escalation handlers.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// Escalation builders allow defining what happens when an approval times out:
/// <code>
/// .OnTimeout(escalation => escalation
///     .Then&lt;LogEscalation&gt;()
///     .EscalateTo&lt;DirectorApprover&gt;(nested => nested
///         .WithContext("Escalated due to manager timeout")
///         .WithTimeout(TimeSpan.FromHours(2))))
/// </code>
/// </para>
/// <para>
/// Escalation handlers can:
/// <list type="bullet">
///   <item><description>Execute workflow steps (notification, logging, etc.)</description></item>
///   <item><description>Escalate to a higher authority via nested approvals</description></item>
///   <item><description>Terminate the workflow if no further escalation is possible</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IApprovalEscalationBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Gets the steps configured for this escalation handler.
    /// </summary>
    IReadOnlyList<StepDefinition> Steps { get; }

    /// <summary>
    /// Gets the nested approval definitions for escalation.
    /// </summary>
    IReadOnlyList<ApprovalDefinition> NestedApprovals { get; }

    /// <summary>
    /// Gets a value indicating whether this escalation terminates the workflow.
    /// </summary>
    bool IsTerminal { get; }

    /// <summary>
    /// Adds a step to the escalation path.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    IApprovalEscalationBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a configured step to the escalation path.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="configure">Action to configure the step occurrence.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is null.
    /// </exception>
    IApprovalEscalationBuilder<TState> Then<TStep>(
        Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Escalates to another approver type with nested configuration.
    /// </summary>
    /// <typeparam name="TNextApprover">The marker type for the next approver level.</typeparam>
    /// <param name="configure">Action to configure the nested approval.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is null.
    /// </exception>
    /// <remarks>
    /// Enables chained escalation patterns like:
    /// timeout → notify → escalate to supervisor → timeout → escalate to director.
    /// </remarks>
    IApprovalEscalationBuilder<TState> EscalateTo<TNextApprover>(
        Action<IApprovalBuilder<TState, TNextApprover>> configure)
        where TNextApprover : class;

    /// <summary>
    /// Marks this escalation as terminal (workflow fails on timeout with no escalation).
    /// </summary>
    /// <remarks>
    /// When Complete() is called, the workflow will terminate if escalation
    /// is triggered and all escalation paths have been exhausted.
    /// </remarks>
    void Complete();

    /// <summary>
    /// Builds the immutable escalation definition.
    /// </summary>
    /// <returns>The configured escalation definition.</returns>
    ApprovalEscalationDefinition Build();
}
