// -----------------------------------------------------------------------
// <copyright file="LoopExitBranchFinallyTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Emission proofs that a loop-exit branch's rejoining case dispatches the declared terminal.
/// </summary>
/// <remarks>
/// <para>
/// Loop-exit branches live on the loop and are deliberately absent from the workflow's branch
/// collection. Path-info and the dedicated handler loop that walk only that collection never
/// emit a path-end handler for those cases, so a rejoining case completes the saga instead of
/// publishing <c>Start{Finally}Command</c> (#184).
/// </para>
/// <para>
/// The fixture is mixed on purpose. Marking every path complete would satisfy the
/// <c>.Complete()</c> sibling on its own, and routing every path to the terminal would satisfy
/// the rejoining case on its own; only reading the case satisfies both.
/// </para>
/// <para>
/// The discriminator is an enum rather than a <c>bool</c>: a bool-discriminated branch does not
/// compile (#179).
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class LoopExitBranchFinallyTests
{
    /// <summary>
    /// The generated hint name of the mixed fixture's saga.
    /// </summary>
    private const string SagaHintName = "SettleClaimSaga.g.cs";

    /// <summary>
    /// A loop-exit branch mixing a rejoining case with a workflow-ending case, ahead of a
    /// declared terminal.
    /// </summary>
    private const string MixedLoopExitWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimOutcome
        {
            Pay = 0,
            Deny = 1,
        }

        public sealed record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool InvestigationComplete { get; init; }
            public ClaimOutcome Outcome { get; init; }
        }

        public sealed class OpenClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class GatherEvidence : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class PayClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class DenyClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class CloseClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("settle-claim")]
        public static partial class SettleClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("settle-claim")
                .StartWith<OpenClaim>()
                .RepeatUntil(
                    state => state.InvestigationComplete,
                    "Investigation",
                    loop => loop.Then<GatherEvidence>(),
                    maxIterations: 5)
                .Branch(state => state.Outcome,
                    BranchCase<ClaimState, ClaimOutcome>.When(
                        ClaimOutcome.Pay, path => path.Then<PayClaim>()),
                    BranchCase<ClaimState, ClaimOutcome>.Otherwise(
                        path => path.Then<DenyClaim>().Complete()))
                .Finally<CloseClaim>();
        }
        """;

    /// <summary>
    /// A rejoining loop-exit case publishes the start command for the declared terminal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoopExit_RejoiningCase_PublishesStartCommandForFinally()
    {
        var handler = HandlerFor("PayClaimCompleted");

        await Assert.That(handler).Contains("return new StartCloseClaimCommand(WorkflowId);")
            .Because("a rejoining loop-exit case must dispatch the declared Finally step.");
        await Assert.That(handler).DoesNotContain("MarkCompleted();")
            .Because("completing at the rejoining case is the skip that left Finally with no incoming edge.");
    }

    /// <summary>
    /// The sibling case that declared <c>.Complete()</c> still ends the workflow at its own last
    /// step and never routes to the declared terminal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoopExit_TerminalCase_MarksCompletedAtCaseLastStep()
    {
        var handler = HandlerFor("DenyClaimCompleted");

        await Assert.That(handler).Contains("MarkCompleted();")
            .Because("the case declared .Complete(), so its last step ends the workflow.");
        await Assert.That(handler).Contains("public void Handle(")
            .Because("a workflow-ending path returns no follow-on command.");
        await Assert.That(handler).DoesNotContain("StartCloseClaimCommand")
            .Because("a denied claim must never be routed to the declared terminal.");
    }

    /// <summary>
    /// Extracts the single completed-event handler method for the named event from the emitted saga.
    /// </summary>
    /// <param name="eventName">The completed-event type name the handler accepts.</param>
    /// <returns>The handler method's source text, signature through closing brace.</returns>
    private static string HandlerFor(string eventName)
    {
        var result = GeneratorTestHelper.RunGenerator(MixedLoopExitWorkflow);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, SagaHintName);

        var parameter = $"{eventName} evt,";
        var parameterIndex = saga.IndexOf(parameter, StringComparison.Ordinal);
        if (parameterIndex < 0)
        {
            throw new InvalidOperationException(
                $"The emitted saga has no handler accepting '{eventName}'. Emitted source:{Environment.NewLine}{saga}");
        }

        // Walk back to the method signature that owns the parameter, then forward to the closing
        // brace at member indentation, so an assertion cannot accidentally read a sibling handler.
        var start = saga.LastIndexOf("    public ", parameterIndex, StringComparison.Ordinal);
        var end = saga.IndexOf($"{Environment.NewLine}    }}", parameterIndex, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException(
                $"Could not delimit the handler for '{eventName}'. Emitted source:{Environment.NewLine}{saga}");
        }

        return saga.Substring(start, end - start);
    }
}
