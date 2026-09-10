using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Testing;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class ActionRefinementProofVectorTests
{
    [Test]
    public async Task RuntimeMatchesSharedWorkflowRefinementVectors()
    {
        var failures = new List<string>();
        foreach (var vector in ActionRefinementProofVectors.All)
        {
            var analysis = ActionCalculus.AnalyzeRefinement(
                Action("specification", vector.Specification),
                Action("implementation", vector.Implementation),
                CreateLattice());
            var actualStatus = ToNeutralStatus(analysis.Status);
            var actualPrimaryObligation = analysis.Failures.IsEmpty
                ? RefinementObligation.None
                : ToNeutralObligation(analysis.Failures[0].Obligation);

            if (actualStatus != vector.ExpectedRuntimeStatus
                || actualPrimaryObligation != vector.ExpectedPrimaryRuntimeObligation)
            {
                failures.Add(
                    $"{vector.Name}: expected "
                    + $"{vector.ExpectedRuntimeStatus}/{vector.ExpectedPrimaryRuntimeObligation}, "
                    + $"got {actualStatus}/{actualPrimaryObligation}; "
                    + string.Join(
                        ", ",
                        analysis.Failures.Select(failure => failure.Obligation + ": " + failure.Message)));
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static ActionDescriptor Action(string name, RefinementContract contract) =>
        new(Subject(contract.Subject), name, name)
        {
            Preconditions =
            [
                new ActionPrecondition(
                    Predicate(contract.Requirement),
                    contract.Requirement.ToString()),
            ],
            Ensures = [new ActionGuarantee(Predicate(contract.Guarantee))],
            TouchedResources = Frame(contract.Frame),
            RequiredAuthority = contract.Authority switch
            {
                RefinementAuthority.None => null,
                RefinementAuthority.Reader => "reader",
                RefinementAuthority.Writer => "writer",
                _ => throw new ArgumentOutOfRangeException(nameof(contract)),
            },
        };

    private static ActionSubject Subject(RefinementSubject subject) => subject switch
    {
        RefinementSubject.Document => new ActionSubject("publication", "Document"),
        RefinementSubject.OtherDocument => new ActionSubject("publication", "OtherDocument"),
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };

    private static IReadOnlyList<ActionResource> Frame(RefinementFrame frame) => frame switch
    {
        RefinementFrame.Stage => [ActionResource.Property("Stage")],
        RefinementFrame.StageAndLedger =>
        [
            ActionResource.Property("Stage"),
            ActionResource.External("ledger"),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(frame)),
    };

    private static ActionPredicate Predicate(RefinementPredicate predicate) => predicate switch
    {
        RefinementPredicate.StageEqualsZero => Equal(0),
        RefinementPredicate.StageEqualsTwo => Equal(2),
        RefinementPredicate.StageNotEqualsZero => Compare(PredicateComparisonOperator.NotEqual, 0),
        RefinementPredicate.StageEqualsZeroOrOne => ActionPredicate.Any(Equal(0), Equal(1)),
        RefinementPredicate.StageEqualsOneOrTwo => ActionPredicate.Any(Equal(1), Equal(2)),
        RefinementPredicate.ContradictoryStage => ActionPredicate.All(Equal(1), Equal(2)),
        RefinementPredicate.CustomReady => ActionPredicate.Custom("publication.ready.v1"),
        _ => throw new ArgumentOutOfRangeException(nameof(predicate)),
    };

    private static ActionPredicate Equal(int value) =>
        Compare(PredicateComparisonOperator.Equal, value);

    private static ActionPredicate Compare(PredicateComparisonOperator comparison, int value) =>
        ActionPredicate.Property(
            new PredicatePropertyReference("Stage", PredicateScalarKind.Integer),
            comparison,
            PredicateLiteral.Integer(value));

    private static AuthorityLattice CreateLattice() => new(
        [new AuthorityAxisDescriptor("access", ["none", "read", "write"])],
        [
            new AuthorityDescriptor("reader")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty.Add("access", "read"),
            },
            new AuthorityDescriptor("writer")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty.Add("access", "write"),
            },
        ]);

    private static RefinementStatus ToNeutralStatus(ActionRefinementStatus status) => status switch
    {
        ActionRefinementStatus.Proven => RefinementStatus.Proven,
        ActionRefinementStatus.Refuted => RefinementStatus.Refuted,
        ActionRefinementStatus.Opaque => RefinementStatus.Opaque,
        ActionRefinementStatus.Invalid => RefinementStatus.Invalid,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static RefinementObligation ToNeutralObligation(
        ActionRefinementObligation obligation) => obligation switch
    {
        ActionRefinementObligation.SpecificationContract => RefinementObligation.SpecificationContract,
        ActionRefinementObligation.ImplementationContract => RefinementObligation.ImplementationContract,
        ActionRefinementObligation.Subject => RefinementObligation.Subject,
        ActionRefinementObligation.Requirements => RefinementObligation.Requirements,
        ActionRefinementObligation.Guarantees => RefinementObligation.Guarantees,
        ActionRefinementObligation.Authority => RefinementObligation.Authority,
        ActionRefinementObligation.Frame => RefinementObligation.Frame,
        _ => throw new ArgumentOutOfRangeException(nameof(obligation)),
    };
}
