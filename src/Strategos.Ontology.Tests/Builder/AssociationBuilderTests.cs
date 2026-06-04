using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

// ---------------------------------------------------------------------------
// Test domain types for the association builder (DR-4)
// ---------------------------------------------------------------------------

public sealed record AssocPerson(string Id);

public sealed record AssocCompany(string Id);

// Reified association: Employment links a Person (left) to a Company (right)
// and carries its own edge attribute (Title) plus its own key.
public sealed record Employment(string Id, AssocPerson Employee, AssocCompany Employer, string Title);

public sealed class AssociationTestOntology : DomainOntology
{
    public override string DomainName => "assoc";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AssocPerson>(obj => obj.Key(p => p.Id));
        builder.Object<AssocCompany>(obj => obj.Key(c => c.Id));

        builder.Association<Employment>("Employment", a =>
        {
            a.Key(e => e.Id);
            a.Between(e => e.Employee).And(e => e.Employer);
            a.Property(e => e.Title).Required();
        });
    }
}

public class AssociationBuilderTests
{
    private static ObjectTypeDescriptor BuildAssociationDescriptor()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<AssociationTestOntology>();
        var graph = graphBuilder.Build();
        return graph.ObjectTypes.First(ot => ot.Name == "Employment");
    }

    [Test]
    public async Task Association_DeclaresRelationWithTwoTypedEndpoints()
    {
        var descriptor = BuildAssociationDescriptor();

        // The association is a standalone object type tagged with the Association kind.
        await Assert.That(descriptor.Kind).IsEqualTo(ObjectKind.Association);

        // It exposes exactly two endpoints, referenced by descriptor name — never
        // by a bare System.Type (INV-8).
        await Assert.That(descriptor.AssociationEndpoints).HasCount().EqualTo(2);

        var left = descriptor.AssociationEndpoints[0];
        var right = descriptor.AssociationEndpoints[1];

        await Assert.That(left.DescriptorName).IsEqualTo(nameof(AssocPerson));
        await Assert.That(right.DescriptorName).IsEqualTo(nameof(AssocCompany));

        // The role records which property on the association object carries the endpoint.
        await Assert.That(left.Role).IsEqualTo(nameof(Employment.Employee));
        await Assert.That(right.Role).IsEqualTo(nameof(Employment.Employer));
    }

    [Test]
    public async Task Association_WithProperty_CapturesEdgeAttribute()
    {
        var descriptor = BuildAssociationDescriptor();

        // The association carries its own properties (edge attributes), like any object type.
        await Assert.That(descriptor.Properties.Count).IsEqualTo(1);
        await Assert.That(descriptor.Properties[0].Name).IsEqualTo(nameof(Employment.Title));
        await Assert.That(descriptor.Properties[0].IsRequired).IsTrue();

        // Its own key/id flows through the DR-1 IdAccessor path like any object type.
        await Assert.That(descriptor.KeyProperty).IsNotNull();
        await Assert.That(descriptor.KeyProperty!.Name).IsEqualTo(nameof(Employment.Id));
        await Assert.That(descriptor.IdAccessor).IsNotNull();

        var instance = new Employment("e1", new AssocPerson("p1"), new AssocCompany("c1"), "Engineer");
        await Assert.That(descriptor.IdAccessor!(instance)).IsEqualTo("e1");
    }
}
