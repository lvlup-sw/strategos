using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.Tests.Retrieval;

/// <summary>
/// Deterministic in-process <see cref="IKeywordSearchProvider"/> used by Strategos tests
/// (and by PR-C wiring tests once <c>OntologyQueryTool</c> learns the hybrid path).
/// </summary>
/// <remarks>
/// Backed by a per-collection list of <c>(documentId, score)</c> tuples and an optional
/// per-document synthetic metadata dictionary used to evaluate
/// <see cref="KeywordSearchRequest.MetadataFilters"/> under AND-semantics. Implements every
/// behavior required by the <see cref="IKeywordSearchProvider"/> contract.
/// </remarks>
internal sealed class InMemoryKeywordSearchProvider : IKeywordSearchProvider
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<(string DocId, double Score)>> _collections;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _metadata;
    private int _backendInvokedCount;

    /// <summary>The number of times the backend "search" path was actually executed.</summary>
    /// <remarks>Used by tests to assert TopK==0 short-circuit semantics.</remarks>
    public int BackendInvokedCount => _backendInvokedCount;

    public InMemoryKeywordSearchProvider(
        Dictionary<string, IReadOnlyList<(string DocId, double Score)>> collections,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? metadata = null)
    {
        _collections = collections;
        _metadata = metadata ?? new Dictionary<string, IReadOnlyDictionary<string, string>>();
    }

    public Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(
        KeywordSearchRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // TopK==0: short-circuit before touching the backend.
        if (request.TopK <= 0)
        {
            return Task.FromResult<IReadOnlyList<KeywordSearchResult>>(Array.Empty<KeywordSearchResult>());
        }

        if (!_collections.TryGetValue(request.CollectionName, out var docs))
        {
            throw new KeywordSearchException(
                $"Collection '{request.CollectionName}' does not exist.");
        }

        Interlocked.Increment(ref _backendInvokedCount);

        // AND-semantics filter: keep only docs whose synthetic metadata matches every k/v pair.
        IEnumerable<(string DocId, double Score)> filtered = docs;
        if (request.MetadataFilters is { Count: > 0 } filters)
        {
            filtered = docs.Where(d => MatchesAllFilters(d.DocId, filters));
        }

        // Sort by Score desc, tie-break by DocumentId ordinal asc for stable 1-indexed rank.
        var sorted = filtered
            .OrderByDescending(d => d.Score)
            .ThenBy(d => d.DocId, StringComparer.Ordinal)
            .Take(request.TopK)
            .Select((d, index) => new KeywordSearchResult(d.DocId, d.Score, Rank: index + 1))
            .ToList();

        return Task.FromResult<IReadOnlyList<KeywordSearchResult>>(sorted);
    }

    private bool MatchesAllFilters(string docId, IReadOnlyDictionary<string, string> filters)
    {
        if (!_metadata.TryGetValue(docId, out var docMeta))
        {
            return false;
        }

        foreach (var (key, expected) in filters)
        {
            if (!docMeta.TryGetValue(key, out var actual) || !string.Equals(actual, expected, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
