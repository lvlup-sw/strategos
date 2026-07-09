// -----------------------------------------------------------------------
// <copyright file="ImportedJsonWorkflow.cs" company="Levelup Software">
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
// Task 017 (DR-12 bridge half + DR-3) — the JSON import KEYSTONE, proven on a
// real host.
//
// Two authoring forms of the SAME three-step linear workflow live here:
//
//   * A GATE-BEARING JSON workflow — `import-gate.workflow.json` (an
//     AdditionalFile) — whose middle step is a `gate` step carrying a `gateId`
//     back-reference and whose root declares a `gates[]` block. The source
//     generator's import front-end bridges it to the SAME WorkflowModel IR and
//     lowers it through the SAME saga emitters at BUILD time, producing
//     `ImportGateSaga`, `StartImportGateCommand`, and `AddImportGateWorkflow()`.
//     The generated saga compiling into THIS assembly is the required semantic
//     check (INV-1: one lowering path). The gate is consumer-plane data the saga
//     never observes (DR-3), so the gate-bearing import lowers identically to a
//     gate-free twin.
//
//   * A gate-free C#-authored twin (`import-twin`) declared below, using DISTINCT
//     step CLR types (the generator mints one command/handler per step type, so a
//     shared step type across two workflow definitions would collide, CS0101).
//
// Both forms share the ImportState state type. Every step increments StepCount
// and records its invocation, so a real-host run can assert the two forms behave
// identically (both complete, both run three steps).
//
// The wire IR carries NO state type; the bridge INFERS ImportState from each
// step's IWorkflowStep<ImportState> implementation. The generated
// StartImportGateCommand therefore binds ImportState (not object) — asserted by
// reflection in JsonWorkflowImportTests.
// =============================================================================

/// <summary>
/// Immutable state shared by the gate-bearing JSON import and its gate-free C# twin. Marked
/// <see cref="WorkflowStateAttribute"/> so the generator emits the reducer both sagas fold through.
/// </summary>
[WorkflowState]
public sealed record ImportState : IWorkflowState
{
    /// <summary>Gets the unique identifier for this workflow instance.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the number of steps that have folded their result into state.</summary>
    public int StepCount { get; init; }
}

// ---------------------------------------------------------------------------
// Step types referenced by the gate-bearing JSON import (import-gate.workflow.json).
// ---------------------------------------------------------------------------

/// <summary>Entry step of the gate-bearing JSON import. Records its invocation and folds state.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImportGatePrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ImportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ImportState>> ExecuteAsync(ImportState state, StepContext context, CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImportGatePrepareStep));
        return Task.FromResult(StepResult<ImportState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// The gate step of the gate-bearing JSON import. It is an ordinary
/// <see cref="IWorkflowStep{TState}"/> — the wire <c>gate</c> kind and its <c>gateId</c> /
/// <c>gates[]</c> declaration are inert consumer-plane data the saga never observes (DR-3).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImportGateDecisionStep(WorkflowInvocationLog log) : IWorkflowStep<ImportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ImportState>> ExecuteAsync(ImportState state, StepContext context, CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImportGateDecisionStep));
        return Task.FromResult(StepResult<ImportState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the gate-bearing JSON import. Its completion drives <c>MarkCompleted()</c>.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImportGateFinishStep(WorkflowInvocationLog log) : IWorkflowStep<ImportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ImportState>> ExecuteAsync(ImportState state, StepContext context, CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImportGateFinishStep));
        return Task.FromResult(StepResult<ImportState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

// ---------------------------------------------------------------------------
// Step types + definition for the gate-free C#-authored twin (import-twin).
// Distinct CLR types avoid the one-step-type-per-workflow CS0101 collision.
// ---------------------------------------------------------------------------

/// <summary>Entry step of the gate-free C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImportTwinPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<ImportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ImportState>> ExecuteAsync(ImportState state, StepContext context, CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImportTwinPrepareStep));
        return Task.FromResult(StepResult<ImportState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Middle step of the gate-free C# twin (the structural counterpart of the JSON gate step).</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImportTwinDecisionStep(WorkflowInvocationLog log) : IWorkflowStep<ImportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ImportState>> ExecuteAsync(ImportState state, StepContext context, CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImportTwinDecisionStep));
        return Task.FromResult(StepResult<ImportState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Terminal step of the gate-free C# twin.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ImportTwinFinishStep(WorkflowInvocationLog log) : IWorkflowStep<ImportState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<ImportState>> ExecuteAsync(ImportState state, StepContext context, CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ImportTwinFinishStep));
        return Task.FromResult(StepResult<ImportState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// The gate-free C#-authored twin of the gate-bearing JSON import: a three-step linear workflow.
/// Drives the generator to emit <c>ImportTwinSaga</c>, <c>StartImportTwinCommand</c>, and
/// <c>AddImportTwinWorkflow()</c> — the twin whose behavior the imported JSON workflow must match.
/// </summary>
[Workflow("import-twin")]
public static partial class ImportTwinWorkflowDefinition
{
    /// <summary>Gets the fluent definition: a linear chain of three deterministic, instrumented steps.</summary>
    public static WorkflowDefinition<ImportState> Definition => Workflow<ImportState>
        .Create("import-twin")
        .StartWith<ImportTwinPrepareStep>()
        .Then<ImportTwinDecisionStep>()
        .Finally<ImportTwinFinishStep>();
}
