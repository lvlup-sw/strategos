// -----------------------------------------------------------------------
// <copyright file="UnboundLoweringDifferentialPinTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Pins the emitted output of unbound workflows (no <c>BoundToWorkflow</c>, no <c>Performs</c>)
/// for the lowering shapes whose generated text changed between 2.12 (<c>45c86a6</c>) and the
/// #167 generator rewrite. Each test was derived from a base-vs-head differential run of the
/// generator over the same fixture; the assertions state the post-rewrite behaviour and, in the
/// <c>Because</c> text, what the 2.12 generator emitted instead.
/// </summary>
public class UnboundLoweringDifferentialPinTests
{
    private const string Prelude = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace PinNamespace;

        """;

    /// <summary>
    /// A non-exhaustive enum branch (no <c>Otherwise</c>) followed by a main-flow step: the
    /// saga's default arm dispatches the fall-through step, and the emitted
    /// <c>ValidTransitions</c> row for the dispatcher now lists that fall-through target.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NonExhaustiveBranch_TransitionsTable_ListsFallThroughTargetTheSagaDispatches()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + """
            public enum Kind { Auto, Home, Life }
            [WorkflowState]
            public record A1State : IWorkflowState { public Guid WorkflowId { get; init; } public Kind Kind { get; init; } }
            public class A1Validate : IWorkflowStep<A1State> { public Task<StepResult<A1State>> ExecuteAsync(A1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<A1State>.FromState(state)); }
            public class A1Auto : IWorkflowStep<A1State> { public Task<StepResult<A1State>> ExecuteAsync(A1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<A1State>.FromState(state)); }
            public class A1Home : IWorkflowStep<A1State> { public Task<StepResult<A1State>> ExecuteAsync(A1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<A1State>.FromState(state)); }
            public class A1Record : IWorkflowStep<A1State> { public Task<StepResult<A1State>> ExecuteAsync(A1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<A1State>.FromState(state)); }
            public class A1Close : IWorkflowStep<A1State> { public Task<StepResult<A1State>> ExecuteAsync(A1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<A1State>.FromState(state)); }

            [Workflow("a1-nonexhaustive")]
            public static partial class A1NonexhaustiveWorkflowDefinition
            {
                public static WorkflowDefinition<A1State> Definition => Workflow<A1State>
                    .Create("a1-nonexhaustive")
                    .StartWith<A1Validate>()
                    .Branch(state => state.Kind,
                        BranchCase<A1State, Kind>.When(Kind.Auto, path => path.Then<A1Auto>()),
                        BranchCase<A1State, Kind>.When(Kind.Home, path => path.Then<A1Home>()))
                    .Then<A1Record>()
                    .Finally<A1Close>();
            }
            """);

        var saga = GeneratorTestHelper.GetGeneratedSource(result, "A1NonexhaustiveSaga.g.cs");
        var transitions = GeneratorTestHelper.GetGeneratedSource(result, "A1NonexhaustiveTransitions.g.cs");

        await Assert.That(saga)
            .Contains("_ => new StartA1RecordCommand(WorkflowId)")
            .Because("the dispatcher's default arm falls through to the next main-flow step (unchanged since 2.12)");

        await Assert.That(transitions)
            .Contains("{ A1NonexhaustivePhase.A1Validate, [A1NonexhaustivePhase.A1Auto, A1NonexhaustivePhase.A1Home, A1NonexhaustivePhase.A1Record, A1NonexhaustivePhase.Failed] }")
            .Because("2.12 emitted [A1Auto, A1Home, Failed] for this row, omitting the A1Record transition the saga actually performs");
    }

    /// <summary>
    /// A <c>Branch</c> inside a <c>RepeatUntil</c> body: the saga handles the completed event
    /// the worker handler publishes, so the generated output compiles.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchInsideLoopBody_SagaHandlesTheCompletedEventTheWorkerPublishes()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + """
            public enum Kind { Auto, Home }
            [WorkflowState]
            public record D1State : IWorkflowState { public Guid WorkflowId { get; init; } public bool Done { get; init; } public Kind Kind { get; init; } }
            public class D1Start : IWorkflowStep<D1State> { public Task<StepResult<D1State>> ExecuteAsync(D1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<D1State>.FromState(state)); }
            public class D1Pick : IWorkflowStep<D1State> { public Task<StepResult<D1State>> ExecuteAsync(D1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<D1State>.FromState(state)); }
            public class D1DoAuto : IWorkflowStep<D1State> { public Task<StepResult<D1State>> ExecuteAsync(D1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<D1State>.FromState(state)); }
            public class D1DoHome : IWorkflowStep<D1State> { public Task<StepResult<D1State>> ExecuteAsync(D1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<D1State>.FromState(state)); }
            public class D1Done : IWorkflowStep<D1State> { public Task<StepResult<D1State>> ExecuteAsync(D1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<D1State>.FromState(state)); }

            [Workflow("d1-branch-in-loop")]
            public static partial class D1BranchInLoopWorkflowDefinition
            {
                public static WorkflowDefinition<D1State> Definition => Workflow<D1State>
                    .Create("d1-branch-in-loop")
                    .StartWith<D1Start>()
                    .RepeatUntil(
                        state => state.Done,
                        "Work",
                        loop => loop
                            .Then<D1Pick>()
                            .Branch(state => state.Kind,
                                BranchCase<D1State, Kind>.When(Kind.Auto, path => path.Then<D1DoAuto>()),
                                BranchCase<D1State, Kind>.Otherwise(path => path.Then<D1DoHome>())),
                        maxIterations: 5)
                    .Finally<D1Done>();
            }
            """);

        var saga = GeneratorTestHelper.GetGeneratedSource(result, "D1BranchInLoopSaga.g.cs");
        var events = GeneratorTestHelper.GetGeneratedSource(result, "D1BranchInLoopEvents.g.cs");

        await Assert.That(events)
            .Contains("public sealed partial record D1DoAutoCompleted(")
            .Because("the worker handler for a loop-body branch step publishes the plain completed event");

        await Assert.That(saga)
            .Contains("D1DoAutoCompleted evt,")
            .Because("the saga must handle the event that exists");

        await Assert.That(saga)
            .DoesNotContain("Work_D1DoAutoCompleted")
            .Because("2.12 emitted a saga handler on Work_D1DoAutoCompleted, a type no emitter declares, so the consumer build failed with CS0246");
    }

    /// <summary>
    /// A loop body step whose instance name contains an underscore is owned by the loop:
    /// the loop's continue edge returns to the first body step rather than skipping it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoopBodyInstanceNameWithUnderscore_LoopContinuesAtFirstBodyStep()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + """
            [WorkflowState]
            public record B5State : IWorkflowState { public Guid WorkflowId { get; init; } public bool Done { get; init; } }
            public class B5Start : IWorkflowStep<B5State> { public Task<StepResult<B5State>> ExecuteAsync(B5State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<B5State>.FromState(state)); }
            public class B5Work : IWorkflowStep<B5State> { public Task<StepResult<B5State>> ExecuteAsync(B5State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<B5State>.FromState(state)); }
            public class B5Check : IWorkflowStep<B5State> { public Task<StepResult<B5State>> ExecuteAsync(B5State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<B5State>.FromState(state)); }
            public class B5Done : IWorkflowStep<B5State> { public Task<StepResult<B5State>> ExecuteAsync(B5State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<B5State>.FromState(state)); }

            [Workflow("b5-underscore-instance")]
            public static partial class B5UnderscoreInstanceWorkflowDefinition
            {
                public static WorkflowDefinition<B5State> Definition => Workflow<B5State>
                    .Create("b5-underscore-instance")
                    .StartWith<B5Start>()
                    .RepeatUntil(
                        state => state.Done,
                        "Scan",
                        loop => loop
                            .Then<B5Work>("Do_Work")
                            .Then<B5Check>(),
                        maxIterations: 5)
                    .Finally<B5Done>();
            }
            """);

        var saga = GeneratorTestHelper.GetGeneratedSource(result, "B5UnderscoreInstanceSaga.g.cs");
        var transitions = GeneratorTestHelper.GetGeneratedSource(result, "B5UnderscoreInstanceTransitions.g.cs");

        await Assert.That(saga)
            .Contains("return new StartScan_Do_WorkCommand(WorkflowId);")
            .Because("2.12 matched loop ownership by phase-name prefix and treated 'Scan_Do_Work' as nested, so the continue edge restarted at Scan_B5Check and skipped Do_Work on every later iteration");

        await Assert.That(transitions)
            .Contains("{ B5UnderscoreInstancePhase.Scan_B5Check, [B5UnderscoreInstancePhase.Scan_Do_Work, B5UnderscoreInstancePhase.B5Done, B5UnderscoreInstancePhase.Failed] }")
            .Because("the transitions table must agree with the saga's continue edge");
    }

    /// <summary>
    /// Fork paths that reuse one step type under distinct instance names get one
    /// <c>NotFound</c> handler per path-qualified completed event.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkPathsReuseOneStepType_NotFoundHandlersArePathQualified()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + """
            [WorkflowState]
            public sealed record G2State : IWorkflowState { public Guid WorkflowId { get; init; } }
            public sealed class G2Start : IWorkflowStep<G2State> { public Task<StepResult<G2State>> ExecuteAsync(G2State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<G2State>.FromState(state)); }
            public sealed class G2Work : IWorkflowStep<G2State> { public Task<StepResult<G2State>> ExecuteAsync(G2State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<G2State>.FromState(state)); }
            public sealed class G2Join : IWorkflowStep<G2State> { public Task<StepResult<G2State>> ExecuteAsync(G2State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<G2State>.FromState(state)); }
            public sealed class G2Done : IWorkflowStep<G2State> { public Task<StepResult<G2State>> ExecuteAsync(G2State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<G2State>.FromState(state)); }

            [Workflow("g2-fork-same-type-named")]
            public static partial class G2ForkSameTypeNamedWorkflowDefinition
            {
                public static WorkflowDefinition<G2State> Definition => Workflow<G2State>
                    .Create("g2-fork-same-type-named")
                    .StartWith<G2Start>()
                    .Fork(
                        path => path.Then<G2Work>("Left"),
                        path => path.Then<G2Work>("Right"))
                    .Join<G2Join>()
                    .Finally<G2Done>();
            }
            """);

        var saga = GeneratorTestHelper.GetGeneratedSource(result, "G2ForkSameTypeNamedSaga.g.cs");

        await Assert.That(saga)
            .Contains("public static void NotFound(LeftCompleted evt,")
            .And.Contains("public static void NotFound(RightCompleted evt,")
            .Because("each fork path publishes its own completed event and needs its own NotFound handler");

        await Assert.That(saga)
            .DoesNotContain("G2WorkCompleted")
            .Because("2.12 emitted NotFound(G2WorkCompleted …) for the shared step type, a type no emitter declares, so the consumer build failed with CS0246");
    }

    /// <summary>
    /// A step and a <c>Complete()</c> declared inside a nested <c>EscalateTo</c> handler belong
    /// to that nested handler: the outer escalation neither dispatches the nested step nor
    /// inherits the nested <c>Complete()</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NestedEscalationHandler_StepAndCompleteStayInsideTheNestedHandler()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + EscalationFixture(outerComplete: false));

        var saga = GeneratorTestHelper.GetGeneratedSource(result, "E1ApprovalEscalationSaga.g.cs");
        var phase = GeneratorTestHelper.GetGeneratedSource(result, "E1ApprovalEscalationPhase.g.cs");
        var timeoutHandler = HandlerBodyFor(saga, "ManagerApprovalTimeoutCommand cmd");
        var notifyCompleted = HandlerBodyFor(saga, "E1NotifyEscalationCompleted evt");

        await Assert.That(timeoutHandler)
            .Contains("return new StartE1NotifyEscalationCommand(WorkflowId);")
            .Because("the outer escalation's first step is E1NotifyEscalation");

        await Assert.That(phase)
            .DoesNotContain("E1NestedRejection")
            .Because("2.12 hoisted the nested rejection step into the outer escalation chain and dispatched it first on timeout");

        await Assert.That(notifyCompleted)
            .DoesNotContain("MarkCompleted()")
            .Because("2.12 also hoisted the nested Complete(), which made the outer escalation terminal");
    }

    /// <summary>
    /// A <c>Complete()</c> on the outer escalation itself is still honoured.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OuterEscalationComplete_IsHonoured()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(Prelude + EscalationFixture(outerComplete: true));

        var saga = GeneratorTestHelper.GetGeneratedSource(result, "E1ApprovalEscalationSaga.g.cs");
        var notifyCompleted = HandlerBodyFor(saga, "E1NotifyEscalationCompleted evt");

        await Assert.That(notifyCompleted)
            .Contains("MarkCompleted()")
            .Because("an explicit Complete() on the outer escalation terminates the workflow after its last step");
    }

    private static string EscalationFixture(bool outerComplete)
    {
        var outerTail = outerComplete ? "\n                            .Complete())" : ")";
        return """
            [WorkflowState]
            public sealed record E1State : IWorkflowState { public Guid WorkflowId { get; init; } }
            public sealed class E1Submit : IWorkflowStep<E1State> { public Task<StepResult<E1State>> ExecuteAsync(E1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<E1State>.FromState(state)); }
            public sealed class E1Release : IWorkflowStep<E1State> { public Task<StepResult<E1State>> ExecuteAsync(E1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<E1State>.FromState(state)); }
            public sealed class E1Record : IWorkflowStep<E1State> { public Task<StepResult<E1State>> ExecuteAsync(E1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<E1State>.FromState(state)); }
            public sealed class E1NotifyEscalation : IWorkflowStep<E1State> { public Task<StepResult<E1State>> ExecuteAsync(E1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<E1State>.FromState(state)); }
            public sealed class E1NestedRejection : IWorkflowStep<E1State> { public Task<StepResult<E1State>> ExecuteAsync(E1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<E1State>.FromState(state)); }
            public sealed class E1LogRejection : IWorkflowStep<E1State> { public Task<StepResult<E1State>> ExecuteAsync(E1State state, StepContext context, CancellationToken ct) => Task.FromResult(StepResult<E1State>.FromState(state)); }
            public sealed class ManagerApprover { }
            public sealed class DirectorApprover { }

            [Workflow("e1-approval-escalation")]
            public static partial class E1ApprovalEscalationWorkflowDefinition
            {
                public static WorkflowDefinition<E1State> Definition => Workflow<E1State>
                    .Create("e1-approval-escalation")
                    .StartWith<E1Submit>()
                    .AwaitApproval<ManagerApprover>(approval => approval
                        .WithContext("A manager must approve.")
                        .OnTimeout(escalation => escalation
                            .Then<E1NotifyEscalation>()
                            .EscalateTo<DirectorApprover>(director => director
                                .OnRejection(r => r.Then<E1NestedRejection>().Complete()))
            """ + outerTail + """

                        .OnRejection(rejection => rejection
                            .Then<E1LogRejection>()
                            .Complete()))
                    .Then<E1Release>()
                    .Finally<E1Record>();
            }
            """;
    }

    private static string HandlerBodyFor(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var bodyStart = source.IndexOf('{', start);
        var depth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}' && --depth == 0)
            {
                return source.Substring(bodyStart, i - bodyStart + 1);
            }
        }

        return source.Substring(bodyStart);
    }
}
