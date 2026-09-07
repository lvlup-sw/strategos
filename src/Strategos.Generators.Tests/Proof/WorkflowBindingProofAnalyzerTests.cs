// -----------------------------------------------------------------------
// <copyright file="WorkflowBindingProofAnalyzerTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// End-to-end generator tests for compilation-local workflow/action refinement proof.
/// </summary>
[Property("Category", "Integration")]
public sealed class WorkflowBindingProofAnalyzerTests
{
    /// <summary>A closed two-step implementation discharges every binding obligation.</summary>
    [Test]
    public async Task ValidLinearWorkflow_WithStringBinding_CompilesAndReportsNoBindingDiagnostic()
    {
        var source = Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"));
        var diagnostics = BindingDiagnostics(source);
        var authoredSourceErrors = GeneratorTestHelper.GetCompilationDiagnostics(source)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => diagnostic.Location.SourceTree is { } sourceTree
                && !sourceTree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
            .ToArray();

        await Assert.That(diagnostics).IsEmpty();
        await Assert.That(authoredSourceErrors).IsEmpty()
            .Because("the existing BoundToWorkflow(string) authoring form remains source-compatible.");
    }

    /// <summary>Transparent receiver wrappers must not hide earlier runtime occurrences from proof.</summary>
    [Test]
    public async Task ParenthesizedMainChainReceiver_DoesNotHideEntryOccurrence()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 99, 1),
            secondAction: Action("complete", 0, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            workflowChainPrefix: "(",
            afterStartWithSuffix: ")"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "bound requirement does not imply entry step 'ReceiveStep' requirement");
    }

    /// <summary>A compile-time constant Create identity may exactly match the workflow catalog key.</summary>
    [Test]
    public async Task MatchingConstantCreateIdentity_IsProved()
    {
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            createWorkflowNameExpression: "WorkflowIdentity"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>The runtime builder identity cannot disagree with the catalog identity being proved.</summary>
    [Test]
    public async Task MismatchedCreateIdentity_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            createWorkflowNameExpression: "\"different-workflow\""));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "Create identity 'different-workflow' does not match [Workflow] identity 'fulfill-order'");
    }

    /// <summary>A runtime-computed Create identity is not guessed for a bound workflow proof.</summary>
    [Test]
    public async Task DynamicCreateIdentity_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            createWorkflowNameExpression: "GetWorkflowIdentity()"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "Create identity is not a compile-time constant string");
    }

    /// <summary>An executable delegate step has no occurrence-scoped action surface and fails closed.</summary>
    [Test]
    public async Task DelegateStepInBoundWorkflow_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            intermediateChain: """
                .Then("Inline", (state, context, cancellationToken) =>
                    Task.FromResult(StepResult<FlowState>.FromState(state)))
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "delegate Then(string, StepDelegate<TState>) occurrence");
        await Assert.That(diagnostic.GetMessage()).Contains("no statically provable action reference");
    }

    /// <summary>
    /// Reordered named arguments on AuthorityAxis and Authority.At are bound by parameter identity,
    /// and the workflow authority join may equal the bound action's authority.
    /// </summary>
    [Test]
    public async Task ReorderedNamedAuthorityArguments_LegalJoin_IsProved()
    {
        var diagnostics = BindingDiagnostics(AuthoritySource(secondAuthority: "manager"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// The same reordered named-argument lattice deterministically rejects a workflow whose joined
    /// leaf authority exceeds the bound action.
    /// </summary>
    [Test]
    public async Task ReorderedNamedAuthorityArguments_ExcessJoin_ReportsStableAgwf041()
    {
        var source = AuthoritySource(secondAuthority: "admin");
        var first = SingleBindingDiagnostic(source);
        var second = SingleBindingDiagnostic(source);

        await Assert.That(first.Id).IsEqualTo("AGWF041");
        await Assert.That(first.GetMessage()).Contains(
            "workflow authority join [admin, reader] exceeds bound authority 'manager'");
        await Assert.That(second.ToString()).IsEqualTo(first.ToString())
            .Because("authority-refinement diagnostics and joined-name ordering must be stable.");
    }

    /// <summary>
    /// A configured fork join contributes its closed action identity to the proof
    /// catalog rather than appearing as an unannotated reachable occurrence.
    /// </summary>
    [Test]
    public async Task ConfiguredForkJoin_ProvidesClosedActionOccurrenceToProof()
    {
        var diagnostics = BindingDiagnostics(ConfiguredForkSource());

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Parallel paths cannot both write the same external frame resource.</summary>
    [Test]
    public async Task ForkPathsWritingSameExternalResource_ReportAgwf041()
    {
        var diagnostic = SingleBindingDiagnostic(ForkCollisionSource(
            ".Touches(ActionResource.External(\"ledger\"))"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage()).Contains("write/write=external|ledger");
    }

    /// <summary>Event publication is a frame write and collides across parallel paths.</summary>
    [Test]
    public async Task ForkPathsEmittingSameEvent_ReportAgwf041()
    {
        var diagnostic = SingleBindingDiagnostic(ForkCollisionSource(
            ".EmitsEvent<OrderChanged<int>>()"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage()).Contains("write/write=event|OrderChanged`1");
    }

    /// <summary>
    /// Low-confidence handlers are part of their originating fork path's concurrent footprint.
    /// </summary>
    [Test]
    public async Task ForkPathConfidenceHandlersWritingSameProperty_ReportStableAgwf041()
    {
        var source = ForkConfidenceFootprintSource(
            leftHandlerContract: ".Modifies(order => order.Shared)",
            rightHandlerContract: ".Modifies(order => order.Shared)");

        var first = SingleBindingDiagnostic(source);
        var second = SingleBindingDiagnostic(source);

        await Assert.That(first.Id).IsEqualTo("AGWF041");
        await Assert.That(first.GetMessage()).Contains("write/write=property|Shared");
        await Assert.That(second.ToString()).IsEqualTo(first.ToString())
            .Because("fork confidence-handler conflict diagnostics must be deterministic.");
    }

    /// <summary>
    /// A handler write conflicts with state read by a handler on a concurrent fork path.
    /// </summary>
    [Test]
    public async Task ForkPathConfidenceHandlerWriteAndRead_ReportAgwf041()
    {
        var diagnostic = SingleBindingDiagnostic(ForkConfidenceFootprintSource(
            leftHandlerContract: ".Modifies(order => order.Shared)",
            rightHandlerContract: ".Requires(order => order.Shared == 0)"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "path 0 write/path 1 read=property|Shared");
    }

    /// <summary>Disjoint confidence-handler frames remain noninterfering.</summary>
    [Test]
    public async Task ForkPathConfidenceHandlersWithDisjointFrames_AreProved()
    {
        var diagnostics = BindingDiagnostics(ForkConfidenceFootprintSource(
            leftHandlerContract: ".Modifies(order => order.LeftOnly)",
            rightHandlerContract: ".Modifies(order => order.RightOnly)"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A binding is rejected when its exact ordinal workflow name is absent.</summary>
    [Test]
    public async Task MissingBoundWorkflow_ReportsAgwf039()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "missing-workflow",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF039");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("missing-workflow");
        await Assert.That(diagnostic.GetMessage()).Contains("resolves to 0 workflow definitions");
    }

    /// <summary>A reachable occurrence with no action declaration fails closed.</summary>
    [Test]
    public async Task MissingStepActionReference_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: string.Empty,
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("Reachable step 'ReceiveStep'");
        await Assert.That(diagnostic.GetMessage()).Contains("no action reference");
    }

    /// <summary>An identity hidden behind a factory is deliberately not guessed.</summary>
    [Test]
    public async Task DynamicStepActionReference_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: "step => step.Performs(ActionReferences.Receive())",
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("dynamic or invalid action reference");
    }

    /// <summary>A closed identity that has no catalog declaration reports the same reference diagnostic.</summary>
    [Test]
    public async Task UnknownStepActionReference_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("does-not-exist"),
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("orders/Order/does-not-exist");
        await Assert.That(diagnostic.GetMessage()).Contains("resolving to 0 declarations");
    }

    /// <summary>An occurrence identity must resolve uniquely, not merely find a matching name.</summary>
    [Test]
    public async Task AmbiguousStepActionReference_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("orders/Order/receive");
        await Assert.That(diagnostic.GetMessage()).Contains("resolving to 2 declarations");
    }

    /// <summary>An ambiguous reference that includes the bound action is not misclassified as recursion.</summary>
    [Test]
    public async Task AmbiguousSelfActionReference_ReportsAgwf040InsteadOfAgwf042()
    {
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("fulfill", 0, 2),
            secondAction: Action("complete", 2, 2),
            firstConfiguration: Performs("fulfill"),
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostics[0].GetMessage()).Contains("orders/Order/fulfill");
        await Assert.That(diagnostics[0].GetMessage()).Contains("resolving to 2 declarations");
    }

    /// <summary>An illegal adjacent seam reports the solver's stable minimal counterexample.</summary>
    [Test]
    public async Task IllegalLinearSeam_ReportsAgwf041WithStableWitness()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 2, 3),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            boundGuarantee: 3));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage())
            .Contains("seam 'ReceiveStep' -> 'CompleteStep' is not composable");
        await Assert.That(diagnostic.GetMessage())
            .Contains("counterexample: property|Stage=1");
    }

    /// <summary>An unrelated real Branch call cannot alter the selected Definition graph.</summary>
    [Test]
    public async Task IllegalLinearSeam_WithUnrelatedHelperBranch_StillReportsAgwf041()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 2, 3),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            boundGuarantee: 3,
            workflowMembers: """
                private static IWorkflowBuilder<FlowState> AddUnrelatedBranch(
                    IWorkflowBuilder<FlowState> builder) =>
                    builder.Branch(
                        state => state.WorkflowId,
                        BranchCase<FlowState, Guid>.Otherwise(path => path.Complete()));
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage())
            .Contains("seam 'ReceiveStep' -> 'CompleteStep' is not composable");
    }

    /// <summary>A legal internal sequence must still establish the bound post-state guarantee.</summary>
    [Test]
    public async Task WorkflowExitThatDoesNotEstablishBoundGuarantee_ReportsAgwf041()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            boundGuarantee: 3));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage())
            .Contains("successful completion after 'CompleteStep' does not establish the bound guarantee");
        await Assert.That(diagnostic.GetMessage())
            .Contains("counterexample: property|Stage=2");
    }

    /// <summary>An explicit custom predicate is opaque and therefore rejected for static binding proof.</summary>
    [Test]
    public async Task OpaqueLeafContract_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: """
                obj.Action("receive")
                    .Requires(ActionPredicate.Custom("orders.receive.ready.v1"))
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage);
                """,
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("orders.receive.ready.v1");
        await Assert.That(diagnostic.GetMessage()).Contains("opaque custom predicate");
    }

    /// <summary>A typed compensation whose contract is the exact inverse is proved.</summary>
    [Test]
    public async Task TypedCompensation_WithExactInverse_IsProved()
    {
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2) + Action("undo-complete", 2, 1),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// Untouched requirements survive the forward and inverse frames and therefore
    /// participate in inverse equivalence through each contract's effective guarantee.
    /// </summary>
    [Test]
    public async Task TypedCompensation_WithPreservedRequirement_UsesEffectiveGuarantees()
    {
        var preservingPair = """
            obj.Action("receive")
                .Requires(order => order.Stage == 0 && order.Guard == 7)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            obj.Action("undo-receive")
                .Requires(order => order.Stage == 1 && order.Guard == 7)
                .Ensures(order => order.Stage == 0)
                .Modifies(order => order.Stage);
            """;
        var completingPair = """
            obj.Action("complete")
                .Requires(order => order.Stage == 1 && order.Guard == 7)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage);
            obj.Action("undo-complete")
                .Requires(order => order.Stage == 2 && order.Guard == 7)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            """;

        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: preservingPair,
            secondAction: completingPair,
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """,
            boundContract: """
                .Requires(order => order.Guard == 7)
                .Ensures(order => order.Guard == 7)
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Authority aliases at the same lattice coordinate are semantically equivalent.</summary>
    [Test]
    public async Task TypedCompensation_WithEquivalentAuthorityAliases_IsProved()
    {
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1, ".RequiresAuthority(\"operator\")")
                + Action("undo-receive", 1, 0, ".RequiresAuthority(\"viewer\")"),
            secondAction: Action("complete", 1, 2) + Action("undo-complete", 2, 1),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """,
            boundContract: ".RequiresAuthority(\"manager\")",
            domainSetup: """
                builder.AuthorityAxis(
                    levels: new[] { "reader", "manager" },
                    name: "clearance");
                builder.Authority(name: "operator")
                    .At(levelName: "reader", axisName: "clearance");
                builder.Authority(name: "viewer")
                    .At(levelName: "reader", axisName: "clearance");
                builder.Authority(name: "manager")
                    .At(levelName: "manager", axisName: "clearance");
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A typed rollback on one leaf makes partial rollback in its containing scope unsafe.</summary>
    [Test]
    public async Task TypedCompensation_WithUncompensatedWrittenSibling_ReportsAgwf045()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF045");
        await Assert.That(diagnostic.GetMessage()).Contains("CompleteStep");
    }

    /// <summary>
    /// Typed compensation opts into static proof and therefore requires at least one
    /// workflow-level ontology contract; otherwise leaf frame completeness is unknowable.
    /// </summary>
    [Test]
    public async Task TypedCompensation_WithoutBoundWorkflowContract_ReportsAgwf045()
    {
        var source = Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2) + Action("undo-complete", 2, 1),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """)
            .Replace(
                ".BoundToWorkflow(\"fulfill-order\")",
                string.Empty,
                StringComparison.Ordinal);

        var diagnostic = SingleBindingDiagnostic(source);

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF045");
        await Assert.That(diagnostic.GetMessage()).Contains("at least one closed BoundToWorkflow action");
        await Assert.That(diagnostic.GetMessage()).Contains("no declaration");
    }

    /// <summary>A single typed occurrence opts a mixed typed/legacy program into proof.</summary>
    [Test]
    public async Task MixedCompensation_WithoutBoundWorkflowContract_ReportsOneAgwf045()
    {
        var source = Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>()
                """)
            .Replace(
                ".BoundToWorkflow(\"fulfill-order\")",
                string.Empty,
                StringComparison.Ordinal);

        var diagnostics = BindingDiagnostics(source);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("AGWF045");
    }

    /// <summary>
    /// Multiple closed specifications may describe the same workflow when they share the
    /// subject; each refinement is proved independently while the inverse program is proved once.
    /// </summary>
    [Test]
    public async Task TypedCompensation_WithMultipleSameSubjectBindings_IsProved()
    {
        var secondBinding = """
            obj.Action("fulfill-alias")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow("fulfill-order");
            """;
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: secondBinding
                + Action("receive", 0, 1)
                + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2) + Action("undo-complete", 2, 1),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>One invalid inverse produces one workflow diagnostic across multiple bindings.</summary>
    [Test]
    public async Task TypedCompensation_WithMultipleBindings_DeduplicatesAgwf044()
    {
        var secondBinding = """
            obj.Action("fulfill-alias")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow("fulfill-order");
            """;
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: secondBinding
                + Action("receive", 0, 1)
                + Action("bad-undo", 9, 0),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "bad-undo"))
                """,
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("AGWF044");
    }

    /// <summary>One incomplete rollback scope produces one diagnostic across multiple bindings.</summary>
    [Test]
    public async Task TypedCompensation_WithMultipleBindings_DeduplicatesAgwf045()
    {
        var secondBinding = """
            obj.Action("fulfill-alias")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow("fulfill-order");
            """;
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: secondBinding
                + Action("receive", 0, 1)
                + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("AGWF045");
    }

    /// <summary>Every workflow binding must be a closed contract before typed rollback is claimed.</summary>
    [Test]
    public async Task TypedCompensation_WithOpaqueSecondBinding_ReportsOneAgwf045()
    {
        var secondBinding = """
            obj.Action("fulfill-opaque")
                .Requires(ActionPredicate.Custom("orders.fulfill.opaque.v1"))
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow("fulfill-order");
            """;
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: secondBinding
                + Action("receive", 0, 1)
                + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2) + Action("undo-complete", 2, 1),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """));

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("AGWF045");
        await Assert.That(diagnostics[0].GetMessage()).Contains("orders.fulfill.opaque.v1");
    }

    /// <summary>Multiple workflow bindings cannot make one typed rollback claim across subjects.</summary>
    [Test]
    public async Task TypedCompensation_WithCrossSubjectBinding_ReportsOneAgwf045()
    {
        var diagnostics = BindingDiagnostics(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2) + Action("undo-complete", 2, 1),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive"))
                """,
            secondConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<ReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete"))
                """,
            domainSetup: """
                builder.Object<Order>("OtherOrder", other =>
                {
                    other.Action("fulfill-other")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("fulfill-order");
                });
                """));

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("AGWF045");
        await Assert.That(diagnostics[0].GetMessage()).Contains("do not share one ontology subject");
    }

    /// <summary>Legacy-only unbound compensation retains its runtime compatibility path.</summary>
    [Test]
    public async Task LegacyCompensation_WithoutBoundWorkflowContract_RemainsRuntimeOnly()
    {
        var source = Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>()
                """,
            secondConfiguration: Performs("complete"))
            .Replace(
                ".BoundToWorkflow(\"fulfill-order\")",
                string.Empty,
                StringComparison.Ordinal);

        await Assert.That(BindingDiagnostics(source)).IsEmpty();
    }

    /// <summary>The legacy compensation facade cannot claim a statically proved inverse.</summary>
    [Test]
    public async Task LegacyCompensation_InBoundWorkflow_ReportsAgwf044()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>()
                """,
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF044");
        await Assert.That(diagnostic.GetMessage()).Contains("legacy Compensate<T>()");
    }

    /// <summary>A helper-produced inverse identity is runtime data and cannot enter static proof.</summary>
    [Test]
    public async Task DynamicCompensationActionReference_InBoundWorkflow_ReportsAgwf044()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("undo-receive", 1, 0),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(CreateInverse())
                """,
            secondConfiguration: Performs("complete"),
            workflowMembers: """
                private static WorkflowActionReference CreateInverse() =>
                    new("orders", "Order", "undo-receive");
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF044");
        await Assert.That(diagnostic.GetMessage()).Contains("dynamic or invalid");
    }

    /// <summary>An authored compensation with a non-inverse requirement is rejected exactly.</summary>
    [Test]
    public async Task TypedCompensation_WithContradictoryContract_ReportsAgwf044()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1) + Action("bad-undo", 9, 0),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: """
                step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<CompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "bad-undo"))
                """,
            secondConfiguration: Performs("complete")));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF044");
        await Assert.That(diagnostic.GetMessage()).Contains("inverse requirement");
        await Assert.That(diagnostic.GetMessage()).Contains("counterexample");
    }

    /// <summary>A bound action claiming rollback safety propagates compensability to all written leaves.</summary>
    [Test]
    public async Task RollbackSafeBoundAction_WithUncompensatedWrittenLeaf_ReportsAgwf045()
    {
        var diagnostic = SingleBindingDiagnostic(Source(
            boundWorkflowName: "fulfill-order",
            firstAction: Action("receive", 0, 1),
            secondAction: Action("complete", 1, 2),
            firstConfiguration: Performs("receive"),
            secondConfiguration: Performs("complete"),
            boundCompensation: ".CompensatedBy(\"undo-fulfill\")"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF045");
        await Assert.That(diagnostic.GetMessage()).Contains("workflow:fulfill-order");
        await Assert.That(diagnostic.GetMessage()).Contains("CompleteStep")
            .Because("non-compensable witnesses are selected in stable ordinal phase order");
    }

    /// <summary>A confidence handler cannot reuse a main-flow phase for a different action.</summary>
    [Test]
    public async Task MainAndConfidenceActionIdentityCollision_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(ConfidenceCollisionSource(mainFlowCollision: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("SharedStep");
        await Assert.That(diagnostic.GetMessage()).Contains("dynamic or invalid action reference");
    }

    /// <summary>Two confidence handlers sharing a phase must agree on its action identity.</summary>
    [Test]
    public async Task ConfidenceHandlerActionIdentityCollision_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(ConfidenceCollisionSource(mainFlowCollision: false));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("SharedStep");
        await Assert.That(diagnostic.GetMessage()).Contains("dynamic or invalid action reference");
    }

    /// <summary>A workflow-bound action cannot recursively implement itself.</summary>
    [Test]
    public async Task SelfRecursiveWorkflowBinding_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic(SelfRecursiveBindingSource());

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("recursive workflow binding cycle");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "orders/Order/run -> orders/Order/run");
    }

    /// <summary>Mutually recursive workflow bindings are rejected for every cycle member.</summary>
    [Test]
    public async Task MutuallyRecursiveWorkflowBindings_ReportAgwf042()
    {
        var diagnostics = BindingDiagnostics(MutuallyRecursiveBindingSource());

        await Assert.That(diagnostics).HasCount().EqualTo(2);
        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
            await Assert.That(diagnostic.GetMessage()).Contains("recursive workflow binding cycle");
        }
    }

    private static Diagnostic[] BindingDiagnostics(string source) =>
        RunBindingGenerator(source).Diagnostics
            .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042" or "AGWF044" or "AGWF045")
            .ToArray();

    private static Diagnostic SingleBindingDiagnostic(string source)
    {
        var result = RunBindingGenerator(source);
        var diagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042" or "AGWF044" or "AGWF045")
            .ToArray();
        if (diagnostics.Length != 1)
        {
            var compilationDiagnostics = GeneratorTestHelper.GetCompilationDiagnostics(source)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            throw new InvalidOperationException(
                $"Expected one binding diagnostic, found {diagnostics.Length}. All diagnostics: "
                + string.Join(" | ", result.Diagnostics.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage()))
                + ". Compilation errors: "
                + string.Join(" | ", compilationDiagnostics.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return diagnostics[0];
    }

    private static GeneratorDriverRunResult RunBindingGenerator(string source)
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            source,
            "AGWF039",
            "AGWF040",
            "AGWF041",
            "AGWF042",
            "AGWF044",
            "AGWF045");
        var unexpectedErrors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(static diagnostic => diagnostic.Id is not (
                "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042" or "AGWF044" or "AGWF045"))
            .ToArray();
        if (unexpectedErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "Workflow-binding fixture produced unexpected generator errors: " +
                string.Join(" | ", unexpectedErrors.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return result;
    }

    private static string Performs(string actionName) =>
        $"step => step.Performs(new WorkflowActionReference(\"orders\", \"Order\", \"{actionName}\"))";

    private static string Action(
        string actionName,
        int requirement,
        int guarantee,
        string contractSuffix = "") => $$"""
        obj.Action("{{actionName}}")
            .Requires(order => order.Stage == {{requirement}})
            .Ensures(order => order.Stage == {{guarantee}})
            .Modifies(order => order.Stage)
            {{contractSuffix}};
        """;

    private static string Source(
        string boundWorkflowName,
        string firstAction,
        string secondAction,
        string firstConfiguration,
        string secondConfiguration,
        int boundGuarantee = 2,
        string boundCompensation = "",
        string boundContract = "",
        string domainSetup = "",
        string createWorkflowNameExpression = "\"fulfill-order\"",
        string intermediateChain = "",
        string workflowMembers = "",
        string workflowChainPrefix = "",
        string afterStartWithSuffix = "") => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;
        using Strategos.Steps;

        namespace BindingProof;

        public sealed class Order
        {
            public int Stage { get; set; }
            public int Guard { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                {{domainSetup}}

                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == {{boundGuarantee}})
                        .Modifies(order => order.Stage)
                        {{boundCompensation}}
                        {{boundContract}}
                        .BoundToWorkflow("{{boundWorkflowName}}");

                    {{firstAction}}
                    {{secondAction}}
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class ReceiveStep : TestStep { }
        public sealed class CompleteStep : TestStep { }

        public static class ActionReferences
        {
            public static WorkflowActionReference Receive() =>
                new("orders", "Order", "receive");
        }

        [Workflow("fulfill-order")]
        public static partial class FulfillOrderWorkflowDefinition
        {
            private const string WorkflowIdentity = "fulfill-order";

            private static string GetWorkflowIdentity() => WorkflowIdentity;

            {{workflowMembers}}

            public static WorkflowDefinition<FlowState> Definition => {{workflowChainPrefix}}Workflow<FlowState>
                .Create({{createWorkflowNameExpression}})
                .StartWith<ReceiveStep>({{firstConfiguration}}){{afterStartWithSuffix}}
                {{intermediateChain}}
                .Finally<CompleteStep>({{secondConfiguration}});
        }
        """;

    private static string AuthoritySource(string secondAuthority) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;
        using Strategos.Steps;

        namespace BindingProof;

        public sealed class Order
        {
            public int Stage { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.AuthorityAxis(
                    levels: new[] { "reader", "manager", "admin" },
                    name: "clearance");
                builder.Authority(name: "reader")
                    .At(levelName: "reader", axisName: "clearance");
                builder.Authority(name: "manager")
                    .At(levelName: "manager", axisName: "clearance");
                builder.Authority(name: "admin")
                    .At(levelName: "admin", axisName: "clearance");

                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .RequiresAuthority("manager")
                        .BoundToWorkflow("authority-flow");

                    obj.Action("receive")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage)
                        .RequiresAuthority("reader");

                    obj.Action("complete")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .RequiresAuthority("{{secondAuthority}}");
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class ReceiveStep : TestStep { }
        public sealed class CompleteStep : TestStep { }

        [Workflow("authority-flow")]
        public static partial class AuthorityFlowWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("authority-flow")
                .StartWith<ReceiveStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "receive")))
                .Finally<CompleteStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "complete")));
        }
        """;

    private static string ConfiguredForkSource() => """
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
                    obj.Action("run")
                        .BoundToWorkflow("fork-order");
                    obj.Action("noop");
                    obj.Action("merge");
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
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
        public sealed class LeftStep : TestStep { }
        public sealed class RightStep : TestStep { }
        public sealed class JoinStep : TestStep { }
        public sealed class DoneStep : TestStep { }

        [Workflow("fork-order")]
        public static partial class ForkOrderWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("fork-order")
                .StartWith<EntryStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")))
                .Fork(
                    path => path.Then<LeftStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "noop"))),
                    path => path.Then<RightStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "noop"))))
                .Join<JoinStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "merge")))
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")));
        }
        """;

    private static string ForkCollisionSource(string frameDeclaration) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;
        using Strategos.Steps;

        namespace BindingProof;

        public sealed class Order { }
        public sealed class OrderChanged<T> { }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("run")
                        {{frameDeclaration}}
                        .BoundToWorkflow("fork-order");
                    obj.Action("noop");
                    obj.Action("left"){{frameDeclaration}};
                    obj.Action("right"){{frameDeclaration}};
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
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
        public sealed class LeftStep : TestStep { }
        public sealed class RightStep : TestStep { }
        public sealed class JoinStep : TestStep { }
        public sealed class DoneStep : TestStep { }

        [Workflow("fork-order")]
        public static partial class ForkOrderWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("fork-order")
                .StartWith<EntryStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")))
                .Fork(
                    path => path.Then<LeftStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "left"))),
                    path => path.Then<RightStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "right"))))
                .Join<JoinStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")))
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")));
        }
        """;

    private static string ForkConfidenceFootprintSource(
        string leftHandlerContract,
        string rightHandlerContract) => $$"""
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
            public int Shared { get; set; }
            public int LeftOnly { get; set; }
            public int RightOnly { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("run")
                        .Requires(order => order.Shared == 0)
                        .Modifies(order => order.Shared)
                        .Modifies(order => order.LeftOnly)
                        .Modifies(order => order.RightOnly)
                        .BoundToWorkflow("fork-confidence");
                    obj.Action("preserve")
                        .Requires(order => order.Shared == 0);
                    obj.Action("noop");
                    obj.Action("left-handler"){{leftHandlerContract}};
                    obj.Action("right-handler"){{rightHandlerContract}};
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
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
        public sealed class LeftStep : TestStep { }
        public sealed class RightStep : TestStep { }
        public sealed class LeftHandler : TestStep { }
        public sealed class RightHandler : TestStep { }
        public sealed class JoinStep : TestStep { }
        public sealed class DoneStep : TestStep { }

        [Workflow("fork-confidence")]
        public static partial class ForkConfidenceWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("fork-confidence")
                .StartWith<EntryStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "preserve")))
                .Fork(
                    path => path.Then<LeftStep>(step => step
                        .Performs(new WorkflowActionReference("orders", "Order", "preserve"))
                        .RequireConfidence(0.8)
                        .OnLowConfidence(handler => handler.Then<LeftHandler>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "left-handler"))))),
                    path => path.Then<RightStep>(step => step
                        .Performs(new WorkflowActionReference("orders", "Order", "preserve"))
                        .RequireConfidence(0.8)
                        .OnLowConfidence(handler => handler.Then<RightHandler>(step => step.Performs(
                            new WorkflowActionReference("orders", "Order", "right-handler"))))))
                .Join<JoinStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")))
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")));
        }
        """;

    private static string ConfidenceCollisionSource(bool mainFlowCollision)
    {
        var middle = mainFlowCollision
            ? """
                .Then<SharedStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "main")))
                """
            : """
                .Then<SecondGate>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                    .RequireConfidence(0.8)
                    .OnLowConfidence(path => path.Then<SharedStep>(handler => handler.Performs(
                        new WorkflowActionReference("orders", "Order", "second-handler")))))
                """;
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

                protected override void Define(IOntologyBuilder builder) =>
                    builder.Object<Order>("Order", obj =>
                    {
                        obj.Action("run").BoundToWorkflow("confidence-flow");
                        obj.Action("noop");
                        obj.Action("first-handler");
                        obj.Action("second-handler");
                        obj.Action("main");
                    });
            }

            [WorkflowState]
            public sealed record FlowState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class TestStep : IWorkflowStep<FlowState>
            {
                public Task<StepResult<FlowState>> ExecuteAsync(
                    FlowState state, StepContext context, CancellationToken cancellationToken) =>
                    Task.FromResult(StepResult<FlowState>.FromState(state));
            }

            public sealed class FirstGate : TestStep { }
            public sealed class SecondGate : TestStep { }
            public sealed class SharedStep : TestStep { }
            public sealed class DoneStep : TestStep { }

            [Workflow("confidence-flow")]
            public static partial class ConfidenceFlowWorkflowDefinition
            {
                public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                    .Create("confidence-flow")
                    .StartWith<FirstGate>(step => step
                        .Performs(new WorkflowActionReference("orders", "Order", "noop"))
                        .RequireConfidence(0.8)
                        .OnLowConfidence(path => path.Then<SharedStep>(handler => handler.Performs(
                            new WorkflowActionReference("orders", "Order", "first-handler")))))
                    {{middle}}
                    .Finally<DoneStep>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "noop")));
            }
            """;
    }

    private static string SelfRecursiveBindingSource() => """
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
            protected override void Define(IOntologyBuilder builder) =>
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("run").BoundToWorkflow("recursive-flow");
                    obj.Action("noop");
                });
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class RecursiveStep : TestStep { }
        public sealed class DoneStep : TestStep { }

        [Workflow("recursive-flow")]
        public static partial class RecursiveFlowWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("recursive-flow")
                .StartWith<RecursiveStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "run")))
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")));
        }
        """;

    private static string MutuallyRecursiveBindingSource() => """
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
            protected override void Define(IOntologyBuilder builder) =>
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("a").BoundToWorkflow("flow-a");
                    obj.Action("b").BoundToWorkflow("flow-b");
                    obj.Action("noop");
                });
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class AStep : TestStep { }
        public sealed class BStep : TestStep { }
        public sealed class ADone : TestStep { }
        public sealed class BDone : TestStep { }

        [Workflow("flow-a")]
        public static partial class FlowAWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("flow-a")
                .StartWith<BStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "b")))
                .Finally<ADone>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")));
        }

        [Workflow("flow-b")]
        public static partial class FlowBWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("flow-b")
                .StartWith<AStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "a")))
                .Finally<BDone>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "noop")));
        }
        """;
}
