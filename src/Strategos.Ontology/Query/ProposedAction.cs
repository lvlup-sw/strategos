namespace Strategos.Ontology.Query;

/// <summary>
/// An action a <see cref="DesignIntent"/> proposes to invoke against a target
/// ontology node.
/// </summary>
/// <param name="ActionName">Name of the action to invoke.</param>
/// <param name="Subject">Target node the action will be invoked against.</param>
/// <param name="Arguments">
/// Optional bag of action arguments expressed as a property dictionary; used
/// by constraint evaluation when a precondition references an argument.
/// </param>
public sealed record ProposedAction(
    string ActionName,
    OntologyNodeRef Subject,
    IReadOnlyDictionary<string, object?>? Arguments);
