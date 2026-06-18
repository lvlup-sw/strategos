// =============================================================================
// <copyright file="WorkflowBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the workflow builder.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class WorkflowBuilder<TState> : IWorkflowBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly string _name;
    private readonly List<StepDefinition> _steps = [];
    private readonly List<TransitionDefinition> _transitions = [];
    private readonly List<BranchPointDefinition> _branchPoints = [];
    private readonly List<LoopDefinition> _loops = [];
    private readonly List<FailureHandlerDefinition> _failureHandlers = [];
    private readonly List<ApprovalDefinition> _approvalPoints = [];
    private readonly List<ForkPointDefinition> _forkPoints = [];
    private readonly List<(string BranchPointId, List<string> LastStepIds)> _pendingBranchRejoins = [];
    private StepDefinition? _entryStep;
    private StepDefinition? _lastStep;
    private bool _hasWorkflowOnFailure;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowBuilder{TState}"/> class.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    internal WorkflowBuilder(string name)
    {
        _name = name;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> StartWith<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        return StartWithInternal<TStep>(instanceName: null, configure: null);
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> StartWith<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return StartWithInternal<TStep>(instanceName, configure: null);
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> StartWith<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        return StartWithInternal<TStep>(instanceName: null, configure);
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> StartWith<TStep>(string instanceName, Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        return StartWithInternal<TStep>(instanceName, configure);
    }

    private IWorkflowBuilder<TState> StartWithInternal<TStep>(
        string? instanceName,
        Action<IStepConfiguration<TState>>? configure)
        where TStep : class, IWorkflowStep<TState>
    {
        if (_entryStep is not null)
        {
            throw new InvalidOperationException("StartWith has already been called.");
        }

        var step = StepDefinition.Create(typeof(TStep), customName: null, instanceName: instanceName);

        // Route any configure lambda through the SAME WithConfiguration path Then(configure)
        // uses, so the entry step carries its per-step resilience into the IR.
        if (configure is not null)
        {
            var configBuilder = new StepConfigurationBuilder<TState>();
            configure(configBuilder);
            step = step.WithConfiguration(configBuilder.Configuration);
        }

        _entryStep = step;
        _lastStep = step;
        _steps.Add(step);

        return this;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        return ThenInternal<TStep>(instanceName: null);
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        return ThenInternal<TStep>(instanceName);
    }

    private IWorkflowBuilder<TState> ThenInternal<TStep>(string? instanceName)
        where TStep : class, IWorkflowStep<TState>
    {
        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before Then.");
        }

        var step = StepDefinition.Create(typeof(TStep), customName: null, instanceName: instanceName);
        _steps.Add(step);

        // Handle pending branch rejoins
        if (_pendingBranchRejoins.Count > 0)
        {
            foreach (var (branchPointId, lastStepIds) in _pendingBranchRejoins)
            {
                // Create transitions from each branch's last step to this step
                foreach (var lastStepId in lastStepIds)
                {
                    var rejoinTransition = TransitionDefinition.Create(lastStepId, step.StepId);
                    _transitions.Add(rejoinTransition);
                }

                // Update branch point with rejoin step
                var branchPointIndex = _branchPoints.FindIndex(bp => bp.BranchPointId == branchPointId);
                if (branchPointIndex >= 0)
                {
                    var updatedBranchPoint = _branchPoints[branchPointIndex] with { RejoinStepId = step.StepId };
                    _branchPoints[branchPointIndex] = updatedBranchPoint;
                }
            }

            _pendingBranchRejoins.Clear();
        }
        else
        {
            // Create transition from previous step to this step
            var transition = TransitionDefinition.Create(_lastStep!.StepId, step.StepId);
            _transitions.Add(transition);
        }

        _lastStep = step;

        return this;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before Then.");
        }

        // Build the step configuration
        var configBuilder = new StepConfigurationBuilder<TState>();
        configure(configBuilder);

        // Create step with configuration
        var step = StepDefinition.Create(typeof(TStep))
            .WithConfiguration(configBuilder.Configuration);

        _steps.Add(step);

        // Handle pending branch rejoins
        if (_pendingBranchRejoins.Count > 0)
        {
            foreach (var (branchPointId, lastStepIds) in _pendingBranchRejoins)
            {
                // Create transitions from each branch's last step to this step
                foreach (var lastStepId in lastStepIds)
                {
                    var rejoinTransition = TransitionDefinition.Create(lastStepId, step.StepId);
                    _transitions.Add(rejoinTransition);
                }

                // Update branch point with rejoin step
                var branchPointIndex = _branchPoints.FindIndex(bp => bp.BranchPointId == branchPointId);
                if (branchPointIndex >= 0)
                {
                    var updatedBranchPoint = _branchPoints[branchPointIndex] with { RejoinStepId = step.StepId };
                    _branchPoints[branchPointIndex] = updatedBranchPoint;
                }
            }

            _pendingBranchRejoins.Clear();
        }
        else
        {
            // Create transition from previous step to this step
            var transition = TransitionDefinition.Create(_lastStep!.StepId, step.StepId);
            _transitions.Add(transition);
        }

        _lastStep = step;

        return this;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Then(string stepName, StepDelegate<TState> stepDelegate)
    {
        ArgumentNullException.ThrowIfNull(stepName, nameof(stepName));
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName, nameof(stepName));
        ArgumentNullException.ThrowIfNull(stepDelegate, nameof(stepDelegate));

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before Then.");
        }

        // Create lambda step definition
        var step = StepDefinition.CreateFromLambda(stepName, stepDelegate);
        _steps.Add(step);

        // Handle pending branch rejoins
        if (_pendingBranchRejoins.Count > 0)
        {
            foreach (var (branchPointId, lastStepIds) in _pendingBranchRejoins)
            {
                // Create transitions from each branch's last step to this step
                foreach (var lastStepId in lastStepIds)
                {
                    var rejoinTransition = TransitionDefinition.Create(lastStepId, step.StepId);
                    _transitions.Add(rejoinTransition);
                }

                // Update branch point with rejoin step
                var branchPointIndex = _branchPoints.FindIndex(bp => bp.BranchPointId == branchPointId);
                if (branchPointIndex >= 0)
                {
                    var updatedBranchPoint = _branchPoints[branchPointIndex] with { RejoinStepId = step.StepId };
                    _branchPoints[branchPointIndex] = updatedBranchPoint;
                }
            }

            _pendingBranchRejoins.Clear();
        }
        else
        {
            // Create transition from previous step to this step
            var transition = TransitionDefinition.Create(_lastStep!.StepId, step.StepId);
            _transitions.Add(transition);
        }

        _lastStep = step;

        return this;
    }

    /// <inheritdoc/>
    public WorkflowDefinition<TState> Finally<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        return FinallyInternal<TStep>(configure: null);
    }

    /// <inheritdoc/>
    public WorkflowDefinition<TState> Finally<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        return FinallyInternal<TStep>(configure);
    }

    private WorkflowDefinition<TState> FinallyInternal<TStep>(Action<IStepConfiguration<TState>>? configure)
        where TStep : class, IWorkflowStep<TState>
    {
        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before Finally.");
        }

        // Create and add terminal step
        var terminalStep = StepDefinition.Create(typeof(TStep)).AsTerminal();

        // Route any configure lambda through the SAME WithConfiguration path Then(configure)
        // uses, so the terminal step carries its per-step resilience into the IR.
        if (configure is not null)
        {
            var configBuilder = new StepConfigurationBuilder<TState>();
            configure(configBuilder);
            terminalStep = terminalStep.WithConfiguration(configBuilder.Configuration);
        }

        _steps.Add(terminalStep);

        // Handle pending branch rejoins
        if (_pendingBranchRejoins.Count > 0)
        {
            foreach (var (branchPointId, lastStepIds) in _pendingBranchRejoins)
            {
                // Create transitions from each branch's last step to terminal step
                foreach (var lastStepId in lastStepIds)
                {
                    var rejoinTransition = TransitionDefinition.Create(lastStepId, terminalStep.StepId);
                    _transitions.Add(rejoinTransition);
                }

                // Update branch point with rejoin step
                var branchPointIndex = _branchPoints.FindIndex(bp => bp.BranchPointId == branchPointId);
                if (branchPointIndex >= 0)
                {
                    var updatedBranchPoint = _branchPoints[branchPointIndex] with { RejoinStepId = terminalStep.StepId };
                    _branchPoints[branchPointIndex] = updatedBranchPoint;
                }
            }

            _pendingBranchRejoins.Clear();
        }
        else
        {
            // Create transition from last step to terminal step
            var transition = TransitionDefinition.Create(_lastStep!.StepId, terminalStep.StepId);
            _transitions.Add(transition);
        }

        // Build the workflow definition
        var definition = WorkflowDefinition<TState>.Create(_name);

        foreach (var step in _steps)
        {
            definition = definition.WithStep(step);
        }

        return definition
            .WithEntryStep(_entryStep)
            .WithTerminalStep(terminalStep)
            .WithTransitions(_transitions)
            .WithBranchPoints(_branchPoints)
            .WithLoops(_loops)
            .WithFailureHandlers(_failureHandlers)
            .WithApprovalPoints(_approvalPoints)
            .WithForkPoints(_forkPoints);
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Branch<TDiscriminator>(
        Func<TState, TDiscriminator> discriminator,
        params BranchCase<TState, TDiscriminator>[] cases)
    {
        ArgumentNullException.ThrowIfNull(discriminator, nameof(discriminator));

        if (cases.Length == 0)
        {
            throw new ArgumentException("At least one branch case is required.", nameof(cases));
        }

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before Branch.");
        }

        var branchPaths = new List<BranchPathDefinition>();
        var lastStepIdsForRejoin = new List<string>();

        foreach (var branchCase in cases)
        {
            // Build the branch path using the BranchBuilder
            var branchBuilder = new BranchBuilder<TState>();
            branchCase.PathBuilder(branchBuilder);

            var conditionDescription = branchCase.IsDefault
                ? "Otherwise"
                : $"When {branchCase.Value}";

            var branchPath = BranchPathDefinition.Create(
                conditionDescription,
                branchBuilder.Steps,
                branchBuilder.IsTerminal,
                branchBuilder.Approval);

            branchPaths.Add(branchPath);

            // If the branch has an approval, add it to the workflow's approval points
            if (branchBuilder.Approval is not null)
            {
                _approvalPoints.Add(branchBuilder.Approval);
            }

            // Add steps from this branch path to the workflow
            foreach (var step in branchBuilder.Steps)
            {
                _steps.Add(step);
            }

            // If not terminal, track the last step for rejoin
            if (!branchBuilder.IsTerminal && branchBuilder.Steps.Count > 0)
            {
                lastStepIdsForRejoin.Add(branchBuilder.Steps[^1].StepId);
            }
        }

        // Create the branch point
        var branchPoint = BranchPointDefinition.Create(
            _lastStep!.StepId,
            branchPaths);

        _branchPoints.Add(branchPoint);

        // Track pending rejoins - these will be connected when Then() or Finally() is called
        if (lastStepIdsForRejoin.Count > 0)
        {
            _pendingBranchRejoins.Add((branchPoint.BranchPointId, lastStepIdsForRejoin));
        }

        return this;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> RepeatUntil(
        Func<TState, bool> condition,
        string loopName,
        Action<ILoopBuilder<TState>> body,
        int maxIterations = 100)
    {
        ArgumentNullException.ThrowIfNull(condition, nameof(condition));
        ArgumentNullException.ThrowIfNull(loopName, nameof(loopName));
        ArgumentNullException.ThrowIfNull(body, nameof(body));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1, nameof(maxIterations));

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before RepeatUntil.");
        }

        // Register the condition in the static registry for runtime lookup by generated sagas
        var conditionId = $"{_name}-{loopName}";
        Services.WorkflowConditionRegistry.Register(conditionId, condition);

        // Build the loop body first (with a placeholder loopId)
        // Then create the LoopDefinition to get its generated LoopId
        // Finally, update the steps with the correct LoopId
        var placeholderLoopId = Guid.NewGuid().ToString("N");
        var loopBuilder = new LoopBuilder<TState>(placeholderLoopId, loopName, parentLoopPrefix: null);
        body(loopBuilder);

        if (loopBuilder.Steps.Count == 0)
        {
            throw new ArgumentException("Loop body must contain at least one step.", nameof(body));
        }

        // Create the loop definition - this generates the real LoopId
        var loop = LoopDefinition.Create(loopName, _lastStep!.StepId, maxIterations, loopBuilder.Steps);

        // Update body steps to have the correct ParentLoopId from the LoopDefinition
        var actualLoopId = loop.LoopId;
        var updatedBodySteps = loopBuilder.Steps
            .Select(step => step with { ParentLoopId = actualLoopId })
            .ToList();

        // Update the loop definition with the corrected body steps
        var correctedLoop = loop with { BodySteps = updatedBodySteps };
        _loops.Add(correctedLoop);

        // Add any nested loop definitions from the LoopBuilder
        foreach (var nestedLoop in loopBuilder.NestedLoops)
        {
            _loops.Add(nestedLoop);
        }

        // Add any fork points from the LoopBuilder
        foreach (var forkPoint in loopBuilder.ForkPoints)
        {
            _forkPoints.Add(forkPoint);
        }

        // Add loop body steps (with correct ParentLoopId) to the workflow
        foreach (var step in updatedBodySteps)
        {
            _steps.Add(step);
        }

        // The last step in the loop body becomes the last step for chaining
        _lastStep = updatedBodySteps[^1];

        return this;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> OnFailure(Action<IFailureBuilder<TState>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before OnFailure.");
        }

        if (_hasWorkflowOnFailure)
        {
            throw new InvalidOperationException("OnFailure has already been called for this workflow.");
        }

        // Build the failure handler path
        var failureBuilder = new FailureBuilder<TState>();
        handler(failureBuilder);

        // Create the failure handler definition
        var failureHandler = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            failureBuilder.Steps,
            failureBuilder.IsTerminal);

        _failureHandlers.Add(failureHandler);
        _hasWorkflowOnFailure = true;

        // Add failure handler steps to the main steps collection
        // If the failure handler is terminal and has steps, mark the last step as terminal
        var stepsToAdd = failureBuilder.Steps.ToList();
        if (failureBuilder.IsTerminal && stepsToAdd.Count > 0)
        {
            var lastStepIndex = stepsToAdd.Count - 1;
            stepsToAdd[lastStepIndex] = stepsToAdd[lastStepIndex].AsTerminal();
        }

        foreach (var step in stepsToAdd)
        {
            _steps.Add(step);
        }

        return this;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> AwaitApproval<TApprover>(
        Action<IApprovalBuilder<TState, TApprover>> configure)
        where TApprover : class
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before AwaitApproval.");
        }

        // Build the approval definition
        var approvalBuilder = new ApprovalBuilder<TState, TApprover>(_lastStep!.StepId);
        configure(approvalBuilder);
        var approvalDefinition = approvalBuilder.Build();

        _approvalPoints.Add(approvalDefinition);

        // Add steps from escalation handler if present
        if (approvalDefinition.EscalationHandler is not null)
        {
            foreach (var step in approvalDefinition.EscalationHandler.Steps)
            {
                _steps.Add(step);
            }
        }

        // Add steps from rejection handler if present
        if (approvalDefinition.RejectionHandler is not null)
        {
            foreach (var step in approvalDefinition.RejectionHandler.Steps)
            {
                _steps.Add(step);
            }
        }

        return this;
    }

    /// <inheritdoc/>
    public IForkJoinBuilder<TState> Fork(params Action<IForkPathBuilder<TState>>[] paths)
    {
        if (paths.Length < 2)
        {
            throw new ArgumentException("Fork must have at least two paths.", nameof(paths));
        }

        if (_entryStep is null)
        {
            throw new InvalidOperationException("StartWith must be called before Fork.");
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

            // Add steps from this fork path to the workflow
            foreach (var step in pathBuilder.Steps)
            {
                _steps.Add(step);
            }

            // Add failure handler steps if present
            if (pathBuilder.FailureHandler is not null)
            {
                foreach (var step in pathBuilder.FailureHandler.Steps)
                {
                    _steps.Add(step);
                }
            }
        }

        // Create a pending fork point (JoinStepId will be set when Join is called)
        var pendingForkPoint = new ForkPointDefinition
        {
            ForkPointId = Guid.NewGuid().ToString("N"),
            FromStepId = _lastStep!.StepId,
            Paths = forkPaths,
            JoinStepId = string.Empty, // Will be set in CompleteForkJoin
        };

        return new ForkJoinBuilder<TState>(this, pendingForkPoint);
    }

    /// <summary>
    /// Completes a fork/join by registering the fork point and join step.
    /// </summary>
    /// <param name="forkPoint">The completed fork point definition.</param>
    /// <param name="joinStep">The join step definition.</param>
    internal void CompleteForkJoin(ForkPointDefinition forkPoint, StepDefinition joinStep)
    {
        _forkPoints.Add(forkPoint);
        _steps.Add(joinStep);
        _lastStep = joinStep;
    }
}
