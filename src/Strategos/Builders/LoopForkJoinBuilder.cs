// =============================================================================
// <copyright file="LoopForkJoinBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the fork/join builder for loops.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class LoopForkJoinBuilder<TState> : ILoopForkJoinBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly LoopBuilder<TState> _loopBuilder;
    private readonly ForkPointDefinition _pendingForkPoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopForkJoinBuilder{TState}"/> class.
    /// </summary>
    /// <param name="loopBuilder">The parent loop builder.</param>
    /// <param name="pendingForkPoint">The fork point awaiting a join step.</param>
    internal LoopForkJoinBuilder(
        LoopBuilder<TState> loopBuilder,
        ForkPointDefinition pendingForkPoint)
    {
        _loopBuilder = loopBuilder;
        _pendingForkPoint = pendingForkPoint;
    }

    /// <inheritdoc/>
    public ILoopBuilder<TState> Join<TJoinStep>()
        where TJoinStep : class, IWorkflowStep<TState>
    {
        return CompleteJoin(StepDefinition.Create(typeof(TJoinStep)));
    }

    /// <inheritdoc/>
    public ILoopBuilder<TState> Join<TJoinStep>(Action<IStepConfiguration<TState>> configure)
        where TJoinStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        var configuration = new StepConfigurationBuilder<TState>();
        configure(configuration);
        return CompleteJoin(configuration.ApplyTo(StepDefinition.Create(typeof(TJoinStep))));
    }

    private ILoopBuilder<TState> CompleteJoin(StepDefinition joinStep)
    {
        // Complete the fork point with the join step ID
        var completedForkPoint = _pendingForkPoint with { JoinStepId = joinStep.StepId };

        // Register the fork point and join step with the loop builder
        _loopBuilder.CompleteForkJoin(completedForkPoint, joinStep);

        return _loopBuilder;
    }
}
