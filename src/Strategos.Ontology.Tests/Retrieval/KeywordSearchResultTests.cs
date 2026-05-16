using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

public class KeywordSearchResultTests
{
    [Test]
    public async Task Ctor_Constructs_AllFieldsRoundTrip()
    {
        var result = new KeywordSearchResult(DocumentId: "doc-42", Score: 7.5, Rank: 1);

        await Assert.That(result.DocumentId).IsEqualTo("doc-42");
        await Assert.That(result.Score).IsEqualTo(7.5);
        await Assert.That(result.Rank).IsEqualTo(1);
    }
}
