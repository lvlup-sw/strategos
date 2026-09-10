// -----------------------------------------------------------------------
// <copyright file="ActionRefinementProofVectorGeneratorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;
using Strategos.Ontology.Testing;

namespace Strategos.Generators.Tests.Proof;

/// <summary>
/// Runs the production workflow generator over the same refinement cases used
/// by the public runtime calculus.
/// </summary>
[Property("Category", "Integration")]
public sealed class ActionRefinementProofVectorGeneratorTests
{
    [Test]
    public async Task GeneratorMatchesSharedRuntimeRefinementVectors()
    {
        var failures = new List<string>();
        foreach (var vector in ActionRefinementProofVectors.All)
        {
            var source = Source(vector);
            var run = GeneratorTestHelper.RunGeneratorWithValidInput(
                source,
                "AGWF039",
                "AGWF040",
                "AGWF041",
                "AGWF042");
            var diagnostics = run.Diagnostics
                .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042")
                .ToArray();
            var unexpectedErrors = run.Diagnostics
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Where(static diagnostic => diagnostic.Id is not (
                    "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042"))
                .ToArray();

            if (unexpectedErrors.Length > 0)
            {
                failures.Add(
                    $"{vector.Name}: fixture produced unexpected generator errors: "
                    + Describe(unexpectedErrors));
            }

            if (vector.ExpectedGeneratorDiagnosticId is null)
            {
                if (diagnostics.Length != 0)
                {
                    failures.Add(
                        $"{vector.Name}: expected no binding diagnostic, got "
                        + Describe(diagnostics));
                }

                continue;
            }

            if (diagnostics.Length != 1
                || diagnostics[0].Id != vector.ExpectedGeneratorDiagnosticId
                || vector.ExpectedGeneratorMessageFragment is null
                || !diagnostics[0].GetMessage().Contains(
                    vector.ExpectedGeneratorMessageFragment,
                    StringComparison.Ordinal))
            {
                failures.Add(
                    $"{vector.Name}: expected {vector.ExpectedGeneratorDiagnosticId} containing "
                    + $"'{vector.ExpectedGeneratorMessageFragment}', got {Describe(diagnostics)}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics) => string.Join(
        " | ",
        diagnostics.Select(diagnostic => diagnostic.Id + ": " + diagnostic.GetMessage()));

    private static string Source(ActionRefinementProofVector vector)
    {
        var specification = ActionDeclaration(
            "obj",
            "fulfill",
            vector.Specification,
            ".BoundToWorkflow(\"refinement-flow\")");
        var requirementGuard = ActionDeclaration(
            "obj",
            "requirement-guard",
            vector.Specification with
            {
                Guarantee = vector.Specification.Requirement,
                Frame = RefinementFrame.Stage,
                Authority = RefinementAuthority.None,
            });
        var implementationReceiver = vector.Implementation.Subject == RefinementSubject.Document
            ? "obj"
            : "other";
        var implementation = ActionDeclaration(
            implementationReceiver,
            "implementation",
            vector.Implementation);
        var primaryImplementation = vector.Implementation.Subject == RefinementSubject.Document
            ? implementation
            : string.Empty;
        var otherObject = vector.Implementation.Subject == RefinementSubject.OtherDocument
            ? $$"""

                builder.Object<OtherDocument>("OtherDocument", other =>
                {
                    {{implementation}}
                });
                """
            : string.Empty;
        var implementationObjectType = vector.Implementation.Subject == RefinementSubject.Document
            ? "Document"
            : "OtherDocument";

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
            using Strategos.Ontology.Descriptors;
            using Strategos.Steps;

            namespace SharedRefinementProof;

            public sealed class Document
            {
                public int Stage { get; set; }
            }

            public sealed class OtherDocument
            {
                public int Stage { get; set; }
            }

            public sealed class PublicationOntology : DomainOntology
            {
                public override string DomainName => "publication";

                protected override void Define(IOntologyBuilder builder)
                {
                    builder.AuthorityAxis("access", "none", "read", "write");
                    builder.Authority("reader").At("access", "read");
                    builder.Authority("writer").At("access", "write");

                    builder.Object<Document>("Document", obj =>
                    {
                        {{specification}}
                        {{requirementGuard}}
                        {{primaryImplementation}}
                    });
                    {{otherObject}}
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

            public sealed class RequirementStep : TestStep { }
            public sealed class ImplementationStep : TestStep { }

            [Workflow("refinement-flow")]
            public static partial class RefinementFlowWorkflowDefinition
            {
                public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                    .Create("refinement-flow")
                    .StartWith<RequirementStep>(step => step.Performs(
                        new WorkflowActionReference(
                            "publication",
                            "Document",
                            "requirement-guard")))
                    .Finally<ImplementationStep>(step => step.Performs(
                        new WorkflowActionReference(
                            "publication",
                            "{{implementationObjectType}}",
                            "implementation")));
            }
            """;
    }

    private static string ActionDeclaration(
        string receiver,
        string name,
        RefinementContract contract,
        string binding = "")
    {
        var authority = contract.Authority switch
        {
            RefinementAuthority.None => string.Empty,
            RefinementAuthority.Reader => "\n    .RequiresAuthority(\"reader\")",
            RefinementAuthority.Writer => "\n    .RequiresAuthority(\"writer\")",
            _ => throw new ArgumentOutOfRangeException(nameof(contract)),
        };
        var expandedFrame = contract.Frame switch
        {
            RefinementFrame.Stage => string.Empty,
            RefinementFrame.StageAndLedger => "\n    .Touches(ActionResource.External(\"ledger\"))",
            _ => throw new ArgumentOutOfRangeException(nameof(contract)),
        };

        return $$"""
            {{receiver}}.Action("{{name}}")
                .Requires({{Predicate(contract.Requirement)}})
                .Ensures({{Predicate(contract.Guarantee)}})
                .Modifies(item => item.Stage){{expandedFrame}}{{authority}}{{binding}};
            """;
    }

    private static string Predicate(RefinementPredicate predicate) => predicate switch
    {
        RefinementPredicate.StageEqualsZero => "item => item.Stage == 0",
        RefinementPredicate.StageEqualsTwo => "item => item.Stage == 2",
        RefinementPredicate.StageNotEqualsZero => "item => item.Stage != 0",
        RefinementPredicate.StageEqualsZeroOrOne => "item => item.Stage == 0 || item.Stage == 1",
        RefinementPredicate.StageEqualsOneOrTwo => "item => item.Stage == 1 || item.Stage == 2",
        RefinementPredicate.ContradictoryStage => "item => item.Stage == 1 && item.Stage == 2",
        RefinementPredicate.CustomReady => "ActionPredicate.Custom(\"publication.ready.v1\")",
        _ => throw new ArgumentOutOfRangeException(nameof(predicate)),
    };
}
