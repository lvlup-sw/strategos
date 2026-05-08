using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP;

public sealed record DesignIntent(
    IReadOnlyList<OntologyNodeRef> AffectedNodes,
    IReadOnlyList<ProposedAction> Actions,
    IReadOnlyDictionary<string, object?>? KnownProperties);
