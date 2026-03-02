using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ScoredObjectSetResultTests
{
    [Test]
    public async Task ScoredObjectSetResult_Create_HasItemsAndScores()
    {
        // Arrange & Act
        var result = new ScoredObjectSetResult<string>(
            ["a", "b"], 2, ObjectSetInclusion.Properties, [0.9, 0.8]);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(2);
        await Assert.That(result.Scores).HasCount().EqualTo(2);
        await Assert.That(result.TotalCount).IsEqualTo(2);
        await Assert.That(result.Inclusion).IsEqualTo(ObjectSetInclusion.Properties);
    }

    [Test]
    public async Task ScoredObjectSetResult_ScoresIndexedToItems_CorrectOrder()
    {
        // Arrange & Act
        var result = new ScoredObjectSetResult<string>(
            ["first", "second"], 2, ObjectSetInclusion.Properties, [0.95, 0.75]);

        // Assert
        await Assert.That(result.Items[0]).IsEqualTo("first");
        await Assert.That(result.Scores[0]).IsEqualTo(0.95);
        await Assert.That(result.Items[1]).IsEqualTo("second");
        await Assert.That(result.Scores[1]).IsEqualTo(0.75);
    }

    [Test]
    public async Task ScoredObjectSetResult_MismatchedCounts_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new ScoredObjectSetResult<string>(
            ["a", "b"], 2, ObjectSetInclusion.Properties, [0.9]))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task ScoredObjectSetResult_EmptyResult_IsValid()
    {
        // Arrange & Act
        var result = new ScoredObjectSetResult<string>(
            [], 0, ObjectSetInclusion.Properties, []);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(0);
        await Assert.That(result.Scores).HasCount().EqualTo(0);
    }
}
