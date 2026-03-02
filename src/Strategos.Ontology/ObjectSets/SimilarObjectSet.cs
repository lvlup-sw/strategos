namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// A lazy query representing a similarity search over ontology-typed domain objects.
/// Returned by <see cref="ObjectSet{T}.SimilarTo"/>.
/// </summary>
/// <typeparam name="T">The domain object type.</typeparam>
public sealed class SimilarObjectSet<T> where T : class
{
    private readonly IObjectSetProvider _provider;

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
}
