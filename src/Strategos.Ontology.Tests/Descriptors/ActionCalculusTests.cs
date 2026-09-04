using System.Collections.Immutable;

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class ActionCalculusTests
{
    [Test]
    public async Task FluentMutations_AddTheirResourcesToTheFrameByConstruction()
    {
        var builder = new ActionBuilder<FramedDocument>("publish");
        builder
            .Modifies(document => document.Status)
            .CreatesLinked<FramedDocument>("versions")
            .EmitsEvent<DocumentPublished>();

        var action = builder.Build();

        await Assert.That(action.TouchedResources).IsEquivalentTo(
        [
            ActionResource.Property("Status"),
            ActionResource.Link("versions"),
            ActionResource.Event(nameof(DocumentPublished)),
        ]);
    }

    [Test]
    public async Task Sequential_ComputesFrameUnionAndAuthorityJoin()
    {
        var lattice = CreateLattice();
        var revise = new ActionDescriptor("revise", "revise")
        {
            RequiredAuthority = "public.writer",
            TouchedResources = [ActionResource.Property("Body")],
        };
        var classify = new ActionDescriptor("classify", "classify")
        {
            RequiredAuthority = "restricted.reader",
            TouchedResources = [ActionResource.Property("Classification")],
        };

        var composite = ActionCalculus.Sequential(lattice, revise, classify);

        await Assert.That(composite.Frame.Resources).IsEquivalentTo(
        [
            ActionResource.Property("Body"),
            ActionResource.Property("Classification"),
        ]);
        await Assert.That(composite.RequiredAuthority.Coordinates["access"]).IsEqualTo("write");
        await Assert.That(composite.RequiredAuthority.Coordinates["sensitivity"])
            .IsEqualTo("restricted");
    }

    [Test]
    public async Task Frames_ExposeTheNonInterferenceAndParallelGate()
    {
        var body = new ActionFrame([ActionResource.Property("Body")]);
        var title = new ActionFrame([ActionResource.Property("Title")]);
        var bodyAndLinks = body.Union(new ActionFrame([ActionResource.Link("versions")]));

        await Assert.That(body.IsDisjointFrom(title)).IsTrue();
        await Assert.That(body.IsDisjointFrom(bodyAndLinks)).IsFalse();
        await Assert.That(bodyAndLinks.Contains(ActionResource.Link("versions"))).IsTrue();
    }

    [Test]
    public async Task RollbackPlan_IsMechanicallyReversedFromTheCompletedPrefix()
    {
        var reserve = new ActionDescriptor("reserve", "reserve")
        {
            CompensatingActionName = "release",
        };
        var charge = new ActionDescriptor("charge", "charge")
        {
            CompensatingActionName = "refund",
        };

        var plan = ActionCalculus.DeriveRollbackPlan([reserve, charge]);

        await Assert.That(plan).IsEquivalentTo(["refund", "release"]);
        await Assert.That(ActionCalculus.AuthoredRollbackAgrees(
            [reserve, charge],
            ["refund", "release"])).IsTrue();
        await Assert.That(ActionCalculus.AuthoredRollbackAgrees(
            [reserve, charge],
            ["release", "refund"])).IsFalse();
    }

    [Test]
    public async Task Build_PostconditionOutsideDeclaredFrame_FailsAont215()
    {
        var exception = BuildFailure<UnsoundFrameOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT215");
        await Assert.That(diagnostic.Message).Contains("Property:Status");
    }

    [Test]
    public async Task Build_CompensationWithDifferentFrame_FailsAont216()
    {
        var exception = BuildFailure<MismatchedCompensationOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT216");
        await Assert.That(diagnostic.Message).Contains("different frame");
    }

    private static AuthorityLattice CreateLattice() => new(
        [
            new AuthorityAxisDescriptor("access", ["read", "write"]),
            new AuthorityAxisDescriptor("sensitivity", ["public", "restricted"]),
        ],
        [
            new AuthorityDescriptor("public.writer")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty
                    .Add("access", "write")
                    .Add("sensitivity", "public"),
            },
            new AuthorityDescriptor("restricted.reader")
            {
                Coordinates = ImmutableDictionary<string, string>.Empty
                    .Add("access", "read")
                    .Add("sensitivity", "restricted"),
            },
        ]);

    private static OntologyCompositionException BuildFailure<T>()
        where T : DomainOntology, new()
    {
        try
        {
            new OntologyGraphBuilder().AddDomain<T>().Build();
        }
        catch (OntologyCompositionException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected graph construction to fail.");
    }

    private sealed class UnsoundFrameOntology : DomainOntology
    {
        public override string DomainName => "frame";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = DomainName,
                ClrType = typeof(FramedDocument),
                Source = DescriptorSource.HandAuthoredContract,
                Actions =
                [
                    new ActionDescriptor("publish", "publish")
                    {
                        Postconditions =
                        [
                            new ActionPostcondition
                            {
                                Kind = PostconditionKind.ModifiesProperty,
                                PropertyName = "Status",
                            },
                        ],
                        TouchedResources = [ActionResource.Property("Body")],
                    },
                ],
            });
        }
    }

    private sealed class MismatchedCompensationOntology : DomainOntology
    {
        public override string DomainName => "frame";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = DomainName,
                ClrType = typeof(FramedDocument),
                Source = DescriptorSource.HandAuthoredContract,
                Actions =
                [
                    new ActionDescriptor("publish", "publish")
                    {
                        TouchedResources = [ActionResource.Property("Status")],
                        CompensatingActionName = "unpublish",
                    },
                    new ActionDescriptor("unpublish", "unpublish")
                    {
                        TouchedResources = [ActionResource.Property("PublishedAt")],
                    },
                ],
            });
        }
    }

    private sealed class FramedDocument
    {
        public string Status { get; init; } = string.Empty;
    }

    private sealed record DocumentPublished;
}
