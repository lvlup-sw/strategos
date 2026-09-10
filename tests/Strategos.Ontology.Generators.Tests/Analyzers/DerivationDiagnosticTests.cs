using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class DerivationDiagnosticTests
{
    [Test]
    public async Task AONT022_DerivedFromUndeclaredProperty_ReportsError()
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
            obj.Property(p => p.Total).Computed().DerivedFrom(p => p.Qty);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivedFromUndeclaredProperty);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT022_DeclaredProperty_NoDiagnostic()
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

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivedFromUndeclaredProperty);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

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

    [Test]
    public async Task AONT024_DerivationCycle_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class TestModel
{
    public System.Guid Id { get; set; }
    public decimal A { get; set; }
    public decimal B { get; set; }
}

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.A).Computed().DerivedFrom(p => p.B);
            obj.Property(p => p.B).Computed().DerivedFrom(p => p.A);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivationCycle);

        await Assert.That(diagnostics.Length).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task AONT024_NoCycle_NoDiagnostic()
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

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivationCycle);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT025_DerivedFromExternal_ReportsWarning()
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
            obj.Property(p => p.Total).Computed().DerivedFromExternal(""other"", ""OtherType"", ""Amount"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivedFromExternalUnresolvable);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT025_LocalDerivedFrom_NoDiagnostic()
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

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.DerivedFromExternalUnresolvable);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
