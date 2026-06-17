// -----------------------------------------------------------------------
// <copyright file="StepModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;
using Strategos.Generators.Utilities;

namespace Strategos.Generators.Models;

/// <summary>
/// Represents a workflow step with its type information for DI registration and handler generation.
/// </summary>
/// <param name="StepName">The name of the step (e.g., "ValidateOrder").</param>
/// <param name="StepTypeName">The fully qualified type name for DI (e.g., "MyApp.Steps.ValidateOrder").</param>
/// <param name="InstanceName">Optional instance name for distinguishing reuses of the same step type.</param>
/// <param name="LoopName">The name of the parent loop, if this step is inside a loop.</param>
/// <param name="ValidationPredicate">The predicate expression text for state validation guard.</param>
/// <param name="ValidationErrorMessage">The error message when validation fails.</param>
/// <param name="Context">The optional context configuration for this step.</param>
/// <param name="Retry">The optional retry policy for this step.</param>
/// <param name="Timeout">The optional timeout policy for this step.</param>
/// <param name="Compensation">The optional compensation (rollback) policy for this step.</param>
/// <param name="Confidence">The optional confidence-gating policy for this step.</param>
internal sealed record StepModel(
    string StepName,
    string StepTypeName,
    string? InstanceName = null,
    string? LoopName = null,
    string? ValidationPredicate = null,
    string? ValidationErrorMessage = null,
    ContextModel? Context = null)
{
    /// <summary>
    /// Gets the optional retry policy for this step.
    /// </summary>
    public RetryModel? Retry { get; init; }

    /// <summary>
    /// Gets the optional timeout policy for this step.
    /// </summary>
    public TimeoutModel? Timeout { get; init; }

    /// <summary>
    /// Gets the optional compensation (rollback) policy for this step.
    /// </summary>
    public CompensationModel? Compensation { get; init; }

    /// <summary>
    /// Gets the optional confidence-gating policy for this step.
    /// </summary>
    public ConfidenceModel? Confidence { get; init; }

    /// <summary>
    /// Gets the effective name for this step, used for duplicate detection and phase naming.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="InstanceName"/> if provided, otherwise returns <see cref="StepName"/>.
    /// This enables same step type to be reused with distinct identities in fork paths.
    /// </remarks>
    public string EffectiveName => InstanceName ?? StepName;

    /// <summary>
    /// Gets the phase name, which includes the loop prefix if this step is inside a loop.
    /// </summary>
    /// <remarks>
    /// For steps outside loops, this returns the effective name directly.
    /// For steps inside loops, this returns "{LoopName}_{EffectiveName}".
    /// </remarks>
    public string PhaseName => LoopName is null ? EffectiveName : $"{LoopName}_{EffectiveName}";

    /// <summary>
    /// Gets a value indicating whether this step has a validation guard.
    /// </summary>
    public bool HasValidation => ValidationPredicate is not null;

    /// <summary>
    /// Gets a value indicating whether this step is a terminal step that should mark the saga as completed.
    /// </summary>
    /// <remarks>
    /// Terminal steps (CompleteStep, FailedStep, TerminateStep, AutoFailStep) should always call
    /// <c>MarkCompleted()</c> regardless of their position in the workflow, as they represent
    /// final outcomes that end the saga.
    /// </remarks>
    public bool IsTerminal => StepName is "CompleteStep" or "FailedStep" or "TerminateStep" or "AutoFailStep";

    /// <summary>
    /// Creates a new <see cref="StepModel"/> with validation of all parameters.
    /// </summary>
    /// <param name="stepName">The name of the step (e.g., "ValidateOrder"). Must be a valid C# identifier.</param>
    /// <param name="stepTypeName">The fully qualified type name for DI (e.g., "MyApp.Steps.ValidateOrder"). Cannot be null or whitespace.</param>
    /// <param name="instanceName">The optional instance name for distinguishing reuses of the same step type.</param>
    /// <param name="loopName">The optional name of the parent loop. If provided, must be a valid C# identifier.</param>
    /// <param name="validationPredicate">The optional predicate expression text for state validation guard.</param>
    /// <param name="validationErrorMessage">The optional error message when validation fails.</param>
    /// <param name="context">The optional context configuration for this step.</param>
    /// <param name="retry">The optional retry policy for this step.</param>
    /// <param name="timeout">The optional timeout policy for this step.</param>
    /// <param name="compensation">The optional compensation (rollback) policy for this step.</param>
    /// <param name="confidence">The optional confidence-gating policy for this step.</param>
    /// <returns>A validated <see cref="StepModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stepName"/> or <paramref name="stepTypeName"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when validation fails or when predicate and message are mismatched.</exception>
    /// <remarks>
    /// Both <paramref name="validationPredicate"/> and <paramref name="validationErrorMessage"/> must be provided together,
    /// or both must be null. Providing only one will throw an <see cref="ArgumentException"/>.
    /// </remarks>
    public static StepModel Create(
        string stepName,
        string stepTypeName,
        string? instanceName = null,
        string? loopName = null,
        string? validationPredicate = null,
        string? validationErrorMessage = null,
        ContextModel? context = null,
        RetryModel? retry = null,
        TimeoutModel? timeout = null,
        CompensationModel? compensation = null,
        ConfidenceModel? confidence = null)
    {
        // Validate required parameters
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        IdentifierValidator.ValidateIdentifier(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNullOrWhiteSpace(stepTypeName, nameof(stepTypeName));

        // Validate optional instance name if provided
        if (instanceName is not null && !IdentifierValidator.IsValidIdentifier(instanceName))
        {
            throw new ArgumentException(
                $"Instance name '{instanceName}' is not a valid C# identifier.",
                nameof(instanceName));
        }

        // Validate optional loop name if provided
        if (loopName is not null && !IdentifierValidator.IsValidIdentifier(loopName))
        {
            throw new ArgumentException(
                $"Loop name '{loopName}' is not a valid C# identifier.",
                nameof(loopName));
        }

        // Validate validation predicate and message are both present or both absent
        var hasPredicateValue = validationPredicate is not null;
        var hasMessageValue = validationErrorMessage is not null;
        if (hasPredicateValue != hasMessageValue)
        {
            throw new ArgumentException(
                "ValidationPredicate and ValidationErrorMessage must both be provided or both be null.",
                hasPredicateValue ? nameof(validationErrorMessage) : nameof(validationPredicate));
        }

        return new StepModel(
            StepName: stepName,
            StepTypeName: stepTypeName,
            InstanceName: instanceName,
            LoopName: loopName,
            ValidationPredicate: validationPredicate,
            ValidationErrorMessage: validationErrorMessage,
            Context: context)
        {
            Retry = retry,
            Timeout = timeout,
            Compensation = compensation,
            Confidence = confidence,
        };
    }
}
