// -----------------------------------------------------------------------
// <copyright file="TerminalReachabilityDiagnosticTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis.CSharp.Syntax;

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// AGWF035 "unreachable termination": the generator holds both the declared terminal and every
/// computed successor, so a workflow whose main flow does not end where its author said it does is
/// decidable at emission. Until this guard existed the only thing that caught the class was a
/// container-backed saga run, which most contributors cannot execute.
/// </summary>
/// <remarks>
/// <para>
/// Two conditions, one code. The declared terminal having a successor AT ALL is the condition that
/// survives a lowering block nobody classified — the step it appended looks like an ordinary
/// main-flow entry, so nothing that recognises a bad successor by its owning construct can see it.
/// A main-flow step whose successor is construct-owned is the condition that catches a
/// classification which has fallen behind the constructs the workflow actually declares. The
/// under-reach arm is the complementary fault: a rejoin construct's last step does not dispatch
/// the declared terminal. A branch whose cases all <c>Complete()</c> plus a <c>Finally</c>
/// legitimately dispatches that terminal zero times and must stay silent.
/// </para>
/// <para>
/// The classification is supplied to the guard rather than read inside it, which is what lets a
/// test hold the workflow fixed and vary only what the emitter would chain by. The under-reach
/// arm accepts a <see cref="PhaseGraph"/> for the same reason: a test strips the Finally edge
/// a rejoin last step should have published.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class TerminalReachabilityDiagnosticTests
{
    /// <summary>The code under test.</summary>
    internal const string UnreachableTerminationId = "AGWF035";

    /// <summary>
    /// A fork workflow: intake, two parallel assessments, a join, and a declared terminal. Its
    /// path steps sit in the step-name list in document order, ahead of the join and the terminal.
    /// </summary>
    internal const string ForkWorkflowSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public sealed record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public sealed class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class AssessDamage : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class AssessLiability : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class AggregateAssessments : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("reachability-fork-claim")]
        public static partial class ReachabilityForkClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("reachability-fork-claim")
                .StartWith<IntakeClaim>()
                .Fork(
                    path => path.Then<AssessDamage>(),
                    path => path.Then<AssessLiability>())
                .Join<AggregateAssessments>()
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A loop-exit branch mixing a rejoining case with a workflow-ending case, ahead of a
    /// declared terminal. The same shape as the #184 emission fixture.
    /// </summary>
    internal const string RejoiningLoopExitSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
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
    /// A branch mixing a rejoining case with a workflow-ending case, ahead of a declared
    /// terminal.
    /// </summary>
    internal const string RejoiningBranchSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
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
    /// A branch whose cases all <c>Complete()</c>, plus a declared terminal. Nothing
    /// dispatches the terminal; that is legitimate and must stay silent.
    /// </summary>
    internal const string AllCompleteBranchSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum CloseOutcome
        {
            Paid = 0,
            Denied = 1,
        }

        public sealed record CloseState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public CloseOutcome Outcome { get; init; }
        }

        public sealed class OpenTicket : IWorkflowStep<CloseState>
        {
            public Task<StepResult<CloseState>> ExecuteAsync(
                CloseState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CloseState>.FromState(state));
        }

        public sealed class PayTicket : IWorkflowStep<CloseState>
        {
            public Task<StepResult<CloseState>> ExecuteAsync(
                CloseState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CloseState>.FromState(state));
        }

        public sealed class DenyTicket : IWorkflowStep<CloseState>
        {
            public Task<StepResult<CloseState>> ExecuteAsync(
                CloseState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CloseState>.FromState(state));
        }

        public sealed class ArchiveTicket : IWorkflowStep<CloseState>
        {
            public Task<StepResult<CloseState>> ExecuteAsync(
                CloseState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CloseState>.FromState(state));
        }

        [Workflow("close-ticket")]
        public static partial class CloseTicketWorkflow
        {
            public static WorkflowDefinition<CloseState> Definition => Workflow<CloseState>
                .Create("close-ticket")
                .StartWith<OpenTicket>()
                .Branch(state => state.Outcome,
                    BranchCase<CloseState, CloseOutcome>.When(
                        CloseOutcome.Paid, path => path.Then<PayTicket>().Complete()),
                    BranchCase<CloseState, CloseOutcome>.Otherwise(
                        path => path.Then<DenyTicket>().Complete()))
                .Finally<ArchiveTicket>();
        }
        """;

    /// <summary>
    /// The declared terminal is not the last main-flow step: a step-name entry sits after it that
    /// no construct claims, so the terminal chains onward instead of completing the saga.
    /// </summary>
    /// <remarks>
    /// This is the shape a lowering block produces when it appends a name for a phase, a worker
    /// handler and commands but never tells the classification the step is reached through its own
    /// construct. Because no construct owns the name, the classification is CORRECT about
    /// everything it knows and the workflow is still broken — which is why the terminal carries a
    /// check of its own rather than relying on the successor being recognisably construct-owned.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_TerminalNotLastMainFlowStep_Fires()
    {
        var model = ForkWorkflowModel(extraUnclassifiedStepAfterTerminal: "ArchiveClaim");

        var diagnostics = Report(model, MainFlowClassification.For(model).OffMainFlowStepNames);

        var reported = diagnostics.FirstOrDefault(d => d.Id == UnreachableTerminationId);
        await Assert.That(reported).IsNotNull()
            .Because("the declared terminal has a successor, so the saga runs past its termination");
        await Assert.That(reported!.Severity).IsEqualTo(DiagnosticSeverity.Error)
            .Because("a workflow that cannot reach its termination does not run");
        await Assert.That(reported.GetMessage()).Contains("SettleClaim")
            .Because("the diagnostic must name the step whose successor is wrong");
        await Assert.That(reported.GetMessage()).Contains("ArchiveClaim")
            .Because("the diagnostic must name the step it chains to");
    }

    /// <summary>
    /// A main-flow step's successor resolves to a step owned by a construct — here the first fork
    /// path step, reached only through the fork's dispatch.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_SuccessorResolvesOffMainFlow_Fires()
    {
        var model = ForkWorkflowModel(extraUnclassifiedStepAfterTerminal: null);

        var diagnostics = Report(model, offMainFlowStepNames: []);

        var reported = diagnostics.FirstOrDefault(d => d.Id == UnreachableTerminationId);
        await Assert.That(reported).IsNotNull()
            .Because("with nothing classified off the main flow, the intake step chains into a fork path");
        await Assert.That(reported!.GetMessage()).Contains("IntakeClaim");
        await Assert.That(reported.GetMessage()).Contains("AssessDamage")
            .Because("a fork path step is reached through the fork's dispatch, never as a main-flow successor");
    }

    /// <summary>
    /// The shipped fork shape, classified as the emitter classifies it, reports nothing.
    /// </summary>
    /// <remarks>
    /// Without this the two positives above only prove the guard can fire, not that it discriminates.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_ForkWorkflowAsClassified_DoesNotFire()
    {
        var model = ForkWorkflowModel(extraUnclassifiedStepAfterTerminal: null);

        var diagnostics = Report(model, MainFlowClassification.For(model).OffMainFlowStepNames);

        await Assert.That(diagnostics.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("the fork's paths are classified off the main flow, so the terminal has no successor");
    }

    /// <summary>
    /// The counterfactual: reverting the off-main-flow classification, with document-ordered step
    /// names left in place, produces the diagnostic — so the guard would have caught the shipped
    /// defect rather than merely claiming it would.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One workflow, one model, two classifications. Before the classification existed, every later
    /// step-name entry was a candidate successor, so a fork path step — reached only through the
    /// fork's dispatch — became the successor of an ordinary main-flow step. Handing the guard an
    /// empty classification reproduces exactly that and nothing else, which is what makes this a
    /// test rather than an argument: a test cannot revert production code, but it can vary the one
    /// input the reverted code would have changed.
    /// </para>
    /// <para>
    /// The two arms must disagree. An assertion that only the empty arm fires proves the guard is
    /// noisy; an assertion that only the classified arm is silent proves nothing at all.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_ClassificationReverted_WouldHaveCaughtTheShippedBug()
    {
        var model = ForkWorkflowModel(extraUnclassifiedStepAfterTerminal: null);

        var asShipped = Report(model, MainFlowClassification.For(model).OffMainFlowStepNames);
        var asReverted = Report(model, offMainFlowStepNames: []);

        await Assert.That(asShipped.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("the classification the emitter chains by resolves no main-flow successor into a fork path");

        var reported = asReverted.FirstOrDefault(d => d.Id == UnreachableTerminationId);
        await Assert.That(reported).IsNotNull()
            .Because("with the classification reverted, the defect is back — and the guard reports it at compile time");
        await Assert.That(reported!.GetMessage()).Contains("AssessDamage")
            .Because("the reverted successor is the fork path step, which is the step the shipped defect chained into");
    }

    /// <summary>
    /// A rejoining loop-exit case whose Finally edge has been stripped reports AGWF035,
    /// naming the declared terminal and the last step that should have dispatched it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_RejoiningLoopExit_FinallyEdgeStripped_Fires()
    {
        var model = ParseWorkflowModel(RejoiningLoopExitSource, "settle-claim", "SettleClaim");
        var graph = PhaseGraph.Build(model).WithoutSuccessor("PayClaim", "CloseClaim");

        var diagnostics = Report(
            model,
            MainFlowClassification.For(model).OffMainFlowStepNames,
            declaredTerminalStepName: "CloseClaim",
            phaseGraph: graph);

        var reported = diagnostics.FirstOrDefault(d => d.Id == UnreachableTerminationId);
        await Assert.That(reported).IsNotNull()
            .Because("a rejoin last step that does not dispatch the declared terminal is under-reach");
        await Assert.That(reported!.GetMessage()).Contains("CloseClaim")
            .Because("{0} names the declared terminal");
        await Assert.That(reported.GetMessage()).Contains("PayClaim")
            .Because("{2} names the last step that should have dispatched the terminal");
    }

    /// <summary>
    /// A rejoining branch case whose Finally edge has been stripped reports AGWF035.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_RejoiningBranch_FinallyEdgeStripped_Fires()
    {
        var model = ParseWorkflowModel(RejoiningBranchSource, "review-order", "ReviewOrder");
        var graph = PhaseGraph.Build(model).WithoutSuccessor("ProcessApprovedOrder", "ShipApprovedOrder");

        var diagnostics = Report(
            model,
            MainFlowClassification.For(model).OffMainFlowStepNames,
            declaredTerminalStepName: "ShipApprovedOrder",
            phaseGraph: graph);

        var reported = diagnostics.FirstOrDefault(d => d.Id == UnreachableTerminationId);
        await Assert.That(reported).IsNotNull()
            .Because("a rejoin last step that does not dispatch the declared terminal is under-reach");
        await Assert.That(reported!.GetMessage()).Contains("ShipApprovedOrder")
            .Because("{0} names the declared terminal");
        await Assert.That(reported.GetMessage()).Contains("ProcessApprovedOrder")
            .Because("{2} names the last step that should have dispatched the terminal");
    }

    /// <summary>
    /// A branch whose cases all <c>Complete()</c> plus a <c>Finally</c> dispatches the
    /// declared terminal zero times and must stay silent — including when the graph is
    /// the one the emitter would emit.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_AllCompleteBranchPlusFinally_DoesNotFire()
    {
        var model = ParseWorkflowModel(AllCompleteBranchSource, "close-ticket", "CloseTicket");

        var diagnostics = Report(
            model,
            MainFlowClassification.For(model).OffMainFlowStepNames,
            declaredTerminalStepName: "ArchiveTicket");

        await Assert.That(diagnostics.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("all-Complete() exclusive paths plus Finally legitimately never dispatch the terminal");

        var generated = GeneratorTestHelper.RunGenerator(AllCompleteBranchSource);
        await Assert.That(generated.Diagnostics.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("the real generator must stay silent on the same all-Complete() plus Finally shape");
    }

    /// <summary>
    /// The shipped fork, mixed branch, and mixed loop-exit shapes, classified and
    /// graphed as the emitter would, report nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_RejoiningConstructsAsEmitted_DoNotFire()
    {
        var fork = ForkWorkflowModel(extraUnclassifiedStepAfterTerminal: null);
        var branch = ParseWorkflowModel(RejoiningBranchSource, "review-order", "ReviewOrder");
        var loopExit = ParseWorkflowModel(RejoiningLoopExitSource, "settle-claim", "SettleClaim");

        var forkDiagnostics = Report(fork, MainFlowClassification.For(fork).OffMainFlowStepNames);
        var branchDiagnostics = Report(
            branch,
            MainFlowClassification.For(branch).OffMainFlowStepNames,
            declaredTerminalStepName: "ShipApprovedOrder");
        var loopDiagnostics = Report(
            loopExit,
            MainFlowClassification.For(loopExit).OffMainFlowStepNames,
            declaredTerminalStepName: "CloseClaim");

        await Assert.That(forkDiagnostics.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("the shipped fork reaches its terminal through the join");
        await Assert.That(branchDiagnostics.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("the mixed branch's rejoining case already dispatches Finally");
        await Assert.That(loopDiagnostics.Any(d => d.Id == UnreachableTerminationId)).IsFalse()
            .Because("the mixed loop-exit rejoining case already dispatches Finally");
    }

    /// <summary>
    /// The diagnostic fires on none of the shipped workflow corpus.
    /// </summary>
    /// <remarks>
    /// A guard is worth its cost only if it fires on the defect it exists for and stays silent
    /// otherwise. This is the second half: every named workflow source in the fixture corpus, run
    /// through the real generator, reports nothing. Enumerated by reflection so a source added to
    /// the corpus is swept without anyone remembering to list it here.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_ExistingCorpus_NeverFires()
    {
        var sources = typeof(SourceTexts)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (Name: f.Name, Source: (string)f.GetRawConstantValue()!))
            .ToList();

        await Assert.That(sources.Count).IsGreaterThanOrEqualTo(30)
            .Because("a sweep over an empty corpus passes vacuously; the fixture corpus has ~34 sources");

        var offenders = new List<string>();
        foreach (var (name, source) in sources)
        {
            var result = GeneratorTestHelper.RunGenerator(source);
            if (result.Diagnostics.Any(d => d.Id == UnreachableTerminationId))
            {
                offenders.Add(name);
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("the guard must not report a workflow that reaches its termination. Offenders: "
                + string.Join(", ", offenders));
    }

    /// <summary>
    /// The guard is reached from the generator's own pipeline, and is handed the shared
    /// classification the emitters chain by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every arm above calls <see cref="TerminalReachabilityGuard.Report"/> directly, so all of
    /// them stay green with the guard unwired from the generator — the code would still be
    /// correct, still be tested, and still never run, while the docs and the diagnostic catalog
    /// kept advertising the code. This is the arm that fails when the call site goes away.
    /// </para>
    /// <para>
    /// It is a structural gate rather than a run of the generator over a workflow that trips the
    /// guard, because no such workflow exists. The guard compares two derivations over the same
    /// model — the emitters' off-main-flow classification against the model's own construct lists
    /// — and today those derivations enumerate exactly the same constructs, so every appended step
    /// name is in both sets and neither condition can hold. The guard exists for the lowering
    /// block that has not been written yet: one that appends a step name for a phase, a worker
    /// handler and commands without telling the classification which construct owns it. Until such
    /// a block exists, "the diagnostic reached a consumer" is only assertable at the seam.
    /// </para>
    /// <para>
    /// Silent is not the same as dead. Narrow the classification the pipeline hands the guard and
    /// the shipped corpus reports through the real generator immediately — which is exactly what
    /// <see cref="Diagnostic_ExistingCorpus_NeverFires"/> would catch. What that arm cannot catch,
    /// and this one can, is the call itself going away.
    /// </para>
    /// <para>
    /// Reading the source rather than the emitted diagnostics is deliberate: a commented-out call
    /// is trivia and not an invocation node, so it cannot satisfy this. The second argument is
    /// checked too — a call that hands the guard a private skip list instead of the shared
    /// classification makes the comparison a tautology and reports nothing, which is
    /// indistinguishable from no call at all.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Diagnostic_GuardCallSite_IsReachedFromTheGeneratorPipeline()
    {
        var callSites = await GuardCallSitesAsync();

        await Assert.That(callSites.Select(c => c.OwningType))
            .Contains(nameof(WorkflowIncrementalGenerator))
            .Because(
                "the guard is only worth its cost if the generator runs it; unwired, every other "
                + "arm of this class still passes and no consumer ever sees the code");

        var pipelineCall = callSites.First(c => c.OwningType == nameof(WorkflowIncrementalGenerator));

        await Assert.That(pipelineCall.ClassificationArgument)
            .Contains(nameof(MainFlowClassification))
            .Because(
                "the guard must be handed the same classification the emitters chain by; a private "
                + "skip list would agree with the model by construction and report nothing");
    }

    /// <summary>
    /// Finds every invocation of <see cref="TerminalReachabilityGuard.Report"/> in the generator
    /// project's own source, with the type that owns it and the classification it passes.
    /// </summary>
    /// <returns>The call sites.</returns>
    private static async Task<List<(string OwningType, string ClassificationArgument)>> GuardCallSitesAsync()
    {
        var callSites = new List<(string OwningType, string ClassificationArgument)>();

        foreach (var file in EnumerateGeneratorSources())
        {
            var tree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(file));
            var root = await tree.GetRootAsync();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax member
                    || member.Expression.ToString() != nameof(TerminalReachabilityGuard)
                    || member.Name.Identifier.ValueText != nameof(TerminalReachabilityGuard.Report))
                {
                    continue;
                }

                var owner = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                var arguments = invocation.ArgumentList.Arguments;

                callSites.Add((
                    owner?.Identifier.ValueText ?? string.Empty,
                    arguments.Count > 1 ? arguments[1].ToString() : string.Empty));
            }
        }

        return callSites;
    }

    /// <summary>
    /// Enumerates the generator project's authored C# sources, excluding build output.
    /// </summary>
    /// <returns>The absolute file paths.</returns>
    private static IEnumerable<string> EnumerateGeneratorSources()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var generatorProject = Path.Combine(dir, "src", "Strategos.Generators");
            if (Directory.Exists(generatorProject))
            {
                return Directory
                    .EnumerateFiles(generatorProject, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !IsBuildOutput(f));
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate src/Strategos.Generators walking up from " + AppContext.BaseDirectory + ".");
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a workflow model from the REAL parse of <see cref="ForkWorkflowSource"/> — its step
    /// names and its fork construct both come from the parser, not from this test.
    /// </summary>
    /// <param name="extraUnclassifiedStepAfterTerminal">
    /// An additional step-name entry to append after the declared terminal, standing in for a
    /// lowering block that appends a name without registering the construct that owns it, or null
    /// to leave the parsed list as it is.
    /// </param>
    /// <returns>The model.</returns>
    internal static WorkflowModel ForkWorkflowModel(string? extraUnclassifiedStepAfterTerminal)
    {
        var (workflowClass, semanticModel) = ParserTestHelper.CompileWorkflow(ForkWorkflowSource);

        var stepNames = FluentDslParser
            .ExtractStepNames(workflowClass, semanticModel, CancellationToken.None)
            .ToList();

        if (extraUnclassifiedStepAfterTerminal is not null)
        {
            stepNames.Add(extraUnclassifiedStepAfterTerminal);
        }

        return new WorkflowModel(
            WorkflowName: "reachability-fork-claim",
            PascalName: "ReachabilityForkClaim",
            Namespace: "TestNamespace",
            StepNames: stepNames,
            StateTypeName: "ClaimState",
            Version: 1,
            PersistenceMode: PersistenceMode.SagaDocument,
            Steps: FluentDslParser.ExtractStepModels(workflowClass, semanticModel, CancellationToken.None),
            Forks: FluentDslParser.ExtractForkModels(
                workflowClass, semanticModel, "ReachabilityForkClaim", CancellationToken.None));
    }

    /// <summary>
    /// Builds a workflow model from a real parse of <paramref name="source"/> — step names
    /// and constructs both come from the parser, not from this test.
    /// </summary>
    /// <param name="source">The workflow source.</param>
    /// <param name="workflowName">The workflow name recorded on the model.</param>
    /// <param name="pascalName">The Pascal-case workflow name recorded on the model.</param>
    /// <returns>The model.</returns>
    internal static WorkflowModel ParseWorkflowModel(string source, string workflowName, string pascalName)
    {
        var (workflowClass, semanticModel) = ParserTestHelper.CompileWorkflow(source);

        return new WorkflowModel(
            WorkflowName: workflowName,
            PascalName: pascalName,
            Namespace: "TestNamespace",
            StepNames: FluentDslParser
                .ExtractStepNames(workflowClass, semanticModel, CancellationToken.None)
                .ToList(),
            StateTypeName: FluentDslParser.ExtractStateTypeName(
                workflowClass, semanticModel, CancellationToken.None),
            Steps: FluentDslParser.ExtractStepModels(workflowClass, semanticModel, CancellationToken.None),
            Loops: FluentDslParser.ExtractLoopModels(
                workflowClass, semanticModel, pascalName, CancellationToken.None),
            Branches: FluentDslParser.ExtractBranchModels(
                workflowClass, semanticModel, pascalName, CancellationToken.None),
            FailureHandlers: FluentDslParser.ExtractFailureHandlerModels(
                workflowClass, semanticModel, pascalName, CancellationToken.None),
            ApprovalPoints: FluentDslParser.ExtractApprovalModels(
                workflowClass, semanticModel, pascalName, CancellationToken.None),
            Forks: FluentDslParser.ExtractForkModels(
                workflowClass, semanticModel, pascalName, CancellationToken.None));
    }

    /// <summary>Runs the guard over a model and returns what it reported.</summary>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlowStepNames">The classification the emitter would chain by.</param>
    /// <param name="declaredTerminalStepName">The step the workflow declared as termination.</param>
    /// <param name="phaseGraph">
    /// The route graph to consult, or null to build it from <paramref name="model"/>.
    /// </param>
    /// <returns>The reported diagnostics.</returns>
    internal static List<Diagnostic> Report(
        WorkflowModel model,
        IReadOnlyCollection<string> offMainFlowStepNames,
        string? declaredTerminalStepName = "SettleClaim",
        PhaseGraph? phaseGraph = null)
    {
        var diagnostics = new List<Diagnostic>();
        TerminalReachabilityGuard.Report(
            model,
            offMainFlowStepNames,
            declaredTerminalStepName,
            Location.None,
            diagnostics,
            phaseGraph);

        return diagnostics;
    }
}
