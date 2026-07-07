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
/// + evidence-completeness guard, the maxForks forced-exit guard, and the compensation
/// seeding that composes with the merged Compensate/OnFailure trigger site (#140).
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
            "stamp-evt-1",
            new[] { "taint-a" }));

        // Refused: the seeded compensation never ran.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        // The saga is untouched — still parked at its anchor, no fork counted.
        var saga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(workflowId);
        await Assert.That(saga).IsNotNull();
        await Assert.That(saga!.DiagnosticForkCount).IsEqualTo(0);
    }

    /// <summary>
    /// PermittedTriggers lowering proof (the DR-8 occurrence chokepoint): a fork carrying a
    /// permitted trigger but WITHOUT its required evidence is refused — evidence completeness
    /// is enforced where the occurrence is born, so no compensation is seeded.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Behavioral_ForkWithoutEvidence_IsRefused()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();
        await this.SeedAtAnchorAsync(workflowId);

        // A permitted trigger at a declared anchor, but with EMPTY evidence.
        await this.host.PublishForkAsync(new ForkDiagnosticForkProofCommand(
            workflowId,
            "DfAnchorStep",
            "ratification_failure",
            string.Empty,
            Array.Empty<string>()));

        // Refused: the incomplete occurrence seeds no compensation.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        var saga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(workflowId);
        await Assert.That(saga).IsNotNull();
        await Assert.That(saga!.DiagnosticForkCount).IsEqualTo(0);
    }

    /// <summary>
    /// CompensationSeed lowering proof: a valid fork (permitted trigger + complete evidence
    /// at a declared anchor) seeds compensation through the merged trigger site (#140) — the
    /// declared seed's rollback step runs — and appends the WorkflowForked audit event.
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
            "stamp-evt-1",
            new[] { "taint-a", "taint-b" }));

        // The fork admitted: its seeded compensation ran the declared rollback step exactly once.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(1);

        // The WorkflowForked audit event landed in the stream carrying the occurrence data.
        var forked = await this.host.WaitForStreamEventAsync<DiagnosticForkProofWorkflowForked>(workflowId);
        await Assert.That(forked).IsNotNull();
        await Assert.That(forked!.Trigger).IsEqualTo("ratification_failure");
        await Assert.That(forked.ProvisionalStampEventId).IsEqualTo("stamp-evt-1");
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
            "stamp-evt-1",
            new[] { "taint-a" }));

        // Overflowing fork blocked: no further compensation seeded.
        await Assert.That(this.host.Invocations.CountFor(nameof(DfRollbackStep))).IsEqualTo(0);

        // Routed to the blocked / human-escalation terminal (the saga is parked, not deleted).
        var saga = await this.host.LoadSagaAsync<DiagnosticForkProofSaga>(workflowId);
        await Assert.That(saga).IsNotNull();
        await Assert.That(saga!.Phase).IsEqualTo(DiagnosticForkProofPhase.ForkBlocked);
    }

    private Task SeedAtAnchorAsync(Guid workflowId, int diagnosticForkCount = 0) =>
        this.host.SeedSagaAsync(new DiagnosticForkProofSaga
        {
            WorkflowId = workflowId,
            Phase = DiagnosticForkProofPhase.DfAnchorStep,
            State = new DiagnosticForkState { Id = workflowId, WorkflowId = workflowId },
            DiagnosticForkCount = diagnosticForkCount,
            StartedAt = DateTimeOffset.UtcNow,
        });
}
