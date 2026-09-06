using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class ObjectTypeBuilderGenericTests
{
    [Test]
    public async Task Action_ReturnsGenericActionBuilder()
    {
        var builder = new ObjectTypeBuilder<TestPositionWithStatus>("Trading");

        var actionBuilder = builder.Action("ExecuteTrade");

        await Assert.That(actionBuilder).IsAssignableTo<IActionBuilder<TestPositionWithStatus>>();
    }

    [Test]
    public async Task Action_WithPreconditions_CapturedInDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestPositionWithStatus>("Trading");

        builder.Action("ExecuteTrade")
            .Description("Execute a trade")
            .Requires(p => p.Status == TestPositionStatus.Active)
            .Requires(p => p.Quantity > 0)
            .RequiresLink("Strategy");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions[0].Preconditions.Count).IsEqualTo(3);
        await Assert.That(descriptor.Actions[0].Preconditions[0].Predicate)
            .IsTypeOf<PropertyComparisonPredicate>();
        await Assert.That(descriptor.Actions[0].Preconditions[2].Predicate)
            .IsTypeOf<LinkExistsPredicate>();
    }

    [Test]
    public async Task Action_WithPostconditions_CapturedInDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestPositionWithStatus>("Trading");

        builder.Action("ExecuteTrade")
            .Modifies(p => p.Quantity)
            .Modifies(p => p.UnrealizedPnL)
            .CreatesLinked<TestTradeOrder>("Orders")
            .EmitsEvent<TestTradeExecutedEvent>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions[0].Postconditions.Count).IsEqualTo(4);
        await Assert.That(descriptor.Actions[0].Postconditions[0].Kind).IsEqualTo(PostconditionKind.ModifiesProperty);
        await Assert.That(descriptor.Actions[0].Postconditions[2].Kind).IsEqualTo(PostconditionKind.CreatesLink);
        await Assert.That(descriptor.Actions[0].Postconditions[3].Kind).IsEqualTo(PostconditionKind.EmitsEvent);
    }

    [Test]
    public async Task Property_ReturnsGenericPropertyBuilder()
    {
        var builder = new ObjectTypeBuilder<TestDerivedPosition>("Trading");

        var propertyBuilder = builder.Property(p => p.UnrealizedPnL);

        await Assert.That(propertyBuilder).IsAssignableTo<IPropertyBuilder<TestDerivedPosition>>();
    }

    [Test]
    public async Task Property_WithDerivedFrom_CapturedInDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestDerivedPosition>("Trading");

        builder.Property(p => p.UnrealizedPnL)
            .Computed()
            .DerivedFrom(p => p.Quantity, p => p.AverageCost, p => p.CurrentPrice);
        var descriptor = builder.Build();

        var pnlProp = descriptor.Properties[0];
        await Assert.That(pnlProp.IsComputed).IsTrue();
        await Assert.That(pnlProp.DerivedFrom.Count).IsEqualTo(3);
        await Assert.That(pnlProp.DerivedFrom[0].PropertyName).IsEqualTo("Quantity");
    }

    [Test]
    public async Task Property_WithDerivedFromExternal_CapturedInDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestDerivedPosition>("Trading");

        builder.Property(p => p.PortfolioWeight)
            .Computed()
            .DerivedFrom(p => p.UnrealizedPnL)
            .DerivedFromExternal("trading", "Portfolio", "TotalValue");
        var descriptor = builder.Build();

        var weightProp = descriptor.Properties[0];
        await Assert.That(weightProp.DerivedFrom.Count).IsEqualTo(2);
        await Assert.That(weightProp.DerivedFrom[1].Kind).IsEqualTo(DerivationSourceKind.External);
        await Assert.That(weightProp.DerivedFrom[1].ExternalDomain).IsEqualTo("trading");
    }

    [Test]
    public async Task FullFluentChaining_AllGenericBuilderMethods()
    {
        var builder = new ObjectTypeBuilder<TestPositionWithStatus>("Trading");

        builder.Key(p => p.Id);
        builder.Property(p => p.Symbol).Required();
        builder.Property(p => p.Quantity);
        builder.HasMany<TestTradeOrder>("Orders");
        builder.HasOne<TestStrategy>("Strategy");

        builder.Action("ExecuteTrade")
            .Description("Execute a trade")
            .Accepts<TestTradeExecutionRequest>()
            .Returns<TestTradeExecutionResult>()
            .BoundToWorkflow("execute-trade")
            .Requires(p => p.Status == TestPositionStatus.Active)
            .Requires(p => p.Quantity > 0)
            .RequiresLink("Strategy")
            .Modifies(p => p.Quantity)
            .Modifies(p => p.UnrealizedPnL)
            .CreatesLinked<TestTradeOrder>("Orders")
            .EmitsEvent<TestTradeExecutedEvent>();

        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions[0].Preconditions.Count).IsEqualTo(3);
        await Assert.That(descriptor.Actions[0].Postconditions.Count).IsEqualTo(4);
    }
}
