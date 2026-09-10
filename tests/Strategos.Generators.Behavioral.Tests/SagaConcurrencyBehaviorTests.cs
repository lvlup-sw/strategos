// -----------------------------------------------------------------------
// <copyright file="SagaConcurrencyBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using JasperFx;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

using Wolverine.Runtime.Handlers;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Runtime proof that a generated saga's persistence write is concurrency-guarded.
/// </summary>
/// <remarks>
/// <para>
/// Before this change the generated saga carried <c>[Version] public new long Version</c>,
/// a shadow over the base <c>Wolverine.Saga.Version</c> (int). Marten's mapping was
/// <c>ConcurrencyMode.Numeric</c>, but Wolverine's
/// <c>MartenPersistenceFrameProvider.DetermineUpdateFrame</c> tests the saga type for
/// <c>JasperFx.IRevisioned</c> — which a <c>new</c> shadow does not implement — and so
/// emitted a plain <c>documentSession.Update(saga)</c>. A plain Update leaves the
/// operation's revision at 0, which short-circuits Marten's
/// <c>? = 0 OR mt_version &lt; ?</c> guard, so two concurrent deliveries for one saga
/// both commit and the loser's whole transition is silently overwritten.
/// </para>
/// <para>
/// These tests are the two halves of that: the mechanism (a two-session race against a
/// persisted generated saga document, which is the control that already threw before
/// the fix and must keep throwing) and the consequence (many fork-join sagas driven
/// concurrently through the real host, where a lost lane-status transition wedges the
/// join forever).
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<CompensationHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class SagaConcurrencyBehaviorTests
{
    /// <summary>
    /// How many fork-join workflows are driven at once. Each one dispatches two lane
    /// commands that complete at nearly the same instant and then contend for the same
    /// saga document, so the batch makes the window very likely to be hit at least once.
    /// </summary>
    private const int ConcurrentForkRuns = 12;

    private readonly CompensationHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="SagaConcurrencyBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">The shared Wolverine + Marten host fixture, injected by TUnit.</param>
    public SagaConcurrencyBehaviorTests(CompensationHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The generated saga type satisfies the interface test Wolverine performs when it
    /// decides which persistence frame to emit. This is the whole mechanism in one line:
    /// <c>DetermineUpdateFrame</c> emits <c>UpdateSagaRevisionFrame</c> if and only if
    /// the saga type can be cast to <c>JasperFx.IRevisioned</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GeneratedSaga_IsIRevisioned_SoWolverineEmitsTheRevisionGuardedUpdate()
    {
        await Assert.That(typeof(DerivedCompensationProofSaga).IsAssignableTo(typeof(IRevisioned)))
            .IsTrue()
            .Because("MartenPersistenceFrameProvider.DetermineUpdateFrame emits "
                + "UpdateSagaRevisionFrame only for a saga type that CanBeCastTo<IRevisioned>; "
                + "otherwise it emits a plain, unguarded documentSession.Update(saga)");

        await Assert.That(typeof(RoundtripForkImportSaga).IsAssignableTo(typeof(IRevisioned)))
            .IsTrue()
            .Because("the emit is unconditional: every generated saga is IRevisioned");
    }

    /// <summary>
    /// The mechanism, reproduced against a persisted generated saga document: two Marten
    /// sessions load the same saga, both mutate it, the first commits, and the second's
    /// revision-guarded update — the exact call Wolverine now emits, with the same
    /// <c>saga.Version + 1</c> expected revision — is REJECTED rather than silently
    /// overwriting the winner.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// This is the control from the investigation that produced this change: the same
    /// document, the same mapping and the same host raised <c>ConcurrencyException</c>
    /// under <c>UpdateRevision</c> while the emitted plain <c>Update</c> committed
    /// last-write-wins. It fails if the saga ever stops being mapped to numeric
    /// revisions — e.g. if the <c>IRevisioned</c> implementation is removed again, or if
    /// a future shadow property hides the mapped member.
    /// </remarks>
    [Test]
    public async Task TwoSessionRace_OnPersistedSagaDocument_RejectsTheStaleWriterWithConcurrencyException()
    {
        var store = this.host.Store;
        var workflowId = Guid.NewGuid();

        await using (var seed = store.LightweightSession())
        {
            seed.Store(new DerivedCompensationProofSaga
            {
                WorkflowId = workflowId,
                State = new DerivedCompensationState { WorkflowId = workflowId },
            });
            await seed.SaveChangesAsync();
        }

        await using var first = store.LightweightSession();
        await using var second = store.LightweightSession();

        var seenByFirst = await first.LoadAsync<DerivedCompensationProofSaga>(workflowId);
        var seenBySecond = await second.LoadAsync<DerivedCompensationProofSaga>(workflowId);

        await Assert.That(seenByFirst).IsNotNull();
        await Assert.That(seenBySecond).IsNotNull();
        await Assert.That(seenBySecond!.Version)
            .IsEqualTo(seenByFirst!.Version)
            .Because("both writers loaded the same revision, which is what makes them race");

        seenByFirst.CompensationFailureMessage = "from-session-1";
        seenBySecond.CompensationFailureMessage = "from-session-2";

        // Exactly the frames Wolverine emits for an IRevisioned saga:
        //   var expectedSagaRevision = 0L; if (saga != null) expectedSagaRevision = saga.Version + 1;
        //   documentSession.UpdateRevision(saga, expectedSagaRevision);
        first.UpdateRevision(seenByFirst, seenByFirst.Version + 1);
        await first.SaveChangesAsync();

        second.UpdateRevision(seenBySecond, seenBySecond.Version + 1);

        await Assert.That(async () => await second.SaveChangesAsync())
            .Throws<ConcurrencyException>()
            .Because("the second writer's expected revision is stale, so Marten's "
                + "`? = 0 OR mt_version < ?` guard matches no row and the operation "
                + "fails instead of overwriting the winner");

        await using var verify = store.QuerySession();
        var settled = await verify.LoadAsync<DerivedCompensationProofSaga>(workflowId);

        await Assert.That(settled!.CompensationFailureMessage)
            .IsEqualTo("from-session-1")
            .Because("the winner's write stands; the loser's whole transaction was rejected, "
                + "not silently applied over it");
    }

    /// <summary>
    /// Many fork-join sagas driven through the real host at once. Each fork dispatches
    /// two lane commands whose completions contend for the same saga document: each one
    /// reads the saga, sets its own lane's <c>ForkPathStatus</c> and re-checks join
    /// readiness. If one lane's status write is lost, the join never becomes ready and
    /// that saga never reaches its terminal phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// The accounting assertion is the "no lost transition" half: every lane occurrence
    /// that completed must be reflected in the saga, so every run must contribute exactly
    /// one invocation of each lane step, one join and one terminal. A silently clobbered
    /// lane completion shows up either as a wedged saga (no terminal) or as a join that
    /// never observed one of the lanes.
    /// </remarks>
    [Test]
    public async Task ConcurrentForkLanes_EveryCompletedOccurrenceIsRetained_AndEverySagaReachesTerminal()
    {
        this.host.Invocations.Reset();

        var workflowIds = Enumerable.Range(0, ConcurrentForkRuns)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var startCommands = workflowIds
            .Select(id => (object)new StartRoundtripForkImportCommand(
                id,
                new RoundTripForkState { WorkflowId = id }))
            .ToArray();

        // Five steps per run: Start, the two lanes, Join, End.
        var wedged = await this.host.RunManyToTerminalAsync<RoundtripForkImportSaga>(
            workflowIds,
            startCommands,
            expectedInvocations: ConcurrentForkRuns * 5);

        await Assert.That(wedged)
            .IsEmpty()
            .Because("a saga that never reaches its terminal phase is a lost lane-status "
                + "transition: the join readiness check never sees both lanes succeed");

        // Every completed occurrence is accounted for exactly once per run.
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportStart)))
            .IsEqualTo(ConcurrentForkRuns);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportLeft)))
            .IsEqualTo(ConcurrentForkRuns);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportRight)))
            .IsEqualTo(ConcurrentForkRuns);
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportJoin)))
            .IsEqualTo(ConcurrentForkRuns)
            .Because("the join runs once per fork, and only after BOTH lane completions "
                + "are durably recorded on the saga");
        await Assert.That(this.host.Invocations.CountFor(nameof(RtForkImportEnd)))
            .IsEqualTo(ConcurrentForkRuns);
    }

    /// <summary>
    /// Wolverine's generated saga handler source calls <c>UpdateRevision</c>, not the
    /// unguarded <c>Update</c>. This reads the code Wolverine actually compiled for the
    /// running host, so it is the emitted-frame claim itself rather than a proxy for it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    /// <remarks>
    /// <c>HandlerChain.SourceCode</c> is internal to Wolverine, so it is read reflectively.
    /// The read is guarded at every step: a missing property, no matching chain, or an
    /// unrecoverable source all fail the test rather than passing an assertion over nothing.
    /// </remarks>
    [Test]
    public async Task WolverineGeneratedSagaHandler_CallsUpdateRevision_NotPlainUpdate()
    {
        var handlers = this.host.Services.GetRequiredService<HandlerGraph>();

        var sagaChains = handlers.Chains
            .Where(chain => chain.Handlers.Any(call =>
                typeof(DerivedCompensationProofSaga).IsAssignableFrom(call.HandlerType)))
            .ToArray();

        await Assert.That(sagaChains)
            .IsNotEmpty()
            .Because("the generated saga must own at least one Wolverine message chain");

        var calledPersistenceMethods = new List<string>();

        foreach (var chain in sagaChains)
        {
            var handler = handlers.HandlerFor(chain.MessageType);

            await Assert.That(handler)
                .IsNotNull()
                .Because($"Wolverine must have compiled a handler for {chain.MessageType.Name}");

            calledPersistenceMethods.AddRange(
                CalledMethodNames(handler!.GetType())
                    .Where(name => name is "Update" or "UpdateRevision" or "Store" or "Insert"));
        }

        // Saga creation legitimately calls Insert; the saga-mutating chains must all use
        // the revision-guarded update.
        await Assert.That(calledPersistenceMethods)
            .Contains("UpdateRevision")
            .Because("MartenPersistenceFrameProvider.DetermineUpdateFrame emits "
                + "UpdateSagaRevisionFrame — documentSession.UpdateRevision(saga, expected) — "
                + "for an IRevisioned saga; this reads the IL Wolverine actually compiled "
                + "for the running host, not a substring of the generator's own output");

        await Assert.That(calledPersistenceMethods)
            .DoesNotContain("Update")
            .Because("the unguarded documentSession.Update(saga) is the last-write-wins path "
                + "and must not survive alongside the revision-guarded one");
    }

    /// <summary>
    /// Reads the names of every method CALLED from the given compiled type's methods, by
    /// walking their IL for <c>call</c>/<c>callvirt</c> opcodes and resolving each operand
    /// token against the declaring module.
    /// </summary>
    /// <param name="type">The Wolverine-generated handler type.</param>
    /// <returns>The distinct names of the methods it calls.</returns>
    private static IEnumerable<string> CalledMethodNames(Type type)
    {
        const byte Call = 0x28;
        const byte Callvirt = 0x6F;

        var names = new HashSet<string>(StringComparer.Ordinal);

        // An `async` handler's real body lives in a compiler-generated nested state
        // machine, so the outer method only calls Create/Start/get_Task. Walk nested
        // types too or the scan sees nothing.
        var methods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Concat(type
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(nested => nested.GetMethods(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)));

        foreach (var method in methods)
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il is null)
            {
                continue;
            }

            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != Call && il[i] != Callvirt)
                {
                    continue;
                }

                var token = BitConverter.ToInt32(il, i + 1);

                try
                {
                    var called = method.Module.ResolveMethod(
                        token,
                        method.DeclaringType?.GetGenericArguments(),
                        method.GetGenericArguments());

                    if (called is not null)
                    {
                        names.Add(called.Name);
                    }
                }
                catch (ArgumentException)
                {
                    // The scan is opcode-approximate: a token-shaped byte sequence inside an
                    // operand does not resolve. Skip it rather than treating it as a call.
                }
            }
        }

        return names;
    }
}
