using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// Verifies the transport-fault wrap contract: providers that catch an underlying
/// transport / backend exception must rethrow as <see cref="KeywordSearchException"/>
/// with the original exception preserved as <see cref="Exception.InnerException"/>.
/// </summary>
public class KeywordSearchProviderFaultWrapTests
{
    /// <summary>
    /// Test-only provider that demonstrates the canonical wrap pattern by catching an
    /// inner <see cref="IOException"/> and rethrowing as <see cref="KeywordSearchException"/>.
    /// </summary>
    private sealed class ThrowingKeywordSearchProvider : IKeywordSearchProvider
    {
        public Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(
            KeywordSearchRequest request,
            CancellationToken ct = default)
        {
            try
            {
                throw new IOException("simulated transport failure");
            }
            catch (IOException ex)
            {
                throw new KeywordSearchException(
                    $"Keyword search backend unreachable for collection '{request.CollectionName}'.",
                    inner: ex);
            }
        }
    }

    [Test]
    public async Task SearchAsync_TransportFault_WrapsAsKeywordSearchException_InnerPreserved()
    {
        IKeywordSearchProvider provider = new ThrowingKeywordSearchProvider();

        var ex = await Assert.ThrowsAsync<KeywordSearchException>(async () =>
            await provider.SearchAsync(new KeywordSearchRequest("q", "docs", TopK: 10)));

        // The caller sees one exception type (KeywordSearchException); diagnostics may
        // walk InnerException to recover the real transport failure.
        await Assert.That(ex!.InnerException).IsNotNull();
        await Assert.That(ex.InnerException).IsTypeOf<IOException>();
        await Assert.That(ex.InnerException!.Message).IsEqualTo("simulated transport failure");
    }
}
