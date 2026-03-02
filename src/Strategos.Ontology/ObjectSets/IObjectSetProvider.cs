namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Provider abstraction for executing object set queries against a backend.
/// </summary>
public interface IObjectSetProvider
{
    /// <summary>
    /// Executes an object set expression and returns a materialized result.
    /// </summary>
    Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Streams results of an object set expression as an async enumerable.
    /// </summary>
    IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Executes a similarity search and returns scored results.
    /// </summary>
    Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(SimilarityExpression expression, CancellationToken ct = default) where T : class;
}
