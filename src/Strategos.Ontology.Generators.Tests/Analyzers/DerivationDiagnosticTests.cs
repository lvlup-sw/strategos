using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class DerivationDiagnosticTests
{
    [Test]
    public async Task AONT023_DerivedFromNonComputed_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel
{
    public System.Guid Id { get; set; }
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
}

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Qty);
            obj.Property(p => p.Price);
            obj.Property(p => p.Total).DerivedFrom(p => p.Qty, p => p.Price);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivedFromNonComputed);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT023_DerivedFromComputed_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel
{
    public System.Guid Id { get; set; }
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Total { get; set; }
}

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Qty);
            obj.Property(p => p.Price);
            obj.Property(p => p.Total).Computed().DerivedFrom(p => p.Qty, p => p.Price);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivedFromNonComputed);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT026_ComputedNoDerivedFrom_ReportsInfo()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel
{
    public System.Guid Id { get; set; }
    public decimal Total { get; set; }
}

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Total).Computed();
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ComputedNoDerivedFrom);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT026_ComputedWithDerivedFrom_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel
{
    public System.Guid Id { get; set; }
    public decimal Qty { get; set; }
    public decimal Total { get; set; }
}

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Qty);
            obj.Property(p => p.Total).Computed().DerivedFrom(p => p.Qty);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ComputedNoDerivedFrom);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
