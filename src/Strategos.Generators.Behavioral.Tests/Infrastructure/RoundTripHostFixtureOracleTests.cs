// -----------------------------------------------------------------------
// <copyright file="RoundTripHostFixtureOracleTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Tests of the harness's own completion oracle, not of any workflow.
/// </summary>
/// <remarks>
/// <para>
/// The harness reported success the moment the Marten saga document was absent. A saga that
/// was never created is absent too, so "never ran" and "ran and completed" produced the same
/// answer — and every acceptance claim in the behavioral suite rests on that answer. These
/// tests pin that the oracle now requires positive evidence the workflow ran.
/// </para>
/// <para>
/// The invocation log is shared across the whole host, so this class runs non-parallel and
/// resets the log at the start of each test.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<RoundTripHostFixture>(Shared = SharedType.PerClass)]
public sealed class RoundTripHostFixtureOracleTests
{
    /// <summary>
    /// A short budget for the runs that are expected NOT to complete, so proving a negative
    /// does not cost the default wait twice over.
    /// </summary>
    private static readonly TimeSpan NoCompletionBudget = TimeSpan.FromSeconds(5);

    private readonly RoundTripHostFixture host;

    /// <summary>Initializes a new instance of the <see cref="RoundTripHostFixtureOracleTests"/> class.</summary>
    /// <param name="host">The shared real-host fixture, injected by TUnit.</param>
    public RoundTripHostFixtureOracleTests(RoundTripHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// A start command that creates no saga and runs no step must NOT be reported as a
    /// completed workflow, even though the polled document is absent for the entire wait.
    /// This is the exact state the old oracle read as success.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunWorkflow_StartCommandCreatesNoSaga_IsNotReportedAsCompleted()
    {
        this.host.Invocations.Reset();

        var neverClaimedId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripForkImportSaga>(
            neverClaimedId,
            new HarnessProbeCommand(neverClaimedId),
            NoCompletionBudget);

        await Assert.That(outcome.DocumentRemoved)
            .IsTrue()
            .Because("no saga was ever created for this identity, so its document is absent throughout");

        await Assert.That(outcome.StepInvocations)
            .IsEqualTo(0)
            .Because("the probe command starts no workflow, so no step runs");

        await Assert.That(outcome.Completed)
            .IsFalse()
            .Because(
                "document absence alone is not evidence of completion — a saga that was never "
                + "created is absent for exactly the same reason a completed one is");

        await Assert.That(outcome.Diagnostic)
            .Contains("no step")
            .Because("the failure must say the workflow never ran, not merely that it did not complete");
    }

    /// <summary>
    /// The boolean-returning overload — the one every existing proof calls — carries the same
    /// strengthened meaning, so an assertion of <c>IsTrue()</c> now fails on a run that did
    /// nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunWorkflow_BooleanOverload_ReportsFalseWhenNothingRan()
    {
        this.host.Invocations.Reset();

        var neverClaimedId = Guid.NewGuid();
        var completed = await this.host.RunWorkflowAsync<RoundtripForkImportSaga>(
            neverClaimedId,
            new HarnessProbeCommand(neverClaimedId),
            NoCompletionBudget);

        await Assert.That(completed)
            .IsFalse()
            .Because(
                "every behavioral proof asserts IsTrue() on this boolean, so it must not be "
                + "satisfiable by a workflow that never ran");
    }

    /// <summary>
    /// The strengthened oracle still accepts a workflow that genuinely completes, and reports
    /// the work it did — so hardening it did not simply make completion unreachable.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunWorkflow_WorkflowThatRunsToCompletion_ReportsCompletedWithItsInvocations()
    {
        this.host.Invocations.Reset();

        var importId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripForkImportSaga>(
            importId,
            new StartRoundtripForkImportCommand(importId, new RoundTripForkState { WorkflowId = importId }));

        await Assert.That(outcome.Completed)
            .IsTrue()
            .Because("a workflow that runs its steps and reaches its terminal phase must still be accepted");

        await Assert.That(outcome.DocumentRemoved)
            .IsTrue()
            .Because("reaching the terminal phase removes the Marten saga document");

        await Assert.That(outcome.StepInvocations)
            .IsGreaterThan(0)
            .Because("the completed run must report the positive evidence it was accepted on");
    }

    /// <summary>
    /// Invocation evidence is attributed to the call that produced it. The log is shared
    /// across the whole host, so a test body that runs two workflows must not be able to have
    /// the first one's work vouch for the second — an absolute count would let it.
    /// </summary>
    /// <remarks>
    /// The prior evidence is seeded into the shared log directly rather than by running a
    /// second real workflow. What is under test is the harness's baselining, and a seeded
    /// non-empty log establishes that precondition exactly, with no dependence on a
    /// particular workflow completing twice in one host process.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunWorkflow_WithPriorEvidenceInTheSharedLog_DoesNotInheritIt()
    {
        this.host.Invocations.Reset();

        // Stand in for work an earlier run in the same test body already recorded.
        this.host.Invocations.Record("ValidateOrder");
        this.host.Invocations.Record("ShipOrder");

        await Assert.That(this.host.Invocations.TotalCount)
            .IsGreaterThan(0)
            .Because("the precondition under test is a shared log that is already non-empty");

        var neverClaimedId = Guid.NewGuid();
        var outcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripForkImportSaga>(
            neverClaimedId,
            new HarnessProbeCommand(neverClaimedId),
            NoCompletionBudget);

        await Assert.That(outcome.StepInvocations)
            .IsEqualTo(0)
            .Because("evidence recorded before this call must not be counted toward it");

        await Assert.That(outcome.Completed)
            .IsFalse()
            .Because(
                "a run that did no work must fail even when the shared invocation log is already "
                + "non-empty from an earlier run in the same test");
    }
}
