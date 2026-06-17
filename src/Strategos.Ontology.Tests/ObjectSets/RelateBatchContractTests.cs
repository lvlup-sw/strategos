using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

/// <summary>
/// DR-13 (R6, #130): contract tests RESERVING the <c>RelateBatchAsync</c> surface
/// on <see cref="IObjectSetWriter"/>. The motivating workload is bulk edge
/// ingestion (#115): relating thousands of endpoint pairs one-at-a-time through
/// the single-pair <c>RelateAsync</c> is a round-trip per edge. R6 reserves the
/// batch shape on the contract NOW so #115 can land a set-based Npgsql lowering
/// without a breaking interface change; the in-memory provider gets a correct
/// (if naive) loop today, and the Npgsql provider explicitly throws
/// <see cref="NotSupportedException"/> until #115 implements the batched DML.
/// </summary>
/// <remarks>
/// The Npgsql throw-until-ingestion half lives in
/// <c>Strategos.Ontology.Npgsql.Tests</c> (only that project references the
/// Npgsql provider); this file pins the in-memory loop semantics.
/// </remarks>
public class RelateBatchContractTests
{
    private sealed record BatchNode(string Id);

    private sealed class BatchRelateOntology : DomainOntology
    {
        public override string DomainName => "batch-relate";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<BatchNode>(obj =>
            {
                obj.Key(n => n.Id);
                obj.HasMany<BatchNode>("links_to");
            });
        }
    }

    private static InMemoryObjectSetProvider SeededProvider(params string[] ids)
    {
        var graph = new OntologyGraphBuilder().AddDomain<BatchRelateOntology>().Build();
        var provider = new InMemoryObjectSetProvider(graph);
        foreach (var id in ids)
        {
            provider.Seed(new BatchNode(id), id, nameof(BatchNode));
        }

        return provider;
    }

    [Test]
    public async Task RelateBatchAsync_InMemory_RelatesAll()
    {
        // Arrange — three sources, all linking to a shared target.
        var provider = SeededProvider("a", "b", "c", "t");
        IObjectSetWriter writer = provider;

        var requests = new List<RelateRequest>
        {
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "a", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "t" },
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "b", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "t" },
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "c", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "t" },
        };

        // Act
        await writer.RelateBatchAsync(requests);

        // Assert — every request materialized exactly as the single-pair path would.
        await Assert.That(provider.GetRelations(nameof(BatchNode), "a", "links_to")).HasCount().EqualTo(1);
        await Assert.That(provider.GetRelations(nameof(BatchNode), "b", "links_to")).HasCount().EqualTo(1);
        await Assert.That(provider.GetRelations(nameof(BatchNode), "c", "links_to")).HasCount().EqualTo(1);
        await Assert.That(provider.GetRelations(nameof(BatchNode), "a", "links_to")[0].TargetId).IsEqualTo("t");
    }

    [Test]
    public async Task RelateBatchAsync_InMemory_PropagatesEndpointValidation()
    {
        // The batch loop must preserve the single-pair eager validation: a request
        // naming a non-existent endpoint surfaces the SAME typed error, never a
        // silent skip.
        var provider = SeededProvider("a");
        IObjectSetWriter writer = provider;

        var requests = new List<RelateRequest>
        {
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "a", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "ghost" },
        };

        await Assert.That(async () => await writer.RelateBatchAsync(requests))
            .Throws<RelationEndpointNotFoundException>();
    }

    [Test]
    public async Task RelateBatchAsync_InMemory_FailingItem_TagsBatchItemIndex()
    {
        // F6: a mid-batch failure must KEEP its typed exception
        // (RelationEndpointNotFoundException) AND carry the failing item's position.
        // Items 0 and 2 are valid; item 1 names a non-existent target, so the throw
        // must originate at index 1 — not 0 — proving the index reflects the actual
        // failing request rather than the loop start.
        var provider = SeededProvider("a", "b", "c", "t");
        IObjectSetWriter writer = provider;

        var requests = new List<RelateRequest>
        {
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "a", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "t" },
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "b", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "ghost" },
            new() { SourceDescriptor = nameof(BatchNode), SourceId = "c", LinkName = "links_to", TargetDescriptor = nameof(BatchNode), TargetId = "t" },
        };

        RelationEndpointNotFoundException? caught = null;
        try
        {
            await writer.RelateBatchAsync(requests);
        }
        catch (RelationEndpointNotFoundException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Data["BatchItemIndex"]).IsEqualTo(1);
        // The offending endpoint identifiers are surfaced too, so the caller can
        // locate the bad request without re-deriving it from the index alone.
        await Assert.That(caught!.Data["BatchItemTargetId"]).IsEqualTo("ghost");
        await Assert.That(caught!.Data["BatchItemSourceId"]).IsEqualTo("b");
    }

    [Test]
    public async Task RelateBatchAsync_InMemory_EmptyBatch_IsNoOp()
    {
        var provider = SeededProvider("a", "t");
        IObjectSetWriter writer = provider;

        await writer.RelateBatchAsync([]);

        await Assert.That(provider.GetRelations(nameof(BatchNode), "a", "links_to")).IsEmpty();
    }
}
