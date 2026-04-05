// -----------------------------------------------------------------------
// <copyright file="WorkflowModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

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
    IReadOnlyList<ForkModel>? Forks = null)
{
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
    /// Gets a value indicating whether this workflow contains any approval checkpoints.
    /// </summary>
    public bool HasApprovalPoints => ApprovalPoints is not null && ApprovalPoints.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this workflow contains any fork constructs.
    /// </summary>
    public bool HasForks => Forks is not null && Forks.Count > 0;

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
        IReadOnlyList<ForkModel>? forks = null)
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
            Forks: forks);
    }
}
