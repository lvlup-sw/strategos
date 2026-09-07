// =============================================================================
// <copyright file="ApprovalRejectionBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Fluent builder for constructing approval rejection handlers.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// This builder creates <see cref="ApprovalRejectionDefinition"/> instances with:
/// <list type="bullet">
///   <item><description>Rejection workflow steps (cleanup, notification)</description></item>
///   <item><description>Terminal flag for workflow termination</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class ApprovalRejectionBuilder<TState> : IApprovalRejectionBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly List<StepDefinition> _steps = [];
    private bool _isTerminal;

    /// <inheritdoc/>
    public IReadOnlyList<StepDefinition> Steps => _steps;

    /// <inheritdoc/>
    public bool IsTerminal => _isTerminal;

    /// <inheritdoc/>
    public IApprovalRejectionBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        var stepDefinition = StepDefinition.Create(typeof(TStep));
        _steps.Add(stepDefinition);
        return this;
    }

    /// <inheritdoc/>
    public IApprovalRejectionBuilder<TState> Then<TStep>(
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
    public void Complete()
    {
        _isTerminal = true;
    }

    /// <inheritdoc/>
    public ApprovalRejectionDefinition Build()
    {
        return ApprovalRejectionDefinition.Create(
            _steps.ToList(),
            _isTerminal);
    }
}
