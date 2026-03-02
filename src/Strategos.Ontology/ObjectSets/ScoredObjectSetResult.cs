namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Result of materializing a similarity search query, with relevance scores.
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
        if (scores.Count != items.Count)
        {
            throw new ArgumentException(
                $"Scores count ({scores.Count}) must match Items count ({items.Count}).");
        }

        Items = items;
        TotalCount = totalCount;
        Inclusion = inclusion;
        Scores = scores;
    }

    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public ObjectSetInclusion Inclusion { get; }
    public IReadOnlyList<double> Scores { get; }
}
