namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class ObjectTypeAnalyzerTests
{
    [Test]
    public async Task ONTO001_ObjectTypeWithoutKey_ReportsError()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity { public string Id { get; set; } }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        // No Key() call - should trigger ONTO001
                    });
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO001")).IsTrue();
    }

    [Test]
    public async Task ONTO001_ObjectTypeWithKey_NoError()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity { public string Id { get; set; } }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO001")).IsFalse();
    }

    [Test]
    public async Task ONTO007_DuplicateObjectType_ReportsError()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class TestEntity { public string Id { get; set; } }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                    builder.Object<TestEntity>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO007")).IsTrue();
    }

    [Test]
    public async Task ONTO007_UniqueObjectTypes_NoError()
    {
        var source = """
            using System;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;

            public class Entity1 { public string Id { get; set; } }
            public class Entity2 { public string Id { get; set; } }

            public class TestOntology : DomainOntology
            {
                public override string DomainName => "test";
                protected override void Define(IOntologyBuilder builder)
                {
                    builder.Object<Entity1>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                    builder.Object<Entity2>(obj =>
                    {
                        obj.Key(e => e.Id);
                    });
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Any(d => d.Id == "ONTO007")).IsFalse();
    }
}
