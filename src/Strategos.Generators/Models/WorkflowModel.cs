// -----------------------------------------------------------------------
// <copyright file="WorkflowModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Polyfills;
using Strategos.Generators.Utilities;

namespace Strategos.Generators.Models;

/// <summary>
/// Complete workflow model extracted from DSL for code generation.
/// </summary>
/// <remarks>
/// This is the shared intermediate representation used by all emitters
/// (PhaseEnumEmitter, CommandsEmitter, EventsEmitter, TransitionsEmitter, SagaEmitter,
/// WorkerHandlerEmitter, ExtensionsEmitter).
/// </remarks>
/// <param name="WorkflowName">The original workflow name (e.g., "process-order").</param>
/// <param name="PascalName">The PascalCase workflow name (e.g., "ProcessOrder").</param>
/// <param name="Namespace">The containing namespace.</param>
/// <param name="StepNames">The ordered list of step phase names.</param>
/// <param name="StateTypeName">The state type name (e.g., "OrderState").</param>
/// <param name="Version">The workflow schema version (default 1).</param>
/// <param name="Steps">The ordered list of step models with type information for DI.</param>
/// <param name="Loops">The loop constructs in this workflow (RepeatUntil/While).</param>
/// <param name="Branches">The branch constructs in this workflow (Case/When).</param>
/// <param name="FailureHandlers">The failure handler constructs in this workflow (OnFailure).</param>
/// <param name="ApprovalPoints">The approval checkpoints in this workflow (AwaitApproval).</param>
/// <param name="Forks">The fork constructs in this workflow (Fork/Join).</param>
/// <param name="ConfidenceHandlerStepNames">
/// The step names lowered from <c>OnLowConfidence</c> handler branches (DR-5). These steps are
/// appended to <paramref name="StepNames"/> so they get full lowering (phase, worker handler,
/// commands, events), but they are NOT part of the main linear flow: they must not displace the
/// main flow's terminal step nor be chained to as a normal "next" step. Their own completed handler
/// is terminal (a single-step handler ends the workflow via <c>MarkCompleted()</c>).
/// </param>
internal sealed record WorkflowModel(
    string WorkflowName,
    string PascalName,
    string Namespace,
    IReadOnlyList<string> StepNames,
    string? StateTypeName = null,
    int Version = 1,
    PersistenceMode PersistenceMode = PersistenceMode.SagaDocument,
    IReadOnlyList<StepModel>? Steps = null,
    IReadOnlyList<LoopModel>? Loops = null,
    IReadOnlyList<BranchModel>? Branches = null,
    IReadOnlyList<FailureHandlerModel>? FailureHandlers = null,
    IReadOnlyList<ApprovalModel>? ApprovalPoints = null,
    IReadOnlyList<ForkModel>? Forks = null,
    IReadOnlyList<string>? ConfidenceHandlerStepNames = null)
{
    /// <summary>
    /// Gets a value indicating whether the workflow's state type exposes a public
    /// instance <c>Phase</c> property.
    /// </summary>
    /// <remarks>
    /// The failure-handler routing lowering syncs the saga's <c>Phase</c> from the
    /// reduced state (<c>Phase = State.Phase</c>) only when this is
    /// <see langword="true"/>. A realistic state type that tracks its phase at the
    /// saga level only (no <c>Phase</c> member) must NOT emit that sync, otherwise
    /// the generated saga references a property the state does not have and fails to
    /// compile. Defaults to <see langword="false"/> so the safe (no-sync) shape is
    /// the default for the many positional/factory model constructions in tests.
    /// </remarks>
    public bool StateHasPhaseProperty { get; init; }

    /// <summary>
    /// Gets the derived phase enum name.
    /// </summary>
    public string PhaseEnumName => $"{PascalName}Phase";

    /// <summary>
    /// Gets the versioned saga class name.
    /// </summary>
    /// <remarks>
    /// Version 1 produces "{PascalName}Saga" (e.g., ProcessOrderSaga).
    /// Version 2+ produces "{PascalName}SagaV{N}" (e.g., ProcessOrderSagaV2).
    /// </remarks>
    public string SagaClassName => Version == 1
        ? $"{PascalName}Saga"
        : $"{PascalName}SagaV{Version}";

    /// <summary>
    /// Gets the reducer type name derived from the state type name.
    /// </summary>
    /// <remarks>
    /// Returns "{StateTypeName}Reducer" (e.g., "OrderStateReducer") when StateTypeName is set,
    /// or null if StateTypeName is not specified.
    /// </remarks>
    public string? ReducerTypeName => StateTypeName is null ? null : $"{StateTypeName}Reducer";

    /// <summary>
    /// Gets a value indicating whether this workflow uses event-sourced persistence.
    /// </summary>
    public bool IsEventSourced => PersistenceMode == PersistenceMode.EventSourced;

    /// <summary>
    /// Gets a value indicating whether this workflow contains any loop constructs.
    /// </summary>
    public bool HasLoops => Loops is not null && Loops.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this workflow contains any branch constructs.
    /// </summary>
    public bool HasBranches => Branches is not null && Branches.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this workflow contains any failure handler constructs.
    /// </summary>
    public bool HasFailureHandlers => FailureHandlers is not null && FailureHandlers.Count > 0;

    /// <summary>
    /// Gets a value indicating whether any step in this workflow declares a
    /// <c>.Compensate&lt;T&gt;()</c> rollback policy (DR-3).
    /// </summary>
    public bool HasCompensation => Steps?.Any(s => s.Compensation is not null) ?? false;

    /// <summary>
    /// Gets the distinct set of steps that declare a compensation policy, in
    /// first-seen order (DR-3). Deduplicated by the compensation step's simple
    /// type name so two steps rolling back to the same compensation type lower a
    /// single compensation handler chain.
    /// </summary>
    /// <remarks>
    /// Empty when no step declares compensation. Used by the compensation
    /// lowering path to drive worker-handler, command, event, and saga-handler
    /// emission for each rollback step.
    /// </remarks>
    public IReadOnlyList<StepModel> CompensationSteps
    {
        get
        {
            if (Steps is null)
            {
                return [];
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<StepModel>();
            foreach (var step in Steps)
            {
                if (step.Compensation is null)
                {
                    continue;
                }

                var compName = NamingHelper.GetSimpleTypeName(step.Compensation.CompensationStepTypeName);
                if (seen.Add(compName))
                {
                    result.Add(step);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this workflow contains any approval checkpoints.
    /// </summary>
    public bool HasApprovalPoints => ApprovalPoints is not null && ApprovalPoints.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this workflow contains any fork constructs.
    /// </summary>
    public bool HasForks => Forks is not null && Forks.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this workflow lowers any <c>OnLowConfidence</c>
    /// handler branch (DR-5).
    /// </summary>
    public bool HasConfidenceHandlers =>
        ConfidenceHandlerStepNames is not null && ConfidenceHandlerStepNames.Count > 0;

    /// <summary>
    /// Determines whether the named step is a lowered <c>OnLowConfidence</c> handler step
    /// (DR-5) and therefore off the main linear flow.
    /// </summary>
    /// <param name="stepName">The step name to test.</param>
    /// <returns>
    /// <see langword="true"/> when the step was lowered from an <c>OnLowConfidence</c> branch;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool IsConfidenceHandlerStep(string stepName) =>
        ConfidenceHandlerStepNames is not null
        && ConfidenceHandlerStepNames.Contains(stepName);

    /// <summary>
    /// Resolves the routing of a lowered <c>OnLowConfidence</c> handler step within its chain
    /// (G-4 / #139): the next handler step to chain to, whether it is the chain's terminal step,
    /// and (for a rejoining chain) the main-flow step to resume at.
    /// </summary>
    /// <param name="stepName">The handler step phase name to resolve.</param>
    /// <returns>
    /// A tuple describing the step's chain position: <c>NextHandlerStepName</c> is the next handler
    /// step in the chain (null when this is the chain's last step), <c>IsLastInChain</c> is true when
    /// no later handler step exists, and <c>RejoinStepName</c> is the main-flow step the chain resumes
    /// at when it rejoins (null when the chain terminates or this is not the last step). Returns all
    /// nulls / false when the step is not a confidence handler step or its chain cannot be resolved.
    /// </returns>
    public (string? NextHandlerStepName, bool IsLastInChain, string? RejoinStepName) GetConfidenceHandlerChainRouting(string stepName)
    {
        if (Steps is null || !IsConfidenceHandlerStep(stepName))
        {
            return (null, false, null);
        }

        // Find the gated step whose OnLowConfidence chain contains this handler step.
        for (var gatedIndex = 0; gatedIndex < Steps.Count; gatedIndex++)
        {
            var chain = Steps[gatedIndex].Confidence?.OnLowConfidenceHandlerChain;
            if (chain is null)
            {
                continue;
            }

            var position = -1;
            for (var i = 0; i < chain.Steps.Count; i++)
            {
                if (string.Equals(chain.Steps[i].StepName, stepName, StringComparison.Ordinal))
                {
                    position = i;
                    break;
                }
            }

            if (position < 0)
            {
                continue;
            }

            var isLastInChain = position == chain.Steps.Count - 1;
            var nextHandlerStepName = isLastInChain ? null : chain.Steps[position + 1].StepName;

            string? rejoinStepName = null;
            if (isLastInChain && chain.RejoinsMainFlow)
            {
                rejoinStepName = NextMainFlowStepName(Steps[gatedIndex].PhaseName);
            }

            return (nextHandlerStepName, isLastInChain, rejoinStepName);
        }

        return (null, false, null);
    }

    /// <summary>
    /// Gets the next MAIN-flow step phase name after the given step phase name, skipping over any
    /// lowered <c>OnLowConfidence</c> handler steps (which are appended to <see cref="StepNames"/>
    /// but are not part of the linear flow). Returns null when no later main-flow step exists.
    /// </summary>
    /// <param name="phaseName">The phase name to search after.</param>
    /// <returns>The next main-flow step phase name, or null if the given step is last in the main flow.</returns>
    private string? NextMainFlowStepName(string phaseName)
    {
        var index = -1;
        for (var i = 0; i < StepNames.Count; i++)
        {
            if (string.Equals(StepNames[i], phaseName, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return null;
        }

        for (var j = index + 1; j < StepNames.Count; j++)
        {
            if (!IsConfidenceHandlerStep(StepNames[j]))
            {
                return StepNames[j];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a value indicating whether any step in this workflow has validation guards.
    /// </summary>
    /// <remarks>
    /// When true, the PhaseEnumEmitter should include a ValidationFailed phase,
    /// and the EventsEmitter should generate a ValidationFailed event type.
    /// </remarks>
    public bool HasAnyValidation => Steps?.Any(s => s.HasValidation) ?? false;

    /// <summary>
    /// Creates a new <see cref="WorkflowModel"/> with validation of all parameters.
    /// </summary>
    /// <param name="workflowName">The original workflow name (e.g., "process-order").</param>
    /// <param name="pascalName">The PascalCase workflow name (e.g., "ProcessOrder"). Must be a valid C# identifier.</param>
    /// <param name="namespace">The containing namespace. Cannot be null or whitespace.</param>
    /// <param name="stepNames">The ordered list of step phase names. Must have at least one step, no duplicates, and all must be valid C# identifiers.</param>
    /// <param name="stateTypeName">The optional state type name (e.g., "OrderState").</param>
    /// <param name="version">The workflow schema version (must be >= 1).</param>
    /// <param name="steps">The optional ordered list of step models with type information for DI.</param>
    /// <param name="loops">The optional loop constructs in this workflow.</param>
    /// <param name="branches">The optional branch constructs in this workflow.</param>
    /// <param name="failureHandlers">The optional failure handler constructs in this workflow.</param>
    /// <param name="approvalPoints">The optional approval checkpoints in this workflow.</param>
    /// <param name="forks">The optional fork constructs in this workflow.</param>
    /// <param name="confidenceHandlerStepNames">
    /// The optional step names lowered from <c>OnLowConfidence</c> handler branches (DR-5).
    /// Threaded through so <see cref="HasConfidenceHandlers"/> / <see cref="IsConfidenceHandlerStep"/>
    /// are correct for factory-built models (consistent with the primary constructor).
    /// </param>
    /// <returns>A validated <see cref="WorkflowModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pascalName"/>, <paramref name="namespace"/>, or <paramref name="stepNames"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any validation fails.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="version"/> is less than 1.</exception>
    public static WorkflowModel Create(
        string workflowName,
        string pascalName,
        string @namespace,
        IReadOnlyList<string> stepNames,
        string? stateTypeName = null,
        int version = 1,
        PersistenceMode persistenceMode = PersistenceMode.SagaDocument,
        IReadOnlyList<StepModel>? steps = null,
        IReadOnlyList<LoopModel>? loops = null,
        IReadOnlyList<BranchModel>? branches = null,
        IReadOnlyList<FailureHandlerModel>? failureHandlers = null,
        IReadOnlyList<ApprovalModel>? approvalPoints = null,
        IReadOnlyList<ForkModel>? forks = null,
        IReadOnlyList<string>? confidenceHandlerStepNames = null)
    {
        // Validate required parameters
        ThrowHelper.ThrowIfNullOrWhiteSpace(workflowName, nameof(workflowName));
        ThrowHelper.ThrowIfNull(pascalName, nameof(pascalName));
        IdentifierValidator.ValidateIdentifier(pascalName, nameof(pascalName));
        ThrowHelper.ThrowIfNullOrWhiteSpace(@namespace, nameof(@namespace));
        ThrowHelper.ThrowIfNull(stepNames, nameof(stepNames));
        ThrowHelper.ThrowIfLessThan(version, 1, nameof(version));

        // Validate stepNames has at least one step
        if (stepNames.Count == 0)
        {
            throw new ArgumentException("Workflow must have at least one step.", nameof(stepNames));
        }

        // Validate each step name is a valid identifier
        foreach (var stepName in stepNames)
        {
            if (!IdentifierValidator.IsValidIdentifier(stepName))
            {
                throw new ArgumentException(
                    $"Step name '{stepName}' is not a valid C# identifier.",
                    nameof(stepNames));
            }
        }

        // Validate no duplicate step names
        var duplicates = stepNames
            .GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new ArgumentException(
                $"Duplicate step names found: {string.Join(", ", duplicates)}.",
                nameof(stepNames));
        }

        return new WorkflowModel(
            WorkflowName: workflowName,
            PascalName: pascalName,
            Namespace: @namespace,
            StepNames: stepNames,
            StateTypeName: stateTypeName,
            Version: version,
            PersistenceMode: persistenceMode,
            Steps: steps,
            Loops: loops,
            Branches: branches,
            FailureHandlers: failureHandlers,
            ApprovalPoints: approvalPoints,
            Forks: forks,
            ConfidenceHandlerStepNames: confidenceHandlerStepNames);
    }
}
