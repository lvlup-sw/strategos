// -----------------------------------------------------------------------
// <copyright file="FailureHandlerModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Specifies the scope at which a failure handler operates.
/// </summary>
/// <remarks>
/// This enum mirrors <c>Strategos.Definitions.FailureHandlerScope</c> for source generation.
/// </remarks>
internal enum FailureHandlerScope
{
    /// <summary>
    /// Workflow-level failure handler that catches any step failure.
    /// </summary>
    Workflow,

    /// <summary>
    /// Step-level failure handler that catches only specific step failures.
    /// </summary>
    Step,
}

/// <summary>
/// Represents a failure handler construct within a workflow for code generation.
/// </summary>
/// <remarks>
/// <para>
/// Failure handler models capture the structure of OnFailure constructs in the workflow DSL.
/// The source generator uses this model to emit:
/// <list type="bullet">
///   <item><description>Try-catch handlers for workflow or step errors</description></item>
///   <item><description>Failure handler step phase names</description></item>
///   <item><description>Recovery path transitions</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="HandlerId">The unique identifier for this failure handler.</param>
/// <param name="Scope">The scope at which this failure handler operates.</param>
/// <param name="StepNames">The ordered list of step names in this failure handler path.</param>
/// <param name="IsTerminal">Whether this handler terminates the workflow (no rejoin to main flow).</param>
/// <param name="TriggerStepName">The step that triggers this handler (for step-scoped handlers only).</param>
/// <param name="Steps">The ordered list of step models with full type information for handler generation.</param>
internal sealed record FailureHandlerModel(
    string HandlerId,
    FailureHandlerScope Scope,
    IReadOnlyList<string> StepNames,
    bool IsTerminal,
    string? TriggerStepName = null,
    IReadOnlyList<StepModel>? Steps = null)
{
    /// <summary>
    /// Gets the ordered, occurrence-scoped phase names of the recovery steps.
    /// </summary>
    /// <remarks>
    /// <see cref="StepNames"/> predates instance-named failure steps and therefore carries
    /// CLR step type names. When full step models are available, their phase names preserve
    /// the instance identity required by dedicated command, event, phase, and handler names.
    /// </remarks>
    public IReadOnlyList<string> StepPhaseNames =>
        Steps is not null && Steps.Count == StepNames.Count
            ? Steps.Select(static step => step.PhaseName).ToList()
            : StepNames;

    /// <summary>
    /// Gets the ordered CLR step type names of the recovery steps.
    /// </summary>
    public IReadOnlyList<string> StepTypeNames =>
        Steps is not null && Steps.Count == StepNames.Count
            ? Steps.Select(static step => step.StepName).ToList()
            : StepNames;

    /// <summary>
    /// Gets the first occurrence-scoped phase name in the failure handler path.
    /// </summary>
    public string FirstStepPhaseName => StepPhaseNames.Count > 0
        ? StepPhaseNames[0]
        : throw new InvalidOperationException("Cannot access FirstStepPhaseName: StepNames is empty.");

    /// <summary>
    /// Gets the last occurrence-scoped phase name in the failure handler path.
    /// </summary>
    public string LastStepPhaseName => StepPhaseNames.Count > 0
        ? StepPhaseNames[StepPhaseNames.Count - 1]
        : throw new InvalidOperationException("Cannot access LastStepPhaseName: StepNames is empty.");

    /// <summary>
    /// Gets the first step name in the failure handler path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="StepNames"/> is empty.</exception>
    public string FirstStepName => StepNames.Count > 0
        ? StepNames[0]
        : throw new InvalidOperationException("Cannot access FirstStepName: StepNames is empty.");

    /// <summary>
    /// Gets the last step name in the failure handler path.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="StepNames"/> is empty.</exception>
    public string LastStepName => StepNames.Count > 0
        ? StepNames[StepNames.Count - 1]
        : throw new InvalidOperationException("Cannot access LastStepName: StepNames is empty.");

    /// <summary>
    /// Gets a value indicating whether this is a workflow-scoped handler.
    /// </summary>
    public bool IsWorkflowScoped => Scope == FailureHandlerScope.Workflow;

    /// <summary>
    /// Creates a new <see cref="FailureHandlerModel"/> with validation.
    /// </summary>
    /// <param name="handlerId">The unique identifier for this failure handler. Cannot be null or whitespace.</param>
    /// <param name="scope">The scope at which this failure handler operates.</param>
    /// <param name="stepNames">The ordered list of step names. Must have at least one step.</param>
    /// <param name="isTerminal">Whether this handler terminates the workflow.</param>
    /// <param name="triggerStepName">The optional step that triggers this handler (for Step scope).</param>
    /// <param name="steps">The optional ordered list of step models with full type information.</param>
    /// <returns>A validated <see cref="FailureHandlerModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static FailureHandlerModel Create(
        string handlerId,
        FailureHandlerScope scope,
        IReadOnlyList<string> stepNames,
        bool isTerminal,
        string? triggerStepName = null,
        IReadOnlyList<StepModel>? steps = null)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(handlerId, nameof(handlerId));
        ThrowHelper.ThrowIfNull(stepNames, nameof(stepNames));

        if (stepNames.Count == 0)
        {
            throw new ArgumentException("Failure handler must have at least one step.", nameof(stepNames));
        }

        return new FailureHandlerModel(
            HandlerId: handlerId,
            Scope: scope,
            StepNames: stepNames,
            IsTerminal: isTerminal,
            TriggerStepName: triggerStepName,
            Steps: steps);
    }
}
