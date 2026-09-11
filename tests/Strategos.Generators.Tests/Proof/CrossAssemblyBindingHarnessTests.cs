// -----------------------------------------------------------------------
// <copyright file="CrossAssemblyBindingHarnessTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// The two-assembly harness (#204): what the #167 proof sees when a declaration is behind a
/// compiled boundary.
/// </summary>
/// <remarks>
/// <para>
/// These tests exist before any manifest code, and they are the reason it is needed. Both
/// halves of a workflow binding are legal, ordinary C#. Which half lives in which assembly is a
/// layout choice, invisible to the author of either half — and until #204 it silently decided
/// whether the #167 guarantee applied at all.
/// </para>
/// <para>
/// Two splits, two different failures:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Ontology behind the boundary.</b> The consumer compiles the workflow; the bound action is
/// in a referenced assembly. <c>OntologyActionCatalog</c> walks
/// <c>Compilation.SyntaxTrees</c>, so it finds no binding, the proof has nothing to discharge,
/// and the consumer's build is clean. The producing assembly did report AGWF039 when it was
/// compiled on its own — and silenced it, because that is what AGWF039's configurable severity
/// is for. So the binding is unproved on both sides, and the only trace of it is one silenced id
/// in a project file that the consuming team never sees.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Workflow behind the boundary.</b> The consumer compiles the ontology; the bound workflow
/// name resolves to zero definitions here. AGWF039 fires — and AGWF039 is deliberately
/// configurable precisely so this layout can be silenced, which makes the visible half
/// silenceable by design.
/// </description>
/// </item>
/// </list>
/// </remarks>
[Property("Category", "Integration")]
public sealed class CrossAssemblyBindingHarnessTests
{
    /// <summary>The harness produces a real referenceable image whose types the consumer binds against.</summary>
    /// <remarks>
    /// Asserted first and on its own, because every other test here reads a NEGATIVE result. If
    /// the producer silently emitted nothing, or the consumer silently failed to reference it,
    /// "no diagnostic was reported" would be true for the wrong reason and the whole file would
    /// pass while proving nothing.
    /// </remarks>
    [Test]
    public async Task Harness_CompilesAProducerTheConsumerActuallyBindsAgainst()
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            "OntologyProducer", OntologyAssembly, runGenerators: true, "AGWF039");

        await Assert.That(producer.Image.Length).IsGreaterThan(0)
            .Because("a producer that emitted no image cannot be referenced by anything.");

        var symbol = TwoAssemblyHarness.ResolveSymbol(producer);
        var ontologyType = symbol.GetTypeByMetadataName("CrossAssembly.Ontology.OrdersOntology");
        await Assert.That(ontologyType).IsNotNull()
            .Because("the consumer must see the producer's ontology type through metadata alone.");

        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);
        var order = consumer.OutputCompilation.GetTypeByMetadataName("CrossAssembly.Ontology.Order");
        await Assert.That(order).IsNotNull()
            .Because("the consumer compilation must genuinely resolve the producer's types.");
    }

    /// <summary>
    /// The same binding, split so the ontology is behind the boundary, produces no diagnostic at
    /// all — the silent hole #204 closes.
    /// </summary>
    [Test]
    public async Task OntologyBehindTheBoundary_ProducesNoDiagnostic_AndNoGuarantee()
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            "OntologyProducer", OntologyAssembly, runGenerators: true, "AGWF039");
        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);

        await Assert.That(consumer.ErrorIds()).IsEmpty()
            .Because(
                "today a cross-assembly binding compiles clean. That is the defect: the layout "
                + "choice silently removed the #167 guarantee, and nothing said so.");

        // The saga is still emitted. The workflow is real and lowers normally; what is missing
        // is the proof that it refines the action it is bound to.
        var sagaTrees = consumer.RunResult.GeneratedTrees
            .Where(tree => tree.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal))
            .ToArray();
        await Assert.That(sagaTrees).IsNotEmpty()
            .Because("the workflow lowers; only its binding proof is absent.");
    }

    /// <summary>
    /// Split the other way, the visible failure is a configurable one: AGWF039 exists to be
    /// silenced for exactly this layout.
    /// </summary>
    [Test]
    public async Task WorkflowBehindTheBoundary_ReportsTheConfigurableResolutionDiagnostic()
    {
        var producer = TwoAssemblyHarness.CompileProducer("WorkflowProducer", WorkflowAssembly);
        var consumer = TwoAssemblyHarness.CompileConsumer(OntologyAssembly, producer);

        var reported = consumer.WithId("AGWF039");
        await Assert.That(reported).IsNotEmpty()
            .Because("a bound workflow name that resolves to nothing in this compilation is reported.");
        await Assert.That(reported[0].GetMessage()).Contains("resolves to 0 workflow definitions");

        var descriptor = reported[0].Descriptor;
        await Assert.That(descriptor.CustomTags).DoesNotContain(WellKnownDiagnosticTags.NotConfigurable)
            .Because(
                "AGWF039 is configurable today so a cross-assembly layout can be silenced in the "
                + "project file. Once the layout can be PROVED, that exemption is what #204 removes.");
    }

    /// <summary>
    /// A producer built without the Strategos generators still compiles and is still
    /// referenceable — the "missing manifest" shape, available to the harness before anything
    /// emits a manifest.
    /// </summary>
    [Test]
    public async Task ProducerWithoutGenerators_IsStillReferenceable()
    {
        var producer = TwoAssemblyHarness.CompileProducer(
            "OntologyProducerNoEmitter", OntologyAssembly, runGenerators: false);

        await Assert.That(producer.GeneratedHintNames).IsEmpty()
            .Because("this producer models an assembly built without the emitter.");

        var consumer = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly, producer);
        await Assert.That(consumer.OutputCompilation.GetTypeByMetadataName("CrossAssembly.Ontology.Order"))
            .IsNotNull()
            .Because("a consumer can reference such an assembly; nothing about the reference fails.");
    }

    /// <summary>
    /// Neither half alone discharges the obligation: the ontology half reports an unresolvable
    /// binding, and the workflow half has nothing to report at all.
    /// </summary>
    /// <remarks>
    /// This is what makes the silence in the split case dangerous rather than merely incomplete.
    /// The obligation exists, and there is no single compilation in which it can be met — so the
    /// only honest states are "proved across the boundary" or "refused", which is what the rest
    /// of #204 builds.
    /// </remarks>
    [Test]
    public async Task NeitherSideAlone_DischargesTheObligation()
    {
        var ontologyOnly = TwoAssemblyHarness.CompileConsumer(OntologyAssembly);
        var workflowOnly = TwoAssemblyHarness.CompileConsumer(WorkflowAssembly);

        await Assert.That(ontologyOnly.WithId("AGWF039")).IsNotEmpty()
            .Because("the ontology half alone cannot resolve its bound workflow.");
        await Assert.That(workflowOnly.ErrorIds()).IsEmpty()
            .Because("the workflow half alone has no binding to discharge, so it is silent.");
    }

    /// <summary>
    /// The ontology half: a domain whose action binds a workflow declared elsewhere.
    /// </summary>
    private const string OntologyAssembly = """
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;

        namespace CrossAssembly.Ontology;

        public sealed class Order
        {
            public int Stage { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("fulfill-order");

                    obj.Action("receive")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("complete")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                });
            }
        }
        """;

    /// <summary>
    /// The workflow half: the implementation the bound action names, every step carrying the
    /// occurrence-scoped action reference the proof reads.
    /// </summary>
    private const string WorkflowAssembly = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology.Descriptors;
        using Strategos.Steps;

        namespace CrossAssembly.Flow;

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

        [Workflow("fulfill-order")]
        public static partial class FulfillOrderWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("fulfill-order")
                .StartWith<ReceiveStep>(step =>
                    step.Performs(new WorkflowActionReference("orders", "Order", "receive")))
                .Finally<CompleteStep>(step =>
                    step.Performs(new WorkflowActionReference("orders", "Order", "complete")));
        }
        """;
}
