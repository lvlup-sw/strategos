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

        var expression = new RootExpression(typeof(TestEntity));

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

        var expression = new SimilarityExpression(typeof(TestEntity), "machine learning neural", topK: 10);

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

        var expression = new SimilarityExpression(typeof(TestEntity), "alpha beta gamma delta", minRelevance: 0.9);

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

        var expression = new SimilarityExpression(typeof(TestEntity), "query match content", topK: 2);

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
        var expression = new SimilarityExpression(typeof(TestEntity), "some query");

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

        var expression = new SimilarityExpression(typeof(TestEntity), "machine learning deep", topK: 10);

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

        var expression = new RootExpression(typeof(TestEntity));

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
        var root = new RootExpression(typeof(TestEntity));
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

        var expression = new SimilarityExpression(typeof(TestEntity), "word1 word2", topK: 10);

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

        var entityExpression = new RootExpression(typeof(TestEntity));
        var otherExpression = new RootExpression(typeof(OtherEntity));

        // Act
        var entityResult = await provider.ExecuteAsync<TestEntity>(entityExpression);
        var otherResult = await provider.ExecuteAsync<OtherEntity>(otherExpression);

        // Assert
        await Assert.That(entityResult.Items).HasCount().EqualTo(1);
        await Assert.That(otherResult.Items).HasCount().EqualTo(1);
        await Assert.That(entityResult.Items[0].Name).IsEqualTo("Entity1");
        await Assert.That(otherResult.Items[0].Value).IsEqualTo(42);
    }
}

// Test helpers
public sealed record TestEntity(string Name);
public sealed record OtherEntity(int Value);
