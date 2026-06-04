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

    // -----------------------------------------------------------------------
    // F-HIGH-1 (DR-4) — write/remove key symmetry + orphaned-association cleanup.
    //
    // The relate-store write key is (TargetDescriptor, TargetId, AssociationObjectId).
    // Unrelate must remove on the SAME key: a plain unrelate removes ONLY the plain
    // row (association id == null); an attributed unrelate removes the single row
    // for its association id AND deletes the now-orphaned association object so it
    // is no longer queryable or traversable.
    // -----------------------------------------------------------------------

    [Test]
    public async Task UnrelateAsync_Plain_LeavesAttributedRowAndObject()
    {
        // Arrange — relate plain x->y AND attributed x->y over the same endpoints.
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;
        var association = new RelateAssociation("emp-1", new RelateNode("a"), new RelateNode("b"), "manages");

        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        await writer.RelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", association);

        // Act — plain unrelate removes ONLY the plain row.
        await writer.UnrelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");

        // Assert — the attributed row survives intact.
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).HasCount().EqualTo(1);
        await Assert.That(rows[0].AssociationObjectId).IsEqualTo("emp-1");

        // Assert — the association object is still queryable and traversable.
        var stored = await provider.ExecuteAsync<RelateAssociation>(
            new RootExpression(typeof(RelateAssociation), "RelateAssociation"));
        await Assert.That(stored.Items).HasCount().EqualTo(1);
        await Assert.That(stored.Items[0].Id).IsEqualTo("emp-1");
    }

    [Test]
    public async Task UnrelateAsync_Attributed_RemovesRowAndAssociationObject()
    {
        // Arrange — a single attributed relate.
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;
        var association = new RelateAssociation("emp-1", new RelateNode("a"), new RelateNode("b"), "manages");

        await writer.RelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", association);

        // Act — attributed unrelate removes the row AND the orphaned association object.
        await writer.UnrelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", "emp-1");

        // Assert — no row remains.
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).IsEmpty();

        // Assert — the association object is no longer queryable.
        var stored = await provider.ExecuteAsync<RelateAssociation>(
            new RootExpression(typeof(RelateAssociation), "RelateAssociation"));
        await Assert.That(stored.Items).IsEmpty();
    }

    [Test]
    public async Task UnrelateAsync_Attributed_LeavesPlainAndSiblingAttributed()
    {
        // Arrange — a plain row + two attributed rows (distinct association ids)
        // over the same endpoint pair.
        var provider = SeededProvider("a", "b");
        IObjectSetWriter writer = provider;
        var first = new RelateAssociation("emp-1", new RelateNode("a"), new RelateNode("b"), "manages");
        var second = new RelateAssociation("emp-2", new RelateNode("a"), new RelateNode("b"), "mentors");

        await writer.RelateAsync(nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b");
        await writer.RelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", first);
        await writer.RelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", second);

        // Act — attributed-unrelate of emp-1 removes ONLY that row + its object.
        await writer.UnrelateAsync(
            nameof(RelateNode), "a", "links_to", nameof(RelateNode), "b",
            "RelateAssociation", "emp-1");

        // Assert — the plain row and the emp-2 attributed row survive.
        var rows = provider.GetRelations(nameof(RelateNode), "a", "links_to");
        await Assert.That(rows).HasCount().EqualTo(2);
        var associationIds = rows.Select(r => r.AssociationObjectId).ToList();
        await Assert.That(associationIds).Contains((string?)null);
        await Assert.That(associationIds).Contains("emp-2");
        await Assert.That(associationIds).DoesNotContain("emp-1");

        // Assert — emp-2 survives as a stored object, emp-1 is gone.
        var stored = await provider.ExecuteAsync<RelateAssociation>(
            new RootExpression(typeof(RelateAssociation), "RelateAssociation"));
        var storedIds = stored.Items.Select(r => r.Id).ToList();
        await Assert.That(storedIds).IsEquivalentTo(new[] { "emp-2" });
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
