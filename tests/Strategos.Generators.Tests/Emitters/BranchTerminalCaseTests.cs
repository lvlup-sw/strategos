// -----------------------------------------------------------------------
// <copyright file="BranchTerminalCaseTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Emission proofs that a branch case's ending is decided from the CASE, not from the branch.
/// </summary>
/// <remarks>
/// <para>
/// The fixture below is the discriminating shape: one case rejoins, one case declares
/// <c>.Complete()</c>, and the workflow declares a terminal. Because the rejoining case gives the
/// branch a convergence point, the branch-level rejoin flag is true — so an emitter that decides a
/// path's ending from that flag alone sends the ending case to the declared terminal, shipping an
/// order that was rejected (#175). A branch whose cases all exit the same way cannot separate the
/// two rules; only a mixed one can.
/// </para>
/// <para>
/// The two assertions are deliberately complementary. Marking every path complete would satisfy the
/// ending case on its own, and routing every path to the rejoin target would satisfy the rejoining
/// case on its own; only reading the case satisfies both.
/// </para>
/// <para>
/// The discriminator is an enum rather than a <c>bool</c>: a bool-discriminated branch does not
/// compile (#179).
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class BranchTerminalCaseTests
{
    /// <summary>
    /// The generated hint name of the mixed fixture's saga.
    /// </summary>
    private const string SagaHintName = "ReviewOrderSaga.g.cs";

    /// <summary>
    /// A branch mixing a rejoining case with a workflow-ending case, ahead of a declared terminal.
    /// </summary>
    private const string MixedBranchWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ReviewOutcome
        {
            Rejected = 0,
            Approved = 1,
        }

        public sealed record ReviewState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ReviewOutcome Outcome { get; init; }
        }

        public sealed class ReviewOrder : IWorkflowStep<ReviewState>
        {
            public Task<StepResult<ReviewState>> ExecuteAsync(
                ReviewState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ReviewState>.FromState(state));
        }

        public sealed class ProcessApprovedOrder : IWorkflowStep<ReviewState>
        {
            public Task<StepResult<ReviewState>> ExecuteAsync(
                ReviewState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ReviewState>.FromState(state));
        }

        public sealed class RejectOrder : IWorkflowStep<ReviewState>
        {
            public Task<StepResult<ReviewState>> ExecuteAsync(
                ReviewState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ReviewState>.FromState(state));
        }

        public sealed class ShipApprovedOrder : IWorkflowStep<ReviewState>
        {
            public Task<StepResult<ReviewState>> ExecuteAsync(
                ReviewState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ReviewState>.FromState(state));
        }

        [Workflow("review-order")]
        public static partial class ReviewOrderWorkflow
        {
            public static WorkflowDefinition<ReviewState> Definition => Workflow<ReviewState>
                .Create("review-order")
                .StartWith<ReviewOrder>()
                .Branch(state => state.Outcome,
                    BranchCase<ReviewState, ReviewOutcome>.When(
                        ReviewOutcome.Approved, path => path.Then<ProcessApprovedOrder>()),
                    BranchCase<ReviewState, ReviewOutcome>.Otherwise(
                        path => path.Then<RejectOrder>().Complete()))
                .Finally<ShipApprovedOrder>();
        }
        """;

    /// <summary>
    /// A case that declared <c>.Complete()</c> completes the saga at its own last step, even though
    /// its sibling case rejoins and the workflow declares a terminal.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Branch_TerminalCase_MarksCompletedAtCaseLastStep()
    {
        var handler = HandlerFor("RejectOrderCompleted");

        await Assert.That(handler).Contains("MarkCompleted();")
            .Because("the case declared .Complete(), so its last step ends the workflow.");
        await Assert.That(handler).Contains("public void Handle(")
            .Because("a workflow-ending path returns no follow-on command.");
        await Assert.That(handler).DoesNotContain("StartShipApprovedOrderCommand")
            .Because("a rejected order must never be routed to the declared terminal — reading the branch-level rejoin flag instead of the case is exactly that defect.");
    }

    /// <summary>
    /// The sibling case that did not declare an ending still routes to the branch's rejoin target.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Branch_RejoiningCase_RoutesToRejoinTarget()
    {
        var handler = HandlerFor("ProcessApprovedOrderCompleted");

        await Assert.That(handler).Contains("return new StartShipApprovedOrderCommand(WorkflowId);")
            .Because("a case with no declared ending falls back to the branch's convergence point.");
        await Assert.That(handler).DoesNotContain("MarkCompleted();")
            .Because("the rejoining case must not end the workflow at its own last step — completing every path would satisfy the terminal case while breaking this one.");
    }

    /// <summary>
    /// Extracts the single completed-event handler method for the named event from the emitted saga.
    /// </summary>
    /// <param name="eventName">The completed-event type name the handler accepts.</param>
    /// <returns>The handler method's source text, signature through closing brace.</returns>
    private static string HandlerFor(string eventName)
    {
        var result = GeneratorTestHelper.RunGenerator(MixedBranchWorkflow);
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
