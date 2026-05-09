namespace Strategos.Ontology.Query;

/// <summary>
/// Records a traversal step that crosses a domain boundary while walking the
/// ontology graph (for example, while computing blast radius).
/// </summary>
/// <param name="FromDomain">Domain the traversal departs from.</param>
/// <param name="ToDomain">Domain the traversal arrives in.</param>
/// <param name="SourceNode">Node on the originating side of the cross-domain link.</param>
/// <param name="TargetNode">Node on the receiving side of the cross-domain link.</param>
public sealed record CrossDomainHop(
    string FromDomain,
    string ToDomain,
    OntologyNodeRef SourceNode,
    OntologyNodeRef TargetNode);
