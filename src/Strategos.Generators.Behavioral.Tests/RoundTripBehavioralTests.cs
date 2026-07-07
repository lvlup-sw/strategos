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
/// host; for the fork-join family the JSON import is run end-to-end as a real-host runtime proof. The
/// corpus itself is runtime builder invocations (not parseable literal source), so these hand-authored
/// twins are the honest behavioral baseline the JSON imports are compared against.
/// </summary>
/// <remarks>
/// Requires a reachable Docker daemon for the Postgres container (see <see cref="RoundTripHostFixture"/>).
/// When Postgres is unavailable this class's fixture fails to initialize and the tests do not run; the
/// required semantic check is the BUILD compiling the imported sagas (the <c>Behavioral.Tests</c> build
/// references the generated <c>AddRoundtrip*ImportWorkflow()</c> extensions), not the runtime execution.
/// The linear family is already proven behaviorally (twin-equivalence) by <c>JsonWorkflowImportHostTests</c>
/// (task 017); this suite adds the config (retry) family twin-equivalence and the fork-join import runtime
/// proof. A C#-authored fork twin is intentionally NOT compared here — a <c>Fork→Join→Finally</c> C#
/// workflow is blocked by a pre-existing fork terminal-detection bug (see <c>RoundTripForkWorkflow</c>);
/// the fork import's structural equivalence is covered by <c>RoundTripEquivalenceTests</c> +
/// <c>RoundTripIrFidelityTests</c>.
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<RoundTripHostFixture>(Shared = SharedType.PerClass)]
public sealed class RoundTripBehavioralTests
{
    private readonly RoundTripHostFixture host;

    /// <summary>Initializes a new instance of the <see cref="RoundTripBehavioralTests"/> class.</summary>
    /// <param name="host">The shared real-host fixture, injected by TUnit.</param>
    public RoundTripBehavioralTests(RoundTripHostFixture host)
    {
        this.host = host;
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
        this.host.Invocations.Reset();

        var importId = Guid.NewGuid();
        var importCompleted = await this.host.RunWorkflowAsync<RoundtripForkImportSaga>(
            importId,
            new StartRoundtripForkImportCommand(importId, new RoundTripForkState { WorkflowId = importId }));

        await Assert.That(importCompleted).IsTrue()
            .Because("the JSON-imported fork-join saga must run to completion on a real host.");

        // The JSON import ran the fork shape: pre-fork, both parallel paths, the join, the terminal —
        // each exactly once (the fork-path steps are dispatched in parallel and gated at the join).
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportLeft))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportRight))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportJoin))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(5)
            .Because("the imported fork-join workflow must run each of its five steps exactly once.");
    }

    /// <summary>
    /// The config (retry) JSON import runs its three steps to completion exactly as its C#-authored
    /// twin — on the happy path both run the retry-configured middle step exactly once, proving the
    /// retry step config lowers through the same emitter path (INV-1) without perturbing the flow.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConfigRetryJsonImport_RunsIdentically_ToCSharpTwin()
    {
        this.host.Invocations.Reset();

        var importId = Guid.NewGuid();
        var importCompleted = await this.host.RunWorkflowAsync<RoundtripConfigImportSaga>(
            importId,
            new StartRoundtripConfigImportCommand(importId, new RoundTripConfigState { WorkflowId = importId }));

        var twinId = Guid.NewGuid();
        var twinCompleted = await this.host.RunWorkflowAsync<RoundtripConfigTwinSaga>(
            twinId,
            new StartRoundtripConfigTwinCommand(twinId, new RoundTripConfigState { WorkflowId = twinId }));

        await Assert.That(importCompleted).IsTrue()
            .Because("the JSON-imported retry-bearing saga must run to completion on a real host.");
        await Assert.That(twinCompleted).IsTrue()
            .Because("the C# retry-bearing twin must run to completion on a real host.");

        // Happy path: the retry-configured step runs exactly once in each form.
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigImportStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigImportWork))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigImportEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigTwinStart))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigTwinWork))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtConfigTwinEnd))).IsEqualTo(1);

        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(6)
            .Because("the imported retry-bearing workflow and its C# twin must run the identical number of steps.");
    }
}
