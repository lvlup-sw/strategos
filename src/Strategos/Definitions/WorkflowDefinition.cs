// =============================================================================
// <copyright file="WorkflowDefinition.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Definitions;

/// <summary>
/// Immutable definition of a complete workflow.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// <para>
/// Workflow definitions serve as the intermediate representation (IR) for:
/// <list type="bullet">
///   <item><description>Source generation of phase enums, commands, and events</description></item>
///   <item><description>Runtime workflow execution and state management</description></item>
///   <item><description>Workflow validation and visualization</description></item>
/// </list>
/// </para>
/// <para>
/// This record is immutable - all mutation methods return new instances.
/// </para>
/// </remarks>
public sealed record WorkflowDefinition<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the ordered collection of step definitions.
    /// </summary>
    public IReadOnlyList<StepDefinition> Steps { get; init; } = [];

    /// <summary>
    /// Gets the collection of transition definitions between steps.
    /// </summary>
    public IReadOnlyList<TransitionDefinition> Transitions { get; init; } = [];

    /// <summary>
    /// Gets the collection of branch point definitions.
    /// </summary>
    public IReadOnlyList<BranchPointDefinition> BranchPoints { get; init; } = [];

    /// <summary>
    /// Gets the collection of loop definitions.
    /// </summary>
    public IReadOnlyList<LoopDefinition> Loops { get; init; } = [];

    /// <summary>
    /// Gets the collection of failure handler definitions.
    /// </summary>
    public IReadOnlyList<FailureHandlerDefinition> FailureHandlers { get; init; } = [];

    /// <summary>
    /// Gets the collection of approval point definitions.
    /// </summary>
    public IReadOnlyList<ApprovalDefinition> ApprovalPoints { get; init; } = [];

    /// <summary>
    /// Gets the collection of fork point definitions.
    /// </summary>
    public IReadOnlyList<ForkPointDefinition> ForkPoints { get; init; } = [];

    /// <summary>
    /// Gets the collection of diagnostic-fork edge definitions (DR-7, #151) — where
    /// this workflow may fork a diagnostic remediation path, the closed triggers
    /// permitted to fork it (with their evidence-ref schema), the compensation seed,
    /// and the per-edge <c>maxForks</c> bound.
    /// </summary>
    public IReadOnlyList<DiagnosticForkDefinition> DiagnosticForks { get; init; } = [];

    /// <summary>
    /// Gets the entry step definition (first step in workflow).
    /// </summary>
    public StepDefinition? EntryStep { get; init; }

    /// <summary>
    /// Gets the terminal step definition (final step in workflow).
    /// </summary>
    public StepDefinition? TerminalStep { get; init; }

    /// <summary>
    /// Creates a new empty workflow definition.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>A new workflow definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    public static WorkflowDefinition<TState> Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        return new WorkflowDefinition<TState>
        {
            Name = name,
        };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified step appended.
    /// </summary>
    /// <param name="step">The step definition to add.</param>
    /// <returns>A new workflow definition with the step appended.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="step"/> is null.</exception>
    public WorkflowDefinition<TState> WithStep(StepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step, nameof(step));

        var newSteps = new List<StepDefinition>(Steps) { step };
        return this with { Steps = newSteps };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified entry step.
    /// </summary>
    /// <param name="step">The entry step definition.</param>
    /// <returns>A new workflow definition with the entry step set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="step"/> is null.</exception>
    public WorkflowDefinition<TState> WithEntryStep(StepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step, nameof(step));

        return this with { EntryStep = step };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified terminal step.
    /// </summary>
    /// <param name="step">The terminal step definition.</param>
    /// <returns>A new workflow definition with the terminal step set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="step"/> is null.</exception>
    /// <remarks>
    /// The step is automatically marked as terminal if not already.
    /// </remarks>
    public WorkflowDefinition<TState> WithTerminalStep(StepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step, nameof(step));

        var terminalStep = step.IsTerminal ? step : step.AsTerminal();
        return this with { TerminalStep = terminalStep };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified transitions.
    /// </summary>
    /// <param name="transitions">The transition definitions.</param>
    /// <returns>A new workflow definition with the transitions set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transitions"/> is null.</exception>
    public WorkflowDefinition<TState> WithTransitions(IEnumerable<TransitionDefinition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions, nameof(transitions));

        return this with { Transitions = transitions.ToList() };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified branch points.
    /// </summary>
    /// <param name="branchPoints">The branch point definitions.</param>
    /// <returns>A new workflow definition with the branch points set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="branchPoints"/> is null.</exception>
    public WorkflowDefinition<TState> WithBranchPoints(IEnumerable<BranchPointDefinition> branchPoints)
    {
        ArgumentNullException.ThrowIfNull(branchPoints, nameof(branchPoints));

        return this with { BranchPoints = branchPoints.ToList() };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified loop appended.
    /// </summary>
    /// <param name="loop">The loop definition to add.</param>
    /// <returns>A new workflow definition with the loop appended.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loop"/> is null.</exception>
    public WorkflowDefinition<TState> WithLoop(LoopDefinition loop)
    {
        ArgumentNullException.ThrowIfNull(loop, nameof(loop));

        var newLoops = new List<LoopDefinition>(Loops) { loop };
        return this with { Loops = newLoops };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified loops.
    /// </summary>
    /// <param name="loops">The loop definitions.</param>
    /// <returns>A new workflow definition with the loops set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="loops"/> is null.</exception>
    public WorkflowDefinition<TState> WithLoops(IEnumerable<LoopDefinition> loops)
    {
        ArgumentNullException.ThrowIfNull(loops, nameof(loops));

        return this with { Loops = loops.ToList() };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified failure handlers.
    /// </summary>
    /// <param name="failureHandlers">The failure handler definitions.</param>
    /// <returns>A new workflow definition with the failure handlers set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="failureHandlers"/> is null.</exception>
    public WorkflowDefinition<TState> WithFailureHandlers(IEnumerable<FailureHandlerDefinition> failureHandlers)
    {
        ArgumentNullException.ThrowIfNull(failureHandlers, nameof(failureHandlers));

        return this with { FailureHandlers = failureHandlers.ToList() };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified approval points.
    /// </summary>
    /// <param name="approvalPoints">The approval point definitions.</param>
    /// <returns>A new workflow definition with the approval points set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="approvalPoints"/> is null.</exception>
    public WorkflowDefinition<TState> WithApprovalPoints(IEnumerable<ApprovalDefinition> approvalPoints)
    {
        ArgumentNullException.ThrowIfNull(approvalPoints, nameof(approvalPoints));

        return this with { ApprovalPoints = approvalPoints.ToList() };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified fork points.
    /// </summary>
    /// <param name="forkPoints">The fork point definitions.</param>
    /// <returns>A new workflow definition with the fork points set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="forkPoints"/> is null.</exception>
    public WorkflowDefinition<TState> WithForkPoints(IEnumerable<ForkPointDefinition> forkPoints)
    {
        ArgumentNullException.ThrowIfNull(forkPoints, nameof(forkPoints));

        return this with { ForkPoints = forkPoints.ToList() };
    }

    /// <summary>
    /// Creates a new workflow definition with the specified diagnostic-fork edges.
    /// </summary>
    /// <param name="diagnosticForks">The diagnostic-fork edge definitions.</param>
    /// <returns>A new workflow definition with the diagnostic-fork edges set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnosticForks"/> is null.</exception>
    public WorkflowDefinition<TState> WithDiagnosticForks(IEnumerable<DiagnosticForkDefinition> diagnosticForks)
    {
        ArgumentNullException.ThrowIfNull(diagnosticForks, nameof(diagnosticForks));

        return this with { DiagnosticForks = diagnosticForks.ToList() };
    }
}
