using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class CoreDiagnosticTests
{
    [Test]
    public async Task AONT001_MissingKey_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public string Name { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Property(p => p.Name).Required();
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.MissingKey);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT001_WithKey_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public string Name { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Name).Required();
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.MissingKey);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT006_DuplicateObjectType_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj => { obj.Key(p => p.Id); });
        builder.Object<TestModel>(obj => { obj.Key(p => p.Id); });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DuplicateObjectType);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT004_ActionNotBound_ReportsWarning()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Action(""DoSomething"").Description(""test"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ActionNotBound);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT004_ActionBoundToWorkflow_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Action(""DoSomething"").BoundToWorkflow(""do-something"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ActionNotBound);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT007_CrossDomainLink_ReportsWarning()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj => { obj.Key(p => p.Id); });
        builder.CrossDomainLink(""TestToExternal"")
            .From<TestModel>()
            .ToExternal(""other"", ""OtherType"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.CrossDomainLinkUnverifiable);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task NonDomainOntology_NoDiagnostics()
    {
        var source = @"
public class NotAnOntology
{
    protected void Define(string builder)
    {
        // This should not trigger any diagnostics
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
