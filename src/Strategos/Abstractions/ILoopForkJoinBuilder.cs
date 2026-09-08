// =============================================================================
// <copyright file="ILoopForkJoinBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Intermediate builder for completing a fork with a join step inside a loop body.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// This builder is returned by <see cref="ILoopBuilder{TState}.Fork"/> and
/// requires a join step to be specified before the loop body can continue:
/// <code>
/// .RepeatUntil(state => state.Complete, "ProcessTargets", loop => loop
///     .Then&lt;SelectNextTarget&gt;()
///     .Fork(
///         path => path.Then&lt;AnalyzeTechnical&gt;(),
///         path => path.Then&lt;AnalyzeFundamental&gt;())
///     .Join&lt;AggregateResults&gt;()
///     .Then&lt;ExecuteTrade&gt;())
/// </code>
/// </para>
/// </remarks>
public interface ILoopForkJoinBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Specifies the join step that merges results from all fork paths.
    /// </summary>
    /// <typeparam name="TJoinStep">The join step implementation type.</typeparam>
    /// <returns>The loop builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The join step executes only after all fork paths reach a terminal status
    /// (Success, Failed, or FailedWithRecovery).
    /// </para>
    /// <para>
    /// The join step receives a <see cref="Steps.ForkContext{TState}"/> containing
    /// the results from each path, including their statuses and state values.
    /// </para>
    /// <para>
    /// After joining, the loop body continues and additional steps can be added
    /// using <see cref="ILoopBuilder{TState}.Then{TStep}"/>.
    /// </para>
    /// </remarks>
    ILoopBuilder<TState> Join<TJoinStep>()
        where TJoinStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Specifies and configures the join step that merges results from all fork paths.
    /// </summary>
    /// <typeparam name="TJoinStep">The join step implementation type.</typeparam>
    /// <param name="configure">Action to configure the join-step occurrence.</param>
    /// <returns>The loop builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configure"/> is null.
    /// </exception>
    /// <remarks>
    /// The configuration is occurrence-scoped and supports the same resilience,
    /// validation, context, and ontology-action identity declarations as other
    /// class-based loop steps.
    /// </remarks>
    ILoopBuilder<TState> Join<TJoinStep>(Action<IStepConfiguration<TState>> configure)
        where TJoinStep : class, IWorkflowStep<TState>;
}
