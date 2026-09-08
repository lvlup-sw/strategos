// =============================================================================
// <copyright file="ApprovalEscalationBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Fluent builder for constructing approval escalation handlers.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// This builder creates <see cref="ApprovalEscalationDefinition"/> instances with:
/// <list type="bullet">
///   <item><description>Escalation workflow steps (logging, notification)</description></item>
///   <item><description>Nested approval requests (escalate to supervisor)</description></item>
///   <item><description>Terminal flag for workflow termination</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class ApprovalEscalationBuilder<TState> : IApprovalEscalationBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly List<StepDefinition> _steps = [];
    private readonly List<ApprovalDefinition> _nestedApprovals = [];
    private bool _isTerminal;

    /// <inheritdoc/>
    public IReadOnlyList<StepDefinition> Steps => _steps;

    /// <inheritdoc/>
    public IReadOnlyList<ApprovalDefinition> NestedApprovals => _nestedApprovals;

    /// <inheritdoc/>
    public bool IsTerminal => _isTerminal;

    /// <inheritdoc/>
    public IApprovalEscalationBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        var stepDefinition = StepDefinition.Create(typeof(TStep));
        _steps.Add(stepDefinition);
        return this;
    }

    /// <inheritdoc/>
    public IApprovalEscalationBuilder<TState> Then<TStep>(
        Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        var configuration = new StepConfigurationBuilder<TState>();
        configure(configuration);

        _steps.Add(configuration.ApplyTo(StepDefinition.Create(typeof(TStep))));
        return this;
    }

    /// <inheritdoc/>
    public IApprovalEscalationBuilder<TState> EscalateTo<TNextApprover>(
        Action<IApprovalBuilder<TState, TNextApprover>> configure)
        where TNextApprover : class
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        // Use a placeholder step ID for nested approvals
        var nestedBuilder = new ApprovalBuilder<TState, TNextApprover>("escalation-nested");
        configure(nestedBuilder);
        _nestedApprovals.Add(nestedBuilder.Build());
        return this;
    }

    /// <inheritdoc/>
    public void Complete()
    {
        _isTerminal = true;
    }

    /// <inheritdoc/>
    public ApprovalEscalationDefinition Build()
    {
        return ApprovalEscalationDefinition.Create(
            _steps.ToList(),
            _nestedApprovals.ToList(),
            _isTerminal);
    }
}
