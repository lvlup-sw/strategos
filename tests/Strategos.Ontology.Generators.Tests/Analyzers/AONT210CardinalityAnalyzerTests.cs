using Microsoft.CodeAnalysis;

using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

/// <summary>
/// DR-6 (#121) — AONT210 association endpoint-cardinality analyzer trigger tests.
/// </summary>
/// <remarks>
/// A reified association (<c>ObjectKind.Association</c>) is a junction object:
/// many association rows fold INTO one endpoint object on each side. The only
/// conformant endpoint cardinality is therefore <c>ManyToOne</c> — the default
/// when no <c>WithCardinality(...)</c> override is declared. AONT210 fires at
/// the <c>Association&lt;TRel&gt;(...)</c> declaration site when an endpoint
/// declares a cardinality (<c>OneToOne</c>, <c>OneToMany</c>, or
/// <c>ManyToMany</c>) that cannot form a valid reified relation.
///
/// Mirrors the AONT037 enforcement style: a single analyzer
/// (<c>OntologyDefinitionAnalyzer</c>), purely syntactic inspection of the
/// configure lambda, reporting at the registration call site.
/// </remarks>
public class AONT210CardinalityAnalyzerTests
{
    [Test]
    public async Task Diagnostic_AONT210_IsRegistered()
    {
        // INV-5: the id is stable and documented in the diagnostic vocabulary;
        // this pins the id/severity so a later edit can't silently renumber it.
        await Assert.That(OntologyDiagnosticIds.AssociationEndpointCardinalityInvalid)
            .IsEqualTo("AONT210");

        var descriptor = OntologyDiagnostics.AssociationEndpointCardinalityInvalid;
        await Assert.That(descriptor.Id).IsEqualTo("AONT210");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    [Test]
    public async Task Analyze_AssociationWithInvalidEndpointCardinality_FiresAONT210()
    {
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

public sealed record AssocPerson(string Id);
public sealed record AssocCompany(string Id);
public sealed record Employment(string Id, AssocPerson Employee, AssocCompany Employer);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""assoc"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AssocPerson>(obj => obj.Key(p => p.Id));
        builder.Object<AssocCompany>(obj => obj.Key(c => c.Id));

        builder.Association<Employment>(""Employment"", a =>
        {
            a.Key(e => e.Id);
            a.Between(e => e.Employee).WithCardinality(EndpointCardinality.OneToMany)
             .And(e => e.Employer);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.AssociationEndpointCardinalityInvalid);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Analyze_ConformantManyToManyAssociation_DoesNotFireAONT210()
    {
        // Two many-to-one endpoints INTO the association object is the valid
        // reified-relation shape: it realizes a many-to-many relationship
        // between the two endpoints via the junction object. Whether the
        // ManyToOne cardinality is explicit or left to the default, AONT210
        // must stay silent.
        var source = @"
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

public sealed record AssocPerson(string Id);
public sealed record AssocCompany(string Id);
public sealed record Employment(string Id, AssocPerson Employee, AssocCompany Employer);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""assoc"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AssocPerson>(obj => obj.Key(p => p.Id));
        builder.Object<AssocCompany>(obj => obj.Key(c => c.Id));

        builder.Association<Employment>(""Employment"", a =>
        {
            a.Key(e => e.Id);
            a.Between(e => e.Employee).WithCardinality(EndpointCardinality.ManyToOne)
             .And(e => e.Employer).WithCardinality(EndpointCardinality.ManyToOne);
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source, OntologyDiagnosticIds.AssociationEndpointCardinalityInvalid);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
