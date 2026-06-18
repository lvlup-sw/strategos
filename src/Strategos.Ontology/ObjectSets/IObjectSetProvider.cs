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
    /// Executes a similarity search expression and returns scored results.
    /// </summary>
    Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct = default) where T : class;

    /// <summary>
    /// SAFE schema-bootstrap for a (possibly multi-registered) CLR type: ensures
    /// the backing schema for EVERY descriptor <typeparamref name="T"/> is
    /// registered under, in one call. Unlike a single-descriptor
    /// <c>EnsureSchema</c>, this never throws on a multi-registered type — it
    /// resolves the full registration set from the ontology graph (keyed on the
    /// resolved descriptor name, INV-8) and ensures each descriptor's schema,
    /// removing the consumer-side loop over
    /// <see cref="Query.IOntologyQuery.GetObjectTypeNames{T}"/> that the
    /// multi-registration footgun otherwise forced (#132).
    /// </summary>
    /// <typeparam name="T">The CLR type whose every registered descriptor's schema should be ensured.</typeparam>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when every descriptor's schema has been ensured.</returns>
    Task EnsureSchemaAsync<T>(CancellationToken ct = default) where T : class;

    /// <summary>
    /// Graph-wide schema bootstrap: ensures the backing schema for EVERY object
    /// descriptor registered in the ontology, in one call. The batch counterpart to
    /// the per-type entry points — a host can stand up the entire physical layout in
    /// a single statement-loop at startup rather than hand-rolling a registration
    /// walk per type (#132). A provider with no schema concept (e.g. the in-memory
    /// provider) treats this as a no-op.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when every object descriptor's schema has been ensured.</returns>
    Task EnsureAllSchemasAsync(CancellationToken ct = default);
}
