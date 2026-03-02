namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Provider abstraction for persisting objects into an object set backend.
/// </summary>
public interface IObjectSetWriter
{
    /// <summary>
    /// Stores a single object into the backend.
    /// </summary>
    Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores a batch of objects into the backend.
    /// </summary>
    Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class;
}
