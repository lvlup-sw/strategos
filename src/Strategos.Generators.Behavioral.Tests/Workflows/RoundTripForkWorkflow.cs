// -----------------------------------------------------------------------
// <copyright file="RoundTripForkWorkflow.cs" company="Levelup Software">
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
// Task 019 (#100), DR-15 — round-trip real-host proof for the fork-join importable
// family. The fork-join JSON import (`roundtrip-fork.workflow.json`) is bridged to
// the SAME WorkflowModel IR and lowered through the SAME fork saga emitters at
// build time (INV-1), producing RoundtripForkImportSaga +
// AddRoundtripForkImportWorkflow(), then RUN end-to-end on a real host:
// Start → {Left ‖ Right} → Join → End, each step once.
//
// DR-15's fork-join twin equivalence (C# .Fork().Join().Finally() twin ≡ the JSON
// import) is DEFERRED, pending strategos#155 — machine-checked, not narrated. The
// C# twin below (RoundtripForkTwinWorkflowDefinition ⇒ AddRoundtripForkTwinWorkflow())
// compiles and registers, but does NOT run to completion on the current generator:
// C#-authoring's StepNames extraction APPENDS the fork-path steps AFTER the
// top-level terminal, so the terminal is not last and its completed handler chains
// back to a fork-path step instead of calling MarkCompleted() (strategos#155). The
// JSON IMPORT does not hit this: the wire export lists the fork-path steps as
// top-level steps in document order, so the terminal ends up last and terminates
// correctly. The equivalence claim lives — SKIPPED, pending strategos#155 — in
// RoundTripBehavioralTests.ForkJoinCSharpTwin_RunsIdentically_ToJsonImport, so it
// goes green automatically once the terminal-detection fix lands (do NOT fix
// strategos#155 here). The fork import's field-for-field IR fidelity + partition
// membership are proven by RoundTripIrFidelityTests / RoundTripEquivalenceTests.
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

// --- Step types + definition for the C#-authored fork-join twin (DR-15 twin equivalence, DEFERRED
// pending strategos#155). Distinct CLR types from the import's steps so the two [Workflow]/import
// definitions do not share a step type (CS0101). The twin's saga compiles + registers; its
// completion is blocked by strategos#155 (see the file header + the SKIPPED
// RoundTripBehavioralTests.ForkJoinCSharpTwin_RunsIdentically_ToJsonImport). ---

/// <summary>Pre-fork entry step of the fork-join C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkTwinStart(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkTwinStart));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Left fork-path step of the fork-join C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkTwinLeft(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkTwinLeft));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Right fork-path step of the fork-join C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkTwinRight(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkTwinRight));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Join step of the fork-join C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkTwinJoin(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkTwinJoin));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the fork-join C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RtForkTwinEnd(WorkflowInvocationLog log) : IWorkflowStep<RoundTripForkState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripForkState>> ExecuteAsync(RoundTripForkState state, StepContext context, CancellationToken cancellationToken)
    {
        log.Record(nameof(RtForkTwinEnd));
        return Task.FromResult(StepResult<RoundTripForkState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// The C#-authored fork-join twin of the fork-join JSON import: the SAME shape
/// (<c>.Fork(left, right).Join&lt;TJoin&gt;().Finally&lt;TEnd&gt;()</c>) authored fluently. Drives the
/// generator to emit <c>RoundtripForkTwinSaga</c>, <c>StartRoundtripForkTwinCommand</c>, and
/// <c>AddRoundtripForkTwinWorkflow()</c>. The saga compiles + registers; its runtime completion is
/// blocked by strategos#155 (the equivalence proof is the SKIPPED
/// <c>RoundTripBehavioralTests.ForkJoinCSharpTwin_RunsIdentically_ToJsonImport</c>).
/// </summary>
[Workflow("roundtrip-fork-twin")]
public static partial class RoundtripForkTwinWorkflowDefinition
{
    /// <summary>Gets the fluent definition: a pre-fork step, two parallel paths, a join, and a terminal.</summary>
    public static WorkflowDefinition<RoundTripForkState> Definition => Workflow<RoundTripForkState>
        .Create("roundtrip-fork-twin")
        .StartWith<RtForkTwinStart>()
        .Fork(
            p => p.Then<RtForkTwinLeft>(),
            p => p.Then<RtForkTwinRight>())
        .Join<RtForkTwinJoin>()
        .Finally<RtForkTwinEnd>();
}
