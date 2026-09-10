using System.Collections.Generic;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Tests.Integration;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// DR-9 (t13): one anchored traversal of the rationale corpus, carrying both the
/// provider-agnostic <see cref="ObjectSetExpression"/> (for the in-memory oracle)
/// and the source/link coordinates the Npgsql read-back needs. The coordinates
/// are passed explicitly rather than re-derived from the expression so the
/// evaluator does not depend on the predicate's closure shape.
/// </summary>
/// <param name="Expression">The corpus traversal expression (in-memory side).</param>
/// <param name="SourceDescriptor">The anchored source descriptor name.</param>
/// <param name="SourceId">The anchored source business id.</param>
/// <param name="LinkName">The traversed link name.</param>
internal sealed record RationaleTraversal(
    ObjectSetExpression Expression,
    string SourceDescriptor,
    string SourceId,
    string LinkName);

/// <summary>
/// DR-9 (t13): the provider-agnostic seam the parity test drives so the SAME
/// corpus traversals can be evaluated by either backend (in-memory or Npgsql) and
/// their <see cref="RationaleNode"/> results compared directly.
/// </summary>
internal interface IRationaleEvaluator
{
    IReadOnlyList<RationaleNode> Evaluate(RationaleTraversal traversal);
}
