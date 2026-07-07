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
// the saga identically for the JSON import and its C# twin; on the happy path
// (no failure) both run the middle step exactly once, proving the config lowers
// through the SAME emitter path (INV-1) without perturbing the linear flow.
//
// Start → Work(retry×3) → End, in two authoring forms sharing RoundTripConfigState,
// with DISTINCT step CLR types per form (CS0101).
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

/// <summary>Retry-configured middle step of the config-bearing JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigImportWork(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigImportWork));
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

/// <summary>Retry-configured middle step of the config-bearing C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtConfigTwinWork(WorkflowInvocationLog log) : IWorkflowStep<RoundTripConfigState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripConfigState>> ExecuteAsync(RoundTripConfigState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtConfigTwinWork));
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
