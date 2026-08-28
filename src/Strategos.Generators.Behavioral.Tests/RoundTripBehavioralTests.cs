// -----------------------------------------------------------------------
// <copyright file="RoundTripBehavioralTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Task 019 (#100), DR-15 — the round-trip real-host proofs. For the config (retry) importable
/// family a hand-authored <c>[Workflow]</c> C# source twin and its exported-JSON counterpart lower
/// through the SAME saga emitters (INV-1) and run to IDENTICAL behavior on a REAL Wolverine + Marten
/// host; for the fork-join family both the JSON import and its C# twin run end-to-end as a real-host
/// twin-equivalence proof. The corpus itself is runtime builder invocations (not parseable literal
/// source), so these hand-authored twins are the honest behavioral baseline the JSON imports are
/// compared against.
/// </summary>
/// <remarks>
/// Requires a reachable Docker daemon for the Postgres container (see <see cref="RoundTripHostFixture"/>).
/// When Postgres is unavailable this class's fixture fails to initialize and the tests do not run; the
/// required semantic check is the BUILD compiling the imported sagas (the <c>Behavioral.Tests</c> build
/// references the generated <c>AddRoundtrip*ImportWorkflow()</c> extensions), not the runtime execution.
/// The linear family is already proven behaviorally (twin-equivalence) by <c>JsonWorkflowImportHostTests</c>
/// (task 017); this suite adds the config (retry) family twin-equivalence and the fork-join twin
/// equivalence. Each test recycles the Wolverine host on the shared Postgres container so a prior
/// family's leftover inbox/outbox work cannot starve the next saga (strategos#180).
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<RoundTripHostFixture>(Shared = SharedType.PerClass)]
public sealed class RoundTripBehavioralTests
{
    /// <summary>The fork-join step roles of the JSON import, for the ordering oracle.</summary>
    private static readonly ForkStepNames ImportForkSteps = new(
        PreFork: nameof(RtForkImportStart),
        LeftPath: nameof(RtForkImportLeft),
        RightPath: nameof(RtForkImportRight),
        Join: nameof(RtForkImportJoin),
        Terminal: nameof(RtForkImportEnd));

    /// <summary>The fork-join step roles of the C#-authored twin, for the ordering oracle.</summary>
    private static readonly ForkStepNames TwinForkSteps = new(
        PreFork: nameof(RtForkTwinStart),
        LeftPath: nameof(RtForkTwinLeft),
        RightPath: nameof(RtForkTwinRight),
        Join: nameof(RtForkTwinJoin),
        Terminal: nameof(RtForkTwinEnd));

    private readonly RoundTripHostFixture host;

    /// <summary>Initializes a new instance of the <see cref="RoundTripBehavioralTests"/> class.</summary>
    /// <param name="host">The shared real-host fixture, injected by TUnit.</param>
    public RoundTripBehavioralTests(RoundTripHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Recycles the Wolverine host before each test so config/onFailure leftovers cannot starve
    /// the fork import (strategos#180). The Postgres container is reused.
    /// </summary>
    /// <returns>A task that completes when the replacement host is running.</returns>
    [Before(Test)]
    public Task RecycleHostBeforeEachTest() => this.host.RecycleHostAsync();

    /// <summary>
    /// Recycle replaces the invocation log and the host while keeping the same Postgres
    /// container, so a leftover name from a prior family cannot be observed after the call.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RecycleHost_ReplacesInvocationLog_KeepingTheSamePostgresContainer()
    {
        this.host.Invocations.Record("LeftoverFromPriorFamily");
        var connectionBefore = this.host.ConnectionString;
        var servicesBefore = this.host.Services;

        await this.host.RecycleHostAsync();

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(0)
            .Because("recycle must replace the invocation log so a prior family's leftover writer is not observed.");
        await Assert.That(this.host.Invocations.CountFor("LeftoverFromPriorFamily")).IsEqualTo(0)
            .Because("a name recorded on the previous log must not survive recycle.");
        await Assert.That(this.host.ConnectionString).IsEqualTo(connectionBefore)
            .Because("recycle must reuse the same Postgres container, not start a second one.");
        await Assert.That(ReferenceEquals(this.host.Services, servicesBefore)).IsFalse()
            .Because("recycle must start a replacement Wolverine host, not keep the polluted one.");
    }

    /// <summary>
    /// The fork-join JSON import runs its five steps (pre-fork → {left ‖ right} → join → terminal) to
    /// completion on a real host, each exactly once — proving the bridged fork workflow lowered
    /// through the SAME fork execution machinery (INV-1) as a C#-authored fork and runs correctly
    /// (parallel paths dispatched, join gated on both, terminal reached).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkJoinJsonImport_RunsAllStepsOnce_OnRealHost()
    {
        var importId = Guid.NewGuid();
        var importOutcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripForkImportSaga>(
            importId,
            new StartRoundtripForkImportCommand(importId, new RoundTripForkState { WorkflowId = importId }));

        await Assert.That(importOutcome.Completed).IsTrue()
            .Because(
                "the JSON-imported fork-join saga must run to completion on a real host. "
                + importOutcome.Diagnostic);

        // The JSON import ran the fork shape: pre-fork, both parallel paths, the join, the terminal —
        // each exactly once (the fork-path steps are dispatched in parallel and gated at the join).
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportLeft))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportRight))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportJoin))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(5)
            .Because("the imported fork-join workflow must run each of its five steps exactly once.");

        // The counts alone are identical whether the fork is real-parallel, degenerate-sequential, or
        // a join that fired early — so assert the fork ORDERING from the invocation log: the pre-fork
        // step precedes BOTH parallel paths, and the join is gated STRICTLY after both paths and before
        // the terminal. A join that fired before both paths, or a terminal that ran before the join,
        // fails here even though every count is still exactly one.
        var violation = ForkOrderingOracle.FindViolation(this.host.Invocations.Invocations, ImportForkSteps);

        await Assert.That(violation).IsNull()
            .Because("the imported fork must run pre-fork → both parallel paths → join → terminal.");
    }

    /// <summary>
    /// DR-15 fork-join twin equivalence. The importable fork-join family proves a C#
    /// <c>.Fork(...).Join&lt;T&gt;().Finally&lt;TEnd&gt;()</c> twin lowers to a saga behaviorally
    /// identical to its exported-JSON import
    /// (<see cref="ForkJoinJsonImport_RunsAllStepsOnce_OnRealHost"/>).
    /// </summary>
    /// <remarks>
    /// Both authoring forms are checked against the same ordering oracle as well as exact per-step
    /// counts. Counts alone are not sufficient: a saga that runs every step once but reaches its
    /// terminal before the join satisfies every count assertion here, so the ordering oracle is what
    /// makes this test able to fail on the property it exists to pin. Host recycle before the class's
    /// tests (strategos#180) is what makes the import half deterministic in the four-test run;
    /// strategos#155 — the C#-authoring terminal-detection defect this test was originally written
    /// for — is already fixed.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkJoinCSharpTwin_RunsIdentically_ToJsonImport()
    {
        var importId = Guid.NewGuid();
        var importOutcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripForkImportSaga>(
            importId,
            new StartRoundtripForkImportCommand(importId, new RoundTripForkState { WorkflowId = importId }));

        var twinId = Guid.NewGuid();
        var twinOutcome = await this.host.RunWorkflowWithOutcomeAsync<RoundtripForkTwinSaga>(
            twinId,
            new StartRoundtripForkTwinCommand(twinId, new RoundTripForkState { WorkflowId = twinId }));

        await Assert.That(importOutcome.Completed).IsTrue()
            .Because(
                "the JSON-imported fork-join saga must run to completion on a real host. "
                + importOutcome.Diagnostic);
        await Assert.That(twinOutcome.Completed).IsTrue()
            .Because(
                "the C# fork-join twin must run to completion on a real host. "
                + twinOutcome.Diagnostic);

        // Each authoring form runs its five fork steps exactly once — the behavioral equivalence
        // (INV-1) of a JSON-imported fork and its C#-authored twin.
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportLeft))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportRight))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportJoin))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkTwinStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkTwinLeft))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkTwinRight))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkTwinJoin))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkTwinEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(10)
            .Because("the imported fork-join workflow and its C# twin must run the identical number of steps.");

        // Counts cannot see ordering. A fix that runs all five twin steps once but places the terminal
        // BEFORE the join satisfies every assertion above — whichever step lands last calls
        // MarkCompleted(), so the saga completes and its document is removed exactly as a correct run
        // would. Equivalence to the import is a claim about the SHAPE of the run, so assert the shape:
        // both authoring forms must obey pre-fork → both paths → join → terminal.
        var importViolation = ForkOrderingOracle.FindViolation(this.host.Invocations.Invocations, ImportForkSteps);
        var twinViolation = ForkOrderingOracle.FindViolation(this.host.Invocations.Invocations, TwinForkSteps);

        await Assert.That(importViolation).IsNull()
            .Because("the JSON import must run pre-fork → both parallel paths → join → terminal.");
        await Assert.That(twinViolation).IsNull()
            .Because("the C# twin must run the identical fork ordering, not merely the identical counts.");
    }

    /// <summary>
    /// The config (retry) JSON import runs IDENTICALLY to its C#-authored twin, and the retry policy is
    /// actually EXERCISED: the retry-configured middle step is flaky — it throws on its first
    /// <see cref="RoundTripConfigRetry.InducedFailures"/> attempts and succeeds on attempt
    /// <see cref="RoundTripConfigRetry.ExpectedWorkInvocations"/>. A passing run therefore proves the
    /// retry config lowered through the same emitter path (INV-1) AND drove real retries in both forms
    /// (a config-free step would throw on attempt 1 and never complete).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConfigRetryJsonImport_RunsIdentically_ToCSharpTwin()
    {
        var importId = Guid.NewGuid();
        var importCompleted = await this.host.RunWorkflowAsync<RoundtripConfigImportSaga>(
            importId,
            new StartRoundtripConfigImportCommand(importId, new RoundTripConfigState { WorkflowId = importId }));

        var twinId = Guid.NewGuid();
        var twinCompleted = await this.host.RunWorkflowAsync<RoundtripConfigTwinSaga>(
            twinId,
            new StartRoundtripConfigTwinCommand(twinId, new RoundTripConfigState { WorkflowId = twinId }));

        await Assert.That(importCompleted).IsTrue()
            .Because("the JSON-imported retry-bearing saga must retry the flaky middle step and run to completion on a real host.");
        await Assert.That(twinCompleted).IsTrue()
            .Because("the C# retry-bearing twin must retry the flaky middle step and run to completion on a real host.");

        // The entry and terminal steps run exactly once; the retry-configured middle step is invoked
        // ExpectedWorkInvocations times (InducedFailures failures + the succeeding attempt) — the
        // observable proof the retry policy actually retried, IDENTICALLY across import and twin.
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigImportStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigImportWork)))
            .IsEqualTo(RoundTripConfigRetry.ExpectedWorkInvocations)
            .Because("the imported retry-configured step must be retried until it succeeds (InducedFailures + 1 invocations).");
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigImportEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigTwinStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigTwinWork)))
            .IsEqualTo(RoundTripConfigRetry.ExpectedWorkInvocations)
            .Because("the C# twin's retry-configured step must be retried the identical number of times as the import.");
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigTwinEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(2 * (2 + RoundTripConfigRetry.ExpectedWorkInvocations))
            .Because("the imported retry-bearing workflow and its C# twin must run the identical number of steps (start + retried work + end, twice).");
    }

    /// <summary>
    /// M11 — the onFailure importable family's bucket-(a) RUNTIME proof. The
    /// <c>failureHandlers</c>-bearing JSON import (<c>roundtrip-onfailure.workflow.json</c>) lowered to
    /// a valid saga (<c>AddRoundtripOnFailureImportWorkflow()</c> — the Behavioral.Tests BUILD compiling
    /// it is the real bucket-(a) compile proof) and runs its happy path (Start → Work → End) to
    /// completion on a real host, each step exactly once. Before this fixture the onFailure family's
    /// bucket-(a) membership was proxied ONLY by the in-memory "a saga tree was emitted" partition-gate
    /// signal, never actual compilation.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnFailureJsonImport_RunsToCompletion_OnRealHost()
    {
        var importId = Guid.NewGuid();
        var importCompleted = await this.host.RunWorkflowAsync<RoundtripOnFailureImportSaga>(
            importId,
            new StartRoundtripOnFailureImportCommand(importId, new RoundTripOnFailureState { WorkflowId = importId }));

        await Assert.That(importCompleted).IsTrue()
            .Because("the JSON-imported onFailure-bearing saga must lower and run to completion on a real host.");

        await Assert.That(this.host.Invocations.CountFor(nameof(RtOnFailureImportStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtOnFailureImportWork))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtOnFailureImportEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(3)
            .Because("the onFailure happy path runs its three top-level steps exactly once (the recovery handler is not triggered).");
    }
}
