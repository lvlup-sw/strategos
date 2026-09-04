using Microsoft.CodeAnalysis;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class AONT216CompensationTests
{
    [Test]
    public async Task FluentCompensationWithDifferentFrame_IsRejectedAtCompileTime()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Modifies(item => item.Status)
                .CompensatedBy("unpublish");
            obj.Action("unpublish").Modifies(item => item.Title);
            """);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].GetMessage()).Contains("unpublish");
    }

    [Test]
    public async Task FluentCompensationWithSameFrame_IsAccepted()
    {
        var diagnostics = await AnalyzeAsync("""
            obj.Action("publish")
                .Modifies(item => item.Status)
                .CompensatedBy("unpublish");
            obj.Action("unpublish").Modifies(item => item.Status);
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> AnalyzeAsync(string actions) =>
        AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            $$"""
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public sealed class Model
            {
                public string Id { get; set; } = "";
                public string Status { get; set; } = "";
                public string Title { get; set; } = "";
            }

            public sealed class TestDomain : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<Model>(obj =>
                    {
                        obj.Key(item => item.Id);
                        obj.Property(item => item.Status);
                        obj.Property(item => item.Title);
                        {{actions}}
                    });
                }
            }
            """,
            OntologyDiagnosticIds.CompensationDisagreesWithInverse);
}
