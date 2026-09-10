// -----------------------------------------------------------------------
// <copyright file="RoundTripConfigWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// Task 019 (#100), DR-15 — round-trip BEHAVIORAL twin for the config importable
// family (a retry-bearing step). The retry policy is step CONFIG that lowers into
// the saga identically for the JSON import and its C# twin: the middle step is a
// FLAKY step that throws on its first N invocations and succeeds on attempt N+1,
// so a passing run PROVES the retry policy actually retried (a config-free step
// would fail on attempt 1 and never complete). Both authoring forms carry the
// identical retry budget (maxAttempts = 3), so both retry the middle step the
// same number of times through the SAME emitter path (INV-1).
//
// Start → Work(retry, flaky) → End, in two authoring forms sharing
// RoundTripConfigState, with DISTINCT step CLR types per form (CS0101).
// =============================================================================

/// <summary>State shared by the config-bearing JSON import and its C# twin.</summary>
[WorkflowState]
public sealed record RoundTripConfigState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>
/// Retry-exercise knobs shared by the config family. The flaky middle step throws
/// on its first <see cref="InducedFailures"/> invocations and succeeds on attempt
/// <see cref="InducedFailures"/> + 1, so the recorded invocation count equals
/// <see cref="ExpectedWorkInvocations"/> ONLY when the retry policy actually
/// retried. <see cref="InducedFailures"/> stays strictly below the lowered retry
/// budget (maxAttempts = 3 ⇒ up to 3 total attempts, i.e. 2 retries after the
/// initial attempt), so the step succeeds before the budget is exhausted.
/// </summary>
public static class RoundTripConfigRetry
{
    /// <summary>The number of transient failures the flaky middle step induces.</summary>
    public const int InducedFailures = 2;

    /// <summary>The total invocations expected of the middle step (failures + the succeeding attempt).</summary>
    public const int ExpectedWorkInvocations = InducedFailures + 1;
}

/// <summary>
/// Transient failure raised by the config family's flaky middle step on its first
/// <see cref="RoundTripConfigRetry.InducedFailures"/> invocations to exercise the
/// lowered retry policy.
/// </summary>
public sealed class TransientConfigStepException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="TransientConfigStepException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public TransientConfigStepException(string message)
        : base(message)
    {
    }
}

// --- Step types referenced by the JSON import (roundtrip-config.workflow.json). ---

/// <summary>Entry step of the config-bearing JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigImportStart(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigImportStart));
        return Task.FromResult(StepResult<RoundTripConfigState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// Retry-configured middle step of the config-bearing JSON import. Deliberately flaky: it records
/// every invocation, throws on its first <see cref="RoundTripConfigRetry.InducedFailures"/> attempts,
/// and succeeds only on attempt <see cref="RoundTripConfigRetry.ExpectedWorkInvocations"/> — so the
/// recorded count proves the imported retry policy actually retried.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigImportWork(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        // Record FIRST, then decide based on this invocation's attempt number (CountFor reflects all
        // prior recordings plus this one) — mirroring the proven RetryFlakyStep pattern.
        log.Record(nameof(RtConfigImportWork));
        var attempt = log.CountFor(nameof(RtConfigImportWork));
        if (attempt <= RoundTripConfigRetry.InducedFailures)
        {
            throw new TransientConfigStepException(
                $"RtConfigImportWork transient failure on attempt {attempt}.");
        }

        return Task.FromResult(StepResult<RoundTripConfigState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the config-bearing JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigImportEnd(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigImportEnd));
        return Task.FromResult(StepResult<RoundTripConfigState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

// --- Step types + definition for the C#-authored config twin. ---

/// <summary>Entry step of the config-bearing C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigTwinStart(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigTwinStart));
        return Task.FromResult(StepResult<RoundTripConfigState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// Retry-configured middle step of the config-bearing C# twin. Deliberately flaky in lock-step with
/// <see cref="RtConfigImportWork"/>: it throws on its first
/// <see cref="RoundTripConfigRetry.InducedFailures"/> attempts and succeeds only on attempt
/// <see cref="RoundTripConfigRetry.ExpectedWorkInvocations"/>, so import and twin retry the identical
/// number of times.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigTwinWork(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigTwinWork));
        var attempt = log.CountFor(nameof(RtConfigTwinWork));
        if (attempt <= RoundTripConfigRetry.InducedFailures)
        {
            throw new TransientConfigStepException(
                $"RtConfigTwinWork transient failure on attempt {attempt}.");
        }

        return Task.FromResult(StepResult<RoundTripConfigState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the config-bearing C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigTwinEnd(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigTwinEnd));
        return Task.FromResult(StepResult<RoundTripConfigState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// The C#-authored config twin of the retry-bearing JSON import: a linear chain whose middle step
/// declares <c>.WithRetry(3)</c>. Drives the generator to emit <c>RoundtripConfigTwinSaga</c>,
/// <c>StartRoundtripConfigTwinCommand</c>, and <c>AddRoundtripConfigTwinWorkflow()</c>.
/// </summary>
[Workflow("roundtrip-config-twin")]
public static partial class RoundtripConfigTwinWorkflowDefinition
{
    /// <summary>Gets the fluent definition: a linear chain with a retry-configured middle step.</summary>
    public static WorkflowDefinition<RoundTripConfigState> Definition => Workflow<RoundTripConfigState>
        .Create("roundtrip-config-twin")
        .StartWith<RtConfigTwinStart>()
        .Then<RtConfigTwinWork>(step => step.WithRetry(3))
        .Finally<RtConfigTwinEnd>();
}
