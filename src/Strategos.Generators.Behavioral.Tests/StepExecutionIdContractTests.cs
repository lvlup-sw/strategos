// -----------------------------------------------------------------------
// <copyright file="StepExecutionIdContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof that <c>StepContext.ExecutionId</c> is the supported
/// idempotency key for a dispatched step: a redelivery of the same generated worker
/// command reaches user code with the same value, that value is the saga's pinned
/// <c>StepExecutionId</c>, and an inverse dispatch surfaces its rollback identity
/// there with <c>IsCompensation</c> derived to <see langword="true"/>.
/// </summary>
/// <remarks>
/// <para>
/// Before this contract existed the forward execution identity was reachable only by
/// parsing <c>CorrelationId</c> — a tracing value the XML doc and migration guide
/// forbid parsing — so a step with external effects had no supported way to
/// deduplicate a redelivery. These assertions run against the generated handler and
/// the real Wolverine+Marten host, not a hand-built context, so a regression in the
/// emitter (not just in the record) reddens them.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes process-shared probes.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<CompensationHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class StepExecutionIdContractTests
{
    private readonly CompensationHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepExecutionIdContractTests"/>
    /// class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared across
    /// the entire test session.
    /// </param>
    public StepExecutionIdContractTests(CompensationHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Handles the SAME generated forward worker command twice through the generated
    /// worker handler resolved from the running host — the redelivery case Wolverine's
    /// at-least-once inbox permits. Asserts the step observed one and the same
    /// <c>ExecutionId</c> on both dispatches, that it equals the command's
    /// <c>StepExecutionId</c>, and that neither dispatch was classified as a
    /// compensation.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForwardRedelivery_OfTheSameWorkerCommand_ObservesTheSameExecutionId()
    {
        this.host.Invocations.Reset();
        this.host.ExecutionIds.Reset();

        var workflowId = Guid.NewGuid();
        var stepExecutionId = Guid.NewGuid();
        var command = new ExecuteDerivedForwardAStepWorkerCommand(
            workflowId,
            stepExecutionId,
            new DerivedCompensationState { WorkflowId = workflowId });

        using var scope = this.host.Services.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<DerivedForwardAStepHandler>();

        // Two deliveries of the identical command, exactly as a redelivered inbox
        // message would arrive.
        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        var observed = this.host.ExecutionIds.For(nameof(DerivedForwardAStep));

        await Assert.That(observed.Count).IsEqualTo(2);
        await Assert.That(observed[0].ExecutionId).IsEqualTo(stepExecutionId);
        await Assert.That(observed[1].ExecutionId).IsEqualTo(observed[0].ExecutionId);
        await Assert.That(observed[0].IsCompensation).IsFalse();
        await Assert.That(observed[1].IsCompensation).IsFalse();
        await Assert.That(observed[0].RollbackId).IsNull();
    }

    /// <summary>
    /// Runs the typed derived-compensation workflow to its terminal phase on the real
    /// host, so the saga mints forward execution ids, journals the completed prefix,
    /// and dispatches the derived inverse steps. Asserts every forward step saw a
    /// non-empty forward <c>ExecutionId</c> with no rollback identity, and every
    /// inverse step saw <c>ExecutionId == RollbackId</c> with <c>IsCompensation</c>
    /// true — the compensation half of the idempotency contract.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CompensationDispatch_ObservesRollbackIdAsTheExecutionId()
    {
        this.host.Invocations.Reset();
        this.host.ExecutionIds.Reset();

        var workflowId = Guid.NewGuid();
        var startCommand = new StartDerivedCompensationProofCommand(
            workflowId,
            new DerivedCompensationState { WorkflowId = workflowId });

        var reachedTerminal = await this.host.RunToTerminalAsync<DerivedCompensationProofSaga>(
            workflowId,
            startCommand);

        await Assert.That(reachedTerminal).IsTrue();

        // Forward dispatches: a durable, non-empty identity that is NOT a rollback id.
        foreach (var stepName in new[]
        {
            nameof(DerivedForwardAStep),
            nameof(DerivedForwardBStep),
            nameof(DerivedFailingCStep),
        })
        {
            var forward = this.host.ExecutionIds.For(stepName);
            await Assert.That(forward.Count).IsGreaterThan(0);

            foreach (var observation in forward)
            {
                await Assert.That(observation.ExecutionId).IsNotEqualTo(Guid.Empty);
                await Assert.That(observation.RollbackId).IsNull();
                await Assert.That(observation.IsCompensation).IsFalse();
            }
        }

        // Inverse dispatches: the execution identity IS the rollback id, and the
        // derived IsCompensation flag agrees with it by construction.
        var inverse = this.host.ExecutionIds.For(nameof(DerivedUndoBStep))
            .Concat(this.host.ExecutionIds.For(nameof(DerivedUndoAStep)))
            .ToList();

        await Assert.That(inverse.Count).IsEqualTo(2);

        foreach (var observation in inverse)
        {
            await Assert.That(observation.IsCompensation).IsTrue();
            await Assert.That(observation.RollbackId).IsNotNull();
            await Assert.That(observation.RollbackId!.Value).IsNotEqualTo(Guid.Empty);
            await Assert.That(observation.ExecutionId).IsEqualTo(observation.RollbackId!.Value);
        }

        // The two inverse dispatches are distinct executions, so they carry distinct
        // idempotency keys.
        await Assert.That(inverse[0].ExecutionId).IsNotEqualTo(inverse[1].ExecutionId);
    }
}
