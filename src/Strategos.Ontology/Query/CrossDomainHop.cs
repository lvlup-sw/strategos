namespace Strategos.Ontology.Query;

public sealed record CrossDomainHop(
    string FromDomain,
    string ToDomain,
    OntologyNodeRef SourceNode,
    OntologyNodeRef TargetNode);
