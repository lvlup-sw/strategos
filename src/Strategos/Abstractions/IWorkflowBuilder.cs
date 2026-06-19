// =============================================================================
// <copyright file="IWorkflowBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Steps;

namespace Strategos.Builders;

/// <summary>
/// Fluent builder interface for constructing workflow definitions.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// The workflow builder provides a fluent DSL for defining workflows:
/// <code>
/// var workflow = Workflow&lt;OrderState&gt;
///     .Create("process-order")
///     .StartWith&lt;ValidateOrder&gt;()
///     .Then&lt;ProcessPayment&gt;()
///     .Finally&lt;SendConfirmation&gt;();
/// </code>
/// </para>
/// <para>
/// Methods are added incrementally as the DSL evolves.
/// </para>
/// <para>
/// <b>Cross-product API stability (#51).</b> This interface is one of the 7
/// <c>Strategos.Builders</c> builder interfaces that form a cross-product
/// contract mirrored by exarchos's <c>strategos-api-mirror.test.ts</c>. Its
/// public surface is baselined in
/// <c>src/Strategos/PublicAPI/PublicAPI.Shipped.txt</c> and enforced by
/// <c>Microsoft.CodeAnalysis.PublicApiAnalyzers</c>. A signature change that is
/// not declared in the baseline fails the build (RS0016/RS0017) with:
/// <i>"Update PublicAPI.Unshipped.txt and add a CHANGELOG entry under
/// Cross-product breaking changes."</i> See <c>CONTRIBUTING.md</c> §
/// "Cross-product API stability".
/// </para>
/// </remarks>
public interface IWorkflowBuilder<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Sets the entry step of the workflow.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has already been called.</exception>
    IWorkflowBuilder<TState> StartWith<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Sets the entry step of the workflow with an instance name.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="instanceName">
    /// The instance name for this step. Enables reusing the same step type
    /// in different contexts with distinct identities.
    /// </param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has already been called.</exception>
    /// <remarks>
    /// <para>
    /// Instance names allow the same step type to be used multiple times
    /// in a workflow with distinct identities for phase tracking and
    /// duplicate detection. For example:
    /// <code>
    /// .StartWith&lt;ValidateStep&gt;("InitialValidation")
    /// </code>
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> StartWith<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Sets the entry step of the workflow with configuration.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="configure">Action to configure the step.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has already been called.</exception>
    /// <remarks>
    /// <para>
    /// Step configuration allows the entry step to declare per-step resilience, e.g.:
    /// <code>
    /// .StartWith&lt;ValidateOrder&gt;(step => step
    ///     .RequireConfidence(0.85)
    ///     .Compensate&lt;RollbackValidation&gt;()
    ///     .WithRetry(3))
    /// </code>
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> StartWith<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Sets the entry step of the workflow with an instance name and configuration.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="instanceName">
    /// The instance name for this step. Enables reusing the same step type
    /// in different contexts with distinct identities.
    /// </param>
    /// <param name="configure">Action to configure the step.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="instanceName"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has already been called.</exception>
    /// <remarks>
    /// <para>
    /// Combines the named-instance and configure overloads so the entry step can carry
    /// both a distinct identity and per-step resilience, e.g.:
    /// <code>
    /// .StartWith&lt;ValidateOrder&gt;("InitialValidation", step => step.WithRetry(3))
    /// </code>
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> StartWith<TStep>(string instanceName, Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a sequential step to the workflow.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    IWorkflowBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a sequential step to the workflow with an instance name.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="instanceName">
    /// The instance name for this step. Enables reusing the same step type
    /// in different contexts with distinct identities.
    /// </param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Instance names allow the same step type to be used multiple times
    /// in a workflow with distinct identities for phase tracking and
    /// duplicate detection. For example:
    /// <code>
    /// .Then&lt;AnalyzeStep&gt;("TechnicalAnalysis")
    /// .Then&lt;AnalyzeStep&gt;("FundamentalAnalysis")
    /// </code>
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a sequential step to the workflow with configuration.
    /// </summary>
    /// <typeparam name="TStep">The step implementation type.</typeparam>
    /// <param name="configure">Action to configure the step.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Step configuration allows defining behavior such as:
    /// <code>
    /// .Then&lt;AssessClaim&gt;(step => step
    ///     .RequireConfidence(0.85)
    ///     .Compensate&lt;RollbackAssessment&gt;()
    ///     .WithRetry(3))
    /// </code>
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a lambda step to the workflow with an inline implementation.
    /// </summary>
    /// <param name="stepName">The name of the step (used for phase enum generation).</param>
    /// <param name="stepDelegate">The lambda delegate implementing the step logic.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stepName"/> or <paramref name="stepDelegate"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="stepName"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Lambda steps allow inline step definitions without creating separate classes:
    /// <code>
    /// .Then("ProcessData", async (state, context, ct) =>
    /// {
    ///     var updatedState = state with { Processed = true };
    ///     return StepResult&lt;MyState&gt;.Success(updatedState);
    /// })
    /// </code>
    /// </para>
    /// <para>
    /// Lambda steps are useful for simple transformations and one-off logic.
    /// For complex or reusable steps, prefer class-based implementations.
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> Then(string stepName, StepDelegate<TState> stepDelegate);

    /// <summary>
    /// Sets the terminal step and builds the workflow definition.
    /// </summary>
    /// <typeparam name="TStep">The terminal step implementation type.</typeparam>
    /// <returns>The completed workflow definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    WorkflowDefinition<TState> Finally<TStep>()
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Sets the terminal step with configuration and builds the workflow definition.
    /// </summary>
    /// <typeparam name="TStep">The terminal step implementation type.</typeparam>
    /// <param name="configure">Action to configure the terminal step.</param>
    /// <returns>The completed workflow definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Step configuration allows the terminal step to declare per-step resilience, e.g.:
    /// <code>
    /// .Finally&lt;SendConfirmation&gt;(step => step
    ///     .WithTimeout(TimeSpan.FromSeconds(5))
    ///     .WithRetry(3))
    /// </code>
    /// </para>
    /// </remarks>
    WorkflowDefinition<TState> Finally<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>;

    /// <summary>
    /// Adds a conditional branch based on a discriminator value.
    /// </summary>
    /// <typeparam name="TDiscriminator">The discriminator type.</typeparam>
    /// <param name="discriminator">A function to extract the discriminator value from state.</param>
    /// <param name="cases">The branch cases mapping discriminator values to paths.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="discriminator"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cases"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Branches allow conditional routing based on state values:
    /// <code>
    /// .Branch(state => state.ClaimType,
    ///     BranchCase&lt;ClaimState, ClaimType&gt;.When(ClaimType.Auto, path => path.Then&lt;AutoProcess&gt;()),
    ///     BranchCase&lt;ClaimState, ClaimType&gt;.Otherwise(path => path.Then&lt;DefaultProcess&gt;()))
    /// </code>
    /// </para>
    /// <para>
    /// By default, branches rejoin at the next step. Use <see cref="IBranchBuilder{TState}.Complete"/>
    /// to terminate a branch without rejoining.
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> Branch<TDiscriminator>(
        Func<TState, TDiscriminator> discriminator,
        params BranchCase<TState, TDiscriminator>[] cases);

    /// <summary>
    /// Adds a repeat-until loop to the workflow.
    /// </summary>
    /// <param name="condition">The condition that must become true to exit the loop.</param>
    /// <param name="loopName">The loop name (used for phase enum prefixing).</param>
    /// <param name="body">Action to configure the loop body steps.</param>
    /// <param name="maxIterations">Maximum iterations allowed (prevents infinite loops).</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="condition"/>, <paramref name="loopName"/>, or
    /// <paramref name="body"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxIterations"/> is less than 1.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// RepeatUntil allows iteration over a set of steps until a condition is met:
    /// <code>
    /// .RepeatUntil(
    ///     condition: state => state.QualityScore >= 0.9m,
    ///     loopName: "Refinement",
    ///     body: loop => loop
    ///         .Then&lt;CritiqueStep&gt;()
    ///         .Then&lt;RefineStep&gt;(),
    ///     maxIterations: 5)
    /// </code>
    /// </para>
    /// <para>
    /// Loop body steps are prefixed with the loop name for phase enum generation
    /// (e.g., Refinement_Critique, Refinement_Refine).
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> RepeatUntil(
        Func<TState, bool> condition,
        string loopName,
        Action<ILoopBuilder<TState>> body,
        int maxIterations = 100);

    /// <summary>
    /// Defines a workflow-level failure handler for error recovery.
    /// </summary>
    /// <param name="handler">Action to configure the failure handler steps.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if StartWith has not been called or OnFailure has already been called.
    /// </exception>
    /// <remarks>
    /// <para>
    /// OnFailure defines a global error handler for the workflow:
    /// <code>
    /// .OnFailure(failure => failure
    ///     .Then&lt;LogFailure&gt;()
    ///     .Then&lt;NotifyAdmin&gt;()
    ///     .Complete())
    /// </code>
    /// </para>
    /// <para>
    /// By default, failure handlers rejoin the main flow after recovery.
    /// Use <see cref="IFailureBuilder{TState}.Complete"/> to terminate on failure.
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> OnFailure(Action<IFailureBuilder<TState>> handler);

    /// <summary>
    /// Adds a human-in-the-loop approval checkpoint to the workflow.
    /// </summary>
    /// <typeparam name="TApprover">
    /// The marker type identifying the approver role (e.g., ManagerApprover, DirectorApprover).
    /// </typeparam>
    /// <param name="configure">Action to configure the approval checkpoint.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// AwaitApproval creates a pause point where human approval is required:
    /// <code>
    /// .AwaitApproval&lt;ManagerApprover&gt;(approval => approval
    ///     .WithContext("Please review this request")
    ///     .WithTimeout(TimeSpan.FromHours(4))
    ///     .WithOption("approve", "Approve", "Approve this request", isDefault: true)
    ///     .WithOption("reject", "Reject", "Reject this request")
    ///     .OnTimeout(escalation => escalation
    ///         .EscalateTo&lt;DirectorApprover&gt;(nested => nested
    ///             .WithContext("Escalated due to timeout")))
    ///     .OnRejection(rejection => rejection
    ///         .Then&lt;NotifyRequester&gt;()
    ///         .Complete()))
    /// </code>
    /// </para>
    /// <para>
    /// The <typeparamref name="TApprover"/> type parameter serves as a marker for:
    /// <list type="bullet">
    ///   <item><description>Type-safe routing of approval requests</description></item>
    ///   <item><description>Dependency injection of approver-specific handlers</description></item>
    ///   <item><description>Configuration binding per approver type</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    IWorkflowBuilder<TState> AwaitApproval<TApprover>(
        Action<IApprovalBuilder<TState, TApprover>> configure)
        where TApprover : class;

    /// <summary>
    /// Adds a fork point that splits execution into parallel paths.
    /// </summary>
    /// <param name="paths">The path builders for each parallel branch (minimum 2 required).</param>
    /// <returns>A fork/join builder that requires a join step to complete the fork.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when fewer than 2 paths are provided.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown if StartWith has not been called.</exception>
    /// <remarks>
    /// <para>
    /// Fork creates parallel execution paths that run concurrently:
    /// <code>
    /// .Fork(
    ///     path => path.Then&lt;ProcessPayment&gt;()
    ///                 .OnFailure(f => f.Then&lt;RefundPayment&gt;()),
    ///     path => path.Then&lt;ReserveInventory&gt;())
    /// .Join&lt;SynthesizeResults&gt;()
    /// </code>
    /// </para>
    /// <para>
    /// Each path can have its own failure handler via <see cref="IForkPathBuilder{TState}.OnFailure"/>.
    /// The join step executes only after all paths reach a terminal status.
    /// </para>
    /// </remarks>
    IForkJoinBuilder<TState> Fork(params Action<IForkPathBuilder<TState>>[] paths);
}
