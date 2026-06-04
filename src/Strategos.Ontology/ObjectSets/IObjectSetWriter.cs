namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Provider abstraction for storing objects in an object set backend.
/// </summary>
/// <remarks>
/// The default <c>StoreAsync</c>/<c>StoreBatchAsync</c> overloads target the
/// descriptor resolved by convention for <typeparamref name="T"/>. When a domain
/// type is registered against multiple descriptors, use the explicit-name
/// overloads to target a specific descriptor partition.
/// </remarks>
public interface IObjectSetWriter
{
    /// <summary>
    /// Stores a single item in the backend under the conventionally-resolved
    /// descriptor for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The domain object type to store.</typeparam>
    /// <param name="item">The item to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the item has been written.</returns>
    Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores a batch of items in the backend under the conventionally-resolved
    /// descriptor for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The domain object type to store.</typeparam>
    /// <param name="items">The items to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all items have been written.</returns>
    Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores a single item in the backend under the descriptor partition
    /// identified by <paramref name="descriptorName"/>. Use this overload when
    /// <typeparamref name="T"/> is registered against multiple descriptors and
    /// the target partition must be chosen explicitly.
    /// </summary>
    /// <typeparam name="T">The domain object type to store.</typeparam>
    /// <param name="descriptorName">
    /// The descriptor name selecting which registered partition to write to.
    /// </param>
    /// <param name="item">The item to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the item has been written.</returns>
    Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores a batch of items in the backend under the descriptor partition
    /// identified by <paramref name="descriptorName"/>. Use this overload when
    /// <typeparamref name="T"/> is registered against multiple descriptors and
    /// the target partition must be chosen explicitly.
    /// </summary>
    /// <typeparam name="T">The domain object type to store.</typeparam>
    /// <param name="descriptorName">
    /// The descriptor name selecting which registered partition to write to.
    /// </param>
    /// <param name="items">The items to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all items have been written.</returns>
    Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Materializes a relation (link instance) from the stored source instance
    /// to the stored target instance under the named link.
    /// </summary>
    /// <remarks>
    /// Endpoint validation is EAGER: both <paramref name="srcId"/> and
    /// <paramref name="tgtId"/> must correspond to instances already stored for
    /// their respective descriptors, or a
    /// <see cref="RelationEndpointNotFoundException"/> is thrown and no row is
    /// written. This eager posture is the contract the future Npgsql provider
    /// mirrors via foreign-key constraints.
    /// </remarks>
    /// <param name="srcDescriptor">Descriptor name of the source endpoint.</param>
    /// <param name="srcId">Projected id of the source instance.</param>
    /// <param name="linkName">Name of the link being materialized.</param>
    /// <param name="tgtDescriptor">Descriptor name of the target endpoint.</param>
    /// <param name="tgtId">Projected id of the target instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the relation has been written.</returns>
    Task RelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default);

    /// <summary>
    /// Removes a previously-materialized relation. Removing a relation that does
    /// not exist is a no-op (no throw).
    /// </summary>
    /// <param name="srcDescriptor">Descriptor name of the source endpoint.</param>
    /// <param name="srcId">Projected id of the source instance.</param>
    /// <param name="linkName">Name of the link being removed.</param>
    /// <param name="tgtDescriptor">Descriptor name of the target endpoint.</param>
    /// <param name="tgtId">Projected id of the target instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the relation has been removed.</returns>
    Task UnrelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default);
}
