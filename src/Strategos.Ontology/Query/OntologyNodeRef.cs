namespace Strategos.Ontology.Query;

/// <summary>
/// Domain-qualified reference to an ontology node. <see cref="Domain"/> and
/// <see cref="ObjectTypeName"/> together identify the object type; the
/// optional <see cref="Key"/> narrows the reference to a specific instance
/// when one is known.
/// </summary>
/// <param name="Domain">Domain that owns the object type.</param>
/// <param name="ObjectTypeName">Simple object type name within the domain.</param>
/// <param name="Key">Optional instance key.</param>
public sealed record OntologyNodeRef(string Domain, string ObjectTypeName, string? Key = null);
