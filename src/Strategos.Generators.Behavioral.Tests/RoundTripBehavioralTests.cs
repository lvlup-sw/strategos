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

        // The counts alone are identical whether the fork is real-parallel, degenerate-sequential, or
        // a join that fired early — so assert the fork ORDERING from the invocation log: the pre-fork
        // step precedes BOTH parallel paths, and the join is gated STRICTLY after both paths and before
        // the terminal. A join that fired before both paths, or a terminal that ran before the join,
        // fails here even though every count is still exactly one.
        var order = this.host.Invocations.Invocations.ToList();
        int FirstIndexOf(string step) => order.IndexOf(step);

        var start = FirstIndexOf(nameof(RtForkImportStart));
        var left = FirstIndexOf(nameof(RtForkImportLeft));
        var right = FirstIndexOf(nameof(RtForkImportRight));
        var join = FirstIndexOf(nameof(RtForkImportJoin));
        var end = FirstIndexOf(nameof(RtForkImportEnd));

        await Assert.That(start).IsLessThan(left)
            .Because("the pre-fork step must run before the left parallel path.");
        await Assert.That(start).IsLessThan(right)
            .Because("the pre-fork step must run before the right parallel path.");
        await Assert.That(join).IsGreaterThan(left)
            .Because("the join must run strictly after the left parallel path completes.");
        await Assert.That(join).IsGreaterThan(right)
            .Because("the join must run strictly after the right parallel path completes.");
        await Assert.That(join).IsLessThan(end)
            .Because("the join must run before the terminal step (the join gates the terminal).");
    }

    /// <summary>
    /// DR-15 fork-join twin equivalence — DEFERRED, pending strategos#155. The importable fork-join
    /// family requires proving a C# <c>.Fork(...).Join&lt;T&gt;().Finally&lt;TEnd&gt;()</c> twin lowers
    /// to a saga behaviorally identical to its exported-JSON import
    /// (<see cref="ForkJoinJsonImport_RunsAllStepsOnce_OnRealHost"/>). The C# twin
    /// (<c>AddRoundtripForkTwinWorkflow()</c>) compiles and registers, but does NOT run to completion
    /// on the current generator: C#-authoring's <c>StepNames</c> extraction APPENDS the fork-path steps
    /// AFTER the top-level terminal, so the terminal is not last and its completed handler chains back
    /// to a fork-path step instead of calling <c>MarkCompleted()</c> (strategos#155). The JSON import
    /// side is unaffected because the wire export lists the fork-path steps as top-level steps in
    /// document order, so the terminal ends up last and terminates correctly.
    /// </summary>
    /// <remarks>
    /// This test lives in the suite (not just a code comment) so the equivalence claim is
    /// machine-checked and will go GREEN automatically once strategos#155 lands the terminal-detection
    /// fix — at which point the <see cref="SkipAttribute"/> is removed. Do NOT try to fix strategos#155
    /// here; the maintainer decision is to ship the fork-join family with this machine-checked deferral.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Skip("blocked on strategos#155: fork-join terminal-detection ordering (C# .Fork().Join().Finally() twin does not complete)")]
    public async Task ForkJoinCSharpTwin_RunsIdentically_ToJsonImport()
    {
        this.host.Invocations.Reset();

        var importId = Guid.NewGuid();
        var importCompleted = await this.host.RunWorkflowAsync<RoundtripForkImportSaga>(
            importId,
            new StartRoundtripForkImportCommand(importId, new RoundTripForkState { WorkflowId = importId }));

        var twinId = Guid.NewGuid();
        var twinCompleted = await this.host.RunWorkflowAsync<RoundtripForkTwinSaga>(
            twinId,
            new StartRoundtripForkTwinCommand(twinId, new RoundTripForkState { WorkflowId = twinId }));

        await Assert.That(importCompleted).IsTrue()
            .Because("the JSON-imported fork-join saga must run to completion on a real host.");
        await Assert.That(twinCompleted).IsTrue()
            .Because("the C# fork-join twin must run to completion on a real host (blocked by strategos#155).");

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
        this.host.Invocations.Reset();

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
