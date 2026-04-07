using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

/// <summary>
/// End-to-end coverage for the fluent similarity chain — from
/// <see cref="IOntologyQuery.GetObjectSet{T}"/> through
/// <see cref="ObjectSet{T}.SimilarTo"/> and the
/// <see cref="SimilarObjectSet{T}"/> immutable setters to materialization
/// against an <see cref="InMemoryObjectSetProvider"/>.
/// </summary>
public class OntologyQueryFluentSimilarityTests
{
    public sealed record TestDoc : ISearchable
    {
        public string Id { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public float[] Embedding { get; init; } = [];
    }

    public sealed class TestDocOntology : DomainOntology
    {
        public override string DomainName => "test-docs";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<TestDoc>(obj =>
            {
                obj.Key(d => d.Id);
                obj.Property(d => d.Content);
            });
        }
    }

    [Test]
    public async Task OntologyQuery_ExecutesFluentSimilarityChainViaInMemoryProvider()
    {
        // Arrange — fake embedding provider returning a deterministic query vector
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.Dimensions.Returns(3);
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[] { 1f, 0f, 0f }));

        var provider = new InMemoryObjectSetProvider(embedder);

        // Seed 5 docs with distinct embeddings — closest to query [1,0,0] is doc-1.
        provider.Seed(new TestDoc { Id = "doc-1", Content = "exact match", Embedding = [1f, 0f, 0f] }, "exact match");
        provider.Seed(new TestDoc { Id = "doc-2", Content = "near match", Embedding = [0.9f, 0.1f, 0f] }, "near match");
        provider.Seed(new TestDoc { Id = "doc-3", Content = "distant", Embedding = [0f, 1f, 0f] }, "distant");
        provider.Seed(new TestDoc { Id = "doc-4", Content = "very distant", Embedding = [0f, 0f, 1f] }, "very distant");
        provider.Seed(new TestDoc { Id = "doc-5", Content = "negated", Embedding = [-1f, 0f, 0f] }, "negated");

        // Build a minimal ontology so OntologyQueryService can resolve TestDoc by name.
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new TestDocOntology());
        var ontology = graphBuilder.Build();

        var dispatcher = Substitute.For<IActionDispatcher>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        var query = new OntologyQueryService(ontology, provider, dispatcher, eventStream);

        // Act — execute the full fluent chain end-to-end
        var hits = await query
            .GetObjectSet<TestDoc>("TestDoc")
            .SimilarTo("test query")
            .WithMinRelevance(0.5)
            .Take(3)
            .ExecuteAsync(CancellationToken.None);

        // Assert — wiring is correct: top-K honored, min-relevance honored, scores descending.
        await Assert.That(hits.Items.Count).IsLessThanOrEqualTo(3);
        await Assert.That(hits.Scores.All(s => s >= 0.5)).IsTrue();
        for (var i = 1; i < hits.Scores.Count; i++)
        {
            await Assert.That(hits.Scores[i]).IsLessThanOrEqualTo(hits.Scores[i - 1]);
        }

        // The closest match (doc-1, exact embedding) must be the top hit.
        await Assert.That(hits.Items.Count).IsGreaterThan(0);
        await Assert.That(hits.Items[0].Id).IsEqualTo("doc-1");
    }
}
