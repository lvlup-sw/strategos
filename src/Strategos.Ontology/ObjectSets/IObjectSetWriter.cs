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
    /// Materializes an ATTRIBUTED relation (DR-4): a relation backed by a
    /// reified association object that carries its own key and edge attributes.
    /// Stores <paramref name="association"/> under
    /// <paramref name="associationDescriptor"/>, projects its id via the DR-1
    /// identity projector, and writes a relation row whose
    /// <see cref="RelationRow.AssociationObjectId"/> equals that id.
    /// </summary>
    /// <remarks>
    /// Endpoint validation is EAGER, identical to the plain
    /// <see cref="RelateAsync(string, string, string, string, string, CancellationToken)"/>:
    /// both <paramref name="srcId"/> and <paramref name="tgtId"/> must correspond
    /// to stored instances or a <see cref="RelationEndpointNotFoundException"/>
    /// is thrown and neither the association nor a row is written. The self-loop
    /// policy applies as well.
    /// </remarks>
    /// <typeparam name="TRel">CLR type backing the association object.</typeparam>
    /// <param name="srcDescriptor">Descriptor name of the source endpoint.</param>
    /// <param name="srcId">Projected id of the source instance.</param>
    /// <param name="linkName">Name of the link being materialized.</param>
    /// <param name="tgtDescriptor">Descriptor name of the target endpoint.</param>
    /// <param name="tgtId">Projected id of the target instance.</param>
    /// <param name="associationDescriptor">Descriptor name of the association object type.</param>
    /// <param name="association">The association instance to store and reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the association and row are written.</returns>
    Task RelateAsync<TRel>(
        string srcDescriptor,
        string srcId,
        string linkName,
        string tgtDescriptor,
        string tgtId,
        string associationDescriptor,
        TRel association,
        CancellationToken ct = default)
        where TRel : class;

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
    /// <remarks>
    /// Removal is symmetric with the plain
    /// <see cref="RelateAsync(string, string, string, string, string, CancellationToken)"/>
    /// write key: this overload removes ONLY the plain (unattributed) row for the
    /// endpoint pair. An attributed row backed by an association object is removed
    /// via the
    /// <see cref="UnrelateAsync(string, string, string, string, string, string, string, CancellationToken)"/>
    /// overload, which also deletes the orphaned association object.
    /// </remarks>
    Task UnrelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default);

    /// <summary>
    /// Removes a previously-materialized ATTRIBUTED relation (DR-4): the single
    /// relation row whose <see cref="RelationRow.AssociationObjectId"/> equals
    /// <paramref name="associationId"/>, and the now-orphaned association object
    /// stored under <paramref name="associationDescriptor"/>. After removal the
    /// association object is no longer queryable or traversable. Removing a
    /// relation that does not exist is a no-op (no throw).
    /// </summary>
    /// <remarks>
    /// This is the symmetric counterpart to the attributed
    /// <see cref="RelateAsync{TRel}(string, string, string, string, string, string, TRel, CancellationToken)"/>:
    /// the relate-store write key is
    /// (TargetDescriptor, TargetId, AssociationObjectId), so removal targets that
    /// same triple. Plain rows and sibling attributed rows (different association
    /// ids) for the same endpoint pair are left intact.
    /// </remarks>
    /// <param name="srcDescriptor">Descriptor name of the source endpoint.</param>
    /// <param name="srcId">Projected id of the source instance.</param>
    /// <param name="linkName">Name of the link being removed.</param>
    /// <param name="tgtDescriptor">Descriptor name of the target endpoint.</param>
    /// <param name="tgtId">Projected id of the target instance.</param>
    /// <param name="associationDescriptor">Descriptor name of the association object type.</param>
    /// <param name="associationId">Projected id of the association object backing the row.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the relation and association object have been removed.</returns>
    Task UnrelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, string associationDescriptor, string associationId, CancellationToken ct = default);
}
