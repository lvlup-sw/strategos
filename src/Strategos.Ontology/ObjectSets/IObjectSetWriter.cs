namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Provider abstraction for storing objects in an object set backend.
/// </summary>
public interface IObjectSetWriter
{
    /// <summary>
    /// Stores a single item in the backend.
    /// </summary>
    Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores a batch of items in the backend.
    /// </summary>
    Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class;
}
