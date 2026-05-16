using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

public class KeywordSearchRequestTests
{
    [Test]
    public async Task Ctor_DefaultMetadataFilters_IsNull()
    {
        var request = new KeywordSearchRequest("q", "c", 5);

        await Assert.That(request.Query).IsEqualTo("q");
        await Assert.That(request.CollectionName).IsEqualTo("c");
        await Assert.That(request.TopK).IsEqualTo(5);
        await Assert.That(request.MetadataFilters).IsNull();
    }

    [Test]
    public async Task WithExpression_NewTopK_PreservesImmutability()
    {
        var original = new KeywordSearchRequest("q", "c", 5);

        var mutated = original with { TopK = 10 };

        // Original unchanged, mutated has new TopK, other fields preserved.
        await Assert.That(original.TopK).IsEqualTo(5);
        await Assert.That(mutated.TopK).IsEqualTo(10);
        await Assert.That(mutated.Query).IsEqualTo("q");
        await Assert.That(mutated.CollectionName).IsEqualTo("c");
        await Assert.That(ReferenceEquals(original, mutated)).IsFalse();
    }
}
