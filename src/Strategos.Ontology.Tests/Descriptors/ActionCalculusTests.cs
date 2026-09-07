using System.Collections.Immutable;

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class ActionCalculusTests
{
    private static readonly ActionSubject Subject = new("frame", "Document");

    [Test]
    public async Task FluentMutations_AddTheirResourcesToTheFrameByConstruction()
    {
        var builder = new ActionBuilder<FramedDocument>("publish", Subject);
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
        var revise = new ActionDescriptor(Subject, "revise", "revise")
        {
            RequiredAuthority = "public.writer",
            TouchedResources = [ActionResource.Property("Body")],
        };
        var classify = new ActionDescriptor(Subject, "classify", "classify")
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
    public async Task ActionDescriptor_DefensivelySnapshotsTouchedResources()
    {
        var resources = new List<ActionResource> { ActionResource.Property("Body") };
        var action = new ActionDescriptor(Subject, "revise", "revise") { TouchedResources = resources };

        resources.Add(ActionResource.Property("Classification"));

        await Assert.That(action.TouchedResources).IsEquivalentTo([ActionResource.Property("Body")]);
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

    [Test]
    public async Task Build_CompensationWithWrongGuarantee_FailsAont216()
    {
        var exception = BuildFailure<SemanticallyWrongCompensationOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT216");
        await Assert.That(diagnostic.Message).Contains("inverse guarantee");
        await Assert.That(diagnostic.Message).Contains("does not imply");
    }

    [Test]
    public async Task Build_MissingNamedCompensation_FailsAont216()
    {
        var exception = BuildFailure<MissingCompensationOntology>();

        var diagnostic = exception.Diagnostics.Single(item => item.Id == "AONT216");
        await Assert.That(diagnostic.Message).Contains("'missing' was not supplied");
    }

    [Test]
    public async Task Build_SemanticallyEquivalentCompensation_PassesInverseProof()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<SemanticallyEquivalentCompensationOntology>()
            .Build();

        await Assert.That(graph.GetObjectType("frame", "Document")!.Actions).HasCount(2);
    }

    [Test]
    public async Task Build_InvalidForwardWithNamedCompensationReportsOnlyAont221()
    {
        var exception = BuildFailure<InvalidForwardCompensationOntology>();

        await Assert.That(exception.Diagnostics.Select(item => item.Id)).Contains("AONT221");
        await Assert.That(exception.Diagnostics.Select(item => item.Id)).DoesNotContain("AONT216");
    }

    [Test]
    public async Task Build_SymbolKeyOnlyCompensation_PassesInverseProof()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<SymbolKeyOnlyCompensationOntology>()
            .Build();

        var descriptor = graph.GetObjectType("frame", "Document");
        await Assert.That(descriptor).IsNotNull();
        await Assert.That(descriptor!.ClrType).IsNull();
        await Assert.That(descriptor.SymbolKey).IsEqualTo("contract://frame/Document");
        await Assert.That(descriptor.Actions).HasCount(2);
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
                    new ActionDescriptor(Subject, "publish", "publish")
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
                    new ActionDescriptor(Subject, "publish", "publish")
                    {
                        TouchedResources = [ActionResource.Property("Status")],
                        CompensatingActionName = "unpublish",
                    },
                    new ActionDescriptor(Subject, "unpublish", "unpublish")
                    {
                        TouchedResources = [ActionResource.Property("PublishedAt")],
                    },
                ],
            });
        }
    }

    private sealed class SemanticallyWrongCompensationOntology : DomainOntology
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
                    Action(
                        "publish",
                        requires: Status(0),
                        ensures: Status(1),
                        compensatingActionName: "unpublish"),
                    Action(
                        "unpublish",
                        requires: Status(1),
                        ensures: Status(2)),
                ],
            });
        }
    }

    private sealed class MissingCompensationOntology : DomainOntology
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
                    Action(
                        "publish",
                        requires: Status(0),
                        ensures: Status(1),
                        compensatingActionName: "missing"),
                ],
            });
        }
    }

    private sealed class SemanticallyEquivalentCompensationOntology : DomainOntology
    {
        public override string DomainName => "frame";

        protected override void Define(IOntologyBuilder builder)
        {
            var one = Status(1);
            var two = Status(2);
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = DomainName,
                ClrType = typeof(FramedDocument),
                Source = DescriptorSource.HandAuthoredContract,
                Actions =
                [
                    Action(
                        "publish",
                        ensures: ActionPredicate.Any(one, two),
                        compensatingActionName: "unpublish"),
                    Action(
                        "unpublish",
                        requires: ActionPredicate.Not(ActionPredicate.All(
                            ActionPredicate.Not(one),
                            ActionPredicate.Not(two)))),
                ],
            });
        }
    }

    private sealed class InvalidForwardCompensationOntology : DomainOntology
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
                    Action(
                        "publish",
                        requires: ActionPredicate.All(Status(0), Status(1)),
                        ensures: Status(1),
                        compensatingActionName: "unpublish"),
                    Action(
                        "unpublish",
                        requires: Status(1),
                        ensures: Status(0)),
                ],
            });
        }
    }

    private sealed class SymbolKeyOnlyCompensationOntology : DomainOntology
    {
        public override string DomainName => "frame";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Document",
                DomainName = DomainName,
                ClrType = null,
                SymbolKey = "contract://frame/Document",
                Source = DescriptorSource.HandAuthoredContract,
                Actions =
                [
                    Action(
                        "publish",
                        requires: Status(0),
                        ensures: Status(1),
                        compensatingActionName: "unpublish"),
                    Action(
                        "unpublish",
                        requires: Status(1),
                        ensures: Status(0)),
                ],
            });
        }
    }

    private static ActionDescriptor Action(
        string name,
        ActionPredicate? requires = null,
        ActionPredicate? ensures = null,
        string? compensatingActionName = null) => new(Subject, name, name)
        {
            CompensatingActionName = compensatingActionName,
            Preconditions = requires is null
                ? []
                : [new ActionPrecondition(requires, requires.Expression)],
            Ensures = ensures is null
                ? []
                : [new ActionGuarantee(ensures)],
            TouchedResources = [ActionResource.Property("Status")],
        };

    private static ActionPredicate Status(int value) => ActionPredicate.Property(
        new PredicatePropertyReference(
            "Status",
            PredicateScalarKind.Integer,
            isNullable: false),
        PredicateComparisonOperator.Equal,
        PredicateLiteral.Integer(value));

    private sealed class FramedDocument
    {
        public string Status { get; init; } = string.Empty;
    }

    private sealed record DocumentPublished;
}
