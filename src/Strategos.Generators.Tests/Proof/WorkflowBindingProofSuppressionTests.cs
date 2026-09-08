// -----------------------------------------------------------------------
// <copyright file="WorkflowBindingProofSuppressionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// Driver-level proof that a refuted binding cannot be silenced. The descriptor tag alone says
/// what Roslyn is asked to do; this class runs the generator through the driver's
/// compilation-option filter with every suppression channel a consumer has (<c>/nowarn</c> via
/// specific diagnostic options and <c>#pragma warning disable</c> in source) and checks what
/// survives. A configurable warning from the same generator is the control: it must vanish
/// under the same options, or the filter was never applied and the test would be vacuous.
/// </summary>
[Property("Category", "Integration")]
public sealed class WorkflowBindingProofSuppressionTests
{
    private const string RefutedBindingWithSuppressions = """
        #pragma warning disable AGWF041
        #pragma warning disable AGWF002
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

        namespace SuppressionProbe;

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

                    // The entry step requires Stage == 99, which the bound requirement
                    // (Stage == 0) does not imply: the binding is refuted (AGWF041).
                    obj.Action("leaf")
                        .Requires(order => order.Stage == 99)
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

        // The control: an attributed workflow with no definition produces the configurable
        // AGWF002 warning, which the same suppressions must remove.
        [Workflow("no-steps")]
        public static partial class NoStepsWorkflow
        {
        }
        """;

    private static CSharpCompilationOptions SuppressingOptions() =>
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal)
            {
                ["AGWF041"] = ReportDiagnostic.Suppress,
                ["AGWF002"] = ReportDiagnostic.Suppress,
            });

    /// <summary>
    /// Control: without suppressions both the refutation and the warning reach the consumer.
    /// </summary>
    [Test]
    public async Task RefutedBinding_WithoutSuppression_ReportsRefutationAndControlWarning()
    {
        var diagnostics = GeneratorTestHelper.RunWorkflowGeneratorThroughDriverFilter(
            WithoutPragmas(RefutedBindingWithSuppressions),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "AGWF041")).IsEqualTo(1);
        await Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "AGWF002")).IsEqualTo(1)
            .Because("the control warning must exist before its suppression can prove the filter ran");
    }

    /// <summary>
    /// The refutation survives every suppression channel; the configurable control does not.
    /// </summary>
    [Test]
    public async Task RefutedBinding_UnderNoWarnAndPragma_StillFailsTheBuild()
    {
        var diagnostics = GeneratorTestHelper.RunWorkflowGeneratorThroughDriverFilter(
            RefutedBindingWithSuppressions,
            SuppressingOptions());

        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "AGWF002")).IsFalse()
            .Because("a configurable warning must be removed by the same options, proving the driver filter was applied");

        var refutations = diagnostics.Where(diagnostic => diagnostic.Id == "AGWF041").ToArray();
        await Assert.That(refutations).HasCount().EqualTo(1)
            .Because("NotConfigurable must carry the refutation past /nowarn and #pragma warning disable");
        await Assert.That(refutations[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(refutations[0].IsSuppressed).IsFalse();
    }

    /// <summary>
    /// The resolution diagnostic for a workflow the compilation cannot see is the sanctioned
    /// exit for a cross-assembly layout: an explicit <c>/nowarn</c> removes it.
    /// </summary>
    [Test]
    public async Task UnresolvedBinding_UnderNoWarn_IsSilencedExplicitly()
    {
        var source = WithoutPragmas(RefutedBindingWithSuppressions)
            .Replace(".BoundToWorkflow(\"flow\")", ".BoundToWorkflow(\"flow-in-another-assembly\")", StringComparison.Ordinal);
        var plain = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var silenced = plain.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal)
        {
            ["AGWF039"] = ReportDiagnostic.Suppress,
        });

        var unsilenced = GeneratorTestHelper.RunWorkflowGeneratorThroughDriverFilter(source, plain);
        var explicitlySilenced = GeneratorTestHelper.RunWorkflowGeneratorThroughDriverFilter(source, silenced);

        await Assert.That(unsilenced.Count(diagnostic => diagnostic.Id == "AGWF039")).IsEqualTo(1)
            .Because("by default an unresolved binding is an Error");
        await Assert.That(explicitlySilenced.Any(diagnostic => diagnostic.Id == "AGWF039")).IsFalse()
            .Because("a consumer whose workflow lives in another assembly needs a visible, explicit exit until #204");
    }

    private static string WithoutPragmas(string source) =>
        source
            .Replace("#pragma warning disable AGWF041", string.Empty, StringComparison.Ordinal)
            .Replace("#pragma warning disable AGWF002", string.Empty, StringComparison.Ordinal);
}
