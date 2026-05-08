namespace Strategos.Ontology.Query;

public sealed record OntologyNodeRef(string Domain, string ObjectTypeName, string? Key = null);
