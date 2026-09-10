using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class ActionRefinementTests
{
    private static readonly ActionSubject Subject = new("publication", "Document");

    [Test]
    public async Task AnalyzeRefinement_ProvesContravariantRequirementsAndCovariantGuarantees()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: NotEqual("Status", "Draft"),
            authority: "writer");
        var implementation = Action(
            "publish-with-review",
            requires: ActionPredicate.Any(Equal("Status", "Draft"), Equal("Status", "Reviewed")),
            ensures: Equal("Status", "Published"),
            authority: "reader");

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Proven);
        await Assert.That(result.IsRefinement).IsTrue();
        await Assert.That(result.Failures).IsEmpty();
    }

    [Test]
    public async Task AnalyzeRefinement_RefutesStrongerImplementationRequirementWithWitness()
    {
        var specification = Action(
            "publish",
            requires: ActionPredicate.Any(Equal("Status", "Draft"), Equal("Status", "Reviewed")),
            ensures: Equal("Status", "Published"));
        var implementation = Action(
            "publish-only-drafts",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"));

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Refuted);
        var failure = result.Failures.Single(item =>
            item.Obligation == ActionRefinementObligation.Requirements);
        await Assert.That(failure.Counterexample).IsNotEmpty();
    }

    [Test]
    public async Task AnalyzeRefinement_RefutesWeakerImplementationGuarantee()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"));
        var implementation = Action(
            "publish-or-review",
            requires: Equal("Status", "Draft"),
            ensures: ActionPredicate.Any(
                Equal("Status", "Published"),
                Equal("Status", "Reviewed")));

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Refuted);
        await Assert.That(result.Failures.Any(item =>
            item.Obligation == ActionRefinementObligation.Guarantees)).IsTrue();
    }

    [Test]
    public async Task AnalyzeRefinement_RefutesAuthorityAndFrameExpansion()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"),
            authority: "reader");
        var implementation = new ActionDescriptor(Subject, "publish-and-notify", "implementation")
        {
            Preconditions = [new ActionPrecondition(Equal("Status", "Draft"), "draft")],
            Ensures = [new ActionGuarantee(Equal("Status", "Published"))],
            TouchedResources =
            [
                ActionResource.Property("Status"),
                ActionResource.Event("PublicationNotified"),
            ],
            RequiredAuthority = "writer",
        };

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Refuted);
        await Assert.That(result.Failures.Select(item => item.Obligation)).IsEquivalentTo(
        [
            ActionRefinementObligation.Authority,
            ActionRefinementObligation.Frame,
        ]);
    }

    [Test]
    public async Task AnalyzeRefinement_DoesNotTreatMayWriteAsAGuarantee()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"));
        var implementation = new ActionDescriptor(Subject, "write-status", "implementation")
        {
            Preconditions = [new ActionPrecondition(Equal("Status", "Draft"), "draft")],
            Postconditions =
            [
                new ActionPostcondition
                {
                    Kind = PostconditionKind.ModifiesProperty,
                    PropertyName = "Status",
                },
            ],
            TouchedResources = [ActionResource.Property("Status")],
        };

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Refuted);
        await Assert.That(result.Failures.Any(item =>
            item.Obligation == ActionRefinementObligation.Guarantees)).IsTrue();
    }

    [Test]
    public async Task AnalyzeRefinement_TreatsCreatedLinkAsASoundGuarantee()
    {
        var link = ActionResource.Link("versions");
        var specification = new ActionDescriptor(Subject, "version", "specification")
        {
            Ensures = [new ActionGuarantee(ActionPredicate.LinkExists("versions"))],
            TouchedResources = [link],
        };
        var implementation = new ActionDescriptor(Subject, "create-version", "implementation")
        {
            Postconditions =
            [
                new ActionPostcondition
                {
                    Kind = PostconditionKind.CreatesLink,
                    LinkName = "versions",
                    TargetTypeName = "DocumentVersion",
                },
            ],
            TouchedResources = [link],
        };

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Proven);
    }

    [Test]
    public async Task AnalyzeRefinement_ReportsOpaqueRatherThanPassingCustomPredicate()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"));
        var implementation = new ActionDescriptor(Subject, "custom-publish", "implementation")
        {
            Preconditions =
            [
                new ActionPrecondition(
                    ActionPredicate.Custom(
                        "publication.ready",
                        readSet: [ActionResource.Property("Status")]),
                    "ready"),
            ],
            Ensures = [new ActionGuarantee(Equal("Status", "Published"))],
            TouchedResources = [ActionResource.Property("Status")],
        };

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Opaque);
        await Assert.That(result.IsRefinement).IsFalse();
    }

    [Test]
    public async Task Analysis_RejectsProvenStatusWithFailures()
    {
        var failure = new ActionRefinementFailure(
            ActionRefinementObligation.Subject,
            "The subjects differ.");

        await Assert.That(() => new ActionRefinementAnalysis(
                ActionRefinementStatus.Proven,
                [failure]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Analysis_RejectsUndefinedStatus()
    {
        await Assert.That(() => new ActionRefinementAnalysis(
                (ActionRefinementStatus)int.MaxValue))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Failure_RejectsUndefinedObligation()
    {
        await Assert.That(() => new ActionRefinementFailure(
                (ActionRefinementObligation)int.MaxValue,
                "invalid"))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Failure_RejectsNullCounterexampleEntry()
    {
        await Assert.That(() => new ActionRefinementFailure(
                ActionRefinementObligation.Requirements,
                "invalid",
                [null!]))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Analysis_RejectsNullFailureEntry()
    {
        await Assert.That(() => new ActionRefinementAnalysis(
                ActionRefinementStatus.Refuted,
                [null!]))
            .Throws<ArgumentException>();
    }

    [Test]
    [Arguments(ActionRefinementStatus.Refuted)]
    [Arguments(ActionRefinementStatus.Opaque)]
    [Arguments(ActionRefinementStatus.Invalid)]
    public async Task Analysis_RejectsNonProvenStatusWithoutFailures(
        ActionRefinementStatus status)
    {
        await Assert.That(() => new ActionRefinementAnalysis(status))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AnalyzeRefinement_RefutationTakesPrecedenceOverOpaqueObligations()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"),
            authority: "reader");
        var implementation = new ActionDescriptor(
            new ActionSubject("publication", "OtherDocument"),
            "custom-publish-and-notify",
            "implementation")
        {
            Preconditions =
            [
                new ActionPrecondition(
                    ActionPredicate.Custom(
                        "publication.ready",
                        readSet: [ActionResource.Property("Status")]),
                    "ready"),
            ],
            Ensures = [new ActionGuarantee(Equal("Status", "Published"))],
            TouchedResources =
            [
                ActionResource.Property("Status"),
                ActionResource.Event("PublicationNotified"),
            ],
            RequiredAuthority = "writer",
        };

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Refuted);
        await Assert.That(result.IsRefinement).IsFalse();
        await Assert.That(result.Failures.Select(failure => failure.Obligation)).Contains(
            ActionRefinementObligation.ImplementationContract);
        await Assert.That(result.Failures.Select(failure => failure.Obligation)).Contains(
            ActionRefinementObligation.Subject);
        await Assert.That(result.Failures.Select(failure => failure.Obligation)).Contains(
            ActionRefinementObligation.Authority);
        await Assert.That(result.Failures.Select(failure => failure.Obligation)).Contains(
            ActionRefinementObligation.Frame);
    }

    [Test]
    public async Task AnalyzeRefinement_InvalidContractTakesPrecedenceOverOpaqueObligations()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: ActionPredicate.Custom(
                "publication.result",
                readSet: [ActionResource.Property("Status")]),
            authority: "missing");
        var implementation = Action(
            "publish-implementation",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"),
            authority: "reader");

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Invalid);
        await Assert.That(result.IsRefinement).IsFalse();
        await Assert.That(result.Failures.Select(failure => failure.Obligation)).Contains(
            ActionRefinementObligation.SpecificationContract);
        await Assert.That(result.Failures.Select(failure => failure.Obligation)).Contains(
            ActionRefinementObligation.Guarantees);
    }

    [Test]
    public async Task AnalyzeRefinement_ProvesASequentialCompositeAsAnActionImplementation()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"),
            authority: "writer");
        var review = Action(
            "review",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Reviewed"),
            authority: "reader");
        var publish = Action(
            "publish-reviewed",
            requires: Equal("Status", "Reviewed"),
            ensures: Equal("Status", "Published"),
            authority: "writer");
        var lattice = CreateLattice();
        var implementation = ActionCalculus.Sequential(lattice, review, publish);

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            lattice);

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Proven);
    }

    [Test]
    public async Task AnalyzeRefinement_ReportsAnUnknownSpecificationAuthorityAsInvalid()
    {
        var specification = Action(
            "publish",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"),
            authority: "missing");
        var implementation = Action(
            "publish-implementation",
            requires: Equal("Status", "Draft"),
            ensures: Equal("Status", "Published"),
            authority: "reader");

        var result = ActionCalculus.AnalyzeRefinement(
            specification,
            implementation,
            CreateLattice());

        await Assert.That(result.Status).IsEqualTo(ActionRefinementStatus.Invalid);
        await Assert.That(result.Failures.Single().Obligation)
            .IsEqualTo(ActionRefinementObligation.SpecificationContract);
    }

    private static ActionDescriptor Action(
        string name,
        ActionPredicate requires,
        ActionPredicate ensures,
        string? authority = null) => new(Subject, name, name)
    {
        Preconditions = [new ActionPrecondition(requires, requires.Expression)],
        Ensures = [new ActionGuarantee(ensures)],
        TouchedResources = [ActionResource.Property("Status")],
        RequiredAuthority = authority,
    };

    private static ActionPredicate Equal(string property, string value) => ActionPredicate.Property(
        new PredicatePropertyReference(property, PredicateScalarKind.String, false),
        PredicateComparisonOperator.Equal,
        PredicateLiteral.String(value));

    private static ActionPredicate NotEqual(string property, string value) => ActionPredicate.Property(
        new PredicatePropertyReference(property, PredicateScalarKind.String, false),
        PredicateComparisonOperator.NotEqual,
        PredicateLiteral.String(value));

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
}
