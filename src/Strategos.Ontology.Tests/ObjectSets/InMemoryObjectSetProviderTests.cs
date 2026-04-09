using System.Linq.Expressions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class InMemoryObjectSetProviderTests
{
    [Test]
    public async Task Seed_AndExecute_ReturnsSeededItems()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Alice"), "alice document");
        provider.Seed(new TestEntity("Bob"), "bob document");

        var expression = new RootExpression(typeof(TestEntity), nameof(TestEntity));

        // Act
        var result = await provider.ExecuteAsync<TestEntity>(expression);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(2);
        await Assert.That(result.TotalCount).IsEqualTo(2);
        await Assert.That(result.Inclusion).IsEqualTo(ObjectSetInclusion.Properties);
    }

    [Test]
    public async Task ExecuteSimilarityAsync_RanksResultsByKeywordScore()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Low"), "unrelated content");
        provider.Seed(new TestEntity("High"), "machine learning deep neural network");
        provider.Seed(new TestEntity("Mid"), "machine learning basics");

        var expression = new SimilarityExpression(new RootExpression(typeof(TestEntity), nameof(TestEntity)), "machine learning neural", 10, 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<TestEntity>(expression);

        // Assert — "High" matches 2/3 terms, "Mid" matches 2/3 terms (machine, learning), "Low" matches 0/3
        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("High");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_MinRelevanceFiltering_ExcludesLowScores()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Match"), "alpha beta gamma delta");
        provider.Seed(new TestEntity("NoMatch"), "completely unrelated content");

        var expression = new SimilarityExpression(new RootExpression(typeof(TestEntity), nameof(TestEntity)), "alpha beta gamma delta", 10, 0.9);

        // Act
        var result = await provider.ExecuteSimilarityAsync<TestEntity>(expression);

        // Assert — "Match" has 4/4 = 1.0 score, "NoMatch" has 0/4 = 0.0 score
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Match");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_TopKLimiting_ReturnsOnlyTopK()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("A"), "query match content");
        provider.Seed(new TestEntity("B"), "query match content");
        provider.Seed(new TestEntity("C"), "query match content");
        provider.Seed(new TestEntity("D"), "query match content");
        provider.Seed(new TestEntity("E"), "query match content");

        var expression = new SimilarityExpression(new RootExpression(typeof(TestEntity), nameof(TestEntity)), "query match content", 2, 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<TestEntity>(expression);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(2);
    }

    [Test]
    public async Task ExecuteSimilarityAsync_EmptyCorpus_ReturnsEmpty()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        var expression = new SimilarityExpression(new RootExpression(typeof(TestEntity), nameof(TestEntity)), "some query", 10, 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<TestEntity>(expression);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(0);
        await Assert.That(result.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteSimilarityAsync_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Item"), "MACHINE LEARNING DEEP");

        var expression = new SimilarityExpression(new RootExpression(typeof(TestEntity), nameof(TestEntity)), "machine learning deep", 10, 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<TestEntity>(expression);

        // Assert — all 3/3 terms should match case-insensitively
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Scores[0]).IsEqualTo(1.0);
    }

    [Test]
    public async Task StreamAsync_ReturnsSeededItems()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("One"), "one");
        provider.Seed(new TestEntity("Two"), "two");

        var expression = new RootExpression(typeof(TestEntity), nameof(TestEntity));

        // Act
        var items = new List<TestEntity>();
        await foreach (var item in provider.StreamAsync<TestEntity>(expression))
        {
            items.Add(item);
        }

        // Assert
        await Assert.That(items).HasCount().EqualTo(2);
    }

    [Test]
    public async Task ExecuteAsync_WithFilterExpression_AppliesFilter()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Alice"), "alice");
        provider.Seed(new TestEntity("Bob"), "bob");
        provider.Seed(new TestEntity("Charlie"), "charlie");

        Expression<Func<TestEntity, bool>> predicate = e => e.Name.StartsWith("A");
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        var filter = new FilterExpression(root, predicate);

        // Act
        var result = await provider.ExecuteAsync<TestEntity>(filter);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Alice");
    }

    [Test]
    public async Task ExecuteSimilarityAsync_ScoresParallelItems_CountsMatch()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("A"), "word1 word2");
        provider.Seed(new TestEntity("B"), "word1");

        var expression = new SimilarityExpression(new RootExpression(typeof(TestEntity), nameof(TestEntity)), "word1 word2", 10, 0.0);

        // Act
        var result = await provider.ExecuteSimilarityAsync<TestEntity>(expression);

        // Assert
        await Assert.That(result.Scores.Count).IsEqualTo(result.Items.Count);
    }

    [Test]
    public async Task Seed_MultipleTypes_SeparateCollections()
    {
        // Arrange
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Entity1"), "entity");
        provider.Seed(new OtherEntity(42), "other");

        var entityExpression = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        var otherExpression = new RootExpression(typeof(OtherEntity), nameof(OtherEntity));

        // Act
        var entityResult = await provider.ExecuteAsync<TestEntity>(entityExpression);
        var otherResult = await provider.ExecuteAsync<OtherEntity>(otherExpression);

        // Assert
        await Assert.That(entityResult.Items).HasCount().EqualTo(1);
        await Assert.That(otherResult.Items).HasCount().EqualTo(1);
        await Assert.That(entityResult.Items[0].Name).IsEqualTo("Entity1");
        await Assert.That(otherResult.Items[0].Value).IsEqualTo(42);
    }

    [Test]
    public async Task InMemoryProvider_PartitionsByDescriptorName_NotClrType()
    {
        // Track E4: the provider must partition seeded items by the
        // descriptor name supplied at Seed time, NOT by typeof(T). A single
        // CLR type may be registered under multiple ontology descriptors
        // (bug #31); a query against one descriptor must not see items from
        // another even when both live in the same CLR collection.
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("TradingOnly"), "trading content", descriptorName: "trading_documents");

        // Query via a root whose descriptor name differs from where we seeded.
        var wrongPartition = new RootExpression(typeof(TestEntity), "knowledge_documents");
        var wrongResult = await provider.ExecuteAsync<TestEntity>(wrongPartition);

        // The item was seeded under "trading_documents" and must NOT be
        // visible from the "knowledge_documents" partition.
        await Assert.That(wrongResult.Items).HasCount().EqualTo(0);

        // Querying the correct partition finds it.
        var rightPartition = new RootExpression(typeof(TestEntity), "trading_documents");
        var rightResult = await provider.ExecuteAsync<TestEntity>(rightPartition);

        await Assert.That(rightResult.Items).HasCount().EqualTo(1);
        await Assert.That(rightResult.Items[0].Name).IsEqualTo("TradingOnly");
    }

    [Test]
    public async Task InMemoryProvider_DefaultSeed_UsesTypeofTName()
    {
        // Track E4 back-compat: existing Seed<T>(item, content) call sites
        // (no descriptorName) must continue to work. The default key is
        // typeof(T).Name, which matches the default root expression built
        // by ObjectSet<T> when no explicit descriptor name is supplied.
        var provider = new InMemoryObjectSetProvider();
        provider.Seed(new TestEntity("Default"), "default content");

        var expression = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        var result = await provider.ExecuteAsync<TestEntity>(expression);

        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Default");
    }
}

// Test helpers
public sealed record TestEntity(string Name);
public sealed record OtherEntity(int Value);
