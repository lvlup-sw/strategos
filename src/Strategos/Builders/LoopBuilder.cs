// =============================================================================
// <copyright file="LoopBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the loop body builder.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class LoopBuilder<TState> : ILoopBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly string _loopId;
    private readonly string _loopName;
    private readonly string? _parentLoopPrefix;
    private readonly List<StepDefinition> _steps = [];
    private readonly List<LoopDefinition> _nestedLoops = [];
    private readonly List<ForkPointDefinition> _forkPoints = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopBuilder{TState}"/> class.
    /// </summary>
    /// <param name="loopId">The loop ID for marking body steps.</param>
    /// <param name="loopName">The loop name for prefixing.</param>
    /// <param name="parentLoopPrefix">The parent loop prefix, if any.</param>
    internal LoopBuilder(string loopId, string loopName = "", string? parentLoopPrefix = null)
    {
        _loopId = loopId;
        _loopName = loopName;
        _parentLoopPrefix = parentLoopPrefix;
    }

    /// <summary>
    /// Gets the steps in this loop body.
    /// </summary>
    internal IReadOnlyList<StepDefinition> Steps => _steps;

    /// <summary>
    /// Gets the nested loop definitions.
    /// </summary>
    internal IReadOnlyList<LoopDefinition> NestedLoops => _nestedLoops;

    /// <summary>
    /// Gets the fork points defined within this loop body.
    /// </summary>
    internal IReadOnlyList<ForkPointDefinition> ForkPoints => _forkPoints;

    /// <summary>
    /// Gets the effective prefix for this loop (including parent prefix).
    /// </summary>
    internal string EffectivePrefix => _parentLoopPrefix is null
        ? _loopName
        : $"{_parentLoopPrefix}_{_loopName}";

    /// <inheritdoc/>
    public ILoopBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        var step = StepDefinition.Create(typeof(TStep))
            .AsLoopBodyStep(_loopId);

        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public ILoopBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        var step = StepDefinition.Create(typeof(TStep), customName: null, instanceName: instanceName)
            .AsLoopBodyStep(_loopId);

        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public ILoopBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        // Build the step configuration
        var configBuilder = new StepConfigurationBuilder<TState>();
        configure(configBuilder);

        // Create step with configuration
        var step = configBuilder.ApplyTo(StepDefinition.Create(typeof(TStep)))
            .AsLoopBodyStep(_loopId);

        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public ILoopBuilder<TState> RepeatUntil(
        Func<TState, bool> condition,
        string loopName,
        Action<ILoopBuilder<TState>> body,
        int maxIterations = 100)
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));
        ArgumentNullException.ThrowIfNull(loopName, nameof(loopName));
        ArgumentNullException.ThrowIfNull(body, nameof(body));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1, nameof(maxIterations));

        // Compute the nested prefix: {ParentPrefix}_{LoopName} or just {LoopName} if no parent
        var nestedPrefix = string.IsNullOrEmpty(EffectivePrefix)
            ? loopName
            : $"{EffectivePrefix}_{loopName}";

        // Create nested loop builder with the computed prefix
        var nestedLoopId = Guid.NewGuid().ToString("N");
        var nestedBuilder = new LoopBuilder<TState>(nestedLoopId, loopName, EffectivePrefix);
        body(nestedBuilder);

        // Get the last step ID for the loop definition (from current steps or use parent)
        var fromStepId = _steps.Count > 0 ? _steps[^1].StepId : _loopId;

        // Create the nested loop definition
        var nestedLoop = LoopDefinition.Create(loopName, fromStepId, maxIterations, nestedBuilder.Steps);

        // Update body steps with the correct ParentLoopId
        var actualNestedLoopId = nestedLoop.LoopId;
        var updatedNestedSteps = nestedBuilder.Steps
            .Select(step => step with { ParentLoopId = actualNestedLoopId })
            .ToList();

        // Add nested loop steps to this builder's steps
        foreach (var step in updatedNestedSteps)
        {
            _steps.Add(step);
        }

        // Track nested loop definition
        var correctedNestedLoop = nestedLoop with { BodySteps = updatedNestedSteps };
        _nestedLoops.Add(correctedNestedLoop);

        // Also add any deeply nested loops
        foreach (var deepNestedLoop in nestedBuilder.NestedLoops)
        {
            _nestedLoops.Add(deepNestedLoop);
        }

        // Also add any fork points from nested loops
        foreach (var nestedForkPoint in nestedBuilder.ForkPoints)
        {
            _forkPoints.Add(nestedForkPoint);
        }

        return this;
    }

    /// <inheritdoc/>
    public ILoopForkJoinBuilder<TState> Fork(params Action<IForkPathBuilder<TState>>[] paths)
    {
        if (paths.Length < 2)
        {
            throw new ArgumentException("Fork must have at least two paths.", nameof(paths));
        }

        var forkPaths = new List<ForkPathDefinition>();

        for (var i = 0; i < paths.Length; i++)
        {
            // Build the fork path
            var pathBuilder = new ForkPathBuilder<TState>();
            paths[i](pathBuilder);

            if (pathBuilder.Steps.Count == 0)
            {
                throw new ArgumentException($"Fork path {i} must have at least one step.", nameof(paths));
            }

            var forkPath = ForkPathDefinition.Create(
                i,
                pathBuilder.Steps,
                pathBuilder.FailureHandler);

            forkPaths.Add(forkPath);

            // Add steps from this fork path to the loop body, marked as loop body steps
            foreach (var step in pathBuilder.Steps)
            {
                _steps.Add(step.AsLoopBodyStep(_loopId));
            }

            // Add failure handler steps if present
            if (pathBuilder.FailureHandler is not null)
            {
                foreach (var step in pathBuilder.FailureHandler.Steps)
                {
                    _steps.Add(step.AsLoopBodyStep(_loopId));
                }
            }
        }

        // Create a pending fork point (JoinStepId will be set when Join is called)
        var pendingForkPoint = new ForkPointDefinition
        {
            ForkPointId = Guid.NewGuid().ToString("N"),
            FromStepId = _steps.Count > 0 ? _steps[^1].StepId : _loopId,
            Paths = forkPaths,
            JoinStepId = string.Empty, // Will be set in CompleteForkJoin
        };

        return new LoopForkJoinBuilder<TState>(this, pendingForkPoint);
    }

    /// <summary>
    /// Completes a fork/join by registering the fork point and join step.
    /// </summary>
    /// <param name="forkPoint">The completed fork point definition.</param>
    /// <param name="joinStep">The join step definition.</param>
    internal void CompleteForkJoin(ForkPointDefinition forkPoint, StepDefinition joinStep)
    {
        _forkPoints.Add(forkPoint);
        _steps.Add(joinStep.AsLoopBodyStep(_loopId));
    }

    /// <inheritdoc/>
    public ILoopBuilder<TState> Branch<TDiscriminator>(
        Func<TState, TDiscriminator> discriminator,
        params BranchCase<TState, TDiscriminator>[] cases)
    {
        ArgumentNullException.ThrowIfNull(discriminator, nameof(discriminator));
        ArgumentNullException.ThrowIfNull(cases, nameof(cases));

        if (cases.Length == 0)
        {
            throw new ArgumentException("Branch must have at least one case.", nameof(cases));
        }

        // Branch inside a loop: collect branch path steps and add them to the loop body
        // The steps from branch paths are added as loop body steps
        foreach (var branchCase in cases)
        {
            var branchBuilder = new BranchBuilder<TState>();
            branchCase.PathBuilder(branchBuilder);

            // Add steps from this branch path to the loop body, marked as loop body steps
            foreach (var step in branchBuilder.Steps)
            {
                _steps.Add(step.AsLoopBodyStep(_loopId));
            }
        }

        return this;
    }
}
