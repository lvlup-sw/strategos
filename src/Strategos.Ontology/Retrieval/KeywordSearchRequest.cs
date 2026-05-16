namespace Strategos.Ontology.Retrieval;

/// <summary>
/// A request to an <see cref="IKeywordSearchProvider"/>.
/// </summary>
/// <param name="Query">The free-text query string to match against the keyword (BM25 / sparse) index.</param>
/// <param name="CollectionName">
/// The logical collection (index) to search. If the provider does not recognize this
/// collection it must throw <see cref="KeywordSearchException"/>.
/// </param>
/// <param name="TopK">
/// The maximum number of results to return. A value of <c>0</c> is valid and the provider
/// must return an empty list without invoking the backend. Negative values are not defined
/// by this contract; providers may treat them as <c>0</c> or throw.
/// </param>
/// <param name="MetadataFilters">
/// Optional metadata filters with AND semantics across all key/value pairs. A document is
/// included only if every key matches its corresponding value exactly. <c>null</c> means
/// "no filter" and is distinct from an empty dictionary, which also means "no filter" but
/// has different record-equality semantics.
/// </param>
public sealed record KeywordSearchRequest(
    string Query,
    string CollectionName,
    int TopK,
    IReadOnlyDictionary<string, string>? MetadataFilters = null);
