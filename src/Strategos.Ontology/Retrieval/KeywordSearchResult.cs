namespace Strategos.Ontology.Retrieval;

/// <summary>
/// A single result returned from an <see cref="IKeywordSearchProvider"/>.
/// </summary>
/// <param name="DocumentId">The stable, unique identifier for the matching document.</param>
/// <param name="Score">
/// A non-negative, unbounded, provider-specific keyword-search score. Higher means a better
/// match. The scale is not aligned across providers: downstream RRF fusion is rank-based,
/// and downstream DBSF fusion normalizes the scale via μ±3σ internally.
/// </param>
/// <param name="Rank">
/// The 1-indexed rank of this result within its result list (rank 1 = highest scored).
/// Matches the BM25 / Lucene convention. Ties in <paramref name="Score"/> must be broken
/// by <paramref name="DocumentId"/> ordinal ascending so that rank assignment is stable.
/// </param>
public sealed record KeywordSearchResult
{
    public KeywordSearchResult(string DocumentId, double Score, int Rank)
    {
        if (string.IsNullOrWhiteSpace(DocumentId))
        {
            throw new ArgumentException("DocumentId must be non-empty.", nameof(DocumentId));
        }

        if (double.IsNaN(Score) || double.IsInfinity(Score))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Score), Score, "Score must be a finite number.");
        }

        if (Rank < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Rank), Rank, "Rank is 1-indexed and must be >= 1.");
        }

        this.DocumentId = DocumentId;
        this.Score = Score;
        this.Rank = Rank;
    }

    public string DocumentId { get; init; }

    public double Score { get; init; }

    public int Rank { get; init; }
}
