// -----------------------------------------------------------------------
// <copyright file="TerminalReachabilityDiagnosticTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

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
/// classification which has fallen behind the constructs the workflow actually declares.
/// </para>
/// <para>
/// The classification is supplied to the guard rather than read inside it, which is what lets a
/// test hold the workflow fixed and vary only what the emitter would chain by.
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

    /// <summary>Runs the guard over a model and returns what it reported.</summary>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlowStepNames">The classification the emitter would chain by.</param>
    /// <returns>The reported diagnostics.</returns>
    internal static List<Diagnostic> Report(
        WorkflowModel model,
        IReadOnlyCollection<string> offMainFlowStepNames)
    {
        var diagnostics = new List<Diagnostic>();
        TerminalReachabilityGuard.Report(
            model,
            offMainFlowStepNames,
            declaredTerminalStepName: "SettleClaim",
            Location.None,
            diagnostics);

        return diagnostics;
    }
}
