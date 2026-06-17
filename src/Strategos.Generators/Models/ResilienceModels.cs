// -----------------------------------------------------------------------
// <copyright file="ResilienceModels.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Strategos.Generators.Models;

/// <summary>
/// Generator IR for a step's retry policy.
/// </summary>
/// <param name="MaxAttempts">The maximum number of retry attempts.</param>
/// <param name="InitialDelay">The initial delay between retries, or null if unspecified.</param>
/// <param name="BackoffMultiplier">The multiplier for exponential backoff, or null if unspecified.</param>
/// <param name="MaxDelay">The maximum delay cap for backoff, or null if unspecified.</param>
/// <param name="UseJitter">A value indicating whether random jitter is added to delays.</param>
/// <remarks>
/// Mirrors the wire-level <c>RetryConfiguration</c>
/// (MaxAttempts/InitialDelay/BackoffMultiplier/MaxDelay/UseJitter). The delay-shaping
/// members are nullable so the IR can carry only the values that were explicitly
/// configured in the DSL; defaulting is applied at emit-time, not in the IR.
/// </remarks>
internal sealed record RetryModel(
    int MaxAttempts,
    TimeSpan? InitialDelay = null,
    double? BackoffMultiplier = null,
    TimeSpan? MaxDelay = null,
    bool UseJitter = false);

/// <summary>
/// Generator IR for a step's timeout policy.
/// </summary>
/// <param name="Timeout">The maximum duration the step may run before timing out.</param>
internal sealed record TimeoutModel(TimeSpan Timeout);

/// <summary>
/// Generator IR for a step's compensation (rollback) policy.
/// </summary>
/// <param name="CompensationStepTypeName">
/// The fully qualified type name of the compensation step.
/// </param>
/// <param name="RequiredOnFailure">
/// A value indicating whether compensation is required when the step fails.
/// </param>
/// <remarks>
/// Per INV-8, the compensation step's identity is carried as a descriptor string
/// (its fully qualified type name), never as a CLR <see cref="System.Type"/>. Symbol
/// resolution of this name happens in the later parse task, not here.
/// </remarks>
internal sealed record CompensationModel(
    string CompensationStepTypeName,
    bool RequiredOnFailure = true);

/// <summary>
/// Generator IR for a step's confidence-gating policy.
/// </summary>
/// <param name="Threshold">
/// The minimum confidence score required for the step's result to be accepted.
/// </param>
/// <param name="OnLowConfidenceHandlerId">
/// The identifier of the handler invoked when confidence falls below
/// <paramref name="Threshold"/>, or null if no handler is configured.
/// </param>
internal sealed record ConfidenceModel(
    double Threshold,
    string? OnLowConfidenceHandlerId = null);
