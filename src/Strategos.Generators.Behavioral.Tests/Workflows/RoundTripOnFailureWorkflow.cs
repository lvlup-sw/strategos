// -----------------------------------------------------------------------
// <copyright file="RoundTripOnFailureWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// Task 019 (#100), DR-15 (M11) — the onFailure importable family's bucket-(a)
// COMPILE + runtime proof. Before this fixture the onFailure family's bucket-(a)
// membership was proxied ONLY by the in-memory partition gate's "a *Saga.g.cs tree
// was emitted" signal (RoundTripEquivalenceTests) — never actual compilation. This
// hand-authored JSON import (`roundtrip-onfailure.workflow.json`) is bridged +
// lowered through the SAME saga emitters at build time (INV-1), producing
// RoundtripOnFailureImportSaga + AddRoundtripOnFailureImportWorkflow() — and the
// Behavioral.Tests BUILD compiling that generated saga is the REAL semantic check
// (parse-only trees miss compile errors). It is then RUN end-to-end on the happy
// path (Start → Work → End) as a runtime proof.
//
// The workflow-scoped failure handler (`failureHandlers[0]`, steps = [Log]) makes
// this a genuine onFailure-family document: the handler's recovery-step shape is
// carried on the wire (a DR-14 presence rule, recoverable without a marker), and
// the importable subset lowers the happy path. The Log recovery step is declared
// below for fidelity to the authored shape.
// =============================================================================

/// <summary>State for the onFailure JSON import real-host proof.</summary>
[WorkflowState]
public sealed record RoundTripOnFailureState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>Entry step of the onFailure JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtOnFailureImportStart(WorkflowInvocationLog log) : IWorkflowStep<RoundTripOnFailureState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripOnFailureState>> ExecuteAsync(RoundTripOnFailureState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtOnFailureImportStart));
        return Task.FromResult(StepResult<RoundTripOnFailureState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Middle step of the onFailure JSON import (the step the workflow-scoped handler guards).</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtOnFailureImportWork(WorkflowInvocationLog log) : IWorkflowStep<RoundTripOnFailureState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripOnFailureState>> ExecuteAsync(RoundTripOnFailureState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtOnFailureImportWork));
        return Task.FromResult(StepResult<RoundTripOnFailureState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the onFailure JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtOnFailureImportEnd(WorkflowInvocationLog log) : IWorkflowStep<RoundTripOnFailureState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripOnFailureState>> ExecuteAsync(RoundTripOnFailureState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtOnFailureImportEnd));
        return Task.FromResult(StepResult<RoundTripOnFailureState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// Recovery step named by the JSON import's workflow-scoped failure handler
/// (<c>failureHandlers[0].steps[0]</c>). Declared for fidelity to the authored onFailure shape; the
/// happy-path run never invokes it.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtOnFailureImportLog(WorkflowInvocationLog log) : IWorkflowStep<RoundTripOnFailureState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripOnFailureState>> ExecuteAsync(RoundTripOnFailureState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtOnFailureImportLog));
        return Task.FromResult(StepResult<RoundTripOnFailureState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}
