namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Result of a similarity search, including per-item relevance scores.
/// </summary>
/// <typeparam name="T">The element type of the result set.</typeparam>
public sealed record ScoredObjectSetResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    ObjectSetInclusion Inclusion,
    IReadOnlyList<double> Scores);
