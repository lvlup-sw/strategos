using Microsoft.CodeAnalysis;

using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class AONT214AuthorityLatticeTests
{
    [Test]
    public async Task Diagnostic_IsRegisteredAsError()
    {
        await Assert.That(OntologyDiagnosticIds.InvalidAuthorityLattice).IsEqualTo("AONT214");
        await Assert.That(OntologyDiagnostics.InvalidAuthorityLattice.DefaultSeverity)
            .IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task Analyze_ValidTwoAxisProduct_IsAccepted()
    {
        var diagnostics = await AnalyzeAsync("""
            builder.AuthorityAxis("access", "read", "write");
            builder.AuthorityAxis("sensitivity", "public", "restricted");
            builder.Authority("reader")
                .At("access", "read")
                .At("sensitivity", "public");
            builder.Authority("restrictedWriter")
                .At("access", "write")
                .At("sensitivity", "restricted")
                .Implies("reader");
            builder.Object<Model>(obj =>
            {
                obj.Key(item => item.Id);
                obj.Action("read").RequiresAuthority("reader");
                obj.Action("write").RequiresAuthority("restrictedWriter");
            });
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task Analyze_MissingCoordinate_IsRejected()
    {
        var diagnostics = await AnalyzeAsync("""
            builder.AuthorityAxis("access", "read", "write");
            builder.AuthorityAxis("sensitivity", "public", "restricted");
            builder.Authority("reader").At("access", "read");
            builder.Object<Model>(obj =>
            {
                obj.Key(item => item.Id);
                obj.Action("read").RequiresAuthority("reader");
            });
            """);

        await Assert.That(diagnostics.Any(diagnostic =>
            diagnostic.GetMessage().Contains("exactly one level on every axis", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task Analyze_NonMonotoneImplication_IsRejected()
    {
        var diagnostics = await AnalyzeAsync("""
            builder.AuthorityAxis("access", "read", "write");
            builder.Authority("reader").At("access", "read").Implies("writer");
            builder.Authority("writer").At("access", "write");
            builder.Object<Model>(obj =>
            {
                obj.Key(item => item.Id);
                obj.Action("read").RequiresAuthority("reader");
                obj.Action("write").RequiresAuthority("writer");
            });
            """);

        await Assert.That(diagnostics.Any(diagnostic =>
            diagnostic.GetMessage().Contains("not at least as strong", StringComparison.Ordinal)))
            .IsTrue();
    }

    [Test]
    public async Task Analyze_NonLiteralAxisLevels_DoesNotEmitFalsePositive()
    {
        var diagnostics = await AnalyzeAsync("""
            var levels = new[] { "read", "write" };
            builder.AuthorityAxis("access", levels);
            builder.Authority("reader").At("access", "read");
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task Analyze_DescriptorRequiredAuthority_CountsAsUse()
    {
        var diagnostics = await AnalyzeAsync("""
            builder.AuthorityAxis("access", "read");
            builder.Authority("reader").At("access", "read");
            builder.ObjectTypeFromDescriptor(new Strategos.Ontology.Descriptors.ObjectTypeDescriptor
            {
                Name = "Model",
                DomainName = "test",
                ClrType = typeof(Model),
                Actions =
                [
                    new Strategos.Ontology.Descriptors.ActionDescriptor("read", "read")
                    {
                        RequiredAuthority = "reader",
                    },
                ],
            });
            """);

        await Assert.That(diagnostics).IsEmpty();
    }

    private static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> AnalyzeAsync(string body) =>
        AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            $$"""
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public sealed class Model { public string Id { get; set; } = ""; }

            public sealed class TestDomain : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    {{body}}
                }
            }
            """,
            OntologyDiagnosticIds.InvalidAuthorityLattice);
}
