using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class PreconditionDiagnosticTests
{
    [Test]
    public async Task AONT009_EmitsEventUndeclared_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Qty { get; set; } }
public record TestEvent(System.Guid Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Qty);
            obj.Action(""Trade"").EmitsEvent<TestEvent>();
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.EmitsEventUndeclared);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT009_EmitsEventDeclared_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public Guid Id { get; set; } public decimal Qty { get; set; } }
public record TestEvent(Guid Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Qty);
            obj.Event<TestEvent>(e => { });
            obj.Action(""Trade"").EmitsEvent<TestEvent>();
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.EmitsEventUndeclared);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT010_ModifiesUndeclaredProperty_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Qty { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Action(""Trade"").Modifies(p => p.Qty);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ModifiesUndeclaredProperty);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT010_ModifiesDeclaredProperty_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } public decimal Qty { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Qty);
            obj.Action(""Trade"").Modifies(p => p.Qty);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ModifiesUndeclaredProperty);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT011_CreatesLinkedUndeclared_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel { public System.Guid Id { get; set; } }
public class TestOrder { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Action(""Trade"").CreatesLinked<TestOrder>(""Orders"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.CreatesLinkedUndeclared);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT012_RequiresLinkUndeclared_ReportsWarning()
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
            obj.Action(""Trade"").RequiresLink(""Strategy"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.RequiresLinkUndeclared);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }
}
