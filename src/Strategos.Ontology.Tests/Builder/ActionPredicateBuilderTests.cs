using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class ActionPredicateBuilderTests
{
    private enum State
    {
        Pending,
        Active,
    }

    private enum SmallState : byte
    {
        Pending,
        Active,
    }

    private enum AliasedState
    {
        Active = 1,
        Enabled = 1,
    }

    private sealed record Order(
        Guid Id,
        State Status,
        State? OptionalStatus,
        SmallState SmallStatus,
        SmallState? OptionalSmallStatus,
        AliasedState AliasedStatus,
        decimal Quantity,
        decimal? OptionalQuantity,
        int Attempts,
        int? OptionalAttempts,
        byte SmallCount,
        System.Numerics.BigInteger Total,
        System.Numerics.BigInteger? OptionalTotal,
        double Ratio,
        string Symbol,
        string? Note,
        bool Approved);

    private sealed class FieldBackedOrder
    {
        public bool Approved = false;
    }

    [Test]
    public async Task Expressions_TranslateIntoClosedCanonicalAst()
    {
        var subject = new ActionSubject("Trading", "Order");
        var builder = new ActionBuilder<Order>("Execute", subject);

        builder.Requires(order =>
            order.Status == State.Active
            && (order.Quantity > 0m || order.Attempts < 3)
            && !order.Approved);
        builder.Ensures(order => order.Approved && order.Symbol != "");

        var descriptor = builder.Build();
        var requirement = descriptor.Preconditions.Single().Predicate;
        var guarantee = descriptor.Ensures.Single().Predicate;

        await Assert.That(requirement).IsTypeOf<AllPredicate>();
        await Assert.That(requirement.ContainsCustom).IsFalse();
        await Assert.That(requirement.Expression).Contains("Status ==");
        await Assert.That(requirement.Expression).Contains("Quantity > 0");
        await Assert.That(guarantee).IsTypeOf<AllPredicate>();
        var symbol = ((AllPredicate)guarantee).Operands
            .Cast<PropertyComparisonPredicate>()
            .Single(comparison => comparison.PropertyReference.Name == "Symbol");
        await Assert.That(symbol.PropertyReference.IsNullable).IsFalse();
    }

    [Test]
    public async Task NullableReferenceProperty_AllowsAnExplicitNullLiteral()
    {
        var builder = new ActionBuilder<Order>(
            "Annotate",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => order.Note == null);
        var comparison = (PropertyComparisonPredicate)builder.Build().Preconditions.Single().Predicate;

        await Assert.That(comparison.PropertyReference.IsNullable).IsTrue();
        await Assert.That(comparison.Value).IsEqualTo(PredicateLiteral.Null);
    }

    [Test]
    public async Task ReversedNumericComparison_NormalizesPropertyToLeft()
    {
        var builder = new ActionBuilder<Order>(
            "Retry",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => 3 > order.Attempts);
        var comparison = (PropertyComparisonPredicate)builder.Build().Preconditions.Single().Predicate;

        await Assert.That(comparison.PropertyReference.Name).IsEqualTo("Attempts");
        await Assert.That(comparison.Operator).IsEqualTo(PredicateComparisonOperator.LessThan);
        await Assert.That(comparison.Value.IntegerValue).IsEqualTo(new System.Numerics.BigInteger(3));
    }

    [Test]
    public async Task EnumExpression_UsesStableOntologyTypeName()
    {
        var builder = new ActionBuilder<Order>(
            "Authorize",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => order.Status == State.Active);
        var enumComparison = (PropertyComparisonPredicate)builder.Build().Preconditions.Single().Predicate;

        await Assert.That(enumComparison.PropertyReference.EnumTypeName).IsEqualTo(nameof(State));
        await Assert.That(enumComparison.Value.TypeName).IsEqualTo(nameof(State));
    }

    [Test]
    public async Task BigIntegerProperty_UsesTheArbitraryPrecisionIntegerDomain()
    {
        var builder = new ActionBuilder<Order>(
            "Count",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => order.Total > 1);
        var comparison = (PropertyComparisonPredicate)builder.Build().Preconditions.Single().Predicate;

        await Assert.That(comparison.PropertyReference.ScalarKind).IsEqualTo(PredicateScalarKind.Integer);
        await Assert.That(comparison.Value.IntegerValue).IsEqualTo(System.Numerics.BigInteger.One);
    }

    [Test]
    public async Task CompilerRequiredRepresentationPreservingConversions_AreAccepted()
    {
        var builder = new ActionBuilder<Order>(
            "AcceptCompilerConversions",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => order.SmallCount < 10L);
        builder.Requires(order => order.Attempts < 10L);
        builder.Requires(order => order.OptionalAttempts == 1);
        builder.Requires(order => order.OptionalQuantity > 1);
        builder.Requires(order => order.OptionalTotal > 1);
        builder.Requires(order => order.OptionalStatus == State.Active);
        builder.Requires(order => order.SmallStatus == SmallState.Active);
        builder.Requires(order => order.OptionalSmallStatus == SmallState.Active);

        var checkedBuilder = new ActionBuilder<Order>(
            "AcceptCheckedWidening",
            new ActionSubject("Trading", "Order"));
        checkedBuilder.Requires(order => checked((long)order.Attempts) < 10L);

        var comparisons = builder.Build().Preconditions
            .Select(precondition => (PropertyComparisonPredicate)precondition.Predicate)
            .ToDictionary(comparison => comparison.PropertyReference.Name, StringComparer.Ordinal);

        await Assert.That(comparisons).HasCount().EqualTo(8);
        await Assert.That(comparisons[nameof(Order.SmallCount)].Value.IntegerValue)
            .IsEqualTo(new System.Numerics.BigInteger(10));
        await Assert.That(comparisons[nameof(Order.Attempts)].Value.IntegerValue)
            .IsEqualTo(new System.Numerics.BigInteger(10));
        await Assert.That(comparisons[nameof(Order.OptionalAttempts)].PropertyReference.IsNullable).IsTrue();
        await Assert.That(comparisons[nameof(Order.OptionalQuantity)].Value.DecimalValue)
            .IsEqualTo(PredicateDecimal.FromDecimal(1m));
        await Assert.That(comparisons[nameof(Order.OptionalTotal)].Value.IntegerValue)
            .IsEqualTo(System.Numerics.BigInteger.One);
        await Assert.That(comparisons[nameof(Order.OptionalStatus)].Value)
            .IsEqualTo(PredicateLiteral.Enum(nameof(State), nameof(State.Active)));
        await Assert.That(comparisons[nameof(Order.SmallStatus)].Value)
            .IsEqualTo(PredicateLiteral.Enum(nameof(SmallState), nameof(SmallState.Active)));
        await Assert.That(comparisons[nameof(Order.OptionalSmallStatus)].Value)
            .IsEqualTo(PredicateLiteral.Enum(nameof(SmallState), nameof(SmallState.Active)));
        await Assert.That(((PropertyComparisonPredicate)checkedBuilder.Build().Preconditions.Single().Predicate).Value.IntegerValue)
            .IsEqualTo(new System.Numerics.BigInteger(10));
    }

    [Test]
    public async Task CapturedValues_AreRejectedInsteadOfBecomingOpaque()
    {
        var minimum = 3;
        var builder = new ActionBuilder<Order>(
            "Retry",
            new ActionSubject("Trading", "Order"));

        await Assert.That(() => builder.Requires(order => order.Attempts > minimum))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task PropertyToPropertyMethodsArithmeticFloatingPointAndFields_AreRejected()
    {
        var subject = new ActionSubject("Trading", "Order");
        var builder = new ActionBuilder<Order>("Reject", subject);
        var fieldBuilder = new ActionBuilder<FieldBackedOrder>("RejectField", subject);

        await Assert.That(() => builder.Requires(order => order.Attempts > order.Attempts))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => order.Symbol.StartsWith("A", StringComparison.Ordinal)))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => order.Attempts + 1 > 3))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => order.Ratio > 0.0))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => fieldBuilder.Requires(order => order.Approved))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task SemanticChangingExpressionConversions_AreRejected()
    {
        var builder = new ActionBuilder<Order>(
            "RejectConversion",
            new ActionSubject("Trading", "Order"));

        await Assert.That(() => builder.Requires(order => (byte)order.Attempts == 0))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => checked((byte)order.Attempts) == 0))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => (uint)order.Attempts == 0U))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => (System.Numerics.BigInteger)order.Quantity > 1))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => order.Attempts == (int)(object)0))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task ExplicitEnumUnderlyingEquality_CanonicalizesToTheUniqueNamedMember()
    {
        var builder = new ActionBuilder<Order>(
            "CanonicalEnumCast",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => (int)order.Status == 1);
        var comparison = (PropertyComparisonPredicate)builder.Build().Preconditions.Single().Predicate;

        await Assert.That(comparison.Value)
            .IsEqualTo(PredicateLiteral.Enum(nameof(State), nameof(State.Active)));
    }

    [Test]
    public async Task DefinedNumericToEnumCast_CanonicalizesToTheUniqueNamedMember()
    {
        var builder = new ActionBuilder<Order>(
            "CanonicalDefinedEnumCast",
            new ActionSubject("Trading", "Order"));

        builder.Requires(order => order.Status == (State)1);
        var comparison = (PropertyComparisonPredicate)builder.Build().Preconditions.Single().Predicate;

        await Assert.That(comparison.Value)
            .IsEqualTo(PredicateLiteral.Enum(nameof(State), nameof(State.Active)));
        await Assert.That(() => builder.Requires(order => order.Status == (State)99))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task AliasedEnumValues_AreRejectedAsSemanticallyAmbiguous()
    {
        await Assert.That(() => PredicateLiteral.Enum(AliasedState.Enabled))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));

        var builder = new ActionBuilder<Order>(
            "RejectAlias",
            new ActionSubject("Trading", "Order"));
        await Assert.That(() => builder.Requires(order => order.AliasedStatus == AliasedState.Enabled))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
        await Assert.That(() => builder.Requires(order => order.AliasedStatus == (AliasedState)1))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
    }

    [Test]
    public async Task NullableNumericOrderingAgainstNull_IsRejected()
    {
        var builder = new ActionBuilder<Order>(
            "RejectNullOrdering",
            new ActionSubject("Trading", "Order"));

#pragma warning disable CS0464 // Comparing a lifted numeric value with null is legal C# but always false.
        await Assert.That(() => builder.Requires(order => order.OptionalAttempts < null))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));
#pragma warning restore CS0464
    }

    [Test]
    public async Task LambdaTrue_IsRejectedInFavorOfExplicitTrue()
    {
        var builder = new ActionBuilder<Order>(
            "Inspect",
            new ActionSubject("Trading", "Order"));

        await Assert.That(() => builder.Requires(_ => true))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentException));

        builder.Requires(ActionPredicate.True, "Explicit wildcard");
        await Assert.That(builder.Build().Preconditions.Single().Predicate)
            .IsEqualTo(ActionPredicate.True);
    }

    [Test]
    public async Task TypedAndSugarOverloads_CreateRequirementsAndGuarantees()
    {
        var builder = new ActionBuilder<Order>(
            "Share",
            new ActionSubject("Trading", "Order"));

        builder
            .Requires(ActionPredicate.LinkExists("Portfolio"))
            .RequiresLinkSoft("Audit")
            .RequiresRelation("owner", "Portfolio")
            .EnsuresLink("Receipt")
            .EnsuresRelation("viewer");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions).HasCount().EqualTo(3);
        await Assert.That(descriptor.Preconditions[1].Strength).IsEqualTo(ConstraintStrength.Soft);
        await Assert.That(descriptor.Preconditions[2].Predicate).IsTypeOf<RelationHoldsPredicate>();
        await Assert.That(descriptor.Ensures).HasCount().EqualTo(2);
    }

    [Test]
    public async Task ObjectBuilder_StampsExplicitSubjectOnActionsAndInterfaceDefaults()
    {
        var builder = new ObjectTypeBuilder<Order>("Trading", "orders");
        builder.Action("Execute");
        builder.Implements<ISearchableAction>(mapping =>
            mapping.ActionDefault("Search", action => action.Ensures(ActionPredicate.True)));

        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions).HasCount().EqualTo(2);
        await Assert.That(descriptor.Actions.All(action =>
            action.Subject == new ActionSubject("Trading", "orders"))).IsTrue();
    }

    private interface ISearchableAction;
}
