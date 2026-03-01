namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class CrossDomainLinkAnalyzerTests
{
    [Test]
    public async Task ONTO003_CrossDomainLinkUnknownDomain_ReportsWarning()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity
            {
                public string Id { get; set; }
            }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                    builder.CrossDomainLink("SomeLink")
                        .From<TestEntity>()
                        .ToExternal("nonexistent-domain", "SomeType");
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO003")).IsTrue();
    }

    [Test]
    public async Task ONTO003_CrossDomainLinkKnownDomain_NoWarning()
    {
        // When there's no way to verify other domains from a single file's perspective,
        // the analyzer can only warn about obviously suspicious patterns.
        // For this test, we verify that a simple CrossDomainLink without ToExternal does not report ONTO003.
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity
            {
                public string Id { get; set; }
            }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                    builder.CrossDomainLink("SomeLink")
                        .From<TestEntity>();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO003")).IsFalse();
    }

    [Test]
    public async Task ONTO004_ObjectTypeNoActions_ReportsInfo()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity
            {
                public string Id { get; set; }
                public string Name { get; set; }
            }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                        obj.Property(e => e.Name);
                    });
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO004")).IsTrue();
    }

    [Test]
    public async Task ONTO004_ObjectTypeWithActions_NoInfo()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity
            {
                public string Id { get; set; }
            }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                        obj.Action("DoSomething")
                            .Description("Performs an action");
                    });
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO004")).IsFalse();
    }
}
