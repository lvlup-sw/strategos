// -----------------------------------------------------------------------
// <copyright file="ValidationBehaviorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// End-to-end behavioral proof (#143, G-6 6.1 backfill) that a step's
/// <c>.ValidateState(predicate, message)</c> declaration is actually honored at
/// runtime: the generated saga's yield-based Guard-Then-Dispatch start handler
/// evaluates the lowered predicate against the saga's folded <c>State</c> BEFORE
/// dispatching the step worker, and when the predicate is false it sets
/// <c>Phase = ValidationFailed</c> and yield-breaks — so the guarded step's worker
/// is never dispatched and the rest of the flow is short-circuited.
/// </summary>
/// <remarks>
/// <para>
/// This is the missing behavioral proof the #143 parity guard
/// (<c>StepConfigParityTests</c>) requires for <c>ValidateState</c>: before this
/// test, the only proof that <c>ValidateState</c> lowered was shape/golden-level
/// (<c>SagaEmitterValidationTests</c> asserting the emitted source CONTAINS the
/// guard). Those tests never run the saga, so they cannot prove the guard actually
/// short-circuits at runtime. Asserting on a real PostgreSQL-backed host that the
/// guarded step's <c>ExecuteAsync</c> never ran AND the persisted saga reached the
/// <c>ValidationFailed</c> phase is the runtime lowering proof.
/// </para>
/// <para>
/// Mutation proof: reverting the lowering so the start handler emits a standard
/// dispatch (no guard) makes this test RED — the guarded step's worker would be
/// dispatched, its <c>ExecuteAsync</c> would run (invocation count 1, not 0), and
/// the saga would proceed past the guard rather than persisting in the
/// <c>ValidationFailed</c> phase.
/// </para>
/// <para>
/// Marked <see cref="NotInParallelAttribute"/> because it shares the single
/// process-wide container + host and observes the process-shared invocation log.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ValidationHostFixture>(Shared = SharedType.PerTestSession)]
public sealed class ValidationBehaviorTests
{
    private readonly ValidationHostFixture host;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationBehaviorTests"/> class.
    /// </summary>
    /// <param name="host">
    /// The shared Wolverine+Marten host fixture, injected by TUnit and shared across
    /// the entire test session.
    /// </param>
    public ValidationBehaviorTests(ValidationHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// Starts the generated validation-proof workflow saga whose middle step declares
    /// <c>.ValidateState(s =&gt; s.IsAuthorized, "...")</c> with the predicate false at
    /// runtime. Asserts the prepare step ran once, the guarded step's worker was never
    /// dispatched (its <c>ExecuteAsync</c> ran zero times), the terminal step never ran,
    /// and the persisted saga reached the <c>ValidationFailed</c> phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Saga_StepWithValidateState_GuardFails_RoutesToValidationFailedWithoutDispatchingStep()
    {
        var workflowId = Guid.NewGuid();
        this.host.Invocations.Reset();

        var initialState = new ValidationState { WorkflowId = workflowId };
        var startCommand = new StartValidationProofCommand(workflowId, initialState);

        var phaseName = await this.host.RunAndGetPhaseAsync<ValidationProofSaga>(workflowId, startCommand);

        // The deterministic prepare step ran exactly once and folded state (leaving
        // IsAuthorized false), so the saga actually started and reached the guard.
        await Assert.That(this.host.Invocations.CountFor(nameof(ValidationPrepareStep))).IsEqualTo(1);

        // Guard proof: the lowered Guard-Then-Dispatch short-circuited the dispatch, so
        // the guarded step's ExecuteAsync NEVER ran. This is the runtime lowering proof
        // a shape/golden test cannot give.
        await Assert.That(this.host.Invocations.CountFor(nameof(ValidationGuardedStep))).IsEqualTo(0);

        // The terminal step was never reached because the guard yield-broke the flow.
        await Assert.That(this.host.Invocations.CountFor(nameof(ValidationNeverReachedStep))).IsEqualTo(0);

        // Phase proof: the persisted saga reached ValidationFailed (it is NOT completed,
        // so the document persists carrying the failed phase).
        await Assert.That(phaseName).IsEqualTo("ValidationFailed");
    }
}
