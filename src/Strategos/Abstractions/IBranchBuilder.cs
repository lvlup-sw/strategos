// =============================================================================
// <copyright file="IBranchBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Fluent builder interface for constructing branch paths within a workflow.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// Branch builders allow defining steps within a branch path:
/// <code>
/// .Branch(state => state.Type,
///     BranchCase.When(Type.A, path => path
///         .Then&lt;StepA&gt;()
///         .Then&lt;StepB&gt;()),
///     BranchCase.Otherwise(path => path
///         .Then&lt;DefaultStep&gt;()))
/// </code>
/// </para>
/// <para>
/// By default, branches rejoin at the next step after the Branch() call.
/// Use <see cref="Complete"/> to terminate a branch without rejoining.
/// </para>
/// </remarks>
public interface IBranchBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Adds a step to this branch path.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    IBranchBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a step to this branch path with an instance name.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="instanceName">
    /// The instance name for this step. Enables reusing the same step type
    /// in different branch paths with distinct identities.
    /// </param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Instance names allow distinguishing same step types in different branches:
    /// <code>
    /// .Branch(state => state.Type,
    ///     BranchCase.When(Type.A, path => path.Then&lt;ProcessStep&gt;("ProcessTypeA")),
    ///     BranchCase.When(Type.B, path => path.Then&lt;ProcessStep&gt;("ProcessTypeB")))
    /// </code>
    /// </para>
    /// </remarks>
    IBranchBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a step to this branch path with configuration.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="configure">Action to configure the step.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Brings branch paths to parity with the top-level <see cref="IWorkflowBuilder{TState}"/>,
    /// loop-body <see cref="ILoopBuilder{TState}"/>, and fork-path <see cref="IForkPathBuilder{TState}"/>
    /// sequencing contexts, which already expose this overload. The configure lambda carries the
    /// full <see cref="IStepConfiguration{TState}"/> surface — <c>.WithRetry</c>, <c>.WithTimeout</c>,
    /// <c>.Compensate</c>, <c>.RequireConfidence</c>, <c>.OnLowConfidence</c>, <c>.ValidateState</c>,
    /// and <c>.WithContext</c>:
    /// <code>
    /// .Branch(state => state.Outcome,
    ///     BranchCase.When(Outcome.Escalate, path => path
    ///         .Then&lt;EscalateClaim&gt;(step => step
    ///             .WithRetry(2, TimeSpan.FromSeconds(5))
    ///             .WithTimeout(TimeSpan.FromMinutes(1)))),
    ///     BranchCase.Otherwise(path => path.Then&lt;AutoApprove&gt;()))
    /// </code>
    /// </para>
    /// <para>
    /// Enforcement parity is partial today: the configuration is captured in the workflow definition
    /// and threaded into the generator's step IR, but the source generator does not yet emit Wolverine
    /// retry/timeout/compensation for branch-path steps (tracked by issue #135).
    /// </para>
    /// </remarks>
    IBranchBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Marks this branch as terminal (does not rejoin the main flow).
    /// </summary>
    /// <remarks>
    /// When a branch calls Complete(), it terminates independently
    /// and does not transition to the step after the Branch() call.
    /// </remarks>
    void Complete();

    /// <summary>
    /// Adds an approval gate to this branch path.
    /// </summary>
    /// <typeparam name="TApprover">The approver type (marker class for routing).</typeparam>
    /// <param name="configure">Action to configure the approval options.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Usage within a branch:
    /// <code>
    /// BranchCase.When(Outcome.NeedsEscalation, path => path
    ///     .AwaitApproval&lt;HumanEscalationApprover&gt;(approval => approval
    ///         .WithTimeout(TimeSpan.FromHours(24))
    ///         .OnTimeout(timeout => timeout.Then&lt;AutoFailStep&gt;())
    ///         .OnRejection(rejection => rejection.Then&lt;TerminateStep&gt;()))
    ///     .Then&lt;CompleteStep&gt;()
    ///     .Complete())
    /// </code>
    /// </para>
    /// </remarks>
    IBranchBuilder<TState> AwaitApproval<TApprover>(
        Action<IApprovalBuilder<TState, TApprover>> configure)
        where TApprover : class;
}
