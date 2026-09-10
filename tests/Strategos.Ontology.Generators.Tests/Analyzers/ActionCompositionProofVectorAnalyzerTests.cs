using Strategos.Ontology.Generators.Diagnostics;
using Strategos.Ontology.Testing;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class ActionCompositionProofVectorAnalyzerTests
{
    [Test]
    public async Task AnalyzerMatchesSharedRuntimeProofVectors()
    {
        var failures = new List<string>();
        foreach (var vector in ActionCompositionProofVectors.All)
        {
            var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
                Source(vector),
                OntologyDiagnosticIds.IllegalActionSeam);
            var actual = diagnostics.Length == 0;
            if (actual != vector.IsLegal)
            {
                failures.Add(
                    $"{vector.Name}: expected legal={vector.IsLegal}, "
                    + $"got AONT217 count={diagnostics.Length}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static string Source(ActionCompositionProofVector vector) => $$"""
        using Strategos.Ontology.Descriptors;

        public static class Consumer
        {
            public static void Compose(AuthorityLattice lattice)
            {
                var subject = new ActionSubject("orders", "Order");
                var property = new PredicatePropertyReference(
                    "count",
                    PredicateScalarKind.Integer);
                var upstream = new ActionDescriptor(subject, "upstream", "upstream")
                {
                    TouchedResources = [ActionResource.Property("count")],
                    Ensures =
                    [
                        new ActionGuarantee(ActionPredicate.Property(
                            property,
                            PredicateComparisonOperator.{{vector.UpstreamComparison}},
                            PredicateLiteral.Integer({{vector.UpstreamValue}}))),
                    ],
                };
                var downstream = new ActionDescriptor(subject, "downstream", "downstream")
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.Property(
                                property,
                                PredicateComparisonOperator.{{vector.DownstreamComparison}},
                                PredicateLiteral.Integer({{vector.DownstreamValue}})),
                            "shared vector"),
                    ],
                };
                ActionCalculus.Sequential(lattice, upstream, downstream);
            }
        }
        """;
}
