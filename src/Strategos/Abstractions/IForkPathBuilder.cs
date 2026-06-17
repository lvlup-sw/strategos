// =============================================================================
// <copyright file="IForkPathBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Fluent builder interface for constructing a single path within a fork.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// Fork path builders allow defining steps for parallel execution:
/// <code>
/// .Fork(
///     path => path.Then&lt;ProcessPayment&gt;()
///                 .OnFailure(f => f.Then&lt;RefundPayment&gt;()),
///     path => path.Then&lt;ReserveInventory&gt;())
/// </code>
/// </para>
/// <para>
/// Each fork path can optionally have a failure handler that executes
/// if any step in the path fails.
/// </para>
/// </remarks>
public interface IForkPathBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Adds a step to this fork path.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    IForkPathBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a step to this fork path with an instance name.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="instanceName">
    /// The instance name for this step. Enables reusing the same step type
    /// in different fork paths with distinct identities.
    /// </param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Instance names are essential for fork paths since the same step type
    /// appearing in different fork paths would otherwise cause duplicate
    /// step detection errors:
    /// <code>
    /// .Fork(
    ///     path => path.Then&lt;AnalyzeStep&gt;("TechnicalAnalysis"),
    ///     path => path.Then&lt;AnalyzeStep&gt;("FundamentalAnalysis"))
    /// </code>
    /// </para>
    /// </remarks>
    IForkPathBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a step to this fork path with configuration.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="configure">Action to configure the step.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Brings fork paths to parity with the top-level <see cref="IWorkflowBuilder{TState}"/>
    /// and loop-body <see cref="ILoopBuilder{TState}"/> sequencing contexts, which already
    /// expose this overload. Each branch configures its steps independently; the join waits
    /// for every path to reach a terminal status:
    /// <code>
    /// .Fork(
    ///     path => path.Then&lt;ProcessPayment&gt;(step => step
    ///         .ValidateState(s => s.Amount > 0, "Amount must be positive")
    ///         .WithRetry(3, TimeSpan.FromSeconds(5))
    ///         .Compensate&lt;RefundPayment&gt;()),
    ///     path => path.Then&lt;ReserveInventory&gt;())
    /// </code>
    /// </para>
    /// <para>
    /// Enforcement parity is partial today. <see cref="IStepConfiguration{TState}.ValidateState"/>
    /// is lowered into the generated saga as a Guard-Then-Dispatch guard for fork-path steps,
    /// matching top-level and loop steps. <see cref="IStepConfiguration{TState}.WithRetry(int)"/>,
    /// <see cref="IStepConfiguration{TState}.WithTimeout"/>, and
    /// <see cref="IStepConfiguration{TState}.Compensate{TCompensation}"/> are <b>declared but
    /// not yet enforced</b> — they are captured in the workflow definition and the declarative
    /// export, but the source generator does not yet emit Wolverine retry/timeout/compensation
    /// for any step kind (tracked by issue #135). <see cref="IStepConfiguration{TState}.WithContext"/>
    /// and <see cref="IStepConfiguration{TState}.RequireConfidence"/> /
    /// <see cref="IStepConfiguration{TState}.OnLowConfidence"/> are likewise not yet lowered for
    /// fork-path steps.
    /// </para>
    /// </remarks>
    IForkPathBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Defines a failure handler for this fork path.
    /// </summary>
    /// <param name="handler">Action to configure the failure handler steps.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// When a step in this path fails:
    /// <list type="number">
    ///   <item><description>Execute the failure handler steps</description></item>
    ///   <item><description>If handler calls <c>Complete()</c>: mark path as <see cref="Definitions.ForkPathStatus.Failed"/></description></item>
    ///   <item><description>Otherwise: mark path as <see cref="Definitions.ForkPathStatus.FailedWithRecovery"/></description></item>
    ///   <item><description>Continue to join point</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    IForkPathBuilder<TState> OnFailure(Action<IFailureBuilder<TState>> handler);
}
