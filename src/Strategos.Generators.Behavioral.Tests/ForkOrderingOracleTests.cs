// -----------------------------------------------------------------------
// <copyright file="ForkOrderingOracleTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Proves the fork ordering oracle can actually reject a wrong run.
/// </summary>
/// <remarks>
/// <para>
/// The fork twin that consumes this oracle is skipped pending the generator fix, so it demonstrates
/// nothing about the oracle's discriminating power on its own — a skipped test never executes its
/// assertions. These tests supply that evidence directly: sequences are constructed and seeded into
/// a <see cref="WorkflowInvocationLog"/>, so no host, no container and no prior test run is involved.
/// </para>
/// <para>
/// The central case is <see cref="TerminalBeforeJoin_PassesTheCountOracle_AndIsRejectedOnOrder"/>,
/// which asserts both halves of the claim at once: the bad sequence satisfies every count assertion
/// the twin already made, and is still rejected once order is considered. That is the whole reason
/// the ordering assertion was added.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class ForkOrderingOracleTests
{
    /// <summary>The step roles of the C#-authored fork twin, as the twin itself declares them.</summary>
    private static readonly ForkStepNames TwinSteps = new(
        PreFork: nameof(RtForkTwinStart),
        LeftPath: nameof(RtForkTwinLeft),
        RightPath: nameof(RtForkTwinRight),
        Join: nameof(RtForkTwinJoin),
        Terminal: nameof(RtForkTwinEnd));

    /// <summary>
    /// A correct fork run — pre-fork, both paths, join, terminal — is accepted.
    /// </summary>
    /// <remarks>
    /// Without this the oracle could be trivially "correct" by rejecting everything, and the
    /// rejection tests below would prove nothing.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CorrectForkSequence_IsAccepted()
    {
        var log = new WorkflowInvocationLog();
        Seed(
            log,
            nameof(RtForkTwinStart),
            nameof(RtForkTwinLeft),
            nameof(RtForkTwinRight),
            nameof(RtForkTwinJoin),
            nameof(RtForkTwinEnd));

        var violation = ForkOrderingOracle.FindViolation(log.Invocations, TwinSteps);

        await Assert.That(violation).IsNull()
            .Because("pre-fork → both paths → join → terminal is the contract the fork must satisfy.");
    }

    /// <summary>
    /// The terminal-before-join sequence passes every count assertion the twin makes, and is
    /// rejected only once ordering is considered.
    /// </summary>
    /// <remarks>
    /// This is the wrong-fix shape: every step runs exactly once, so whichever step lands last
    /// calls <c>MarkCompleted()</c> and the saga document is removed exactly as a correct run would
    /// remove it. Counts and document absence both accept it. Order does not.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TerminalBeforeJoin_PassesTheCountOracle_AndIsRejectedOnOrder()
    {
        var log = new WorkflowInvocationLog();
        Seed(
            log,
            nameof(RtForkTwinStart),
            nameof(RtForkTwinLeft),
            nameof(RtForkTwinRight),
            nameof(RtForkTwinEnd),
            nameof(RtForkTwinJoin));

        // Exactly the assertions the twin's count-only oracle made — all of them still hold.
        await Assert.That(log.CountFor(nameof(RtForkTwinStart))).IsEqualTo(1);
        await Assert.That(log.CountFor(nameof(RtForkTwinLeft))).IsEqualTo(1);
        await Assert.That(log.CountFor(nameof(RtForkTwinRight))).IsEqualTo(1);
        await Assert.That(log.CountFor(nameof(RtForkTwinJoin))).IsEqualTo(1);
        await Assert.That(log.CountFor(nameof(RtForkTwinEnd))).IsEqualTo(1);
        await Assert.That(log.TotalCount).IsEqualTo(5)
            .Because("the wrong-fix sequence runs every fork step exactly once, so counts cannot reject it.");

        var violation = ForkOrderingOracle.FindViolation(log.Invocations, TwinSteps);

        await Assert.That(violation).IsNotNull()
            .Because("a terminal that ran before the join must be rejected, though every count is one.");
        await Assert.That(violation!).Contains(nameof(RtForkTwinEnd))
            .Because("the diagnostic must name the terminal step that ran out of order.");
        await Assert.That(violation!).Contains(nameof(RtForkTwinJoin))
            .Because("the diagnostic must name the join the terminal was required to follow.");
    }

    /// <summary>
    /// A join that fired before one of the parallel paths finished is rejected.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JoinBeforeBothPathsComplete_IsRejected()
    {
        var log = new WorkflowInvocationLog();
        Seed(
            log,
            nameof(RtForkTwinStart),
            nameof(RtForkTwinLeft),
            nameof(RtForkTwinJoin),
            nameof(RtForkTwinRight),
            nameof(RtForkTwinEnd));

        var violation = ForkOrderingOracle.FindViolation(log.Invocations, TwinSteps);

        await Assert.That(violation).IsNotNull()
            .Because("the join gates on BOTH paths, so firing after only the left path is wrong.");
        await Assert.That(violation!).Contains(nameof(RtForkTwinRight))
            .Because("the diagnostic must name the path the join failed to wait for.");
    }

    /// <summary>
    /// A fork path that ran before the pre-fork step is rejected.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkPathBeforePreForkStep_IsRejected()
    {
        var log = new WorkflowInvocationLog();
        Seed(
            log,
            nameof(RtForkTwinLeft),
            nameof(RtForkTwinStart),
            nameof(RtForkTwinRight),
            nameof(RtForkTwinJoin),
            nameof(RtForkTwinEnd));

        var violation = ForkOrderingOracle.FindViolation(log.Invocations, TwinSteps);

        await Assert.That(violation).IsNotNull()
            .Because("the pre-fork step must run before the fork dispatches either path.");
    }

    /// <summary>
    /// A sequence missing a step is rejected as absent rather than silently passing on order.
    /// </summary>
    /// <remarks>
    /// A missing name has no position, and treating that as "before everything" would let a fork
    /// whose join never ran satisfy "the join runs after both paths". The oracle must reject it for
    /// the real reason.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SequenceMissingTheJoin_IsRejectedAsAbsent_NotAcceptedOnOrder()
    {
        var log = new WorkflowInvocationLog();
        Seed(
            log,
            nameof(RtForkTwinStart),
            nameof(RtForkTwinLeft),
            nameof(RtForkTwinRight),
            nameof(RtForkTwinEnd));

        var violation = ForkOrderingOracle.FindViolation(log.Invocations, TwinSteps);

        await Assert.That(violation).IsNotNull()
            .Because("a join that never ran cannot satisfy the ordering contract by having no position.");
        await Assert.That(violation!).Contains("never ran")
            .Because("the diagnostic must report absence, not an ordering comparison against -1.");
        await Assert.That(violation!).Contains(nameof(RtForkTwinJoin))
            .Because("the diagnostic must name the step that is missing.");
    }

    /// <summary>
    /// The oracle reads only its own five step names, so a shared log carrying another workflow's
    /// invocations does not disturb the verdict.
    /// </summary>
    /// <remarks>
    /// The twin test runs the JSON import and the C# twin into the same session-scoped log, so the
    /// oracle is always handed a sequence containing both families.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForeignStepsInTheSharedLog_AreIgnored()
    {
        var log = new WorkflowInvocationLog();
        Seed(
            log,
            nameof(RtForkImportStart),
            nameof(RtForkImportLeft),
            nameof(RtForkImportRight),
            nameof(RtForkImportJoin),
            nameof(RtForkImportEnd),
            nameof(RtForkTwinStart),
            nameof(RtForkTwinLeft),
            nameof(RtForkTwinRight),
            nameof(RtForkTwinJoin),
            nameof(RtForkTwinEnd));

        var violation = ForkOrderingOracle.FindViolation(log.Invocations, TwinSteps);

        await Assert.That(violation).IsNull()
            .Because("the import family's invocations sit before the twin's and must not affect it.");
    }

    /// <summary>Records the given step names into the log in the order supplied.</summary>
    /// <param name="log">The log to seed.</param>
    /// <param name="stepNames">The step names, in the order they are to be recorded.</param>
    private static void Seed(WorkflowInvocationLog log, params string[] stepNames)
    {
        foreach (var stepName in stepNames)
        {
            log.Record(stepName);
        }
    }
}
