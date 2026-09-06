using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Testing;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class ActionCompositionProofVectorTests
{
    private static readonly ActionSubject Subject = new("orders", "Order");
    private static readonly AuthorityLattice EmptyLattice = new([], []);

    [Test]
    public async Task RuntimeMatchesSharedProofVectors()
    {
        var failures = new List<string>();
        foreach (var vector in ActionCompositionProofVectors.All)
        {
            var upstream = Action(
                "upstream-" + vector.Name,
                ensures: Predicate(vector.UpstreamComparison, vector.UpstreamValue),
                frame: [ActionResource.Property("count")]);
            var downstream = Action(
                "downstream-" + vector.Name,
                requires: Predicate(vector.DownstreamComparison, vector.DownstreamValue));

            var analysis = ActionCalculus.AnalyzeSequential(
                EmptyLattice,
                upstream,
                downstream);
            var actual = analysis.Status == ActionCompositionAnalysisStatus.Proven;
            if (actual != vector.IsLegal)
            {
                failures.Add($"{vector.Name}: expected legal={vector.IsLegal}, got {analysis.Status}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static ActionDescriptor Action(
        string name,
        ActionPredicate? requires = null,
        ActionPredicate? ensures = null,
        IReadOnlyList<ActionResource>? frame = null) => new(Subject, name, name)
        {
            Preconditions = requires is null
                ? []
                : [new ActionPrecondition(requires, requires.Expression)],
            Ensures = ensures is null
                ? []
                : [new ActionGuarantee(ensures)],
            TouchedResources = frame ?? [],
        };

    private static ActionPredicate Predicate(ProofComparison comparison, int value) =>
        ActionPredicate.Property(
            new PredicatePropertyReference("count", PredicateScalarKind.Integer),
            comparison switch
            {
                ProofComparison.Equal => PredicateComparisonOperator.Equal,
                ProofComparison.NotEqual => PredicateComparisonOperator.NotEqual,
                ProofComparison.LessThan => PredicateComparisonOperator.LessThan,
                ProofComparison.LessThanOrEqual => PredicateComparisonOperator.LessThanOrEqual,
                ProofComparison.GreaterThan => PredicateComparisonOperator.GreaterThan,
                ProofComparison.GreaterThanOrEqual => PredicateComparisonOperator.GreaterThanOrEqual,
                _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
            },
            PredicateLiteral.Integer(value));
}
