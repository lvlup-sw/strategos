using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;
using TUnit.Assertions.Enums;

namespace Strategos.Ontology.Tests.ObjectSets;

// ---------------------------------------------------------------------------
// Test domain types for the relate-store (DR-2)
// ---------------------------------------------------------------------------

public sealed record RelateNode(string Id);

// DR-4 (Task 17): a reified association linking two RelateNode endpoints with
// its own key + edge attribute, for the attributed-relate seam.
public sealed record RelateAssociation(string Id, RelateNode From, RelateNode To, string Label);

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

        builder.Association<RelateAssociation>("RelateAssociation", a =>
        {
            a.Key(r => r.Id);
            a.Between(r => r.From).And(r => r.To);
            a.Property(r => r.Label).Required();
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

    // -----------------------------------------------------------------------
    // Task 6
    // -----------------------------------------------------------------------

    [Test]
    public async Task RelateAsync_DuplicateTriple_IsIdempotent()
    {
        // Arrange
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;

        // Act — relate the same (src, link, tgt) twice
        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");

        // Assert — only one row exists
        await Assert.That(rows).HasCount().EqualTo(1);
        await Assert.That(rows[0].TargetId).IsEqualTo("b");
    }

    [Test]
    public async Task UnrelateAsync_MissingRow_IsNoOp()
    {
        // Arrange
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;

        // Act & Assert — unrelating an absent row does not throw
        await writer.UnrelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");

        await Assert.That(rows).IsEmpty();
    }

    // -----------------------------------------------------------------------
    // Task 7 — deterministic ordinal-by-id read order (INV-7 / replay)
    // -----------------------------------------------------------------------

    [Test]
    public async Task Relations_ReadOrder_IsOrdinalByTargetId()
    {
        // Arrange — seed targets and relate them in a deliberately non-sorted order
        var provider = SeededProvider("src", "delta", "alpha", "charlie", "bravo");
        IObjectSetWriter writer = provider;

        await writer.RelateAsync(nameof(RelateNode), "src", "links_to", nameof(RelateNode), "delta");
        await writer.RelateAsync(nameof(RelateNode), "src", "links_to", nameof(RelateNode), "alpha");
        await writer.RelateAsync(nameof(RelateNode), "src", "links_to", nameof(RelateNode), "charlie");
        await writer.RelateAsync(nameof(RelateNode), "src", "links_to", nameof(RelateNode), "bravo");

        // Act
        var rows = provider.GetRelations(nameof(RelateNode), "src", "links_to");

        // Assert — read order is ordinal by TargetId, regardless of insert order
        var targetIds = rows.Select(r => r.TargetId).ToList();
        await Assert.That(targetIds)
            .IsEquivalentTo(new[] { "alpha", "bravo", "charlie", "delta" }, CollectionOrdering.Matching);
    }

    // -----------------------------------------------------------------------
    // Task 8 (DR-8) — eager endpoint validation
    // -----------------------------------------------------------------------

    [Test]
    public async Task RelateAsync_NonExistentEndpoint_ThrowsTypedError()
    {
        // Arrange — only the source is stored; the target id has no stored instance
        var provider = SeededProvider("a");
        IObjectSetWriter writer = provider;

        // Act & Assert — eager validation throws a typed error
        await Assert.That(async () =>
                await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "ghost"))
            .Throws<RelationEndpointNotFoundException>();

        // Assert — no dangling row was created
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).IsEmpty();
    }

    // -----------------------------------------------------------------------
    // Task 9 (DR-8) — self-loop policy
    // -----------------------------------------------------------------------

    [Test]
    public async Task RelateAsync_SelfLoop_WhenDisallowed_ThrowsTypedError()
    {
        // Arrange — default "links_to" link has AllowsSelfLoop = false
        var provider = SeededProvider("a");
        IObjectSetWriter writer = provider;

        // Act & Assert — relating an instance to itself is rejected
        await Assert.That(async () =>
                await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "a"))
            .Throws<SelfLoopNotAllowedException>();

        // Assert — no row was created
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).IsEmpty();
    }

    [Test]
    public async Task RelateAsync_SelfLoop_WhenAllowed_CreatesRow()
    {
        // Arrange — rewrite the "links_to" link to allow self-loops
        var provider = SelfLoopAllowedProvider("a");
        IObjectSetWriter writer = provider;

        // Act
        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "a");

        // Assert — the self-loop row exists
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).HasCount().EqualTo(1);
        await Assert.That(rows[0].TargetId).IsEqualTo("a");
    }

    // -----------------------------------------------------------------------
    // Task 17 (DR-4) — attributed relate stores the association object and a
    // row referencing it via AssociationObjectId.
    // -----------------------------------------------------------------------

    [Test]
    public async Task RelateWithAssociation_StoresObjectAndRowReferencingIt()
    {
        // Arrange — both endpoints stored; the association object carries its own id.
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;
        var association = new RelateAssociation("emp-1", new RelateNode("a"), new RelateNode("b"), "manages");

        // Act — attributed relate stores the association AND writes a row that points at it.
        await writer.RelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", association);

        // Assert — the row references the association object's projected id.
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).HasCount().EqualTo(1);
        await Assert.That(rows[0].TargetDescriptor).IsEqualTo(nameof(RelateNode));
        await Assert.That(rows[0].TargetId).IsEqualTo("b");
        await Assert.That(rows[0].AssociationObjectId).IsEqualTo("emp-1");

        // Assert — the association object itself was stored under its descriptor.
        var stored = await provider.ExecuteAsync<RelateAssociation>(
            new RootExpression(typeof(RelateAssociation), "RelateAssociation"));
        await Assert.That(stored.Items).HasCount().EqualTo(1);
        await Assert.That(stored.Items[0].Id).IsEqualTo("emp-1");
    }

    private static InMemoryObjectSetProvider SelfLoopAllowedProvider(params string[] ids)
    {
        var baseGraph = BuildGraph();
        var rewritten = baseGraph.ObjectTypes
            .Select(ot => ot with
            {
                Links = ot.Links
                    .Select(l => l with { AllowsSelfLoop = true })
                    .ToList(),
            })
            .ToList();

        var graph = new OntologyGraph(
            domains: baseGraph.Domains,
            objectTypes: rewritten,
            interfaces: baseGraph.Interfaces,
            crossDomainLinks: baseGraph.CrossDomainLinks,
            workflowChains: baseGraph.WorkflowChains);

        var provider = new InMemoryObjectSetProvider(graph);
        foreach (var id in ids)
        {
            provider.Seed(new RelateNode(id), id, nameof(RelateNode));
        }

        return provider;
    }
}
