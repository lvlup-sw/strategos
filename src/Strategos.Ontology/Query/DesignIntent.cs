namespace Strategos.Ontology.Query;

/// <summary>
/// Describes a proposed change against the ontology — the affected nodes, the
/// actions the agent intends to invoke, and any partially-known properties
/// that should participate in constraint evaluation.
/// </summary>
/// <param name="AffectedNodes">Nodes the design intent expects to read or modify.</param>
/// <param name="Actions">Actions the design intent proposes to invoke.</param>
/// <param name="KnownProperties">
/// Optional bag of property values already known to the caller; consumed by
/// constraint evaluation when a precondition references one of the keys.
/// </param>
public sealed record DesignIntent(
    IReadOnlyList<OntologyNodeRef> AffectedNodes,
    IReadOnlyList<ProposedAction> Actions,
    IReadOnlyDictionary<string, object?>? KnownProperties);
