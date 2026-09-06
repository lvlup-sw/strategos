using System.Numerics;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class ActionPredicateTests
{
    [Test]
    public async Task All_FlattensSortsAndDeduplicatesWithStructuralIdentity()
    {
        var status = ActionPredicate.Property(
            new PredicatePropertyReference("Status", PredicateScalarKind.String),
            PredicateComparisonOperator.Equal,
            PredicateLiteral.String("active"));
        var quantity = ActionPredicate.Property(
            new PredicatePropertyReference("Quantity", PredicateScalarKind.Integer),
            PredicateComparisonOperator.GreaterThan,
            PredicateLiteral.Integer(BigInteger.Zero));

        var left = ActionPredicate.All(quantity, ActionPredicate.All(status, quantity), ActionPredicate.True);
        var right = ActionPredicate.All(status, quantity);

        await Assert.That(left).IsEqualTo(right);
        await Assert.That(left == right).IsTrue();
        await Assert.That(left.CanonicalToken).IsEqualTo(right.CanonicalToken);
        await Assert.That(((AllPredicate)left).Operands).HasCount().EqualTo(2);
        await Assert.That(left.Expression).IsEqualTo(right.Expression);
    }

    [Test]
    public async Task AnyAndNot_ApplyIdentitiesAndDoubleNegationWithoutDistribution()
    {
        var link = ActionPredicate.LinkExists("Orders");

        await Assert.That(ActionPredicate.Any(ActionPredicate.False, link)).IsEqualTo(link);
        await Assert.That(ActionPredicate.Any(ActionPredicate.True, link)).IsEqualTo(ActionPredicate.True);
        await Assert.That(ActionPredicate.All(ActionPredicate.False, link)).IsEqualTo(ActionPredicate.False);
        await Assert.That(ActionPredicate.Not(ActionPredicate.Not(link))).IsEqualTo(link);
    }

    [Test]
    public async Task AggregateAndCustomInputs_AreDefensivelySnapshotted()
    {
        var predicates = new List<ActionPredicate> { ActionPredicate.LinkExists("Orders") };
        var arguments = new List<PredicateLiteral> { PredicateLiteral.String("policy") };
        var reads = new List<ActionResource> { ActionResource.Property("Status") };

        var aggregate = ActionPredicate.All(predicates);
        var custom = (CustomPredicate)ActionPredicate.Custom("policy.check", arguments, reads);
        predicates.Add(ActionPredicate.LinkExists("Owner"));
        arguments.Add(PredicateLiteral.Boolean(true));
        reads.Add(ActionResource.Link("Owner"));

        await Assert.That(aggregate).IsEqualTo(ActionPredicate.LinkExists("Orders"));
        await Assert.That(custom.Arguments).HasCount().EqualTo(1);
        await Assert.That(custom.ReadSet).HasCount().EqualTo(1);
        await Assert.That(custom.ContainsCustom).IsTrue();
    }

    [Test]
    public async Task Relation_ExposesPathAndReferencedResources()
    {
        var relation = (RelationHoldsPredicate)ActionPredicate.RelationHolds(
            "owner",
            "Portfolio",
            "Space");

        await Assert.That(relation.RelationName).IsEqualTo("owner");
        await Assert.That(relation.LinkPath).IsEquivalentTo(["Portfolio", "Space"]);
        await Assert.That(relation.ReferencedResources.Select(resource => resource.Name))
            .IsEquivalentTo(["Portfolio", "Space", "owner"]);
        await Assert.That(relation.ContainsRelation).IsTrue();
    }

    [Test]
    public async Task PredicateDecimal_IsArbitraryPrecisionAndCanonical()
    {
        var left = PredicateDecimal.Parse("000123456789012345678901234567890.1200");
        var right = new PredicateDecimal(
            BigInteger.Parse("12345678901234567890123456789012"),
            2);

        await Assert.That(left).IsEqualTo(right);
        await Assert.That(left.ToString()).IsEqualTo("123456789012345678901234567890.12");
        await Assert.That(PredicateDecimal.Parse("0.01") < PredicateDecimal.Parse("0.1")).IsTrue();
        await Assert.That(PredicateLiteral.Decimal("+1.20"))
            .IsEqualTo(PredicateLiteral.Decimal("1.2"));
    }

    [Test]
    public async Task Literal_KeepsNullDistinctFromStringAndCarriesEnumTypeName()
    {
        var nullLiteral = PredicateLiteral.Null;
        var text = PredicateLiteral.String("null");
        var enumValue = PredicateLiteral.Enum("Trading.Status", "Active");
        var symbol = PredicateLiteral.Symbol("01hzy3f4x8");

        await Assert.That(nullLiteral).IsNotEqualTo(text);
        await Assert.That(enumValue.TypeName).IsEqualTo("Trading.Status");
        await Assert.That(enumValue.EnumMemberName).IsEqualTo("Active");
        await Assert.That(symbol.StringValue).IsEqualTo("01hzy3f4x8");
        await Assert.That(symbol).IsNotEqualTo(PredicateLiteral.String("01hzy3f4x8"));
    }

    [Test]
    public async Task Comparison_RejectsOrderingForNonNumericScalar()
    {
        await Assert.That(() => ActionPredicate.Property(
                new PredicatePropertyReference("Status", PredicateScalarKind.String),
                PredicateComparisonOperator.GreaterThan,
                PredicateLiteral.String("active")))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task PropertyReferenceAndComparison_RejectUnknownDiscriminators()
    {
        await Assert.That(() => new PredicatePropertyReference("Status", (PredicateScalarKind)999))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentOutOfRangeException));
        await Assert.That(() => ActionPredicate.Property(
                new PredicatePropertyReference("Attempts", PredicateScalarKind.Integer),
                (PredicateComparisonOperator)999,
                PredicateLiteral.Integer(BigInteger.One)))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentOutOfRangeException));
    }

    [Test]
    public async Task Custom_RejectsUnknownReadSetResourceKind()
    {
        await Assert.That(() => ActionPredicate.Custom(
                "policy.check",
                readSet: [new ActionResource((ActionResourceKind)999, "Status")]))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task Comparison_RejectsAValueFromAnotherEnumType()
    {
        await Assert.That(() => ActionPredicate.Property(
                new PredicatePropertyReference(
                    "Status",
                    PredicateScalarKind.Enum,
                    enumTypeName: "Trading.Status"),
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Enum("Workflow.Status", "Active")))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task GenericEnumLiteral_RejectsUnnamedNumericValues()
    {
        await Assert.That(() => PredicateLiteral.Enum((DayOfWeek)999))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task ActionContractWrappers_ProjectExpressionWithoutWritableLegacyFields()
    {
        var predicate = ActionPredicate.LinkExists("Orders");
        var precondition = new ActionPrecondition(predicate, "Orders required");
        var guarantee = new ActionGuarantee(predicate, "Orders now exist");

        await Assert.That(precondition.Expression).IsEqualTo("link(\"Orders\") exists");
        await Assert.That(guarantee.Expression).IsEqualTo(precondition.Expression);
        await Assert.That(typeof(ActionPrecondition).GetProperty(nameof(ActionPrecondition.Expression))!.CanWrite)
            .IsFalse();
    }

    [Test]
    public async Task ActionDescriptor_SnapshotsEveryContractCollection()
    {
        var preconditions = new List<ActionPrecondition>
        {
            new(ActionPredicate.LinkExists("Owner"), "Owner required"),
        };
        var ensures = new List<ActionGuarantee>
        {
            new(ActionPredicate.LinkExists("Orders")),
        };
        var effects = new List<ActionPostcondition>
        {
            new() { Kind = PostconditionKind.CreatesLink, LinkName = "Orders" },
        };
        var clients = new List<string> { "mcp" };
        var touched = new List<ActionResource> { ActionResource.Link("Orders") };
        var subject = new ActionSubject("Trading", "Position");

        var descriptor = new ActionDescriptor(subject, "CreateOrder", "Create an order")
        {
            Preconditions = preconditions,
            Ensures = ensures,
            Postconditions = effects,
            AllowedClients = clients,
            TouchedResources = touched,
        };
        preconditions.Clear();
        ensures.Clear();
        effects.Clear();
        clients.Clear();
        touched.Clear();

        await Assert.That(descriptor.Subject).IsEqualTo(subject);
        await Assert.That(descriptor.Preconditions).HasCount().EqualTo(1);
        await Assert.That(descriptor.Ensures).HasCount().EqualTo(1);
        await Assert.That(descriptor.Postconditions).HasCount().EqualTo(1);
        await Assert.That(descriptor.AllowedClients).HasCount().EqualTo(1);
        await Assert.That(descriptor.TouchedResources).HasCount().EqualTo(1);
    }
}
