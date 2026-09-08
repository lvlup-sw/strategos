// =============================================================================
// <copyright file="IForkJoinBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Intermediate builder for completing a fork with a join step.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// This builder is returned by <see cref="IWorkflowBuilder{TState}.Fork"/> and
/// requires a join step to be specified before the workflow can continue:
/// <code>
/// .Fork(
///     path => path.Then&lt;ProcessPayment&gt;(),
///     path => path.Then&lt;ReserveInventory&gt;())
/// .Join&lt;SynthesizeResults&gt;()
/// .Then&lt;SendConfirmation&gt;()
/// </code>
/// </para>
/// </remarks>
public interface IForkJoinBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Specifies the join step that merges results from all fork paths.
    /// </summary>
    /// <typeparam name="TJoinStep">The join step implementation type.</typeparam>
    /// <returns>The workflow builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The join step executes only after all fork paths reach a terminal status
    /// (Success, Failed, or FailedWithRecovery).
    /// </para>
    /// <para>
    /// The join step receives a <see cref="Steps.ForkContext{TState}"/> containing
    /// the results from each path, including their statuses and state values.
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> Join<TJoinStep>()
        where TJoinStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Specifies and configures the join step that merges results from all fork paths.
    /// </summary>
    /// <typeparam name="TJoinStep">The join step implementation type.</typeparam>
    /// <param name="configure">Action to configure the join-step occurrence.</param>
    /// <returns>The workflow builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is null.
    /// </exception>
    /// <remarks>
    /// The configuration is occurrence-scoped and supports the same resilience,
    /// validation, context, and ontology-action identity declarations as other
    /// class-based workflow steps.
    /// </remarks>
    IWorkflowBuilder<TState> Join<TJoinStep>(Action<IStepConfiguration<TState>> configure)
        where TJoinStep : class, IWorkflowStep<TState>;
}
