using System.Collections.Generic;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Tests.Integration;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// DR-9 (t13): the in-memory ORACLE side of the cross-provider parity test. A
/// thin adapter over T12's <see cref="InMemoryExpressionEvaluator"/> exposing the
/// provider-agnostic <see cref="IRationaleEvaluator"/> seam, so the corpus's
/// traversal expressions can be replayed identically by either backend and their
/// <see cref="RationaleNode"/> results compared directly.
/// </summary>
internal sealed class InMemoryRationaleEvaluator : IRationaleEvaluator
{
    private readonly RationaleOntologyFixture _fixture;
    private readonly InMemoryExpressionEvaluator _evaluator;

    public InMemoryRationaleEvaluator(RationaleOntologyFixture fixture)
    {
        _fixture = fixture;
        _evaluator = new InMemoryExpressionEvaluator(
            fixture.Graph,
            fixture.RelationResolver,
            idProjector: null);
    }

    public IReadOnlyList<RationaleNode> Evaluate(RationaleTraversal traversal) =>
        _evaluator.Evaluate<RationaleNode>(traversal.Expression, _fixture.ResolveItems);
}
