using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Chunking;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Events;
using Strategos.Ontology.Ingestion;
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

    // ---- G1: End-to-end multi-registration regression guard (bug #31) ----
    //
    // These fixtures exercise the full fluent similarity chain against an
    // ontology where a single CLR type (SemanticDocument) is registered twice
    // under distinct descriptor names in one domain. The test asserts that
    // every read and write through the chain is dispatched against the
    // caller-supplied descriptor name — not typeof(T).Name — so items seeded
    // into one partition never leak into a query routed to the other.
    //
    // Wiring points covered:
    //   Track A — RootExpression.ObjectTypeName walked by SimilarityExpression
    //   Track B — Object<T>(name, ...) builder overload registers two descriptors
    //   Track C — graph freeze accepts leaf multi-registration (no AONT041)
    //   Track D — OntologyQueryService.GetObjectSet<T>(name) threads ot.Name
    //             into the RootExpression; GetObjectTypeNames<T>() reverse index
    //   Track E — InMemoryObjectSetProvider partitions reads by descriptor name
    //   Track F — IObjectSetWriter.StoreAsync(name, item) seeds into the
    //             chosen partition
    //
    // If any of the above regresses, this test is expected to fail with
    // bleed-through between the two partitions.

    public sealed record SemanticDocument : ISearchable
    {
        public string Id { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public float[] Embedding { get; init; } = [];
    }

    public sealed class MultiRegistrationTestDomain : DomainOntology
    {
        public override string DomainName => "multi-reg-test";

        protected override void Define(IOntologyBuilder builder)
        {
            // Two descriptor registrations of the same leaf CLR type. No links
            // are declared on either registration — multi-registered types
            // cannot participate in structural links under AONT041, and this
            // test intentionally models the Basileus happy path (a content
            // carrier type partitioned across logical collections).
            builder.Object<SemanticDocument>("trading_documents", obj =>
            {
                obj.Key(d => d.Id);
                obj.Property(d => d.Content);
            });

            builder.Object<SemanticDocument>("knowledge_documents", obj =>
            {
                obj.Key(d => d.Id);
                obj.Property(d => d.Content);
            });
        }
    }

    [Test]
    public async Task EndToEnd_MultiRegistration_FluentSimilarityChain_IsolatesByDescriptorName()
    {
        // Arrange — build an ontology with SemanticDocument registered twice
        // under distinct descriptor names in the same domain.
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new MultiRegistrationTestDomain());
        var ontology = graphBuilder.Build();

        // Use the InMemory provider with no embedding provider — scoring
        // falls back to keyword matching, and with MinRelevance(0.0) every
        // item in the queried partition passes the filter. The assertion is
        // about partition isolation, not ranking quality.
        var provider = new InMemoryObjectSetProvider();

        var dispatcher = Substitute.For<IActionDispatcher>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        var query = new OntologyQueryService(ontology, provider, dispatcher, eventStream);

        var tradingDoc1 = new SemanticDocument { Id = "t-1", Content = "market data for AAPL" };
        var tradingDoc2 = new SemanticDocument { Id = "t-2", Content = "bond yields" };
        var knowledgeDoc1 = new SemanticDocument { Id = "k-1", Content = "how authentication works" };
        var knowledgeDoc2 = new SemanticDocument { Id = "k-2", Content = "kubernetes pod lifecycle" };

        // Write path — seed each partition through the explicit-name
        // StoreAsync overload. If the writer still reached for typeof(T).Name
        // instead of descriptorName, all four items would collide on a single
        // "SemanticDocument" partition and the isolation assertions below
        // would fail with 4-item bleed-through.
        IObjectSetWriter writer = provider;
        await writer.StoreAsync("trading_documents", tradingDoc1);
        await writer.StoreAsync("trading_documents", tradingDoc2);
        await writer.StoreAsync("knowledge_documents", knowledgeDoc1);
        await writer.StoreAsync("knowledge_documents", knowledgeDoc2);

        // Act — execute the full fluent similarity chain for the trading
        // partition. The whole chain (GetObjectSet → SimilarTo →
        // WithMinRelevance → Take → ExecuteAsync) must thread the descriptor
        // name from the ObjectSet root through the SimilarityExpression into
        // the provider's partition lookup.
        var tradingResults = await query
            .GetObjectSet<SemanticDocument>("trading_documents")
            .SimilarTo("market data")
            .WithMinRelevance(0.0)
            .Take(10)
            .ExecuteAsync();

        // Assert — only items seeded under trading_documents are returned.
        var tradingContent = tradingResults.Items.Select(d => d.Content).ToList();
        await Assert.That(tradingResults.Items).Count().IsEqualTo(2);
        await Assert.That(tradingContent).Contains("market data for AAPL");
        await Assert.That(tradingContent).Contains("bond yields");
        await Assert.That(tradingContent).DoesNotContain("how authentication works");
        await Assert.That(tradingContent).DoesNotContain("kubernetes pod lifecycle");

        // Act — execute the chain for the knowledge partition with a distinct
        // query. The same service instance must dispatch to a different
        // partition based solely on the descriptor name passed to GetObjectSet.
        var knowledgeResults = await query
            .GetObjectSet<SemanticDocument>("knowledge_documents")
            .SimilarTo("auth flow")
            .WithMinRelevance(0.0)
            .Take(10)
            .ExecuteAsync();

        // Assert — only items seeded under knowledge_documents are returned.
        var knowledgeContent = knowledgeResults.Items.Select(d => d.Content).ToList();
        await Assert.That(knowledgeResults.Items).Count().IsEqualTo(2);
        await Assert.That(knowledgeContent).Contains("how authentication works");
        await Assert.That(knowledgeContent).Contains("kubernetes pod lifecycle");
        await Assert.That(knowledgeContent).DoesNotContain("market data for AAPL");
        await Assert.That(knowledgeContent).DoesNotContain("bond yields");

        // Assert — the public reverse-index API surfaces both registrations
        // in registration order (Track D3).
        var allNames = query.GetObjectTypeNames<SemanticDocument>();
        await Assert.That(allNames).Count().IsEqualTo(2);
        await Assert.That(allNames).Contains("trading_documents");
        await Assert.That(allNames).Contains("knowledge_documents");
    }

    // ---- G1 optional extras ----

    [Test]
    public async Task EndToEnd_MultiRegistration_NonSimilarityExecute_IsolatesByDescriptorName()
    {
        // Mirror of the G1 test but exercising the non-similarity materialization
        // path (ObjectSet<T>.ExecuteAsync → provider.ExecuteAsync) — confirms
        // that plain read dispatch respects the descriptor name too, not only
        // the similarity path. Guards against a regression that re-introduces
        // typeof(T).Name lookup on the Execute path while leaving similarity
        // correct.
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new MultiRegistrationTestDomain());
        var ontology = graphBuilder.Build();

        var provider = new InMemoryObjectSetProvider();
        var dispatcher = Substitute.For<IActionDispatcher>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        var query = new OntologyQueryService(ontology, provider, dispatcher, eventStream);

        IObjectSetWriter writer = provider;
        await writer.StoreAsync("trading_documents", new SemanticDocument { Id = "t-1", Content = "trade-1" });
        await writer.StoreAsync("trading_documents", new SemanticDocument { Id = "t-2", Content = "trade-2" });
        await writer.StoreAsync("knowledge_documents", new SemanticDocument { Id = "k-1", Content = "knowledge-1" });

        var tradingResult = await query
            .GetObjectSet<SemanticDocument>("trading_documents")
            .ExecuteAsync();

        var knowledgeResult = await query
            .GetObjectSet<SemanticDocument>("knowledge_documents")
            .ExecuteAsync();

        await Assert.That(tradingResult.Items).Count().IsEqualTo(2);
        await Assert.That(tradingResult.Items.Select(d => d.Id)).Contains("t-1");
        await Assert.That(tradingResult.Items.Select(d => d.Id)).Contains("t-2");
        await Assert.That(tradingResult.Items.Select(d => d.Id)).DoesNotContain("k-1");

        await Assert.That(knowledgeResult.Items).Count().IsEqualTo(1);
        await Assert.That(knowledgeResult.Items.Select(d => d.Id)).Contains("k-1");
    }

    [Test]
    public async Task EndToEnd_MultiRegistration_IngestionPipeline_WritesToCorrectPartition()
    {
        // Exercises the end-to-end ingestion pipeline with an explicit
        // descriptor name (Track F6). The pipeline must dispatch its batch
        // write through the explicit-name StoreBatchAsync overload so the
        // seeded chunks land in the named partition, not under typeof(T).Name.
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new MultiRegistrationTestDomain());
        var ontology = graphBuilder.Build();

        var provider = new InMemoryObjectSetProvider();
        var dispatcher = Substitute.For<IActionDispatcher>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        var query = new OntologyQueryService(ontology, provider, dispatcher, eventStream);

        // Deterministic fake embedder — IngestionPipeline requires an
        // embedding provider to Build(); content correctness is asserted
        // by the downstream ObjectSet read, not by embedding fidelity.
        var embedder = Substitute.For<IEmbeddingProvider>();
        embedder.Dimensions.Returns(3);
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[] { 0f, 0f, 0f }));
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var texts = ci.Arg<IReadOnlyList<string>>();
                IReadOnlyList<float[]> batch = texts.Select(_ => new float[] { 0f, 0f, 0f }).ToList();
                return Task.FromResult(batch);
            });

        var pipeline = IngestionPipeline<SemanticDocument>.Create()
            .Embed(embedder)
            .Map((chunk, emb) => new SemanticDocument
            {
                Id = Guid.NewGuid().ToString(),
                Content = chunk.Content,
                Embedding = emb,
            })
            .WriteTo(provider, "trading_documents")
            .Build();

        await pipeline.ExecuteAsync(new[] { "AAPL spot price", "bond yield curve" });

        // Both chunks should land in trading_documents only.
        var tradingResult = await query
            .GetObjectSet<SemanticDocument>("trading_documents")
            .ExecuteAsync();

        var knowledgeResult = await query
            .GetObjectSet<SemanticDocument>("knowledge_documents")
            .ExecuteAsync();

        await Assert.That(tradingResult.Items).Count().IsEqualTo(2);
        await Assert.That(tradingResult.Items.Select(d => d.Content)).Contains("AAPL spot price");
        await Assert.That(tradingResult.Items.Select(d => d.Content)).Contains("bond yield curve");
        await Assert.That(knowledgeResult.Items).Count().IsEqualTo(0);
    }
}
