namespace Strategos.Ontology.Query;

public sealed record ProposedAction(
    string ActionName,
    OntologyNodeRef Subject,
    IReadOnlyDictionary<string, object?>? Arguments);
