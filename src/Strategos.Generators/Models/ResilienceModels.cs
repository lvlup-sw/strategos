// -----------------------------------------------------------------------
// <copyright file="ResilienceModels.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

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
/// <param name="IsRegisteredStep">
/// A value indicating whether the compensation type resolves to a type implementing
/// <c>IWorkflowStep&lt;TState&gt;</c>. <see langword="false"/> when the type could not be
/// resolved as a workflow step (drives the compensate-not-a-step diagnostic; DR-8 / INV-5). Defaults to
/// <see langword="true"/> so models created without a semantic-model check (e.g. tests, the
/// compensation step-type fold) are not flagged.
/// </param>
/// <remarks>
/// Per INV-8, the compensation step's identity is carried as a descriptor string
/// (its fully qualified type name), never as a CLR <see cref="System.Type"/>. Symbol
/// resolution of this name happens at parse time; only the boolean verdict is retained.
/// </remarks>
internal sealed record CompensationModel(
    string CompensationStepTypeName,
    bool RequiredOnFailure = true,
    bool IsRegisteredStep = true);

/// <summary>
/// Generator IR for an ordered chain of <c>OnLowConfidence</c> handler steps and
/// the chain's exit semantics (G-4 / #139).
/// </summary>
/// <param name="Steps">
/// The ordered handler steps lowered from every <c>Then&lt;THandler&gt;()</c> call
/// inside an <c>OnLowConfidence(alt =&gt; ...)</c> lambda. The confidence gate routes
/// to the first step; each non-last step chains to the next; the last step's exit
/// is governed by <paramref name="RejoinsMainFlow"/>. Carries each step's fully
/// qualified type name (needed for DI / worker-handler lowering) in addition to its
/// simple name. Per INV-8 every step's identity is a descriptor string, never a CLR
/// <see cref="System.Type"/>.
/// </param>
/// <param name="RejoinsMainFlow">
/// A value indicating whether the chain REJOINS the main flow at the step after the
/// gated step once its last step completes (<see langword="true"/>), or TERMINATES
/// the workflow via <c>MarkCompleted()</c> (<see langword="false"/>). Defaults to
/// <see langword="false"/> — terminating is the back-compat default (DR-5's
/// single-step handler terminated). Inferred from the handler lambda shape: a
/// <c>.RejoinMainFlow()</c> call opts into rejoining; its absence terminates.
/// </param>
/// <remarks>
/// Sealed, init-only IR (INV-6): positional-record params lower to init-only setters,
/// so a downstream pipeline stage cannot rewrite the chain after parse. The record's
/// structural equality is value-based on its members, which the incremental generator
/// pipeline relies on for cache-keying.
/// </remarks>
internal sealed record LowConfidenceHandlerChainModel(
    IReadOnlyList<StepModel> Steps,
    bool RejoinsMainFlow = false);

/// <summary>
/// Generator IR for a step's confidence-gating policy.
/// </summary>
/// <param name="Threshold">
/// The minimum confidence score required for the step's result to be accepted.
/// </param>
/// <param name="OnLowConfidenceHandlerId">
/// The identifier of the handler invoked when confidence falls below
/// <paramref name="Threshold"/>, or null if no handler is configured. This is the
/// simple type name of the first <c>Then&lt;THandler&gt;()</c> step declared inside
/// the <c>OnLowConfidence(alt =&gt; ...)</c> lambda.
/// </param>
/// <param name="OnLowConfidenceHandlerStep">
/// The fully-resolved <see cref="StepModel"/> for the FIRST low-confidence handler
/// step, or null if no handler is configured. Carries the handler step's fully
/// qualified type name (needed for DI / worker-handler lowering) in addition to its
/// simple name. The confidence-gated completed handler routes to this step via a
/// Wolverine cascade (INV-1) when confidence is below the threshold. Retained
/// alongside <paramref name="OnLowConfidenceHandlerChain"/> for back-compat — it is
/// the chain's first step.
/// </param>
/// <param name="OnLowConfidenceHandlerChain">
/// The ordered handler chain (G-4 / #139), or null if no handler is configured. When
/// present, every step in <see cref="LowConfidenceHandlerChainModel.Steps"/> is
/// lowered into the saga (its own phase, worker handler, start/completed commands and
/// events); non-last steps chain to the next, and the last step either rejoins the
/// main flow or terminates per <see cref="LowConfidenceHandlerChainModel.RejoinsMainFlow"/>.
/// </param>
internal sealed record ConfidenceModel(
    double Threshold,
    string? OnLowConfidenceHandlerId = null,
    StepModel? OnLowConfidenceHandlerStep = null,
    LowConfidenceHandlerChainModel? OnLowConfidenceHandlerChain = null);
