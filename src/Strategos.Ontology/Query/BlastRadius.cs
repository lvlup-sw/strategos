namespace Strategos.Ontology.Query;

public sealed record BlastRadius(
    IReadOnlyList<OntologyNodeRef> DirectlyAffected,
    IReadOnlyList<OntologyNodeRef> TransitivelyAffected,
    IReadOnlyList<CrossDomainHop> CrossDomainHops,
    BlastRadiusScope Scope);
