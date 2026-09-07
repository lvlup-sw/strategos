// -----------------------------------------------------------------------
// <copyright file="WorkflowBindingTopologySemanticsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// Exercises workflow/action refinement against the real graph extracted for each
/// statically closed fluent topology construct.
/// </summary>
[Property("Category", "Integration")]
public sealed class WorkflowBindingTopologySemanticsTests
{
    /// <summary>Both exhaustive branch paths may establish a shared rejoin contract.</summary>
    [Test]
    public async Task BranchPaths_WithLegalIngressAndRejoin_AreProved()
    {
        var diagnostics = RunClosedProof(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("branch-left", 1, 2),
                ("branch-right", 1, 2),
                ("done", 2, 3)),
            topology: BranchTopology()));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Transparent grouping around a branch chain preserves its entry and rejoin edges.</summary>
    [Test]
    public async Task BranchPaths_WithParenthesizedChain_AreProved()
    {
        var diagnostics = RunClosedProof(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("branch-left", 1, 2),
                ("branch-right", 1, 2),
                ("done", 2, 3)),
            topology: BranchTopology(),
            workflowChainPrefix: "(",
            beforeFinallySuffix: ")"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>The dispatching action must establish every exhaustive branch entry.</summary>
    [Test]
    public async Task BranchPath_WithRefutedIngress_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("branch-left", 1, 2),
                ("branch-right", 2, 2),
                ("done", 2, 3)),
            topology: BranchTopology()));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'EntryStep' -> 'BranchRightStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=1");
    }

    /// <summary>Every nonterminal branch tail must establish the common rejoin requirement.</summary>
    [Test]
    public async Task BranchPath_WithRefutedRejoin_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("branch-left", 1, 2),
                ("branch-right", 1, 4),
                ("done", 2, 3)),
            topology: BranchTopology()));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'BranchRightStep' -> 'DoneStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=4");
    }

    /// <summary>The loop tail may soundly establish both its repeat and exit transitions.</summary>
    [Test]
    public async Task Loop_WithLegalBackEdgeAndExit_IsProved()
    {
        var diagnostics = RunClosedProof(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("loop-head", 1, 2),
                ("loop-tail", 2, 1),
                ("done", 1, 3)),
            topology: LoopTopology()));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>The loop continue decision cannot assume a fact its tail does not guarantee.</summary>
    [Test]
    public async Task Loop_WithRefutedBackEdge_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("loop-head", 1, 2),
                ("loop-tail", 2, 4),
                ("done", 4, 3)),
            topology: LoopTopology()));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'Retry_LoopTailStep' -> 'Retry_LoopHeadStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=4");
    }

    /// <summary>The same loop tail must independently establish the post-loop continuation.</summary>
    [Test]
    public async Task Loop_WithRefutedExit_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("loop-head", 1, 2),
                ("loop-tail", 2, 1),
                ("done", 4, 3)),
            topology: LoopTopology()));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'Retry_LoopTailStep' -> 'DoneStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=1");
    }

    /// <summary>An underscore in a direct loop step name cannot make its back-edge disappear.</summary>
    [Test]
    public async Task Loop_WithUnderscoreNamedBodyStep_StillChecksBackEdge()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("loop-tail", 1, 2),
                ("done", 2, 3)),
            topology: """
                .RepeatUntil(state => state.Done, "Retry", loop => loop
                    .Then<Loop_TailStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "loop-tail"))))
                """));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'Retry_Loop_TailStep' -> 'Retry_Loop_TailStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=2");
    }

    /// <summary>
    /// A later top-level loop whose name begins with the preceding loop's name is a sibling,
    /// not a nested loop; the first loop's exit must therefore refine the sibling's entry.
    /// </summary>
    [Test]
    public async Task SiblingLoop_WithPrefixedName_StillChecksIngress()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: """
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 4)
                .Modifies(order => order.Stage)
                """,
            actionDeclarations: """
                obj.Action("entry")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 2)
                    .Modifies(order => order.Stage);
                obj.Action("first-loop")
                    .Requires(order => order.Stage == 2)
                    .Ensures(order => order.Stage == 2)
                    .Modifies(order => order.Stage);
                obj.Action("second-loop")
                    .Requires(order => order.Stage == 3)
                    .Ensures(order => order.Stage == 3)
                    .Modifies(order => order.Stage);
                obj.Action("done")
                    .Requires(order => order.Stage == 2 || order.Stage == 3)
                    .Ensures(order => order.Stage == 4)
                    .Modifies(order => order.Stage);
                """,
            topology: """
                .RepeatUntil(state => state.Done, "A", loop => loop
                    .Then<LoopHeadStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "first-loop"))))
                .RepeatUntil(state => state.Done, "A_B", loop => loop
                    .Then<LoopTailStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "second-loop"))))
                """));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'A_LoopHeadStep' -> 'A_B_LoopTailStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=2");
    }

    /// <summary>
    /// Structural ancestry, rather than a flattened name prefix plus depth, identifies a
    /// prefixed sibling whose first executable step belongs to its nested child loop.
    /// </summary>
    [Test]
    public async Task PrefixedSiblingLoop_StartingWithNestedLoop_StillChecksNestedIngress()
    {
        var diagnostic = SingleRefinementFailure(TopologySource(
            boundContract: """
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 4)
                .Modifies(order => order.Stage)
                """,
            actionDeclarations: """
                obj.Action("entry")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 2)
                    .Modifies(order => order.Stage);
                obj.Action("first-loop")
                    .Requires(order => order.Stage == 2)
                    .Ensures(order => order.Stage == 2)
                    .Modifies(order => order.Stage);
                obj.Action("nested-loop")
                    .Requires(order => order.Stage == 3)
                    .Ensures(order => order.Stage == 3)
                    .Modifies(order => order.Stage);
                obj.Action("second-loop-tail")
                    .Requires(order => order.Stage == 2 || order.Stage == 3)
                    .Ensures(order => order.Stage == 3)
                    .Modifies(order => order.Stage);
                obj.Action("done")
                    .Requires(order => order.Stage == 2 || order.Stage == 3)
                    .Ensures(order => order.Stage == 4)
                    .Modifies(order => order.Stage);
                """,
            topology: """
                .RepeatUntil(state => state.Done, "A", loop => loop
                    .Then<LoopHeadStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "first-loop"))))
                .RepeatUntil(state => state.Done, "A_B", loop => loop
                    .RepeatUntil(state => state.Done, "C", nested => nested
                        .Then<BranchLeftStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "nested-loop"))))
                    .Then<LoopTailStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "second-loop-tail"))))
                """));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'A_LoopHeadStep' -> 'A_B_C_BranchLeftStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=2");
    }

    /// <summary>
    /// Approval success, terminal rejection, and nonterminal timeout paths all refine the
    /// bound post-state when their ingress and exit contracts agree.
    /// </summary>
    [Test]
    public async Task ApprovalSuccessRejectionAndTimeout_WithLegalContracts_AreProved()
    {
        var diagnostics = RunClosedProof(ApprovalSource(
            doneRequirement: 1,
            rejectionRequirement: 1,
            rejectionGuarantee: 3,
            timeoutRequirement: 1,
            timeoutGuarantee: 1));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>The ordinary approval-success continuation remains a checked transition.</summary>
    [Test]
    public async Task ApprovalSuccess_WithRefutedContinuation_ReportsAgwf041()
    {
        var diagnostic = SingleRefinementFailure(ApprovalSource(
            doneRequirement: 2,
            rejectionRequirement: 1,
            rejectionGuarantee: 3,
            timeoutRequirement: 1,
            timeoutGuarantee: 2));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'EntryStep' -> 'DoneStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=1");
    }

    /// <summary>Approval rejection handlers are checked against the preceding action.</summary>
    [Test]
    public async Task ApprovalRejection_WithRefutedIngress_ReportsAgwf041()
    {
        var diagnostic = SingleRefinementFailure(ApprovalSource(
            doneRequirement: 1,
            rejectionRequirement: 2,
            rejectionGuarantee: 3,
            timeoutRequirement: 1,
            timeoutGuarantee: 1));

        await Assert.That(diagnostic.GetMessage())
            .Contains("rejection ingress to 'RejectedStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=1");
    }

    /// <summary>Approval timeout handlers are checked against the preceding action.</summary>
    [Test]
    public async Task ApprovalTimeout_WithRefutedIngress_ReportsAgwf041()
    {
        var diagnostic = SingleRefinementFailure(ApprovalSource(
            doneRequirement: 1,
            rejectionRequirement: 1,
            rejectionGuarantee: 3,
            timeoutRequirement: 2,
            timeoutGuarantee: 1));

        await Assert.That(diagnostic.GetMessage())
            .Contains("escalation ingress to 'TimedOutStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=1");
    }

    /// <summary>A terminal rejection handler must itself establish the bound guarantee.</summary>
    [Test]
    public async Task ApprovalRejection_WithRefutedTerminalExit_ReportsAgwf041()
    {
        var diagnostic = SingleRefinementFailure(ApprovalSource(
            doneRequirement: 1,
            rejectionRequirement: 1,
            rejectionGuarantee: 2,
            timeoutRequirement: 1,
            timeoutGuarantee: 1));

        await Assert.That(diagnostic.GetMessage())
            .Contains("successful completion after 'RejectedStep' does not establish the bound guarantee");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=2");
    }

    /// <summary>A nonterminal timeout handler must establish the main-flow rejoin contract.</summary>
    [Test]
    public async Task ApprovalTimeout_WithRefutedRejoin_ReportsAgwf041()
    {
        var diagnostic = SingleRefinementFailure(ApprovalSource(
            doneRequirement: 1,
            rejectionRequirement: 1,
            rejectionGuarantee: 3,
            timeoutRequirement: 1,
            timeoutGuarantee: 2));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'TimedOutStep' -> 'DoneStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=2");
    }

    /// <summary>Every root-flow failure state may enter a terminal handler through preserved facts.</summary>
    [Test]
    public async Task RootFailureHandler_WithLegalIngressAndExit_IsProved()
    {
        var diagnostics = RunClosedProof(FailureSource(recoveryGuard: 1));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A root failure handler requirement must follow from every possible trigger action.</summary>
    [Test]
    public async Task RootFailureHandler_WithRefutedIngress_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(FailureSource(recoveryGuard: 2));

        await Assert.That(diagnostic.GetMessage()).Contains("failure ingress from 'DoneStep'");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Guard=1");
    }

    /// <summary>A low-confidence alternate may rejoin when its action contract preserves the seam.</summary>
    [Test]
    public async Task LowConfidenceHandler_WithLegalIngressAndRejoin_IsProved()
    {
        var diagnostics = RunClosedProof(LowConfidenceSource(handlerRequirement: 1));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A low-confidence path is an additional checked transition, not presentation metadata.</summary>
    [Test]
    public async Task LowConfidenceHandler_WithRefutedIngress_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(LowConfidenceSource(handlerRequirement: 2));

        await Assert.That(diagnostic.GetMessage())
            .Contains("internal seam 'EntryStep' -> 'ReviewStep' is not composable");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Stage=1");
    }

    /// <summary>
    /// Disjoint fork paths may contribute separate facts whose conjunction establishes the
    /// configured join requirement and survives through the workflow exit.
    /// </summary>
    [Test]
    public async Task DisjointFork_WithJointGuarantee_IsProved()
    {
        var diagnostics = RunClosedProof(JointForkSource(rightGuarantee: 1));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Weakening one disjoint tail must make the fork-specific conjunction fail; ordinary
    /// tail-to-join seam checks deliberately skip these edges.
    /// </summary>
    [Test]
    public async Task DisjointFork_WithMissingTailGuarantee_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(JointForkSource(rightGuarantee: 2));

        await Assert.That(diagnostic.GetMessage()).Contains("parallel join for fork");
        await Assert.That(diagnostic.GetMessage()).Contains("does not establish 'JoinStep' requirement");
        await Assert.That(diagnostic.GetMessage()).Contains("property|Right=2");
    }

    /// <summary>
    /// A root failure handler may execute while a sibling fork worker is still active, so its
    /// footprint must participate in every path's noninterference check.
    /// </summary>
    [Test]
    public async Task Fork_WithRootFailureHandlerConflict_ReportsStableAgwf041()
    {
        var diagnostic = SingleRefinementFailure(RootFailureForkSource());

        await Assert.That(diagnostic.GetMessage()).Contains(
            "fork 'TopologySemantics-Fork0' paths 0 and 1 interfere");
        await Assert.That(diagnostic.GetMessage()).Contains("write/write=property|Right");
    }

    private static string JointForkSource(int rightGuarantee) => TopologySource(
        boundContract: """
            .Requires(order => order.Stage == 0)
            .Ensures(order => order.Stage == 3)
            .Ensures(order => order.Left == 1)
            .Ensures(order => order.Right == 1)
            .Modifies(order => order.Stage)
            .Modifies(order => order.Left)
            .Modifies(order => order.Right)
            """,
        actionDeclarations: $$"""
            obj.Action("entry")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            obj.Action("fork-left")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Left == 1)
                .Modifies(order => order.Left);
            obj.Action("fork-right")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Right == {{rightGuarantee}})
                .Modifies(order => order.Right);
            obj.Action("join")
                .Requires(order => order.Stage == 1)
                .Requires(order => order.Left == 1)
                .Requires(order => order.Right == 1)
                .Ensures(order => order.Stage == 2)
                .Ensures(order => order.Left == 1)
                .Ensures(order => order.Right == 1)
                .Modifies(order => order.Stage);
            obj.Action("done")
                .Requires(order => order.Stage == 2)
                .Requires(order => order.Left == 1)
                .Requires(order => order.Right == 1)
                .Ensures(order => order.Stage == 3)
                .Ensures(order => order.Left == 1)
                .Ensures(order => order.Right == 1)
                .Modifies(order => order.Stage);
            """,
        topology: """
            .Fork(
                path => path.Then<ForkLeftStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "fork-left"))),
                path => path.Then<ForkRightStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "fork-right"))))
            .Join<JoinStep>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "join")))
            """);

    private static string RootFailureForkSource() => TopologySource(
        boundContract: """
            .Requires(order => order.Stage == 0)
            .Requires(order => order.Guard == 1)
            .Ensures(order => order.Stage == 3)
            .Modifies(order => order.Stage)
            .Modifies(order => order.Left)
            .Modifies(order => order.Right)
            """,
        actionDeclarations: """
            obj.Action("entry")
                .Requires(order => order.Stage == 0)
                .Requires(order => order.Guard == 1)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            obj.Action("fork-left")
                .Requires(order => order.Stage == 1)
                .Requires(order => order.Guard == 1)
                .Ensures(order => order.Left == 1)
                .Modifies(order => order.Left);
            obj.Action("fork-right")
                .Requires(order => order.Stage == 1)
                .Requires(order => order.Guard == 1)
                .Ensures(order => order.Right == 1)
                .Modifies(order => order.Right);
            obj.Action("join")
                .Requires(order => order.Stage == 1)
                .Requires(order => order.Guard == 1)
                .Requires(order => order.Left == 1)
                .Requires(order => order.Right == 1)
                .Ensures(order => order.Stage == 2)
                .Ensures(order => order.Left == 1)
                .Ensures(order => order.Right == 1)
                .Modifies(order => order.Stage);
            obj.Action("done")
                .Requires(order => order.Stage == 2)
                .Requires(order => order.Guard == 1)
                .Requires(order => order.Left == 1)
                .Requires(order => order.Right == 1)
                .Ensures(order => order.Stage == 3)
                .Modifies(order => order.Stage);
            obj.Action("recover")
                .Requires(order => order.Guard == 1)
                .Ensures(order => order.Right == 0)
                .Modifies(order => order.Right);
            """,
        topology: """
            .Fork(
                path => path.Then<ForkLeftStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "fork-left"))),
                path => path.Then<ForkRightStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "fork-right"))))
            .Join<JoinStep>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "join")))
            .OnFailure(failure => failure
                .Then<RecoveryStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "recover")))
                .Complete())
            """);

    private static string BoundStageContract() => """
        .Requires(order => order.Stage == 0)
        .Ensures(order => order.Stage == 3)
        .Modifies(order => order.Stage)
        """;

    private static string StageActions(params (string Name, int Requirement, int Guarantee)[] actions) =>
        string.Join(
            Environment.NewLine,
            actions.Select(action => $$"""
                obj.Action("{{action.Name}}")
                    .Requires(order => order.Stage == {{action.Requirement}})
                    .Ensures(order => order.Stage == {{action.Guarantee}})
                    .Modifies(order => order.Stage);
                """));

    private static string BranchTopology() => """
        .Branch(state => state.Route,
            BranchCase<FlowState, int>.When(1, path => path.Then<BranchLeftStep>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "branch-left")))),
            BranchCase<FlowState, int>.Otherwise(path => path.Then<BranchRightStep>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "branch-right")))))
        """;

    private static string LoopTopology() => """
        .RepeatUntil(state => state.Done, "Retry", loop => loop
            .Then<LoopHeadStep>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "loop-head")))
            .Then<LoopTailStep>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "loop-tail"))))
        """;

    private static string ApprovalSource(
        int doneRequirement,
        int rejectionRequirement,
        int rejectionGuarantee,
        int timeoutRequirement,
        int timeoutGuarantee) => TopologySource(
            boundContract: BoundStageContract(),
            actionDeclarations: StageActions(
                ("entry", 0, 1),
                ("done", doneRequirement, 3),
                ("reject", rejectionRequirement, rejectionGuarantee),
                ("timeout", timeoutRequirement, timeoutGuarantee)),
            topology: """
                .AwaitApproval<Reviewer>(approval => approval
                    .OnRejection(rejection => rejection
                        .Then<RejectedStep>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "reject")))
                        .Complete())
                    .OnTimeout(timeout => timeout.Then<TimedOutStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "timeout")))))
                """);

    private static string FailureSource(int recoveryGuard) => TopologySource(
        boundContract: """
            .Requires(order => order.Stage == 0)
            .Requires(order => order.Guard == 1)
            .Ensures(order => order.Stage == 3)
            .Modifies(order => order.Stage)
            """,
        actionDeclarations: $$"""
            obj.Action("entry")
                .Requires(order => order.Stage == 0)
                .Requires(order => order.Guard == 1)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            obj.Action("done")
                .Requires(order => order.Stage == 1)
                .Requires(order => order.Guard == 1)
                .Ensures(order => order.Stage == 3)
                .Modifies(order => order.Stage);
            obj.Action("recover")
                .Requires(order => order.Guard == {{recoveryGuard}})
                .Ensures(order => order.Stage == 3)
                .Modifies(order => order.Stage);
            """,
        topology: """
            .OnFailure(failure => failure
                .Then<RecoveryStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "recover")))
                .Complete())
            """);

    private static string LowConfidenceSource(int handlerRequirement) => TopologySource(
        boundContract: BoundStageContract(),
        actionDeclarations: StageActions(
            ("entry", 0, 1),
            ("review", handlerRequirement, 1),
            ("done", 1, 3)),
        topology: string.Empty,
        startConfiguration: """
            step => step
                .Performs(new WorkflowActionReference("orders", "Order", "entry"))
                .RequireConfidence(0.8)
                .OnLowConfidence(handler => handler
                    .Then<ReviewStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "review")))
                    .RejoinMainFlow())
            """);

    private static Diagnostic[] RunClosedProof(string source)
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            source,
            "AGWF039",
            "AGWF040",
            "AGWF041",
            "AGWF042");
        var unexpectedGeneratorErrors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => diagnostic.Id is not ("AGWF039" or "AGWF040" or "AGWF041" or "AGWF042"))
            .ToArray();
        if (unexpectedGeneratorErrors.Length != 0)
        {
            throw new InvalidOperationException(
                "Fixture produced unrelated generator errors: "
                + string.Join(" | ", unexpectedGeneratorErrors.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return result.Diagnostics
            .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042")
            .ToArray();
    }

    private static Diagnostic SingleRefinementFailure(string source)
    {
        var diagnostics = RunClosedProof(source);
        if (diagnostics.Length != 1 || diagnostics[0].Id != "AGWF041")
        {
            throw new InvalidOperationException(
                $"Expected one AGWF041 diagnostic, found {diagnostics.Length}: "
                + string.Join(" | ", diagnostics.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return diagnostics[0];
    }

    private static string TopologySource(
        string boundContract,
        string actionDeclarations,
        string topology,
        string startConfiguration = """
            step => step.Performs(new WorkflowActionReference("orders", "Order", "entry"))
            """,
        string workflowChainPrefix = "",
        string beforeFinallySuffix = "") => $$"""
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

        public sealed class Order
        {
            public int Stage { get; set; }
            public int Guard { get; set; }
            public int Left { get; set; }
            public int Right { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("run")
                        {{boundContract}}
                        .BoundToWorkflow("topology-semantics");

                    {{actionDeclarations}}
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public int Route { get; init; }
            public bool Done { get; init; }
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
        public sealed class BranchLeftStep : TestStep { }
        public sealed class BranchRightStep : TestStep { }
        public sealed class LoopHeadStep : TestStep { }
        public sealed class LoopTailStep : TestStep { }
        public sealed class Loop_TailStep : TestStep { }
        public sealed class RejectedStep : TestStep { }
        public sealed class TimedOutStep : TestStep { }
        public sealed class RecoveryStep : TestStep { }
        public sealed class ReviewStep : TestStep { }
        public sealed class ForkLeftStep : TestStep { }
        public sealed class ForkRightStep : TestStep { }
        public sealed class JoinStep : TestStep { }
        public sealed class DoneStep : TestStep { }
        public sealed class Reviewer { }

        [Workflow("topology-semantics")]
        public static partial class TopologySemanticsWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => {{workflowChainPrefix}}Workflow<FlowState>
                .Create("topology-semantics")
                .StartWith<EntryStep>({{startConfiguration}})
                {{topology}}
                {{beforeFinallySuffix}}
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "done")));
        }
        """;
}
