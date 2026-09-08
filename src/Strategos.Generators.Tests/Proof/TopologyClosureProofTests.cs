// -----------------------------------------------------------------------
// <copyright file="TopologyClosureProofTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// Verifies that workflow binding proof fails closed when fluent topology is not fully extracted.
/// </summary>
[Property("Category", "Integration")]
public sealed class TopologyClosureProofTests
{
    /// <summary>A discriminator stored as a delegate is runtime-valid but not closed generator syntax.</summary>
    [Test]
    public async Task DynamicBranchDiscriminator_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Branch(SelectStage,
                    BranchCase<FlowState, int>.When(0, path => path.Then<BranchStep>()))
                """,
            """
            private static readonly Func<FlowState, int> SelectStage = state => state.Stage;
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("Branch discriminator");
    }

    /// <summary>A branch-case array hidden behind a property must not disappear from proof.</summary>
    [Test]
    public async Task DynamicBranchCases_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Branch(state => state.Stage, Cases)
                """,
            """
            private static BranchCase<FlowState, int>[] Cases => new[]
            {
                BranchCase<FlowState, int>.When(0, path => path.Then<BranchStep>()),
            };
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("case declarations");
    }

    /// <summary>A literal-looking constant still falls outside the extractor's loop identity grammar.</summary>
    [Test]
    public async Task DynamicLoopIdentity_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .RepeatUntil(state => state.Done, LoopIdentity, loop => loop.Then<LoopStep>())
                """,
            """
            private const string LoopIdentity = "retry";
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("RepeatUntil loop identity");
    }

    /// <summary>Named arguments must not let semantic closure validate a loop the syntax parser misreads.</summary>
    [Test]
    public async Task ReorderedNamedRepeatUntilArguments_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .RepeatUntil(
                    loopName: "retry",
                    condition: state => state.Done,
                    body: loop => loop.Then<LoopStep>())
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("RepeatUntil arguments");
        await Assert.That(diagnostic.GetMessage()).Contains("canonical source order");
    }

    /// <summary>A method-group loop body executes at runtime but is invisible to syntax lowering.</summary>
    [Test]
    public async Task MethodGroupLoopBody_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .RepeatUntil(state => state.Done, "retry", BuildLoop)
                """,
            """
            private static void BuildLoop(ILoopBuilder<FlowState> loop) => loop.Then<LoopStep>();
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("RepeatUntil body callback");
    }

    /// <summary>An empty top-level loop would otherwise disappear from the extracted graph.</summary>
    [Test]
    public async Task EmptyTopLevelLoopBody_RecordsClosureFailure()
    {
        var failures = ExtractTopologyClosureFailures(Source(
            """
                .RepeatUntil(state => state.Done, "retry", loop => { }, maxIterations: 2)
                """));
        var failure = failures.Single(reason => reason.Contains("body callback", StringComparison.Ordinal));

        await Assert.That(failure).Contains("statically closed loop step");
    }

    /// <summary>An empty nested loop must not be erased while its enclosing loop remains visible.</summary>
    [Test]
    public async Task EmptyNestedLoopBody_RecordsClosureFailure()
    {
        var failures = ExtractTopologyClosureFailures(Source(
            """
                .RepeatUntil(state => state.Done, "outer", loop => loop
                    .Then<LoopStep>()
                    .RepeatUntil(state => state.Done, "inner", inner => { }, maxIterations: 2),
                    maxIterations: 2)
                """));
        var failure = failures.Single(reason => reason.Contains("body callback", StringComparison.Ordinal));

        await Assert.That(failure).Contains("statically closed loop step");
    }

    /// <summary>A top-level loop bound must be a compile-time positive integer.</summary>
    /// <param name="maxIterations">The unprovable authored iteration bound.</param>
    [Test]
    [Arguments("MaximumIterations")]
    [Arguments("0")]
    public async Task UnprovableTopLevelLoopMaximum_ReportsAgwf042(string maxIterations)
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            $"""
                .RepeatUntil(
                    state => state.Done,
                    "retry",
                    loop => loop.Then<LoopStep>(),
                    maxIterations: {maxIterations})
                """,
            """
            private static int MaximumIterations => 2;
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("maxIterations");
        await Assert.That(diagnostic.GetMessage()).Contains("statically known positive integer");
    }

    /// <summary>A nested loop bound is subject to the same closed static grammar.</summary>
    /// <param name="maxIterations">The unprovable authored iteration bound.</param>
    [Test]
    [Arguments("MaximumIterations")]
    [Arguments("0")]
    public async Task UnprovableNestedLoopMaximum_ReportsAgwf042(string maxIterations)
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            $"""
                .RepeatUntil(state => state.Done, "outer", loop => loop
                    .Then<LoopStep>()
                    .RepeatUntil(
                        state => state.Done,
                        "inner",
                        inner => inner.Then<RecoveryStep>(),
                        maxIterations: {maxIterations}),
                    maxIterations: 2)
                """,
            """
            private static int MaximumIterations => 2;
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("maxIterations");
        await Assert.That(diagnostic.GetMessage()).Contains("statically known positive integer");
    }

    /// <summary>Type-coincident reordered case arguments can otherwise lower the value as the path.</summary>
    [Test]
    public async Task ReorderedNamedBranchCaseArguments_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Branch(state => state.Route,
                    BranchCase<FlowState, Action<IBranchBuilder<FlowState>>>.When(
                        pathBuilder: path => path.Then<BranchStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "noop"))),
                        value: path => path.Then<RecoveryStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "recover")))))
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("case declarations");
        await Assert.That(diagnostic.GetMessage()).Contains("authored workflow topology");
        await Assert.That(diagnostic.GetMessage()).Contains("statically closed branch grammar");
    }

    /// <summary>When every fork path is dynamic, proof must reject the fully dropped fork.</summary>
    [Test]
    public async Task AllMethodGroupForkPaths_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Fork(LeftPath, RightPath)
                .Join<JoinStep>()
                """,
            ForkPathHelpers));

        await Assert.That(diagnostic.GetMessage()).Contains("2 of 2 path callbacks");
    }

    /// <summary>One visible fork path must not let a second dynamic path disappear from proof.</summary>
    [Test]
    public async Task PartiallyDynamicForkPaths_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Fork(
                    path => path.Then<LeftStep>(),
                    RightPath)
                .Join<JoinStep>()
                """,
            ForkPathHelpers));

        await Assert.That(diagnostic.GetMessage()).Contains("1 of 2 path callbacks");
    }

    /// <summary>Inline fork paths rooted at their own IForkPathBuilder parameters are owned.</summary>
    [Test]
    public async Task ValidInlineFork_HasNoTopologyClosureFailures()
    {
        var diagnostics = RunBindingGenerator(Source(
            """
                .Fork(
                    path => path.Then<LeftStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "noop"))),
                    path => path.Then<RightStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "noop"))))
                .Join<JoinStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")))
                """))
            .Diagnostics
            .Where(diagnostic => diagnostic.Id == "AGWF042")
            .ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A statically visible top-level fork still requires at least two paths.</summary>
    [Test]
    public async Task SinglePathTopLevelFork_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Fork(path => path.Then<LeftStep>())
                .Join<JoinStep>()
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("1 statically visible path callbacks");
        await Assert.That(diagnostic.GetMessage()).Contains("at least two are required");
    }

    /// <summary>A fork nested in a loop cannot bypass the same minimum cardinality.</summary>
    [Test]
    public async Task SinglePathNestedLoopFork_RecordsClosureFailure()
    {
        var failures = ExtractTopologyClosureFailures(Source(
            """
                .RepeatUntil(state => state.Done, "retry", loop => loop
                    .Fork(path => path.Then<LeftStep>())
                    .Join<JoinStep>(),
                    maxIterations: 2)
                """));
        var failure = failures.Single(reason => reason.Contains("Fork contains", StringComparison.Ordinal));

        await Assert.That(failure).Contains("1 statically visible path callbacks");
        await Assert.That(failure).Contains("at least two are required");
    }

    /// <summary>A visible path step must not mask additional topology authored through a helper.</summary>
    [Test]
    public async Task InlineForkPathBuilderEscape_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Fork(
                    path =>
                    {
                        path.Then<LeftStep>();
                        AddHiddenPath(path);
                    },
                    path => path.Then<RightStep>())
                .Join<JoinStep>()
                """,
            """
            private static void AddHiddenPath(IForkPathBuilder<FlowState> path) =>
                path.Then<RecoveryStep>();
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("1 of 2 path callbacks");
    }

    /// <summary>Conditionally constructed topology is not one deterministic static path.</summary>
    [Test]
    public async Task ConditionalForkPathTopology_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Fork(
                    path =>
                    {
                        if (DateTime.UtcNow.Ticks > 0)
                        {
                            path.Then<LeftStep>();
                        }
                    },
                    path => path.Then<RightStep>())
                .Join<JoinStep>()
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("1 of 2 path callbacks");
    }

    /// <summary>A custom extension in the main receiver chain can mutate topology at runtime.</summary>
    [Test]
    public async Task TopLevelWorkflowBuilderExtension_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AddHiddenStep()
                """,
            types: """
            public static class HiddenWorkflowExtensions
            {
                public static IWorkflowBuilder<FlowState> AddHiddenStep(
                    this IWorkflowBuilder<FlowState> workflow)
                {
                    workflow.Then<RecoveryStep>();
                    return workflow;
                }
            }
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("fluent chain call 'AddHiddenStep'");
    }

    /// <summary>A different member's earlier Finally call cannot replace the Definition root.</summary>
    [Test]
    public async Task DecoyFinallyOutsideDefinition_DoesNotAffectBoundProof()
    {
        var source = Source(
            topology: string.Empty,
            members: """
            private static WorkflowDefinition<FlowState> Decoy => Workflow<FlowState>
                .Create("decoy")
                .StartWith<RecoveryStep>()
                .Finally<RecoveryStep>();
            """);

        var diagnostics = RunBindingGenerator(source).Diagnostics
            .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042")
            .ToArray();

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A conditional return in Definition cannot select a smaller runtime topology.</summary>
    [Test]
    public async Task ConditionalDefinitionGetter_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            conditionalDefinition: true));

        await Assert.That(diagnostic.GetMessage()).Contains(
            "exactly one direct return statement");
    }

    /// <summary>A nested workflow expression used as data cannot contribute outer topology.</summary>
    [Test]
    public async Task NestedWorkflowConstructionInsideDefinition_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            startConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                    .ValidateState(
                        state => Workflow<FlowState>
                            .Create("nested-data")
                            .StartWith<RecoveryStep>()
                            .Branch(
                                nested => nested.Stage,
                                BranchCase<FlowState, int>.Otherwise(path => path.Then<ReviewStep>()))
                            .Finally<RejectedStep>() != null,
                        "nested workflow is data")
                """));

        await Assert.That(diagnostic.GetMessage()).Contains(
            "nested Workflow<TState> construction outside the selected fluent chain");
    }

    /// <summary>A homonymous non-Strategos Branch cannot spoof extracted topology.</summary>
    [Test]
    public async Task HomonymousBranchInsideDefinition_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            types: """
                public sealed class TopologySpoof
                {
                    public bool Branch(
                        Func<FlowState, int> discriminator,
                        params BranchCase<FlowState, int>[] cases) => true;
                }
                """,
            startConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                    .ValidateState(
                        state => new TopologySpoof().Branch(
                            nested => nested.Stage,
                            BranchCase<FlowState, int>.Otherwise(path => path.Then<ReviewStep>())),
                        "homonymous branch is data")
                """));

        await Assert.That(diagnostic.GetMessage()).Contains(
            "model-affecting call 'Branch' that does not resolve to the Strategos DSL");
    }

    /// <summary>A genuine Branch on a captured separate builder is not outer workflow topology.</summary>
    [Test]
    public async Task CapturedSeparateWorkflowBuilderBranch_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            members: """
                private static readonly IWorkflowBuilder<FlowState> OtherWorkflowBuilder =
                    Workflow<FlowState>
                        .Create("other-workflow")
                        .StartWith<RecoveryStep>();
                """,
            startConfiguration: """
                step =>
                {
                    step.Performs(new WorkflowActionReference("orders", "Order", "noop"));
                    OtherWorkflowBuilder.Branch(
                        state => state.Stage,
                        BranchCase<FlowState, int>.Otherwise(path => path.Then<ReviewStep>()));
                }
                """));

        await Assert.That(diagnostic.GetMessage()).Contains(
            "model-affecting Strategos call 'Branch' that is not rooted in an owned inline builder callback");
    }

    /// <summary>A workflow failure method group must not be silently omitted.</summary>
    [Test]
    public async Task MethodGroupOnFailure_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .OnFailure(HandleFailure)
                """,
            """
            private static void HandleFailure(IFailureBuilder<FlowState> failure)
            {
                failure.Then<RecoveryStep>();
                failure.Complete();
            }
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("workflow OnFailure callback");
    }

    /// <summary>A fork-path failure method group is not made safe by the enclosing inline path.</summary>
    [Test]
    public async Task MethodGroupForkPathOnFailure_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .Fork(
                    path => path.Then<LeftStep>().OnFailure(HandleFailure),
                    path => path.Then<RightStep>())
                .Join<JoinStep>()
                """,
            """
            private static void HandleFailure(IFailureBuilder<FlowState> failure)
            {
                failure.Then<RecoveryStep>();
                failure.Complete();
            }
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("fork-path OnFailure callback");
    }

    /// <summary>Nonterminal workflow recovery remains unprovable until runtime rejoin lowering exists.</summary>
    [Test]
    public async Task NonterminalWorkflowOnFailure_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .OnFailure(failure => failure.Then<RecoveryStep>())
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("workflow OnFailure is nonterminal");
        await Assert.That(diagnostic.GetMessage()).Contains("cannot rejoin the main flow");
    }

    /// <summary>The runtime rejects a second root failure handler, so static proof must too.</summary>
    [Test]
    public async Task DuplicateWorkflowOnFailure_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .OnFailure(failure => failure.Then<RecoveryStep>().Complete())
                .OnFailure(failure => failure.Then<ReviewStep>().Complete())
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("duplicate OnFailure callbacks");
        await Assert.That(diagnostic.GetMessage()).Contains("runtime rejects the second declaration");
    }

    /// <summary>A low-confidence method group is a real alternate path and must fail closed.</summary>
    [Test]
    public async Task MethodGroupOnLowConfidence_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            members: """
            private static void HandleLowConfidence(IBranchBuilder<FlowState> branch)
            {
                branch.Then<ReviewStep>();
                branch.Complete();
            }
            """,
            startConfiguration: """
            step => step
                .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                .RequireConfidence(0.5)
                .OnLowConfidence(HandleLowConfidence)
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("OnLowConfidence callback");
    }

    /// <summary>Nested confidence branches are not recursively lowered by the current emitter.</summary>
    [Test]
    public async Task NestedOnLowConfidence_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            startConfiguration: """
            step => step
                .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                .RequireConfidence(0.8)
                .OnLowConfidence(branch => branch.Then<ReviewStep>(handler => handler
                    .RequireConfidence(0.7)
                    .OnLowConfidence(inner => inner.Then<RejectedStep>())))
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("nested OnLowConfidence handlers");
    }

    /// <summary>A homonymous marker must not turn a terminating confidence path into a rejoin.</summary>
    [Test]
    public async Task HomonymousRejoinMainFlowInsideLowConfidence_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            topology: string.Empty,
            types: """
                public static class TopologySpoof
                {
                    public static void RejoinMainFlow() { }
                }
                """,
            startConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                    .RequireConfidence(0.8)
                    .OnLowConfidence(branch =>
                    {
                        branch.Then<ReviewStep>(handler => handler.Performs(
                            new WorkflowActionReference("orders", "Order", "review")));
                        TopologySpoof.RejoinMainFlow();
                    })
                """));

        await Assert.That(diagnostic.GetMessage()).Contains(
            "model-affecting call 'RejoinMainFlow' that does not resolve to the Strategos DSL");
    }

    /// <summary>An approval configured by method group must not be reduced to a bare checkpoint.</summary>
    [Test]
    public async Task MethodGroupAwaitApprovalConfiguration_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(ConfigureApproval)
                """,
            """
            private static void ConfigureApproval(IApprovalBuilder<FlowState, Reviewer> approval) =>
                approval.OnRejection(rejection => rejection.Then<RejectedStep>().Complete());
            """));

        await Assert.That(diagnostic.GetMessage()).Contains("AwaitApproval configuration");
    }

    /// <summary>A method-group rejection path must remain visible to bound proof.</summary>
    [Test]
    public async Task MethodGroupApprovalRejection_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(approval => approval.OnRejection(HandleRejection))
                """,
            ApprovalHandlerHelpers));

        await Assert.That(diagnostic.GetMessage()).Contains("OnRejection callback");
    }

    /// <summary>A method-group timeout path must remain visible to bound proof.</summary>
    [Test]
    public async Task MethodGroupApprovalTimeout_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(approval => approval.OnTimeout(HandleTimeout))
                """,
            ApprovalHandlerHelpers));

        await Assert.That(diagnostic.GetMessage()).Contains("OnTimeout callback");
    }

    /// <summary>A direct handler must not mask a sibling approval handler that is dynamic.</summary>
    [Test]
    public async Task PartiallyDynamicApprovalHandlers_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(approval => approval
                    .OnRejection(rejection => rejection.Then<RejectedStep>().Complete())
                    .OnTimeout(HandleTimeout))
                """,
            ApprovalHandlerHelpers));

        await Assert.That(diagnostic.GetMessage()).Contains("OnTimeout callback");
    }

    /// <summary>Duplicate rejection handlers are last-wins at runtime but first-wins in extraction.</summary>
    [Test]
    public async Task DuplicateApprovalRejectionHandlers_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(approval => approval
                    .OnRejection(rejection => rejection
                        .Then<ReviewStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "review")))
                        .Complete())
                    .OnRejection(rejection => rejection
                        .Then<RejectedStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "reject")))
                        .Complete()))
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("duplicate OnRejection");
        await Assert.That(diagnostic.GetMessage()).Contains("last-wins");
    }

    /// <summary>Duplicate timeout handlers with different actions cannot share one static proof.</summary>
    [Test]
    public async Task DuplicateApprovalTimeoutHandlers_ReportAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(approval => approval
                    .OnTimeout(timeout => timeout
                        .Then<TimedOutStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "timeout")))
                        .Complete())
                    .OnTimeout(timeout => timeout
                        .Then<RecoveryStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "recover")))
                        .Complete()))
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("duplicate OnTimeout");
        await Assert.That(diagnostic.GetMessage()).Contains("last-wins");
    }

    /// <summary>Nested approvals remain unprovable until their runtime saga route is fully lowered.</summary>
    [Test]
    public async Task NestedEscalateToApproval_ReportsAgwf042()
    {
        var diagnostic = SingleTopologyDiagnostic(Source(
            """
                .AwaitApproval<Reviewer>(approval => approval.OnTimeout(timeout => timeout
                    .EscalateTo<SeniorReviewer>(nested => nested.WithContext("second review"))))
                """));

        await Assert.That(diagnostic.GetMessage()).Contains("nested EscalateTo approval");
        await Assert.That(diagnostic.GetMessage()).Contains("current closed workflow proof");
    }

    private const string ForkPathHelpers = """
        private static void LeftPath(IForkPathBuilder<FlowState> path) => path.Then<LeftStep>();
        private static void RightPath(IForkPathBuilder<FlowState> path) => path.Then<RightStep>();
        """;

    private const string ApprovalHandlerHelpers = """
        private static void HandleRejection(IApprovalRejectionBuilder<FlowState> rejection)
        {
            rejection.Then<RejectedStep>();
            rejection.Complete();
        }

        private static void HandleTimeout(IApprovalEscalationBuilder<FlowState> timeout)
        {
            timeout.Then<TimedOutStep>();
            timeout.Complete();
        }
        """;

    private static Diagnostic SingleTopologyDiagnostic(string source)
    {
        var result = RunBindingGenerator(source);
        var diagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042")
            .ToArray();
        if (diagnostics.Length != 1 || diagnostics[0].Id != "AGWF042")
        {
            var compilationErrors = GeneratorTestHelper.GetCompilationDiagnostics(source)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            throw new InvalidOperationException(
                $"Expected one AGWF042 diagnostic, found {diagnostics.Length}: "
                + string.Join(" | ", result.Diagnostics.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage()))
                + ". Compilation errors: "
                + string.Join(" | ", compilationErrors.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return diagnostics[0];
    }

    private static IReadOnlyList<string> ExtractTopologyClosureFailures(string source)
    {
        var (workflowClass, semanticModel) = ParserTestHelper.CompileWorkflowValidated(source);
        return FluentDslParser.ExtractTopologyClosureFailures(
            workflowClass,
            semanticModel,
            "topology-flow",
            CancellationToken.None);
    }

    private static GeneratorDriverRunResult RunBindingGenerator(string source)
    {
        var result = GeneratorTestHelper.RunRejectedTopologyWithValidInput(
            source,
            "AGWF039",
            "AGWF040",
            "AGWF041",
            "AGWF042");
        var unexpectedErrors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(static diagnostic => diagnostic.Id is not (
                "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042"))
            .ToArray();
        if (unexpectedErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "Topology-closure fixture produced unexpected generator errors: " +
                string.Join(" | ", unexpectedErrors.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return result;
    }

    private static string Source(
        string topology,
        string members = "",
        string types = "",
        string startConfiguration = """
            step => step.Performs(new WorkflowActionReference("orders", "Order", "noop"))
            """,
        bool conditionalDefinition = false)
    {
        var definitionOpen = conditionalDefinition
            ? """
                public static WorkflowDefinition<FlowState> Definition
                {
                    get
                    {
                        if (DateTime.UtcNow.Ticks > 0)
                        {
                            return Workflow<FlowState>
                                .Create("topology-flow")
                                .StartWith<EntryStep>(step => step.Performs(
                                    new WorkflowActionReference("orders", "Order", "noop")))
                                .Finally<DoneStep>(step => step.Performs(
                                    new WorkflowActionReference("orders", "Order", "noop")));
                        }

                        return
                """
            : "public static WorkflowDefinition<FlowState> Definition =>";
        var definitionClose = conditionalDefinition
            ? """
                        ;
                    }
                }
                """
            : ";";

        return $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Steps;

        namespace BindingProof;

        public sealed class Order { }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("run").BoundToWorkflow("topology-flow");
                    obj.Action("noop");
                    obj.Action("review");
                    obj.Action("reject");
                    obj.Action("timeout");
                    obj.Action("recover");
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public int Stage { get; init; }
            public bool Done { get; init; }
            public Action<IBranchBuilder<FlowState>> Route { get; init; } = _ => { };
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class EntryStep : TestStep { }
        public sealed class DoneStep : TestStep { }
        public sealed class BranchStep : TestStep { }
        public sealed class LoopStep : TestStep { }
        public sealed class LeftStep : TestStep { }
        public sealed class RightStep : TestStep { }
        public sealed class JoinStep : TestStep { }
        public sealed class RecoveryStep : TestStep { }
        public sealed class ReviewStep : TestStep { }
        public sealed class RejectedStep : TestStep { }
        public sealed class TimedOutStep : TestStep { }
        public sealed class Reviewer { }
        public sealed class SeniorReviewer { }

        {{types}}

        [Workflow("topology-flow")]
        public static partial class TopologyFlowWorkflowDefinition
        {
            {{members}}

            {{definitionOpen}}
                Workflow<FlowState>
                .Create("topology-flow")
                .StartWith<EntryStep>({{startConfiguration}})
                {{topology}}
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")))
            {{definitionClose}}
        }
        """;
    }
}
