using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP;

public sealed record ProposedAction(
    string ActionName,
    OntologyNodeRef Subject,
    IReadOnlyDictionary<string, object?>? Arguments);
