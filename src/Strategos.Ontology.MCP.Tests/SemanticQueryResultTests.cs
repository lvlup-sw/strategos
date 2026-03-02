namespace Strategos.Ontology.MCP.Tests;

public class SemanticQueryResultTests
{
    [Test]
    public async Task SemanticQueryResult_ExtendsQueryResult_HasScores()
    {
        // Arrange
        var items = new List<object> { new { Id = "doc1" } };
        var scores = new List<double> { 0.95 };

        // Act
        var result = new SemanticQueryResult("TestDocument", items)
        {
            Scores = scores,
            SemanticQuery = "find relevant documents",
            TopK = 5,
            MinRelevance = 0.7,
        };

        // Assert — SemanticQueryResult is a QueryResult
        QueryResult queryResult = result;
        await Assert.That(queryResult).IsNotNull();
        await Assert.That(result.ObjectType).IsEqualTo("TestDocument");
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Scores).HasCount().EqualTo(1);
        await Assert.That(result.Scores[0]).IsEqualTo(0.95);
        await Assert.That(result.SemanticQuery).IsEqualTo("find relevant documents");
        await Assert.That(result.TopK).IsEqualTo(5);
        await Assert.That(result.MinRelevance).IsEqualTo(0.7);
    }
}
