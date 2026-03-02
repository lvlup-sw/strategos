namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Result of a similarity search, pairing items with their relevance scores.
/// </summary>
/// <typeparam name="T">The element type of the result set.</typeparam>
public sealed record ScoredObjectSetResult<T>
{
    public ScoredObjectSetResult(
        IReadOnlyList<T> items,
        int totalCount,
        ObjectSetInclusion inclusion,
        IReadOnlyList<double> scores)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(scores);

        if (scores.Count != items.Count)
        {
            throw new ArgumentException(
                $"Scores count ({scores.Count}) must match Items count ({items.Count}).",
                nameof(scores));
        }

        Items = items;
        TotalCount = totalCount;
        Inclusion = inclusion;
        Scores = scores;
    }

    /// <summary>
    /// The matched items, ordered by descending relevance.
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Total number of matching items before TopK limiting.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Which data facets are included in the results.
    /// </summary>
    public ObjectSetInclusion Inclusion { get; }

    /// <summary>
    /// Relevance scores parallel to Items (Scores[i] corresponds to Items[i]).
    /// </summary>
    public IReadOnlyList<double> Scores { get; }
}
