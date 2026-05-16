namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Extension point for sparse / keyword (e.g. BM25 / Lucene) search.
/// </summary>
/// <remarks>
/// Strategos defines this contract and provides no default DI registration. Consumers
/// register an implementation in their composition root and the wiring slice
/// (<c>OntologyQueryTool</c>) consults it through optional constructor injection.
/// Implementations must conform to the behavior table below; the contract is exercised
/// by <c>KeywordSearchProviderContractTests</c> against the in-memory test provider.
/// <para>
/// Behavior contract:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Rank semantics.</b> Returned <see cref="KeywordSearchResult.Rank"/> is
///       1-indexed (rank 1 = highest score). Matches the BM25 / Lucene convention.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Score semantics.</b> <see cref="KeywordSearchResult.Score"/> is non-negative,
///       unbounded, and provider-specific scale. Downstream RRF fusion is rank-based, so
///       scales need not align across providers. Downstream DBSF fusion normalizes scale
///       via μ±3σ internally.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Ordering.</b> Results sorted by <see cref="KeywordSearchResult.Score"/>
///       descending; ties broken by <see cref="KeywordSearchResult.DocumentId"/> ordinal
///       ascending for stable rank assignment.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Empty results.</b> Return an empty list — never <c>null</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b><see cref="KeywordSearchRequest.TopK"/> == 0.</b> Valid. Return an empty list
///       without invoking the backend.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b><see cref="KeywordSearchRequest.TopK"/> greater than collection size.</b>
///       Return all matching documents ranked.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Metadata filters.</b> AND-semantics across all key/value pairs in
///       <see cref="KeywordSearchRequest.MetadataFilters"/>. A document is included only
///       if every key matches its value exactly. Providers may map these to backend-native
///       filters.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Collection not found.</b> Throw <see cref="KeywordSearchException"/> whose
///       message names the missing collection.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Cancellation.</b> Must honor <paramref name="ct"/> and propagate
///       <see cref="OperationCanceledException"/>. Cancellation is NOT wrapped as
///       <see cref="KeywordSearchException"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Transport / backend faults.</b> Wrap any underlying transport, parse, or
///       backend exception as the inner exception of a <see cref="KeywordSearchException"/>.
///       Callers observe exactly one exception type from this seam.
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
public interface IKeywordSearchProvider
{
    /// <summary>
    /// Executes a keyword (sparse / BM25) search against the underlying backend.
    /// </summary>
    /// <param name="request">The search request. See <see cref="KeywordSearchRequest"/>.</param>
    /// <param name="ct">
    /// Cancellation token. Implementations must observe this and propagate
    /// <see cref="OperationCanceledException"/> on cancellation.
    /// </param>
    /// <returns>
    /// An ordered, 1-indexed-ranked, never-<c>null</c> list of <see cref="KeywordSearchResult"/>.
    /// </returns>
    /// <exception cref="KeywordSearchException">
    /// The collection named in <paramref name="request"/> does not exist, or the underlying
    /// transport/backend faulted (with the original exception as <see cref="Exception.InnerException"/>).
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> was cancelled before or during execution.
    /// </exception>
    Task<IReadOnlyList<KeywordSearchResult>> SearchAsync(
        KeywordSearchRequest request,
        CancellationToken ct = default);
}
