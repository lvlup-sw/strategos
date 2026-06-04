using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

// ---------------------------------------------------------------------------
// Test domain types for the relate-store (DR-2)
// ---------------------------------------------------------------------------

public sealed record RelateNode(string Id);

public sealed class RelateTestOntology : DomainOntology
{
    public override string DomainName => "relate";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<RelateNode>(obj =>
        {
            obj.Key(n => n.Id);
            obj.HasMany<RelateNode>("links_to");
        });
    }
}

public class InMemoryRelateStoreTests
{
    private static OntologyGraph BuildGraph()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<RelateTestOntology>();
        return graphBuilder.Build();
    }

    private static InMemoryObjectSetProvider SeededProvider(params string[] ids)
    {
        var provider = new InMemoryObjectSetProvider(BuildGraph());
        foreach (var id in ids)
        {
            provider.Seed(new RelateNode(id), id, nameof(RelateNode));
        }

        return provider;
    }

    // -----------------------------------------------------------------------
    // Task 5
    // -----------------------------------------------------------------------

    [Test]
    public async Task RelateAsync_TwoInstances_CreatesRow()
    {
        // Arrange
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;

        // Act
        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");

        // Assert
        await Assert.That(rows).HasCount().EqualTo(1);
        await Assert.That(rows[0].TargetDescriptor).IsEqualTo(nameof(RelateNode));
        await Assert.That(rows[0].TargetId).IsEqualTo("b");
        await Assert.That(rows[0].AssociationObjectId).IsNull();
    }

    [Test]
    public async Task UnrelateAsync_ExistingRow_RemovesIt()
    {
        // Arrange
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;
        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");

        // Act
        await writer.UnrelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");

        // Assert
        await Assert.That(rows).IsEmpty();
    }
}
