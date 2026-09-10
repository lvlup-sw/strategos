using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class InterfaceActionDiagnosticTests
{
    [Test]
    public async Task AONT027_InterfaceActionUnmapped_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"");
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Implements<ISearchable>(m => { });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.InterfaceActionUnmapped);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT027_Mapped_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"");
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Action(""DoSearch"").BoundToWorkflow(""search"");
            obj.Implements<ISearchable>(m =>
            {
                m.ActionVia(""Search"", ""DoSearch"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.InterfaceActionUnmapped);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT028_ActionViaBadReference_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"");
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Implements<ISearchable>(m =>
            {
                m.ActionVia(""Search"", ""NonExistentAction"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ActionViaBadReference);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT028_ValidReference_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"");
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Action(""DoSearch"").BoundToWorkflow(""search"");
            obj.Implements<ISearchable>(m =>
            {
                m.ActionVia(""Search"", ""DoSearch"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ActionViaBadReference);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT029_IncompatibleAcceptsType_ReportsError()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class SearchRequest { public string Query { get; set; } }
public class TradeRequest { public decimal Amount { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"").Accepts<SearchRequest>();
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Action(""Search"").Accepts<TradeRequest>().BoundToWorkflow(""search"");
            obj.Implements<ISearchable>(m =>
            {
                m.ActionVia(""Search"", ""Search"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.InterfaceActionIncompatible);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT029_CompatibleTypes_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class SearchRequest { public string Query { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"").Accepts<SearchRequest>();
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Action(""Search"").Accepts<SearchRequest>().BoundToWorkflow(""search"");
            obj.Implements<ISearchable>(m =>
            {
                m.ActionVia(""Search"", ""Search"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.InterfaceActionIncompatible);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT030_InterfaceWithActionsNoImplementors_ReportsWarning()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class TestModel { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"");
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.InterfaceActionNoImplementors);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT030_InterfaceWithImplementors_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface ISearchable { string Query { get; set; } }
public class TestModel : ISearchable { public System.Guid Id { get; set; } public string Query { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>(""ISearchable"", iface =>
        {
            iface.Action(""Search"");
        });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Query);
            obj.Action(""Search"").BoundToWorkflow(""search"");
            obj.Implements<ISearchable>(m =>
            {
                m.ActionVia(""Search"", ""Search"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.InterfaceActionNoImplementors);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
