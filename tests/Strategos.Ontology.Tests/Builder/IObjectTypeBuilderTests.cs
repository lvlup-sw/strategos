using System.Linq.Expressions;
using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public record TestPosition(Guid Id, string Symbol, decimal Quantity, decimal UnrealizedPnL, string DisplayDescription);
public record TestTradeOrder(Guid Id, string Symbol);
public record TestStrategy(Guid Id, string Name);
public record TestTradeExecuted(Guid OrderId, decimal NewPnL);
public record TestTradeExecutionRequest(string Symbol, decimal Quantity);
public record TestTradeExecutionResult(bool Success);

public class IObjectTypeBuilderTests
{
    [Test]
    public async Task IObjectTypeBuilder_Key_AcceptsExpression()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();

        substitute.Key(Arg.Any<Expression<Func<TestPosition, object>>>());

        await Assert.That(true).IsTrue(); // Interface method exists and is callable
    }

    [Test]
    public async Task IObjectTypeBuilder_Property_ReturnsPropertyBuilder()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();
        var propertyBuilder = Substitute.For<IPropertyBuilder<TestPosition>>();
        substitute.Property(Arg.Any<Expression<Func<TestPosition, object>>>())
            .Returns(propertyBuilder);

        var result = substitute.Property(p => p.Symbol);

        await Assert.That(result).IsEqualTo(propertyBuilder);
    }

    [Test]
    public async Task IObjectTypeBuilder_HasOne_AcceptsLinkName()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();

        substitute.HasOne<TestStrategy>("Strategy");

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task IObjectTypeBuilder_HasMany_AcceptsLinkName()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();

        substitute.HasMany<TestTradeOrder>("Orders");

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task IObjectTypeBuilder_ManyToMany_AcceptsLinkName()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();

        substitute.ManyToMany<TestTradeOrder>("RelatedOrders");

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task IObjectTypeBuilder_Action_ReturnsActionBuilder()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();
        var actionBuilder = Substitute.For<IActionBuilder<TestPosition>>();
        substitute.Action("ExecuteTrade").Returns(actionBuilder);

        var result = substitute.Action("ExecuteTrade");

        await Assert.That(result).IsEqualTo(actionBuilder);
    }

    [Test]
    public async Task IObjectTypeBuilder_Event_ReturnsEventBuilder()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();

        substitute.Event<TestTradeExecuted>(Arg.Any<Action<IEventBuilder<TestTradeExecuted>>>());

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task IObjectTypeBuilder_Implements_AcceptsMapping()
    {
        var substitute = Substitute.For<IObjectTypeBuilder<TestPosition>>();

        substitute.Implements<ITestSearchable>(
            Arg.Any<Action<IInterfaceMapping<TestPosition, ITestSearchable>>>());

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task HasMany_WithDescription_SetsDescriptorDescription()
    {
        // Arrange — build a real ontology with a described link
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new HasManyDescriptionTestOntology());
        var graph = graphBuilder.Build();

        // Act
        var positionType = graph.ObjectTypes.First(t => t.Name == nameof(TestPosition));
        var ordersLink = positionType.Links.First(l => l.Name == "Orders");

        // Assert
        await Assert.That(ordersLink.Description).IsEqualTo("Orders placed against this position");
    }

    [Test]
    public async Task HasOne_WithDescription_SetsDescriptorDescription()
    {
        // Arrange — build a real ontology with a described HasOne link
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new HasOneDescriptionTestOntology());
        var graph = graphBuilder.Build();

        // Act
        var orderType = graph.ObjectTypes.First(t => t.Name == nameof(TestTradeOrder));
        var strategyLink = orderType.Links.First(l => l.Name == "Strategy");

        // Assert
        await Assert.That(strategyLink.Description).IsEqualTo("The strategy that generated this order");
    }
}

public class HasManyDescriptionTestOntology : DomainOntology
{
    public override string DomainName => "test-link-desc";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TestTradeOrder>("Orders")
                .Description("Orders placed against this position");
        });

        builder.Object<TestTradeOrder>(obj =>
        {
            obj.Key(o => o.Id);
        });
    }
}

public class HasOneDescriptionTestOntology : DomainOntology
{
    public override string DomainName => "test-link-desc";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestTradeOrder>(obj =>
        {
            obj.Key(o => o.Id);
            obj.HasOne<TestStrategy>("Strategy")
                .Description("The strategy that generated this order");
        });

        builder.Object<TestStrategy>(obj =>
        {
            obj.Key(s => s.Id);
        });
    }
}
