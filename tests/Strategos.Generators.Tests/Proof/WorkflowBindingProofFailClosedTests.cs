// -----------------------------------------------------------------------
// <copyright file="WorkflowBindingProofFailClosedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Proof;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// Proves that the workflow binding proof cannot report success by accident: an internal failure
/// of the proof is a build error, and the proof diagnostics cannot be suppressed or downgraded
/// by consumer configuration.
/// </summary>
/// <remarks>
/// Roslyn turns an exception escaping a source-output node into CS8785, a warning, and drops
/// every diagnostic that node would have produced. Without the guard under test, a crash inside
/// the proof would read as "no diagnostics" to a consumer build and to every fixture that asserts
/// an empty diagnostic set.
/// </remarks>
[Property("Category", "Integration")]
public sealed class WorkflowBindingProofFailClosedTests
{
    private const string LegalBindingSource = """
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

        namespace FailClosedProbe;

        public sealed class Order { public int Stage { get; set; } }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("flow");

                    obj.Action("leaf")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("done")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState { public Guid WorkflowId { get; init; } }

        public sealed class Leaf : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class Done : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        [Workflow("flow")]
        public static partial class FlowWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("flow")
                .StartWith<Leaf>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "leaf")))
                .Finally<Done>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "done")));
        }
        """;

    /// <summary>
    /// A compilation that has something to prove without containing the literal
    /// <c>BoundToWorkflow</c> anywhere: the workflow declares typed derived compensation, which
    /// the proof rejects precisely because no ontology action binds the workflow. The fail-closed
    /// gate must therefore not be a text scan for the binding builder method.
    /// </summary>
    private const string TypedCompensationWithoutBindingSource = """
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

        namespace FailClosedTypedProbe;

        public sealed class Order { public int Stage { get; set; } }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("leaf")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("undo-leaf")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 0)
                        .Modifies(order => order.Stage);

                    obj.Action("done")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                });
            }
        }

        [WorkflowState]
        public sealed record TypedState : IWorkflowState { public Guid WorkflowId { get; init; } }

        public sealed class LeafStep : IWorkflowStep<TypedState>
        {
            public Task<StepResult<TypedState>> ExecuteAsync(
                TypedState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<TypedState>.FromState(state));
        }

        public sealed class UndoLeafStep : IWorkflowStep<TypedState>
        {
            public Task<StepResult<TypedState>> ExecuteAsync(
                TypedState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<TypedState>.FromState(state));
        }

        public sealed class DoneStep : IWorkflowStep<TypedState>
        {
            public Task<StepResult<TypedState>> ExecuteAsync(
                TypedState state, StepContext context, CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<TypedState>.FromState(state));
        }

        [Workflow("typed-flow")]
        public static partial class TypedFlowWorkflowDefinition
        {
            public static WorkflowDefinition<TypedState> Definition => Workflow<TypedState>
                .Create("typed-flow")
                .StartWith<LeafStep>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "leaf"))
                    .Compensate<UndoLeafStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-leaf")))
                .Finally<DoneStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "done")));
        }
        """;

    /// <summary>Control: the fixture is a legal binding that the proof accepts.</summary>
    [Test]
    [NotInParallel(nameof(WorkflowBindingProofAnalyzer.ProofFaultInjection))]
    public async Task LegalBinding_WithoutFault_ReportsNoErrors()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(LegalBindingSource);

        var errors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Kill fixture for the fail-closed guard: an exception inside the proof must surface as one
    /// AGWF042 error, never as the generator-crash warning with an empty diagnostic set.
    /// </summary>
    [Test]
    [NotInParallel(nameof(WorkflowBindingProofAnalyzer.ProofFaultInjection))]
    public async Task ProofInternalFailure_ReportsAgwf042Error_NotGeneratorCrash()
    {
        WorkflowBindingProofAnalyzer.ProofFaultInjection = FaultOnlyForThisFixture;
        try
        {
            var result = GeneratorTestHelper.RunGeneratorWithValidInput(LegalBindingSource, "AGWF042");

            var errors = result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();

            await Assert.That(errors).HasCount().EqualTo(1);
            await Assert.That(errors[0].Id).IsEqualTo("AGWF042");
            await Assert.That(errors[0].GetMessage())
                .Contains("failed internally with InvalidOperationException: injected proof fault");
            await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "CS8785")).IsFalse()
                .Because("the proof must not surface as a Roslyn generator-crash warning");
        }
        finally
        {
            WorkflowBindingProofAnalyzer.ProofFaultInjection = null;
        }
    }

    /// <summary>
    /// Control for the typed-compensation fail-closed fixture: with no fault injected the proof
    /// rejects the compilation, and the fixture contains the literal <c>BoundToWorkflow</c>
    /// nowhere. This is what the fault run must not be allowed to silence.
    /// </summary>
    [Test]
    [NotInParallel(nameof(WorkflowBindingProofAnalyzer.ProofFaultInjection))]
    public async Task TypedCompensationWithoutBinding_WithoutFault_ReportsAgwf045()
    {
        await Assert.That(TypedCompensationWithoutBindingSource.Contains("BoundToWorkflow", StringComparison.Ordinal))
            .IsFalse()
            .Because("the fixture only proves anything while the syntactic gate cannot see it");

        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            TypedCompensationWithoutBindingSource,
            "AGWF045");

        var errors = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(errors).HasCount().EqualTo(1);
        await Assert.That(errors[0].Id).IsEqualTo("AGWF045");
        await Assert.That(errors[0].GetMessage()).Contains("no declaration binds this workflow");
    }

    /// <summary>
    /// Kill fixture for the fail-closed gate itself: a compilation whose typed compensation the
    /// proof rejects, but which never mentions the binding builder method, must still fail closed
    /// when the proof throws. Before the model-derived predicate this run reported nothing at all
    /// while the derived-compensation saga was emitted unproved.
    /// </summary>
    [Test]
    [NotInParallel(nameof(WorkflowBindingProofAnalyzer.ProofFaultInjection))]
    public async Task ProofInternalFailure_WithTypedCompensationAndNoBindingText_ReportsAgwf042()
    {
        WorkflowBindingProofAnalyzer.ProofFaultInjection = FaultOnlyForTypedFixture;
        try
        {
            var result = GeneratorTestHelper.RunGeneratorWithValidInput(
                TypedCompensationWithoutBindingSource,
                "AGWF042");

            var errors = result.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();

            await Assert.That(errors).HasCount().EqualTo(1)
                .Because("a compilation with typed compensation has something to prove even without a BoundToWorkflow mention");
            await Assert.That(errors[0].Id).IsEqualTo("AGWF042");
            await Assert.That(errors[0].GetMessage())
                .Contains("failed internally with InvalidOperationException: injected proof fault");
            await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "CS8785")).IsFalse()
                .Because("the proof must not surface as a Roslyn generator-crash warning");
        }
        finally
        {
            WorkflowBindingProofAnalyzer.ProofFaultInjection = null;
        }
    }

    /// <summary>
    /// A generator-internal failure is an Error only for a compilation that has something to
    /// prove. A compilation that never binds an action to a workflow keeps building: the proof
    /// is vacuous there, and turning every consumer build red for a bug in the scanner is the
    /// wrong blast radius.
    /// </summary>
    [Test]
    [NotInParallel(nameof(WorkflowBindingProofAnalyzer.ProofFaultInjection))]
    public async Task InjectedProofFault_InCompilationThatBindsNothing_ReportsNothing()
    {
        const string unboundSource = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace FailClosedProbe.Unbound;

            [WorkflowState]
            public sealed record PlainState : IWorkflowState { public Guid WorkflowId { get; init; } }

            public sealed class First : IWorkflowStep<PlainState>
            {
                public Task<StepResult<PlainState>> ExecuteAsync(
                    PlainState state, StepContext context, CancellationToken cancellationToken) =>
                    Task.FromResult(StepResult<PlainState>.FromState(state));
            }

            public sealed class Last : IWorkflowStep<PlainState>
            {
                public Task<StepResult<PlainState>> ExecuteAsync(
                    PlainState state, StepContext context, CancellationToken cancellationToken) =>
                    Task.FromResult(StepResult<PlainState>.FromState(state));
            }

            [Workflow("plain")]
            public static partial class PlainWorkflowDefinition
            {
                public static WorkflowDefinition<PlainState> Definition => Workflow<PlainState>
                    .Create("plain")
                    .StartWith<First>()
                    .Finally<Last>();
            }
            """;

        WorkflowBindingProofAnalyzer.ProofFaultInjection = FaultOnlyForThisFixture;
        try
        {
            var result = GeneratorTestHelper.RunGeneratorWithValidInput(unboundSource);

            await Assert.That(result.Diagnostics.Where(diagnostic => diagnostic.Id == "AGWF042")).IsEmpty()
                .Because("a compilation with no BoundToWorkflow call has nothing to prove, so an internal proof failure must not fail its build");
            await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "CS8785")).IsFalse()
                .Because("the failure must still be caught rather than surfacing as a driver crash");
        }
        finally
        {
            WorkflowBindingProofAnalyzer.ProofFaultInjection = null;
        }
    }

    /// <summary>
    /// The proof node runs even when the compilation declares no workflow at all: the
    /// bound action's contract is EXPORTED so a referencing compilation can discharge
    /// the obligation, rather than the binding being silently accepted because the
    /// workflow collection was empty.
    /// </summary>
    /// <remarks>
    /// Before the cross-assembly catalog this compilation reported an unresolved
    /// binding, which an ontology-only project then silenced — there was no other way
    /// to ship one. Exporting the contract is the other way. The property under test
    /// is unchanged: an empty workflow collection must not short-circuit the proof
    /// node. What changed is the observable effect of it having run.
    /// </remarks>
    [Test]
    public async Task BoundAction_InCompilationWithNoWorkflows_ExportsAndDefersTheBinding()
    {
        const string ontologyOnlySource = """
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            namespace ZeroWorkflowProbe;

            public sealed class Order { public int Stage { get; set; } }

            public sealed class OrdersOntology : DomainOntology
            {
                public override string DomainName => "orders";

                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<Order>("Order", obj =>
                    {
                        obj.Action("fulfill")
                            .Requires(order => order.Stage == 0)
                            .Ensures(order => order.Stage == 1)
                            .Modifies(order => order.Stage)
                            .BoundToWorkflow("flow-declared-nowhere");
                    });
                }
            }
            """;

        var result = GeneratorTestHelper.RunGeneratorWithValidInput(ontologyOnlySource);

        var catalog = result.GeneratedTrees
            .FirstOrDefault(tree => tree.FilePath.EndsWith(
                "StrategosProofCatalog.g.cs", StringComparison.Ordinal));
        await Assert.That(catalog).IsNotNull()
            .Because("an empty workflow collection must not short-circuit the proof node: it "
                + "still has to export the obligation it cannot discharge here.");
        await Assert.That(catalog!.GetText().ToString()).Contains("flow-declared-nowhere")
            .Because("the exported contract must name the workflow it claims to implement.");

        await Assert.That(result.Diagnostics.Any(diagnostic => diagnostic.Id == "AGWF039")).IsFalse()
            .Because("a binding whose contract was exported is deferred to whoever lowers the "
                + "workflow, not reported as unresolvable here.");
    }

    /// <summary>
    /// Every binding diagnostic — the two resolution ids and the five proof outcomes —
    /// is a machine-checked guarantee a consumer must not be able to switch off with
    /// <c>NoWarn</c> or an <c>.editorconfig</c> severity.
    /// </summary>
    /// <remarks>
    /// The two resolution ids were configurable until the cross-assembly catalog
    /// existed, because a layout the compilation-local proof could not see had no
    /// other exit and the alternative was deleting the binding. Exporting the contract
    /// is that exit, so the exemption is withdrawn: an unresolved binding now means
    /// the obligation reaches nobody, which is not a thing to silence.
    /// </remarks>
    [Test]
    [Arguments("AGWF039")]
    [Arguments("AGWF040")]
    [Arguments("AGWF041")]
    [Arguments("AGWF042")]
    [Arguments("AGWF043")]
    [Arguments("AGWF044")]
    [Arguments("AGWF045")]
    public async Task ProofOutcomeDiagnostics_AreNotConfigurable(string id)
    {
        var descriptor = ProofDescriptor(id);

        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.CustomTags).Contains(WellKnownDiagnosticTags.NotConfigurable);
    }

    /// <summary>
    /// Throws only for compilations that carry this class's fixture namespace. The seam is a
    /// process-wide static and other generator tests run concurrently; a fault that fired for
    /// every compilation would leak spurious AGWF042 into unrelated suites.
    /// </summary>
    /// <param name="compilation">The compilation the proof is about to analyze.</param>
    private static void FaultOnlyForThisFixture(Compilation compilation)
    {
        if (compilation.SyntaxTrees.Any(tree =>
                tree.GetText().ToString().Contains("namespace FailClosedProbe", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("injected proof fault");
        }
    }

    /// <summary>
    /// Throws only for the typed-compensation fixture, whose namespace the fixture-scoped fault
    /// above deliberately does not match.
    /// </summary>
    /// <param name="compilation">The compilation the proof is about to analyze.</param>
    private static void FaultOnlyForTypedFixture(Compilation compilation)
    {
        if (compilation.SyntaxTrees.Any(tree =>
                tree.GetText().ToString().Contains("namespace FailClosedTypedProbe", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("injected proof fault");
        }
    }

    private static DiagnosticDescriptor ProofDescriptor(string id) =>
        new[]
        {
            WorkflowDiagnostics.BoundWorkflowNotFound,
            WorkflowDiagnostics.WorkflowActionReferenceInvalid,
            WorkflowDiagnostics.WorkflowBindingRefinementFailed,
            WorkflowDiagnostics.WorkflowContractUnprovable,
            WorkflowDiagnostics.WorkflowEmissionIdentityCollision,
            WorkflowDiagnostics.AuthoredInverseDisagrees,
            WorkflowDiagnostics.CompensationScopeNotDerivable,
        }.Single(candidate => candidate.Id == id);
}
