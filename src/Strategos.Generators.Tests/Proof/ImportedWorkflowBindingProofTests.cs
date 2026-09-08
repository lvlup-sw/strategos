// -----------------------------------------------------------------------
// <copyright file="ImportedWorkflowBindingProofTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Threading;

using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// End-to-end proof coverage for ontology actions bound to imported workflow IR.
/// </summary>
[Property("Category", "Integration")]
public sealed class ImportedWorkflowBindingProofTests
{
    /// <summary>A structured wire action reference participates in the same legal proof as C# authoring.</summary>
    [Test]
    public async Task ImportedLinearWorkflow_WithStructuredActionReferences_IsProved()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1),
            ("imported-flow.workflow.json", ImportedWorkflowJson));

        var generatorErrors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(generatorErrors).IsEmpty()
            .Because("a valid imported workflow and its source-visible ontology contracts must prove cleanly.");
        await Assert.That(result.GeneratedTrees.Any(static tree =>
                tree.FilePath.EndsWith("ImportedFlowSaga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("the real import pipeline must lower the workflow, not merely parse the JSON fixture.");
    }

    /// <summary>An imported illegal seam reports the same deterministic solver witness as C# authoring.</summary>
    [Test]
    public async Task ImportedIllegalSeam_ReportsStableAgwf041Witness()
    {
        var source = Source(secondRequirement: 2);
        var firstResult = RunGenerator(
            source,
            ("imported-flow.workflow.json", ImportedWorkflowJson));
        var secondResult = RunGenerator(
            source,
            ("imported-flow.workflow.json", ImportedWorkflowJson));
        var first = SingleBindingDiagnostic(firstResult);
        var second = SingleBindingDiagnostic(secondResult);

        await Assert.That(first.Id).IsEqualTo("AGWF041");
        await Assert.That(first.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(first.GetMessage())
            .Contains("seam 'ReceiveStep' -> 'CompleteStep' is not composable");
        await Assert.That(first.GetMessage())
            .Contains("counterexample: property|Stage=1");
        await Assert.That(second.ToString()).IsEqualTo(first.ToString())
            .Because("an imported contract violation must have a stable diagnostic and witness.");
        await Assert.That(firstResult.Diagnostics.Count(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error))
            .IsEqualTo(1)
            .Because("the illegal seam must fail through AGWF041 without an unrelated generator error.");
    }

    /// <summary>A C# and JSON definition with the same ordinal identity make the binding ambiguous.</summary>
    [Test]
    public async Task MixedCSharpAndJsonDuplicateWorkflowIdentity_ReportsAgwf039()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1, includeAuthoredWorkflow: true),
            ("imported-flow.workflow.json", ImportedWorkflowJson));
        var diagnostic = SingleBindingDiagnostic(result);

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF039");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("workflow 'imported-flow'");
        await Assert.That(diagnostic.GetMessage()).Contains("resolves to 2 workflow definitions");
        await Assert.That(result.Diagnostics.Count(static item =>
                item.Severity == DiagnosticSeverity.Error))
            .IsEqualTo(1)
            .Because("AGWF039 must be reported before duplicate source hint names can crash the generator.");
    }

    /// <summary>Two JSON definitions with the same ordinal identity also make the binding ambiguous.</summary>
    [Test]
    public async Task DuplicateJsonWorkflowIdentity_ReportsAgwf039WithoutHintCollision()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1),
            ("imported-flow.workflow.json", ImportedWorkflowJson),
            ("imported-flow-copy.workflow.json", ImportedWorkflowJson));
        var diagnostic = SingleBindingDiagnostic(result);

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF039");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("workflow 'imported-flow'");
        await Assert.That(diagnostic.GetMessage()).Contains("resolves to 2 workflow definitions");
        await Assert.That(result.Diagnostics.Count(static item =>
                item.Severity == DiagnosticSeverity.Error))
            .IsEqualTo(1)
            .Because("AGWF039 must be reported before duplicate imported hint names can crash the generator.");
    }

    /// <summary>Two C# definitions with the same ordinal identity also make the binding ambiguous.</summary>
    [Test]
    public async Task DuplicateCSharpWorkflowIdentity_ReportsAgwf039WithoutHintCollision()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1, includeAuthoredWorkflow: true)
                + AuthoredWorkflowDuplicate);
        var diagnostic = SingleBindingDiagnostic(result);

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF039");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("workflow 'imported-flow'");
        await Assert.That(diagnostic.GetMessage()).Contains("resolves to 2 workflow definitions");
        await Assert.That(result.Diagnostics.Count(static item =>
                item.Severity == DiagnosticSeverity.Error))
            .IsEqualTo(1)
            .Because("AGWF039 must be reported before duplicate authored hint names can crash the generator.");
    }

    /// <summary>A dynamic binding cannot let a C#/C# hint collision replace its stable diagnostic.</summary>
    [Test]
    public async Task DynamicBindingWithDuplicateCSharpIdentity_ReportsStableDiagnostics()
    {
        var result = RunGenerator(
            Source(
                secondRequirement: 1,
                includeAuthoredWorkflow: true,
                bindingExpression: "WorkflowNames.ResolveName()")
            + AuthoredWorkflowDuplicate);

        await AssertDynamicCollisionFailsCleanly(result);
    }

    /// <summary>A dynamic binding cannot let a C#/JSON hint collision replace its stable diagnostic.</summary>
    [Test]
    public async Task DynamicBindingWithMixedDuplicateIdentity_ReportsStableDiagnostics()
    {
        var result = RunGenerator(
            Source(
                secondRequirement: 1,
                includeAuthoredWorkflow: true,
                bindingExpression: "WorkflowNames.ResolveName()"),
            ("imported-flow.workflow.json", ImportedWorkflowJson));

        await AssertDynamicCollisionFailsCleanly(result);
    }

    /// <summary>A dynamic binding cannot let a JSON/JSON hint collision replace its stable diagnostic.</summary>
    [Test]
    public async Task DynamicBindingWithDuplicateJsonIdentity_ReportsStableDiagnostics()
    {
        var result = RunGenerator(
            Source(
                secondRequirement: 1,
                bindingExpression: "WorkflowNames.ResolveName()"),
            ("imported-flow.workflow.json", ImportedWorkflowJson),
            ("imported-flow-copy.workflow.json", ImportedWorkflowJson));

        await AssertDynamicCollisionFailsCleanly(result);
    }

    /// <summary>Case-distinct C# workflow ids that share one emitted identity fail stably.</summary>
    [Test]
    public async Task CaseVariantCSharpEmissionIdentityCollision_ReportsAgwf043()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1, includeAuthoredWorkflow: true)
            + AuthoredWorkflowWithName("IMPORTED-FLOW", "CaseVariantWorkflow"));

        await AssertEmissionCollision(result, "'IMPORTED-FLOW', 'imported-flow'");
    }

    /// <summary>A C# and JSON id that normalize to one emitted name fail stably.</summary>
    [Test]
    public async Task MixedKebabEmissionIdentityCollision_ReportsAgwf043()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1, includeAuthoredWorkflow: true),
            ("kebab-collision.workflow.json", WorkflowJsonWithName("imported--flow")));

        await AssertEmissionCollision(result, "'imported--flow', 'imported-flow'");
    }

    /// <summary>Two imported ids that normalize to one emitted name fail stably.</summary>
    [Test]
    public async Task JsonCaseVariantEmissionIdentityCollision_ReportsAgwf043()
    {
        var result = RunGenerator(
            Source(secondRequirement: 1),
            ("imported-flow.workflow.json", ImportedWorkflowJson),
            ("case-collision.workflow.json", WorkflowJsonWithName("IMPORTED-FLOW")));

        await AssertEmissionCollision(result, "'IMPORTED-FLOW', 'imported-flow'");
    }

    private const string ImportedWorkflowJson = """
        {
          "schemaVersion": "1.0",
          "name": "imported-flow",
          "steps": [
            {
              "kind": "skill",
              "stepId": "receive",
              "stepName": "ReceiveStep",
              "isTerminal": false,
              "stepType": "ReceiveStep",
              "action": {
                "domainName": "orders",
                "objectTypeName": "Order",
                "actionName": "receive"
              }
            },
            {
              "kind": "skill",
              "stepId": "complete",
              "stepName": "CompleteStep",
              "isTerminal": true,
              "stepType": "CompleteStep",
              "action": {
                "domainName": "orders",
                "objectTypeName": "Order",
                "actionName": "complete"
              }
            }
          ],
          "transitions": [
            {
              "transitionId": "receive-complete",
              "fromStepId": "receive",
              "toStepId": "complete",
              "isDefault": true
            }
          ],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "receive",
          "terminalStepId": "complete"
        }
        """;

    private static string Source(
        int secondRequirement,
        bool includeAuthoredWorkflow = false,
        string bindingExpression = "\"imported-flow\"") => $$"""
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

        namespace ImportedBindingProof;

        public sealed class Order
        {
            public int Stage { get; set; }
        }

        public static class WorkflowNames
        {
            public static string ResolveName() => "imported-flow";
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
                        .BoundToWorkflow({{bindingExpression}});

                    obj.Action("receive")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);

                    obj.Action("complete")
                        .Requires(order => order.Stage == {{secondRequirement}})
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
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

        {{(includeAuthoredWorkflow ? AuthoredWorkflow : string.Empty)}}
        """;

    private const string AuthoredWorkflow = """
        [Workflow("imported-flow")]
        public static partial class ImportedFlowWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("imported-flow")
                .StartWith<ReceiveStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "receive")))
                .Finally<CompleteStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "complete")));
        }
        """;

    private const string AuthoredWorkflowDuplicate = """

        [Workflow("imported-flow")]
        public static partial class ImportedFlowWorkflowDuplicate
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("imported-flow")
                .StartWith<ReceiveStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "receive")))
                .Finally<CompleteStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "complete")));
        }
        """;

    private static string AuthoredWorkflowWithName(string workflowName, string typeName) => $$"""

        [Workflow("{{workflowName}}")]
        public static partial class {{typeName}}
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("{{workflowName}}")
                .StartWith<ReceiveStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "receive")))
                .Finally<CompleteStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "complete")));
        }
        """;

    private static string WorkflowJsonWithName(string workflowName) =>
        ImportedWorkflowJson.Replace(
            "\"name\": \"imported-flow\"",
            $"\"name\": \"{workflowName}\"",
            StringComparison.Ordinal);

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        params (string Path, string Content)[] additionalTexts)
    {
        var texts = additionalTexts
            .Select(static item => (AdditionalText)new InMemoryAdditionalText(item.Path, item.Content))
            .ToArray();
        return GeneratorTestHelper.RunGeneratorWithValidInput(
            source,
            texts,
            "AGWF039",
            "AGWF040",
            "AGWF041",
            "AGWF042",
            "AGWF043");
    }

    private static async Task AssertDynamicCollisionFailsCleanly(GeneratorDriverRunResult result)
    {
        var errors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(errors.Any(error => error.Id == "AGWF042")).IsTrue();
        await Assert.That(errors.All(error => error.Id is "AGWF042" or "AGWF043")).IsTrue()
            .Because("a collision may add a stable identity diagnostic, but never a generator exception");
        await Assert.That(errors.Single(error => error.Id == "AGWF042").GetMessage())
            .Contains("workflow binding name is dynamic");
    }

    private static async Task AssertEmissionCollision(
        GeneratorDriverRunResult result,
        string expectedNames)
    {
        var errors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        await Assert.That(errors).HasCount().EqualTo(1);
        await Assert.That(errors[0].Id).IsEqualTo("AGWF043");
        await Assert.That(errors[0].GetMessage()).Contains(expectedNames);
        await Assert.That(errors[0].GetMessage()).Contains("generated name 'ImportedFlow'");
    }

    private static Diagnostic SingleBindingDiagnostic(GeneratorDriverRunResult result)
    {
        var unexpectedErrors = result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(static diagnostic => diagnostic.Id is not (
                "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042"))
            .ToArray();
        if (unexpectedErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "Imported workflow-binding fixture produced unexpected generator errors: "
                + string.Join(" | ", unexpectedErrors.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        var diagnostics = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042")
            .ToArray();
        if (diagnostics.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one binding diagnostic, found {diagnostics.Length}: "
                + string.Join(" | ", result.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        }

        return diagnostics[0];
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
