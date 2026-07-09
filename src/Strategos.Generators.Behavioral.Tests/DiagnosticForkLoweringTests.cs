// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkLoweringTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proofs (DR-9, #151) that a declared <c>AllowDiagnosticFork</c>
/// edge lowers into a runnable saga decision site: the anchor guard, the permitted-trigger
/// + per-trigger evidence-completeness guard, the maxForks forced-exit guard, and the
/// compensation seeding that composes with the merged Compensate/OnFailure trigger site
/// (#140).
/// </summary>
/// <remarks>
/// <para>
/// The fork decision command carries <c>[SagaIdentity]</c>, so each proof seeds the saga at
/// its anchor phase and then publishes the fork command to that live saga (a deterministic
/// way to sit the saga at a declared anchor). The compile of the generated fork saga is the
/// required semantic check; these runtime proofs additionally exercise it against a real
/// Wolverine+Marten host when PostgreSQL is available.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because they share the single process-wide
/// host + invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<DiagnosticForkHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class DiagnosticForkLoweringTests
{
    private readonly DiagnosticForkHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticForkLoweringTests"/> class.
    /// </summary>
    /// <param name="host">The shared Wolverine+Marten host fixture injected by TUnit.</param>
    public DiagnosticForkLoweringTests(DiagnosticForkHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// AnchorStepIds lowering proof: a fork whose claimed anchor is NOT a declared anchor
    /// moniker is refused by the anchor guard — it seeds no compensation and the saga stays
    /// parked at its anchor phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_ForkAtUndeclaredAnchor_IsRefused()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        await this.SeedAtAnchorAsync(workflowId);

        // A permitted trigger with complete evidence, but at an anchor the edge never declared.
        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            workflowId,
            "NotADeclaredAnchor",
            "ratification_failure",
            Evidence(("provisionalStampEventId", "stamp-evt-1"), ("taints", "taint-a"))));

        // Refused: the seeded compensation never ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        // The saga is untouched — still PARKED AT ITS ANCHOR (not crashed/deleted), no fork
        // counted. Asserting the phase is the positive discriminator: a swallowed handler
        // exception would ALSO leave the count at 0, so the phase distinguishes refusal.
        var saga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(workflowId);
        await Assert.That(saga).IsNotNull();
        await Assert.That(saga!.DiagnosticForkCount_0).IsEqualTo(0);
        await Assert.That(saga.Phase).IsEqualTo(DiagnosticForkProofPhase.DfAnchorStep);
    }

    /// <summary>
    /// PermittedTriggers lowering proof (the DR-8 occurrence chokepoint): a fork carrying a
    /// permitted trigger but WITHOUT its required evidence (an empty evidence map) is refused
    /// — evidence completeness is enforced where the occurrence is born, so no compensation
    /// is seeded.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_ForkWithoutEvidence_IsRefused()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        await this.SeedAtAnchorAsync(workflowId);

        // A permitted trigger at a declared anchor, but with an EMPTY evidence map.
        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            workflowId,
            "DfAnchorStep",
            "ratification_failure",
            Evidence()));

        // Refused: the incomplete occurrence seeds no compensation.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        // Positive discriminator: still parked at the anchor phase (a refused fork, not a crash).
        var saga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(workflowId);
        await Assert.That(saga).IsNotNull();
        await Assert.That(saga!.DiagnosticForkCount_0).IsEqualTo(0);
        await Assert.That(saga.Phase).IsEqualTo(DiagnosticForkProofPhase.DfAnchorStep);
    }

    /// <summary>
    /// CompensationSeed lowering proof: a valid fork (permitted trigger + complete evidence
    /// at a declared anchor) seeds compensation through the merged trigger site (#140) — the
    /// declared seed's rollback step runs — and appends the WorkflowForked audit event
    /// carrying the fired trigger's evidence map.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_ValidFork_SeedsCompensationThroughMergedTriggerSite()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        await this.SeedAtAnchorAsync(workflowId);

        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            workflowId,
            "DfAnchorStep",
            "ratification_failure",
            Evidence(("provisionalStampEventId", "stamp-evt-1"), ("taints", "taint-a"))));

        // The fork admitted: its seeded compensation ran the declared rollback step exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(1);

        // The WorkflowForked audit event landed in the stream carrying the occurrence data.
        var forked = await this.host.WaitForStreamEventAsync<DiagnosticForkProofWorkflowForked>(workflowId);
        await Assert.That(forked).IsNotNull();
        await Assert.That(forked!.Trigger).IsEqualTo("ratification_failure");
        await Assert.That(forked.Evidence["provisionalStampEventId"]).IsEqualTo("stamp-evt-1");
        await Assert.That(forked.Evidence["taints"]).IsEqualTo("taint-a");
    }

    /// <summary>
    /// Per-trigger evidence proof (DR-8, the core fix): the <c>gate_contradiction</c> trigger
    /// requires ITS OWN declared evidence fields (<c>leftGateId</c>/<c>rightGateId</c>), not
    /// the ratification fields. A gate_contradiction fork carrying those fields is ADMITTED;
    /// the SAME trigger carrying ratification evidence (its own fields absent) is REFUSED —
    /// the hardcoded guard would have wrongly admitted the latter.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_GateContradictionFork_RequiresItsOwnDeclaredEvidence()
    {
        // Admitted: gate_contradiction WITH its declared leftGateId/rightGateId evidence.
        var admittedId = Guid.NewGuid();
        this.host.Invocations.Reset();
        await this.SeedAtAnchorAsync(admittedId);

        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            admittedId,
            "DfAnchorStep",
            "gate_contradiction",
            Evidence(("leftGateId", "gate-L"), ("rightGateId", "gate-R"))));

        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(1);

        var forked = await this.host.WaitForStreamEventAsync<DiagnosticForkProofWorkflowForked>(admittedId);
        await Assert.That(forked).IsNotNull();
        await Assert.That(forked!.Trigger).IsEqualTo("gate_contradiction");
        await Assert.That(forked.Evidence["leftGateId"]).IsEqualTo("gate-L");
        await Assert.That(forked.Evidence["rightGateId"]).IsEqualTo("gate-R");

        // Refused: gate_contradiction carrying the WRONG (ratification) evidence — its own
        // declared fields are absent. The old hardcoded guard checked only
        // provisionalStampEventId + taints, so it would have ADMITTED this; the per-trigger
        // guard refuses it.
        var refusedId = Guid.NewGuid();
        this.host.Invocations.Reset();
        await this.SeedAtAnchorAsync(refusedId);

        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            refusedId,
            "DfAnchorStep",
            "gate_contradiction",
            Evidence(("provisionalStampEventId", "stamp-evt-1"), ("taints", "taint-a"))));

        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        var refusedSaga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(refusedId);
        await Assert.That(refusedSaga).IsNotNull();
        await Assert.That(refusedSaga!.DiagnosticForkCount_0).IsEqualTo(0);
        await Assert.That(refusedSaga.Phase).IsEqualTo(DiagnosticForkProofPhase.DfAnchorStep);
    }

    /// <summary>
    /// MaxForks lowering proof: with the saga already at its maxForks bound, the next valid
    /// fork routes to the blocked / human-escalation terminal phase (the loop MaxIterations
    /// forced-exit precedent) and seeds NO further compensation.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_ForkExceedingMaxForks_RoutesToBlockedTerminal()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        // Seed already AT the maxForks bound (the edge declares MaxForks(1)).
        await this.SeedAtAnchorAsync(workflowId, diagnosticForkCount: 1);

        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            workflowId,
            "DfAnchorStep",
            "ratification_failure",
            Evidence(("provisionalStampEventId", "stamp-evt-1"), ("taints", "taint-a"))));

        // Overflowing fork blocked: no further compensation seeded.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        // Routed to the blocked / human-escalation terminal (the saga is parked, not deleted).
        var saga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(workflowId);
        await Assert.That(saga).IsNotNull();
        await Assert.That(saga!.Phase).IsEqualTo(DiagnosticForkProofPhase.ForkBlocked);
    }

    /// <summary>
    /// Builds a fork occurrence evidence map (field-name → value) from the given pairs — the
    /// occurrence-side payload keyed by the trigger's declared evidence fields.
    /// </summary>
    /// <param name="fields">The evidence field/value pairs.</param>
    /// <returns>The evidence map.</returns>
    private static IReadOnlyDictionary<string, string> Evidence(params (string Field, string Value)[] fields)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in fields)
        {
            map[field] = value;
        }

        return map;
    }

    private Task SeedAtAnchorAsync(Guid workflowId, int diagnosticForkCount = 0) =>
        this.host.SeedSagaAsync(new DiagnosticForkProofSaga
        {
            WorkflowId = workflowId,
            Phase = DiagnosticForkProofPhase.DfAnchorStep,
            State = new DiagnosticForkState { Id = workflowId, WorkflowId = workflowId },
            DiagnosticForkCount_0 = diagnosticForkCount,
            StartedAt = DateTimeOffset.UtcNow,
        });
}
