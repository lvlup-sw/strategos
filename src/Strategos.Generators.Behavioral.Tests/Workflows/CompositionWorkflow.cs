// -----------------------------------------------------------------------
// <copyright file="CompositionWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

// =============================================================================
// Composition fixtures (epic #135, DR-10, T019/T020/T021).
//
// These workflows are the FIRST time all four lowered step-resilience
// capabilities are declared together on a SINGLE step:
//
//     .WithRetry(n).WithTimeout(t).Compensate<TRollback>()
//         .RequireConfidence(x).OnLowConfidence(alt => alt.Then<THandler>())
//
// The four capabilities lower onto DISJOINT generated surfaces (so they compose
// without a duplicate-Handle CS0111 collision):
//   * retry + compensation  -> worker handler's static Configure(HandlerChain)
//   * timeout               -> StepStart cascade of {Phase}Timeout + saga Handle
//   * confidence            -> StepCompleted handler routing on evt.Confidence
//
// The three precedence scenarios (T019) need DISTINCT runtime behavior, so each
// lives in its own [Workflow] definition with its own distinct step CLR types
// (the generator emits worker handlers / commands / events per step TYPE within
// one compilation, so reusing a step type across two [Workflow]s would emit
// duplicate generated types -> CS0101).
//
// T020 (timeout-vs-retry race) and T021 (immutable input across retries) get
// their own dedicated workflows further down.
// =============================================================================

/// <summary>
/// Immutable state shared by the composition fixtures (DR-10). A step that
/// always throws records nothing in state; the deterministic steps fold a
/// monotonically increasing <see cref="StepCount"/> so a test can confirm how
/// far the saga progressed. <see cref="LastSeenInput"/> records the input state
/// instance each retried attempt received (T021 / INV-7).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer the saga uses to fold each step's returned state.
/// </remarks>
[WorkflowState]
public sealed record CompositionState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Gets an opaque token (T021): a per-run marker the test sets so a retried
    /// step can confirm Wolverine re-delivers the SAME immutable input envelope
    /// across attempts rather than a mutated one.
    /// </summary>
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// Permanent failure raised by the always-throwing composition step on every
/// invocation, so its <c>.WithRetry(2)</c> exhausts and the lowered compensation
/// path fires.
/// </summary>
public sealed class CompositionPermanentException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositionPermanentException"/>
    /// class with the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CompositionPermanentException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Transient failure raised by the transient-then-success composition step on its
/// first two attempts to exercise the lowered retry policy under composition.
/// </summary>
public sealed class CompositionTransientException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositionTransientException"/>
    /// class with the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CompositionTransientException(string message)
        : base(message)
    {
    }
}

// =============================================================================
// SCENARIO 1 (T019) — transient-then-success, HIGH confidence.
// The all-four step throws on attempts 1-2, then on attempt 3 succeeds with HIGH
// confidence (0.9 >= 0.85). Expected precedence: retry carries it through, then
// the completed handler sees acceptable confidence and proceeds on the primary
// path. No compensation, no low-confidence route.
// =============================================================================

/// <summary>Leading kickoff step for the transient scenario (timed steps need a
/// preceding step because <c>StartWith</c> has no configure overload).</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TransientKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TransientKickoffStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// The all-four-resilience step for scenario 1. Throws a transient exception on
/// attempts 1 and 2, then succeeds on attempt 3 with HIGH confidence (0.9). The
/// recorded invocation count therefore equals the number of attempts Wolverine
/// made under the composed retry policy.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TransientAllResilienceStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TransientAllResilienceStep));
        var attempt = this.log.CountFor(nameof(TransientAllResilienceStep));

        if (attempt < 3)
        {
            throw new CompositionTransientException(
                $"TransientAllResilienceStep transient failure on attempt {attempt}.");
        }

        var updated = state with { StepCount = state.StepCount + 1 };

        // High confidence (>= 0.85): the completed handler must proceed on the
        // primary path, NOT route to the low-confidence handler.
        return Task.FromResult(StepResult<CompositionState>.WithConfidence(updated, 0.9));
    }
}

/// <summary>The low-confidence handler for scenario 1. Must NOT run (the step
/// succeeds with high confidence). Its zero invocation count proves the
/// confidence gate did not divert.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TransientReviewStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TransientReviewStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>The rollback (compensation) step for scenario 1. Must NOT run (the
/// step eventually succeeds). Its zero invocation count proves compensation did
/// not fire on a successful step.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TransientRollbackStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TransientRollbackStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>The terminal step for scenario 1. Runs on the primary path once the
/// transient step finally succeeds with acceptable confidence; its completion
/// drives the saga to its terminal Completed phase.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class TransientFinishStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(TransientFinishStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// Scenario 1 composition workflow: the all-four-resilience step is
/// transient-then-success with HIGH confidence, so retry carries it through and
/// the saga proceeds on the primary path to <see cref="TransientFinishStep"/>.
/// </summary>
[Workflow("composition-transient")]
public static partial class CompositionTransientWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff -> all-four step
    /// (<c>.WithRetry(2).WithTimeout(30s).Compensate&lt;TransientRollbackStep&gt;()
    /// .RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;TransientReviewStep&gt;())</c>)
    /// -> terminal finish.
    /// </summary>
    public static WorkflowDefinition<CompositionState> Definition => Workflow<CompositionState>
        .Create("composition-transient")
        .StartWith<TransientKickoffStep>()
        .Then<TransientAllResilienceStep>(step => step
            .WithRetry(2)
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Compensate<TransientRollbackStep>()
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt.Then<TransientReviewStep>()))
        .Finally<TransientFinishStep>();
}

// =============================================================================
// SCENARIO 2 (T019) — first-try success, LOW confidence (0.5).
// The all-four step succeeds on its first attempt but returns LOW confidence
// (0.5 < 0.85). Expected precedence: success-but-low-confidence is NOT a failure,
// so compensation must NOT run; the completed handler routes to the
// OnLowConfidence handler step instead.
// =============================================================================

/// <summary>Leading kickoff step for the low-confidence scenario.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LowConfKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LowConfKickoffStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// The all-four-resilience step for scenario 2. Succeeds on the FIRST attempt
/// (never throws) but returns LOW confidence (0.5 &lt; 0.85). Proves that a
/// successful-but-low-confidence result routes to the OnLowConfidence handler and
/// does NOT trip the compensation (failure) path.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LowConfAllResilienceStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LowConfAllResilienceStep));
        var updated = state with { StepCount = state.StepCount + 1 };

        // Low confidence (< 0.85): the completed handler must route to the
        // OnLowConfidence handler. The step still SUCCEEDED, so compensation must
        // not run.
        return Task.FromResult(StepResult<CompositionState>.WithConfidence(updated, 0.5));
    }
}

/// <summary>The low-confidence handler for scenario 2. As a single-step
/// OnLowConfidence handler it terminates the workflow (the generated handler
/// calls <c>MarkCompleted()</c>). Its single invocation is the routing proof.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LowConfReviewStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LowConfReviewStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>The rollback step for scenario 2. Must NOT run: the step succeeded
/// (low confidence is not a failure), so the compensation path never fires.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LowConfRollbackStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LowConfRollbackStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>The primary finish step for scenario 2. Must NOT run: the gate
/// diverts to the handler branch (which completes the saga) before this step is
/// reached.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class LowConfFinishStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(LowConfFinishStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// Scenario 2 composition workflow: the all-four-resilience step succeeds first
/// try with LOW confidence, so the saga routes to <see cref="LowConfReviewStep"/>
/// (NOT compensation) and the primary finish step is skipped.
/// </summary>
[Workflow("composition-low-confidence")]
public static partial class CompositionLowConfidenceWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff -> all-four step (succeeds first try,
    /// confidence 0.5) -> terminal finish (skipped via the low-confidence route).
    /// </summary>
    public static WorkflowDefinition<CompositionState> Definition => Workflow<CompositionState>
        .Create("composition-low-confidence")
        .StartWith<LowConfKickoffStep>()
        .Then<LowConfAllResilienceStep>(step => step
            .WithRetry(2)
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Compensate<LowConfRollbackStep>()
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt.Then<LowConfReviewStep>()))
        .Finally<LowConfFinishStep>();
}

// =============================================================================
// SCENARIO 3 (T019) — always throws.
// The all-four step throws on EVERY attempt, so the retry policy exhausts and the
// lowered compensation path fires. Expected precedence: retries exhaust ->
// compensation (rollback) runs -> terminal Failed. No low-confidence route (the
// completed event never fires).
// =============================================================================

/// <summary>Leading kickoff step for the always-fail scenario.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailKickoffStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailKickoffStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// The all-four-resilience step for scenario 3. Throws on EVERY invocation, so
/// the composed <c>.WithRetry(2)</c> exhausts (initial + two retries = three
/// attempts) and the lowered compensation path fires. Its recorded count is the
/// retry-exhaustion proof under composition.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailAllResilienceStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailAllResilienceStep));

        throw new CompositionPermanentException(
            "FailAllResilienceStep always fails to force retry exhaustion and compensation under composition.");
    }
}

/// <summary>The low-confidence handler for scenario 3. Must NOT run: the step
/// never produces a completed event (it always throws), so the confidence gate is
/// never reached.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailReviewStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailReviewStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>The rollback (compensation) step for scenario 3. Runs exactly once
/// when the failing step exhausts its retries. INV-7: returns NEW state rather
/// than mutating the input.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailRollbackStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailRollbackStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>The terminal step for scenario 3. Must NOT run: compensation routes
/// the saga to Failed before the happy path is reached.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailFinishStep(WorkflowInvocationLog log) : IWorkflowStep<CompositionState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompositionState>> ExecuteAsync(
        CompositionState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailFinishStep));
        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompositionState>.FromState(updated));
    }
}

/// <summary>
/// Scenario 3 composition workflow: the all-four-resilience step always throws,
/// so retries exhaust and the compensation (<see cref="FailRollbackStep"/>) runs,
/// routing the saga to its terminal Failed phase.
/// </summary>
[Workflow("composition-fail")]
public static partial class CompositionFailWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: kickoff -> all-four step (always throws) ->
    /// terminal finish (never reached; compensation routes to Failed).
    /// </summary>
    public static WorkflowDefinition<CompositionState> Definition => Workflow<CompositionState>
        .Create("composition-fail")
        .StartWith<FailKickoffStep>()
        .Then<FailAllResilienceStep>(step => step
            .WithRetry(2)
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Compensate<FailRollbackStep>()
            .RequireConfidence(0.85)
            .OnLowConfidence(alt => alt.Then<FailReviewStep>()))
        .Finally<FailFinishStep>();
}
