using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology;

/// <summary>
/// Abstract base of the ontology event vocabulary emitted by
/// <see cref="IOntologySource"/> implementations.
/// </summary>
/// <remarks>
/// DR-4 (Task 8). Eight sealed-record variants cover object-type,
/// property, and link granularity. The mechanical ingester is forbidden
/// from constructing <c>Add</c>/<c>Update</c> deltas whose descriptors
/// contain <c>Actions</c>, <c>Events</c>, or <c>Lifecycle</c> (AONT205);
/// validation occurs at delta-apply time, not at delta-construction time.
/// <see cref="RenameProperty"/> is a single delta — not a Remove+Add pair
/// — to preserve identity through the matcher.
/// </remarks>
public abstract record OntologyDelta
{
    /// <summary>Identifier of the <see cref="IOntologySource"/> emitting the delta.</summary>
    public required string SourceId { get; init; }

    /// <summary>Wall-clock instant of the originating change.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Adds an <see cref="ObjectTypeDescriptor"/> to the ontology graph.</summary>
    public sealed record AddObjectType(ObjectTypeDescriptor Descriptor) : OntologyDelta;

    /// <summary>Replaces the descriptor matching <c>(DomainName, Name)</c> of <paramref name="Descriptor"/>.</summary>
    public sealed record UpdateObjectType(ObjectTypeDescriptor Descriptor) : OntologyDelta;

    /// <summary>Removes the object type identified by <paramref name="DomainName"/> + <paramref name="TypeName"/>.</summary>
    public sealed record RemoveObjectType(string DomainName, string TypeName) : OntologyDelta;

    /// <summary>Appends a property to an existing object type.</summary>
    public sealed record AddProperty(string DomainName, string TypeName, PropertyDescriptor Descriptor) : OntologyDelta;

    /// <summary>
    /// Renames a property in place, preserving identity through the
    /// rename matcher. Always single-delta — never expanded into a
    /// <see cref="RemoveProperty"/> + <see cref="AddProperty"/> pair.
    /// </summary>
    public sealed record RenameProperty(string DomainName, string TypeName, string FromName, string ToName) : OntologyDelta;

    /// <summary>Removes a property by name from the parent object type.</summary>
    public sealed record RemoveProperty(string DomainName, string TypeName, string PropertyName) : OntologyDelta;

    /// <summary>Appends a link to an existing source object type.</summary>
    public sealed record AddLink(string DomainName, string SourceTypeName, LinkDescriptor Descriptor) : OntologyDelta;

    /// <summary>Removes a link by name from its source object type.</summary>
    public sealed record RemoveLink(string DomainName, string SourceTypeName, string LinkName) : OntologyDelta;
}
