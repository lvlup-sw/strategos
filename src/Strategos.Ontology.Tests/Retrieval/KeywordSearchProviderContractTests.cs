using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// Behavior-table conformance for <see cref="IKeywordSearchProvider"/> implementations,
/// exercised against <see cref="InMemoryKeywordSearchProvider"/>. Per design §4.2 this
/// table is the authoritative provider contract.
/// </summary>
public class KeywordSearchProviderContractTests
{
    private const string Collection = "docs";

    [Test]
    public async Task SearchAsync_TwoMatchingDocs_RankIs1Indexed_TopRankHighestScore()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = new (string, double)[]
            {
                ("doc-a", 1.0),
                ("doc-b", 3.0),
            }
        });

        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 10));

        await Assert.That(results).HasCount().EqualTo(2);
        await Assert.That(results[0].DocumentId).IsEqualTo("doc-b");
        await Assert.That(results[0].Rank).IsEqualTo(1);
        await Assert.That(results[0].Score).IsEqualTo(3.0);
        await Assert.That(results[1].DocumentId).IsEqualTo("doc-a");
        await Assert.That(results[1].Rank).IsEqualTo(2);
    }

    [Test]
    public async Task SearchAsync_EmptyMatchingDocs_ReturnsEmptyList_NeverNull()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = Array.Empty<(string, double)>(),
        });

        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 10));

        await Assert.That(results).IsNotNull();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task SearchAsync_TopKZero_ReturnsEmptyList_NoBackendInvoked()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = new (string, double)[] { ("doc-a", 5.0) },
        });

        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 0));

        await Assert.That(results).IsEmpty();
        // Provider must short-circuit before invoking the backend — assert via tracking flag.
        await Assert.That(provider.BackendInvokedCount).IsEqualTo(0);
    }

    [Test]
    public async Task SearchAsync_TopKExceedsCollectionSize_ReturnsAllMatchingRanked()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = new (string, double)[]
            {
                ("doc-a", 1.0),
                ("doc-b", 2.0),
                ("doc-c", 3.0),
            }
        });

        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 100));

        await Assert.That(results).HasCount().EqualTo(3);
        await Assert.That(results[0].Rank).IsEqualTo(1);
        await Assert.That(results[1].Rank).IsEqualTo(2);
        await Assert.That(results[2].Rank).IsEqualTo(3);
    }

    [Test]
    public async Task SearchAsync_MetadataFiltersAllMatch_ReturnsFiltered()
    {
        var docs = new (string, double)[]
        {
            ("doc-a", 1.0),
            ("doc-b", 2.0),
            ("doc-c", 3.0),
        };
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["doc-a"] = new Dictionary<string, string> { ["lang"] = "en", ["kind"] = "spec" },
            ["doc-b"] = new Dictionary<string, string> { ["lang"] = "en", ["kind"] = "spec" },
            ["doc-c"] = new Dictionary<string, string> { ["lang"] = "fr", ["kind"] = "spec" },
        };
        var provider = new InMemoryKeywordSearchProvider(
            new() { [Collection] = docs },
            metadata);

        var filter = new Dictionary<string, string> { ["lang"] = "en", ["kind"] = "spec" };
        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 10, filter));

        await Assert.That(results).HasCount().EqualTo(2);
        await Assert.That(results.Select(r => r.DocumentId)).Contains("doc-a");
        await Assert.That(results.Select(r => r.DocumentId)).Contains("doc-b");
    }

    [Test]
    public async Task SearchAsync_MetadataFiltersOneMismatch_ExcludesDoc()
    {
        var docs = new (string, double)[]
        {
            ("doc-a", 1.0),
            ("doc-b", 2.0),
        };
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["doc-a"] = new Dictionary<string, string> { ["lang"] = "en", ["kind"] = "spec" },
            ["doc-b"] = new Dictionary<string, string> { ["lang"] = "en", ["kind"] = "draft" },
        };
        var provider = new InMemoryKeywordSearchProvider(
            new() { [Collection] = docs },
            metadata);

        // doc-b mismatches kind=spec — must be excluded under AND semantics.
        var filter = new Dictionary<string, string> { ["lang"] = "en", ["kind"] = "spec" };
        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 10, filter));

        await Assert.That(results).HasCount().EqualTo(1);
        await Assert.That(results[0].DocumentId).IsEqualTo("doc-a");
    }

    [Test]
    public async Task SearchAsync_UnknownCollection_ThrowsKeywordSearchException_NamesCollection()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = new (string, double)[] { ("doc-a", 1.0) },
        });

        var ex = await Assert.ThrowsAsync<KeywordSearchException>(async () =>
            await provider.SearchAsync(new KeywordSearchRequest("q", "missing-collection", TopK: 10)));

        await Assert.That(ex!.Message).Contains("missing-collection");
    }

    [Test]
    public async Task SearchAsync_CancelledTokenAtCall_ThrowsOperationCanceledException()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = new (string, double)[] { ("doc-a", 1.0) },
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 10), cts.Token));
    }

    [Test]
    public async Task SearchAsync_TiedScores_TieBrokenByDocumentIdOrdinalAscending()
    {
        var provider = new InMemoryKeywordSearchProvider(new()
        {
            [Collection] = new (string, double)[]
            {
                ("doc-z", 1.0),
                ("doc-a", 1.0),
                ("doc-m", 1.0),
            }
        });

        var results = await provider.SearchAsync(new KeywordSearchRequest("q", Collection, TopK: 10));

        await Assert.That(results.Select(r => r.DocumentId).ToArray())
            .IsEquivalentTo(new[] { "doc-a", "doc-m", "doc-z" });
        await Assert.That(results[0].Rank).IsEqualTo(1);
        await Assert.That(results[1].Rank).IsEqualTo(2);
        await Assert.That(results[2].Rank).IsEqualTo(3);
    }
}
