using Microsoft.CodeAnalysis;
using Strategos.Ontology.Generators.Analyzers;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// Task t6 — AONT209 EdgePropertyAuthoringRemoved diagnostic.
/// </summary>
/// <remarks>
/// DR-5 (#120, closes #114): the schema-only edge-properties surface
/// (<c>IEdgeBuilder</c>, the <c>ManyToMany&lt;T&gt;(name, edgeConfig)</c>
/// overload, <c>ICrossDomainLinkBuilder.WithEdge</c>,
/// <c>IExtensionPointBuilder.RequiresEdgeProperty</c>) has been removed.
/// Those authoring vectors no longer resolve to a symbol, so AONT209 is a
/// purely syntactic analyzer rule (INV-2: analyzer only, no codegen) that
/// fires on any residual edge-property authoring attempt and steers the
/// author to <c>Association&lt;T&gt;</c>.
/// </remarks>
public class AONT209EdgePropertyRemovalTests
{
    // ---- Registration / id stability (INV-5) ----

    [Test]
    public async Task AONT209_DiagnosticId_IsEdgePropertyAuthoringRemoved()
    {
        await Assert.That(OntologyDiagnosticIds.EdgePropertyAuthoringRemoved).IsEqualTo("AONT209");
    }

    [Test]
    public async Task AONT209_DescriptorRegistered_HasErrorSeverity()
    {
        var descriptor = OntologyDiagnostics.EdgePropertyAuthoringRemoved;

        await Assert.That(descriptor.Id).IsEqualTo("AONT209");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    [Test]
    public async Task AONT209_HelperMessage_NamesAssociationAndDesignDoc()
    {
        var descriptor = OntologyDiagnostics.EdgePropertyAuthoringRemoved;
        var message = descriptor.MessageFormat.ToString();

        await Assert.That(message).Contains("Association<T>");
        await Assert.That(message).Contains("DR-5");
    }

    [Test]
    public async Task AONT209_RegisteredInAnalyzerSupportedDiagnostics()
    {
        var analyzer = new OntologyDefinitionAnalyzer();

        var supported = analyzer.SupportedDiagnostics;

        await Assert.That(supported.Any(d => d.Id == "AONT209")).IsTrue();
    }

    // ---- Trigger: residual edge-property authoring fires AONT209 with the fix-it ----

    [Test]
    public async Task Analyze_ResidualEdgePropertyAuthoring_FiresAONT209WithAssociationFixit()
    {
        // Residual ManyToMany(name, edgeConfig) authoring — the removed
        // edge-config overload. Authored against a DomainOntology.Define()
        // body so the analyzer's Define-scoping picks it up.
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class Document { public System.Guid Id { get; set; } }
public class Tag { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""kb"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Document>(o =>
        {
            o.ManyToMany<Tag>(""Tags"", edge => edge.Property<double>(""Relevance""));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.EdgePropertyAuthoringRemoved);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("Association<T>");
    }

    [Test]
    public async Task Analyze_ResidualWithEdgeAuthoring_FiresAONT209()
    {
        // Residual ICrossDomainLinkBuilder.WithEdge authoring.
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class Order { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""trading"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.CrossDomainLink(""OrderToInstrument"", link =>
            link.From<Order>()
                .ToExternal(""market-data"", ""Instrument"")
                .ManyToMany()
                .WithEdge(edge => edge.Property<double>(""Weight"")));
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.EdgePropertyAuthoringRemoved);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Analyze_ResidualRequiresEdgeProperty_FiresAONT209()
    {
        // Residual IExtensionPointBuilder.RequiresEdgeProperty authoring.
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class Instrument { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""market-data"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Instrument>(o =>
        {
            o.AcceptsExternalLinks(""inbound"", ep =>
                ep.RequiresEdgeProperty<double>(""Relevance""));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.EdgePropertyAuthoringRemoved);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    // ---- Negative: plain ManyToMany (no edge config) must NOT fire ----

    [Test]
    public async Task Analyze_PlainManyToMany_DoesNotFireAONT209()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public class Document { public System.Guid Id { get; set; } }
public class Tag { public System.Guid Id { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""kb"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Document>(o =>
        {
            o.ManyToMany<Tag>(""Tags"");
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.EdgePropertyAuthoringRemoved);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
