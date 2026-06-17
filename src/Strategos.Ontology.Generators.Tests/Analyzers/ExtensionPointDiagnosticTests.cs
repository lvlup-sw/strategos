using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class ExtensionPointDiagnosticTests
{
    [Test]
    public async Task AONT031_CrossDomainLinkNoExtensionPoint_ReportsWarning()
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

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.CrossDomainLinkNoExtensionPoint);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT031_WithExtensionPoint_NoDiagnostic()
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
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.Description(""allows external links"");
            });
        });
        builder.CrossDomainLink(""TestToExternal"")
            .From<TestModel>()
            .ToExternal(""other"", ""OtherType"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.CrossDomainLinkNoExtensionPoint);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT032_ExtensionPointInterfaceUnsatisfied_ReportsWarning()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface IAuditable { }
public class TestModel { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.FromInterface<IAuditable>();
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ExtensionPointInterfaceUnsatisfied);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT032_InterfaceSatisfied_NoDiagnostic()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public interface IAuditable { }
public class TestModel : IAuditable { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IAuditable>(""IAuditable"", iface => { });
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Implements<IAuditable>(m => { });
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.FromInterface<IAuditable>();
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ExtensionPointInterfaceUnsatisfied);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    // AONT033 (ExtensionPointEdgeMissing) trigger tests were removed in DR-5
    // (#120, closes #114): they authored the now-deleted RequiresEdgeProperty
    // and WithEdge edge-property surface, which AONT209 now flags. The AONT033
    // descriptor/id remain registered (INV-5) but dormant.

    [Test]
    public async Task AONT034_ExtensionPointNoLinksMatch_ReportsInfo()
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
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.Description(""accepts external links"");
            });
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ExtensionPointNoLinks);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT034_ExtensionPointWithLinks_NoDiagnostic()
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
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.Description(""accepts external links"");
            });
        });
        builder.CrossDomainLink(""InboundLink"")
            .From<TestModel>()
            .ToExternal(""other"", ""OtherType"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ExtensionPointNoLinks);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT035_MaxLinksExceeded_ReportsWarning()
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
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.MaxLinks(1);
            });
        });
        builder.CrossDomainLink(""Link1"")
            .From<TestModel>()
            .ToExternal(""other"", ""OtherType1"");
        builder.CrossDomainLink(""Link2"")
            .From<TestModel>()
            .ToExternal(""other"", ""OtherType2"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ExtensionPointMaxLinksExceeded);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT035_WithinMaxLinks_NoDiagnostic()
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
            obj.AcceptsExternalLinks(""inbound"", ep =>
            {
                ep.MaxLinks(5);
            });
        });
        builder.CrossDomainLink(""Link1"")
            .From<TestModel>()
            .ToExternal(""other"", ""OtherType1"");
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.ExtensionPointMaxLinksExceeded);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
