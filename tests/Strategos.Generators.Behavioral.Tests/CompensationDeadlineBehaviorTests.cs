// -----------------------------------------------------------------------
// <copyright file="CompensationDeadlineBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof that the deadline authored with
/// <c>Compensate&lt;T&gt;(inverseAction, timeout)</c> governs one inverse execution on a
/// real Wolverine+Marten host.
/// </summary>
/// <remarks>
/// <para>
/// The authored value is the only thing that decides the outcome. The workflow's inverse
/// step takes twenty seconds; the authored deadline is one second and the generated default
/// is five minutes. If the deadline did not lower, the rollback would finish and the saga
/// would complete (its document removed) well inside this test's budget. Because it does
/// lower, the generated <c>CompensationRollbackTimeout</c> arrives while the inverse is
/// still in flight, the saga records an unknown inverse outcome and is RETAINED for
/// operator inspection rather than assumed successful.
/// </para>
/// <para>
/// This is what makes <c>CompensationConfiguration.Timeout</c> / <c>WithTimeout</c>
/// classifiable as Lowered in the step-config parity guard: before the authoring overloads
/// existed, no DSL author could set the value at all, so nothing downstream of it could be
/// proven by running the saga.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single process-wide
/// container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<CompensationHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class CompensationDeadlineBehaviorTests
{
    private readonly CompensationHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompensationDeadlineBehaviorTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared across the
    /// entire test session.
    /// </param>
    public CompensationDeadlineBehaviorTests(CompensationHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Runs the deadline-proof workflow: its forward step completes, its terminal step
    /// fails, and the derived rollback dispatches an inverse that outlives the authored
    /// one-second deadline. Asserts the saga observed the rollback timeout and retained
    /// itself with an unknown inverse outcome.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_AuthoredInverseDeadline_FiresRollbackTimeoutBeforeTheInverseFinishes()
    {
        this.host.Invocations.Reset();
        this.host.ExecutionIds.Reset();

        var workflowId = Guid.NewGuid();
        var startCommand = new StartCompensationDeadlineProofCommand(
            workflowId,
            new CompensationDeadlineState { WorkflowId = workflowId });

        var settled = await this.host.RunToSettledSagaAsync<CompensationDeadlineProofSaga>(
            workflowId,
            startCommand,
            saga => saga.CompensationOutcomeUnknown,
            // Comfortably above the authored deadline and the inverse's own duration, and
            // far below the generated default deadline: a saga that had taken the default
            // would have COMPLETED (and been deleted) inside this window instead.
            TimeSpan.FromSeconds(60));

        await Assert.That(settled).IsNotNull()
            .Because(
                "the authored one-second inverse deadline must fire while the twenty-second "
                + "inverse is still running, leaving the saga retained with an unknown outcome");

        await Assert.That(settled!.CompensationFailureMessage)
            .IsEqualTo("Inverse outcome unknown after timeout.")
            .Because("the retained saga must name the rollback timeout as the reason");

        // The rollback really was dispatched — the timeout did not fire on an empty journal.
        await Assert.That(this.host.Invocations.CountFor(nameof(DeadlineForwardStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(DeadlineSlowUndoStep)))
            .IsGreaterThanOrEqualTo(1);

        // The failing step's own inverse never runs: it never completed forward.
        await Assert.That(this.host.Invocations.CountFor(nameof(DeadlineUnreachedUndoStep)))
            .IsEqualTo(0);

        // The authored deadline is strictly shorter than the inverse it bounds; if that ever
        // stopped being true this test would pass for the wrong reason.
        await Assert.That(CompensationDeadlineProofWorkflowDefinition.AuthoredInverseDeadline)
            .IsLessThan(DeadlineSlowUndoStep.InverseDuration);
    }
}
