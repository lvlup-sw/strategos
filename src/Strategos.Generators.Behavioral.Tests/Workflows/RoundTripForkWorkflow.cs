// -----------------------------------------------------------------------
// <copyright file="RoundTripForkWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// Task 019 (#100), DR-15 — round-trip real-host proof for the fork-join importable
// family. The fork-join JSON import (`roundtrip-fork.workflow.json`) is bridged to
// the SAME WorkflowModel IR and lowered through the SAME fork saga emitters at
// build time (INV-1), producing RoundtripForkImportSaga +
// AddRoundtripForkImportWorkflow(), then RUN end-to-end on a real host:
// Start → {Left ‖ Right} → Join → End, each step once.
//
// NOTE (a finding, not this task's fix): a C#-authored fork twin of the shape
// `.Fork(...).Join<T>().Finally<TEnd>()` does NOT complete on the current
// generator — the C#-authoring StepNames extraction APPENDS the fork-path steps
// AFTER the top-level terminal, so the terminal is not last in StepNames and its
// completed handler chains back to a fork-path step instead of calling
// MarkCompleted() (the existing `fork-path-confidence` fixture only sidesteps this
// by diverting through its OnLowConfidence handler before reaching its terminal).
// The JSON IMPORT does not hit this: the wire export lists the fork-path steps as
// top-level steps in document order, so the terminal ends up last and terminates
// correctly. The fork import's field-for-field IR fidelity + partition membership
// are proven by RoundTripIrFidelityTests / RoundTripEquivalenceTests; here we add
// the real-host runtime proof for the fork import.
// =============================================================================

/// <summary>State for the fork-join JSON import real-host proof.</summary>
[WorkflowState]
public sealed record RoundTripForkState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>Pre-fork entry step of the fork-join JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkImportStart(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkImportStart));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Left fork-path step of the fork-join JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkImportLeft(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkImportLeft));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Right fork-path step of the fork-join JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkImportRight(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkImportRight));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Join step of the fork-join JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkImportJoin(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkImportJoin));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the fork-join JSON import.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkImportEnd(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkImportEnd));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}
