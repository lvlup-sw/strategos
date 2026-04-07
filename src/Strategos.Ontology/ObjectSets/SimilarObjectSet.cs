namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// A lazy query representing a similarity search over ontology-typed domain objects.
/// Returned by <see cref="ObjectSet{T}.SimilarTo"/>.
/// </summary>
/// <typeparam name="T">The domain object type.</typeparam>
public sealed class SimilarObjectSet<T> where T : class
{
    private readonly IObjectSetProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimilarObjectSet{T}"/> class.
    /// </summary>
    /// <param name="expression">The similarity expression defining the query.</param>
    /// <param name="provider">The provider that executes the similarity search.</param>
    public SimilarObjectSet(SimilarityExpression expression, IObjectSetProvider provider)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(provider);
        Expression = expression;
        _provider = provider;
    }

    /// <summary>
    /// The similarity expression representing this query.
    /// </summary>
    public SimilarityExpression Expression { get; }

    /// <summary>
    /// Materializes the similarity search and returns scored results.
    /// </summary>
    public Task<ScoredObjectSetResult<T>> ExecuteAsync(CancellationToken ct = default)
    {
        return _provider.ExecuteSimilarityAsync<T>(Expression, ct);
    }

    /// <summary>
    /// Returns a new <see cref="SimilarObjectSet{T}"/> with the minimum relevance score updated.
    /// The original instance is unchanged.
    /// </summary>
    public SimilarObjectSet<T> WithMinRelevance(double minRelevance)
        => new(new SimilarityExpression(Expression, minRelevance: minRelevance), _provider);

    /// <summary>
    /// Returns a new <see cref="SimilarObjectSet{T}"/> that limits results to the top <paramref name="topK"/> matches.
    /// The original instance is unchanged.
    /// </summary>
    public SimilarObjectSet<T> Take(int topK)
        => new(new SimilarityExpression(Expression, topK: topK), _provider);

    /// <summary>
    /// Returns a new <see cref="SimilarObjectSet{T}"/> using the specified distance metric.
    /// The original instance is unchanged.
    /// </summary>
    public SimilarObjectSet<T> WithMetric(DistanceMetric metric)
        => new(new SimilarityExpression(Expression, metric: metric), _provider);
}
