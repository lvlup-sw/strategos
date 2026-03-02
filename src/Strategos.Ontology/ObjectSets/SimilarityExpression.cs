namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Expression representing a similarity (vector/semantic) search query.
/// </summary>
public sealed class SimilarityExpression
{
    public SimilarityExpression(
        Type objectType,
        string queryText,
        int topK = 5,
        double minRelevance = 0.0,
        DistanceMetric metric = DistanceMetric.Cosine)
    {
        ArgumentNullException.ThrowIfNull(objectType);
        ArgumentNullException.ThrowIfNull(queryText);

        ObjectType = objectType;
        QueryText = queryText;
        TopK = topK;
        MinRelevance = minRelevance;
        Metric = metric;
    }

    /// <summary>
    /// The CLR type to search over.
    /// </summary>
    public Type ObjectType { get; }

    /// <summary>
    /// The natural-language query text for similarity matching.
    /// </summary>
    public string QueryText { get; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int TopK { get; }

    /// <summary>
    /// Minimum relevance score threshold (0.0 to 1.0).
    /// </summary>
    public double MinRelevance { get; }

    /// <summary>
    /// The distance metric to use for scoring.
    /// </summary>
    public DistanceMetric Metric { get; }
}
