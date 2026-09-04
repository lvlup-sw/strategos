using Microsoft.CodeAnalysis;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class AONT215ActionFrameTests
{
    [Test]
    public async Task DescriptorPostconditionOutsideFrame_IsRejectedAtCompileTime()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(Source("""
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = "test",
                ClrType = typeof(Model),
                Actions =
                [
                    new ActionDescriptor("publish", "Publish")
                    {
                        TouchedResources = [ActionResource.Property("Title")],
                        Postconditions =
                        [
                            new ActionPostcondition
                            {
                                Kind = PostconditionKind.ModifiesProperty,
                                PropertyName = "Status",
                            },
                        ],
                    },
                ],
            });
            """), OntologyDiagnosticIds.ActionFrameUnsound);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].GetMessage()).Contains("Property:Status");
    }

    [Test]
    public async Task DescriptorPostconditionInsideFrame_IsAccepted()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(Source("""
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = "test",
                ClrType = typeof(Model),
                Actions =
                [
                    new ActionDescriptor("publish", "Publish")
                    {
                        TouchedResources = [ActionResource.Property("Status")],
                        Postconditions =
                        [
                            new ActionPostcondition
                            {
                                Kind = PostconditionKind.ModifiesProperty,
                                PropertyName = "Status",
                            },
                        ],
                    },
                ],
            });
            """), OntologyDiagnosticIds.ActionFrameUnsound);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static string Source(string body) => $$"""
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;

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
                {{body}}
            }
        }
        """;
}
