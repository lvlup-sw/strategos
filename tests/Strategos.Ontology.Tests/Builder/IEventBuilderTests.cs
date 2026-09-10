using System.Linq.Expressions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public record TestEvent(string OrderId, decimal NewPnL);
public record TestOwner(string Symbol, decimal UnrealizedPnL);

public class IEventBuilderTests
{
    [Test]
    public async Task IEventBuilder_Description_ReturnsSelf()
    {
        var substitute = Substitute.For<IEventBuilder<TestEvent>>();
        substitute.Description("desc").Returns(substitute);

        var result = substitute.Description("desc");

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IEventBuilder_MaterializesLink_AcceptsLinkNameAndExpression()
    {
        var substitute = Substitute.For<IEventBuilder<TestEvent>>();
        substitute.MaterializesLink<TestOwner>(
            Arg.Any<string>(),
            Arg.Any<Expression<Func<TestEvent, object>>>()).Returns(substitute);

        var result = substitute.MaterializesLink<TestOwner>("Orders", e => e.OrderId);

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IEventBuilder_UpdatesProperty_AcceptsPropertyAndExpression()
    {
        var substitute = Substitute.For<IEventBuilder<TestEvent>>();
        substitute.UpdatesProperty(
            Arg.Any<Expression<Func<TestOwner, object>>>(),
            Arg.Any<Expression<Func<TestEvent, object>>>()).Returns(substitute);

        var result = substitute.UpdatesProperty<TestOwner>(
            p => p.UnrealizedPnL,
            e => e.NewPnL);

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IEventBuilder_Severity_AcceptsEventSeverity()
    {
        var substitute = Substitute.For<IEventBuilder<TestEvent>>();
        substitute.Severity(EventSeverity.Warning).Returns(substitute);

        var result = substitute.Severity(EventSeverity.Warning);

        await Assert.That(result).IsEqualTo(substitute);
    }
}
