// =============================================================================
// <copyright file="DiagnosticForkBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

using ForkTrigger = Strategos.Contracts.Generated.ForkTrigger;

namespace Strategos.Builders;

/// <summary>
/// Entry stage of the diagnostic-fork builder (DR-7, #151): the anchor stage. The
/// construct is <b>inexpressible</b> without declaring where the workflow may fork —
/// <see cref="Anchor"/> is the only member, and it returns the trigger stage
/// (<see cref="IDiagnosticForkTriggerStage{TState}"/>), so the compiler refuses any
/// chain that skips the anchor.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
/// <remarks>
/// This staged (make-illegal-states-unrepresentable) surface is a nested-fluent
/// continuation reached only through the <c>AllowDiagnosticFork</c> entrypoint; like
/// <see cref="IForkPathBuilder{TState}"/> it is intentionally outside the seven gated
/// builder entrypoints (#51 INV-1).
/// </remarks>
public interface IDiagnosticForkAnchorStage<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Declares the anchor step monikers — the step ids where the workflow may fork a
    /// diagnostic path (INV-8: simple-name step id monikers, never CLR types).
    /// </summary>
    /// <param name="anchorStepId">The first anchor step id (required, non-empty).</param>
    /// <param name="additionalAnchorStepIds">Any further anchor step ids.</param>
    /// <returns>The trigger stage, which requires at least one permitted trigger.</returns>
    IDiagnosticForkTriggerStage<TState> Anchor(
        string anchorStepId,
        params string[] additionalAnchorStepIds);
}

/// <summary>
/// Trigger stage of the diagnostic-fork builder: at least one permitted trigger MUST
/// be declared before the edge can be closed. <see cref="PermitTrigger"/> is the only
/// member, so the compiler refuses any chain that reaches the closure without first
/// permitting a trigger (DR-7: the construct is inexpressible without a trigger).
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
public interface IDiagnosticForkTriggerStage<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Permits a closed trigger to fork the workflow, paired with the DECLARATION-side
    /// evidence-ref schema — the field NAMES a future fork occurrence must carry to
    /// justify this trigger (declaration side, never runtime values, which do not
    /// exist at authoring time).
    /// </summary>
    /// <param name="trigger">The closed trigger permitted to fork the workflow (DR-8).</param>
    /// <param name="requiredEvidenceField">
    /// The first evidence field name the trigger's occurrences must carry (required,
    /// non-empty) — so a permitted trigger always names at least one justification field.
    /// </param>
    /// <param name="additionalEvidenceFields">Any further evidence field names.</param>
    /// <returns>The closure stage, where more triggers, the compensation seed, and the fork bound are set.</returns>
    IDiagnosticForkClosure<TState> PermitTrigger(
        ForkTrigger trigger,
        string requiredEvidenceField,
        params string[] additionalEvidenceFields);
}

/// <summary>
/// Closure stage of the diagnostic-fork builder: with at least one anchor and one
/// permitted trigger already declared, this stage adds further triggers and sets the
/// compensation seed and the <c>maxForks</c> bound. The compensation seed and the
/// bound are required to close the edge; omitting either throws when the edge is built
/// (they carry no meaningful default — a bound of 0 forbids the very fork the edge
/// exists to permit, and a fork with no compensation seed has nowhere to route
/// rollback).
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
public interface IDiagnosticForkClosure<TState>
    where TState : class, IWorkflowState
{
    /// <summary>
    /// Permits an additional closed trigger to fork the workflow with its own
    /// evidence-ref schema (see
    /// <see cref="IDiagnosticForkTriggerStage{TState}.PermitTrigger"/>).
    /// </summary>
    /// <param name="trigger">The closed trigger permitted to fork the workflow (DR-8).</param>
    /// <param name="requiredEvidenceField">The first evidence field name (required, non-empty).</param>
    /// <param name="additionalEvidenceFields">Any further evidence field names.</param>
    /// <returns>The closure stage, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="trigger"/> is already permitted.</exception>
    IDiagnosticForkClosure<TState> PermitTrigger(
        ForkTrigger trigger,
        string requiredEvidenceField,
        params string[] additionalEvidenceFields);

    /// <summary>
    /// Sets the compensation seed moniker (INV-8: a plain string moniker, never a CLR
    /// type) — the seed the fork routes compensation to.
    /// </summary>
    /// <param name="compensationSeedStepId">The compensation seed step id (required, non-empty).</param>
    /// <returns>The closure stage, for fluent chaining.</returns>
    IDiagnosticForkClosure<TState> WithCompensationSeed(string compensationSeedStepId);

    /// <summary>
    /// Sets the upper bound on the forks this edge may spawn (DR-9; the
    /// <see cref="Definitions.LoopDefinition.MaxIterations"/> forced-exit precedent).
    /// </summary>
    /// <param name="maxForks">The upper bound on forks (must be at least 1).</param>
    /// <returns>The closure stage, for fluent chaining.</returns>
    IDiagnosticForkClosure<TState> MaxForks(int maxForks);
}

/// <summary>
/// Internal implementation of the staged diagnostic-fork builder (DR-7, #151). Sealed
/// (INV-6): a leaf collaborator with no intended subclassing.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class DiagnosticForkBuilder<TState> :
    IDiagnosticForkAnchorStage<TState>,
    IDiagnosticForkTriggerStage<TState>,
    IDiagnosticForkClosure<TState>
    where TState : class, IWorkflowState
{
    private readonly List<string> _anchorStepIds = [];
    private readonly List<PermittedForkTriggerDefinition> _permittedTriggers = [];
    private readonly HashSet<ForkTrigger> _permittedTriggerKinds = [];
    private string? _compensationSeed;
    private int? _maxForks;

    /// <inheritdoc/>
    public IDiagnosticForkTriggerStage<TState> Anchor(
        string anchorStepId,
        params string[] additionalAnchorStepIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorStepId, nameof(anchorStepId));
        ArgumentNullException.ThrowIfNull(additionalAnchorStepIds, nameof(additionalAnchorStepIds));

        _anchorStepIds.Add(anchorStepId);
        foreach (var extra in additionalAnchorStepIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(extra, nameof(additionalAnchorStepIds));
            _anchorStepIds.Add(extra);
        }

        return this;
    }

    /// <inheritdoc cref="IDiagnosticForkTriggerStage{TState}.PermitTrigger"/>
    public IDiagnosticForkClosure<TState> PermitTrigger(
        ForkTrigger trigger,
        string requiredEvidenceField,
        params string[] additionalEvidenceFields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredEvidenceField, nameof(requiredEvidenceField));
        ArgumentNullException.ThrowIfNull(additionalEvidenceFields, nameof(additionalEvidenceFields));

        if (!_permittedTriggerKinds.Add(trigger))
        {
            throw new InvalidOperationException(
                $"Trigger '{trigger}' is already permitted for this diagnostic fork; " +
                "declare each trigger at most once.");
        }

        var evidenceFields = new List<string>(1 + additionalEvidenceFields.Length) { requiredEvidenceField };
        evidenceFields.AddRange(additionalEvidenceFields);

        _permittedTriggers.Add(PermittedForkTriggerDefinition.Create(trigger, evidenceFields));

        return this;
    }

    /// <inheritdoc/>
    public IDiagnosticForkClosure<TState> WithCompensationSeed(string compensationSeedStepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compensationSeedStepId, nameof(compensationSeedStepId));

        _compensationSeed = compensationSeedStepId;
        return this;
    }

    /// <inheritdoc/>
    public IDiagnosticForkClosure<TState> MaxForks(int maxForks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxForks, 1, nameof(maxForks));

        _maxForks = maxForks;
        return this;
    }

    /// <summary>
    /// Materializes the accumulated stages into a validated
    /// <see cref="DiagnosticForkDefinition"/>. The staged surface guarantees an anchor
    /// and a permitted trigger were declared; this additionally requires the
    /// compensation seed and the <c>maxForks</c> bound (which have no meaningful
    /// default) to have been set.
    /// </summary>
    /// <returns>The validated diagnostic-fork definition.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the compensation seed or the <c>maxForks</c> bound was not set.
    /// </exception>
    internal DiagnosticForkDefinition Build()
    {
        if (_compensationSeed is null)
        {
            throw new InvalidOperationException(
                "A diagnostic fork must set a compensation seed via WithCompensationSeed(...).");
        }

        if (_maxForks is null)
        {
            throw new InvalidOperationException(
                "A diagnostic fork must set an upper bound via MaxForks(...).");
        }

        return DiagnosticForkDefinition.Create(
            _anchorStepIds,
            _permittedTriggers,
            _compensationSeed,
            _maxForks.Value);
    }
}

/// <summary>
/// Fluent entrypoint for declaring a diagnostic-fork edge on a workflow (DR-7, #151).
/// </summary>
public static class DiagnosticForkWorkflowBuilderExtensions
{
    /// <summary>
    /// Declares a diagnostic-fork edge: where the workflow may fork a diagnostic
    /// remediation path, the closed triggers permitted to fork it (each paired with its
    /// evidence-ref schema), the compensation seed the fork routes to, and the
    /// <c>maxForks</c> upper bound (DR-7, #151).
    /// </summary>
    /// <typeparam name="TState">The workflow state type.</typeparam>
    /// <param name="builder">The workflow builder to declare the edge on.</param>
    /// <param name="configure">
    /// The staged fork configuration. The staging makes the construct inexpressible
    /// without declaring at least one anchor and at least one permitted trigger — the
    /// compiler refuses any chain that reaches the closure stage without them:
    /// <code>
    /// .AllowDiagnosticFork(fork => fork
    ///     .Anchor("RatifyDeployment")
    ///     .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId")
    ///     .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
    ///     .WithCompensationSeed("RollbackProvisionalStamp")
    ///     .MaxForks(3))
    /// </code>
    /// </param>
    /// <returns>The workflow builder, for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>StartWith</c> has not been called, when the compensation seed or
    /// <c>maxForks</c> bound was not set, when a trigger is permitted more than once, or
    /// when the builder was not produced by <see cref="Workflow{TState}.Create"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Lowering contract (deferred, #151).</b> The declared edge is captured in the
    /// builder IR (<see cref="WorkflowDefinition{TState}.DiagnosticForks"/>) and
    /// projected to the wire contract
    /// (<c>WorkflowDefinitionV1.diagnosticForks</c>) by
    /// <c>WorkflowDefinitionProjection</c>. The source generator will lower each edge
    /// into the generated Wolverine+Marten saga (DR-9): a fork-guard that admits a fork
    /// only for a permitted trigger whose occurrence carries every declared evidence
    /// field, enforces the <c>maxForks</c> bound (routing an overflowing fork to a
    /// blocked / human-escalation terminal, the
    /// <see cref="Definitions.LoopDefinition.MaxIterations"/> precedent), and seeds
    /// compensation from the declared seed. That saga lowering is NOT yet emitted; it is
    /// tracked under #151 and, until then, the declared edge is registered as a deferred
    /// member of the generator parity guard with a declared-but-inert diagnosability
    /// guarantee, so it can never masquerade as lowered.
    /// </para>
    /// </remarks>
    public static IWorkflowBuilder<TState> AllowDiagnosticFork<TState>(
        this IWorkflowBuilder<TState> builder,
        Func<IDiagnosticForkAnchorStage<TState>, IDiagnosticForkClosure<TState>> configure)
        where TState : class, IWorkflowState
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        if (builder is not WorkflowBuilder<TState> workflowBuilder)
        {
            throw new InvalidOperationException(
                "AllowDiagnosticFork requires the workflow builder produced by Workflow<TState>.Create.");
        }

        var forkBuilder = new DiagnosticForkBuilder<TState>();
        configure(forkBuilder);
        var definition = forkBuilder.Build();

        workflowBuilder.AddDiagnosticFork(definition);
        return builder;
    }
}
