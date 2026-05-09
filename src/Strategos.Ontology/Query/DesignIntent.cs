namespace Strategos.Ontology.Query;

public sealed record DesignIntent(
    IReadOnlyList<OntologyNodeRef> AffectedNodes,
    IReadOnlyList<ProposedAction> Actions,
    IReadOnlyDictionary<string, object?>? KnownProperties);
