using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

public class KeywordSearchExceptionTests
{
    [Test]
    public async Task Ctor_MessageAndInner_PreservesBothViaThrow()
    {
        var inner = new IOException("boom");

        try
        {
            throw new KeywordSearchException("collection missing", inner);
        }
        catch (KeywordSearchException ex)
        {
            await Assert.That(ex.Message).IsEqualTo("collection missing");
            await Assert.That(ex.InnerException).IsSameReferenceAs(inner);
        }
    }
}
