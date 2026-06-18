// =============================================================================
// <copyright file="IStepConfiguration.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Linq.Expressions;

namespace Strategos.Builders;

/// <summary>
/// Fluent builder interface for configuring workflow step behavior.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// Step configuration allows defining:
/// <list type="bullet">
///   <item><description>Confidence thresholds for agent steps</description></item>
///   <item><description>Low confidence handlers for alternative paths</description></item>
///   <item><description>Compensation steps for rollback</description></item>
///   <item><description>Retry policies for transient failures</description></item>
///   <item><description>Execution timeouts</description></item>
///   <item><description>State validation guards</description></item>
/// </list>
/// </para>
/// <para>
/// Example usage:
/// <code>
/// .Then&lt;AssessClaim&gt;(step => step
///     .RequireConfidence(0.85)
///     .OnLowConfidence(alt => alt.Then&lt;HumanReview&gt;())
///     .Compensate&lt;RollbackAssessment&gt;()
///     .WithRetry(3, TimeSpan.FromSeconds(5))
///     .WithTimeout(TimeSpan.FromMinutes(5)))
/// </code>
/// </para>
/// </remarks>
public interface IStepConfiguration<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Sets the minimum confidence threshold for automatic continuation.
    /// </summary>
    /// <param name="threshold">The confidence threshold (0.0 to 1.0).</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="threshold"/> is less than 0.0 or greater than 1.0.
    /// </exception>
    IStepConfiguration<TState> RequireConfidence(double threshold);

    /// <summary>
    /// Configures the handler path for when confidence is below threshold.
    /// </summary>
    /// <param name="handler">Action to configure the handler path.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
    /// <remarks>
    /// OnLowConfidence should be called after RequireConfidence.
    /// </remarks>
    IStepConfiguration<TState> OnLowConfidence(Action<IBranchBuilder<TState>> handler);

    /// <summary>
    /// Sets the compensation step type for rollback on failure.
    /// </summary>
    /// <typeparam name="TCompensation">The compensation step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    IStepConfiguration<TState> Compensate<TCompensation>()
        where TCompensation : class, IWorkflowStep<TState>;

    /// <summary>
    /// Configures retry behavior for transient failures.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxAttempts"/> is less than 1.
    /// </exception>
    IStepConfiguration<TState> WithRetry(int maxAttempts);

    /// <summary>
    /// Configures retry behavior with custom initial delay.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts.</param>
    /// <param name="initialDelay">Initial delay between retries.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxAttempts"/> is less than 1.
    /// </exception>
    IStepConfiguration<TState> WithRetry(int maxAttempts, TimeSpan initialDelay);

    /// <summary>
    /// Sets the execution timeout for this step.
    /// </summary>
    /// <param name="timeout">The maximum execution time.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Lowered into the generated saga as a Wolverine <c>TimeoutMessage</c> deadline
    /// <em>race</em>: the timeout is scheduled when the step starts, and the saga routes to
    /// its failure path only if the step has not already completed (the completion event
    /// won the race). This is a saga-level deadline, not hard cancellation of an in-flight
    /// step — the step's <see cref="System.Threading.CancellationToken"/> is provided for
    /// cooperative cancellation.
    /// </para>
    /// <para>
    /// <b>Durability:</b> cross-restart timeout delivery requires the Marten transactional
    /// outbox (<c>AddMarten(…).IntegrateWithWolverine()</c>); without it, scheduled timeout
    /// delivery is in-memory only.
    /// </para>
    /// </remarks>
    IStepConfiguration<TState> WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Configures a state validation guard that runs before step execution.
    /// </summary>
    /// <param name="predicate">Expression that must evaluate to true for execution to proceed.</param>
    /// <param name="errorMessage">Error message when validation fails.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="predicate"/> or <paramref name="errorMessage"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Validation guards implement the Guard-Then-Dispatch pattern. When the guard fails:
    /// <list type="bullet">
    ///   <item><description>No exception is thrown (avoids useless Wolverine retries)</description></item>
    ///   <item><description>Workflow transitions to ValidationFailed phase</description></item>
    ///   <item><description>ValidationFailed event is emitted for audit</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// .Then&lt;ProcessPayment&gt;(step => step
    ///     .ValidateState(state => state.Order.Total > 0, "Order total must be positive"))
    /// </code>
    /// </para>
    /// </remarks>
    IStepConfiguration<TState> ValidateState(
        Expression<Func<TState, bool>> predicate,
        string errorMessage);

    /// <summary>
    /// Configures RAG context injection for this step.
    /// </summary>
    /// <param name="configure">Action to configure context sources.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Context configuration enables per-step RAG context assembly from multiple sources:
    /// <list type="bullet">
    ///   <item><description>State properties: Inject workflow state values</description></item>
    ///   <item><description>Retrieval: Semantic search against RAG collections</description></item>
    ///   <item><description>Literals: Static context strings</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// .Then&lt;AnalyzeDocument&gt;(step => step
    ///     .WithContext(ctx => ctx
    ///         .FromState(s => s.DocumentSummary)
    ///         .FromRetrieval&lt;ResearchLibrary&gt;(r => r
    ///             .Query(s => s.AnalysisQuery)
    ///             .TopK(5)
    ///             .MinRelevance(0.8m))
    ///         .FromLiteral("Always cite sources.")))
    /// </code>
    /// </para>
    /// </remarks>
    IStepConfiguration<TState> WithContext(Action<IContextBuilder<TState>> configure);
}
