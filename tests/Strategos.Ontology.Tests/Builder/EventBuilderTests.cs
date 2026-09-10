using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class EventBuilderTests
{
    [Test]
    public async Task EventBuilder_Build_ProducesDescriptorWithEventType()
    {
        var builder = new EventBuilder<TestTradeExecuted>();

        var descriptor = builder.Build();

        await Assert.That(descriptor.EventType).IsEqualTo(typeof(TestTradeExecuted));
    }

    [Test]
    public async Task EventBuilder_Description_SetsDescription()
    {
        var builder = new EventBuilder<TestTradeExecuted>();

        builder.Description("A trade was executed");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Description).IsEqualTo("A trade was executed");
    }

    [Test]
    public async Task EventBuilder_MaterializesLink_AddsLinkToDescriptor()
    {
        var builder = new EventBuilder<TestTradeExecuted>();

        builder.MaterializesLink<TestPosition>("Orders", e => e.OrderId);
        var descriptor = builder.Build();

        await Assert.That(descriptor.MaterializedLinks.Count).IsEqualTo(1);
        await Assert.That(descriptor.MaterializedLinks[0]).IsEqualTo("Orders");
    }

    [Test]
    public async Task EventBuilder_UpdatesProperty_AddsPropertyToDescriptor()
    {
        var builder = new EventBuilder<TestTradeExecuted>();

        builder.UpdatesProperty<TestPosition>(p => p.UnrealizedPnL, e => e.NewPnL);
        var descriptor = builder.Build();

        await Assert.That(descriptor.UpdatedProperties.Count).IsEqualTo(1);
        await Assert.That(descriptor.UpdatedProperties[0]).IsEqualTo("UnrealizedPnL");
    }

    [Test]
    public async Task EventBuilder_Severity_SetsSeverity()
    {
        var builder = new EventBuilder<TestTradeExecuted>();

        builder.Severity(EventSeverity.Critical);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Severity).IsEqualTo(EventSeverity.Critical);
    }

    [Test]
    public async Task EventBuilder_MultipleMaterializesLink_AllRecorded()
    {
        var builder = new EventBuilder<TestTradeExecuted>();

        builder.MaterializesLink<TestPosition>("Orders", e => e.OrderId);
        builder.MaterializesLink<TestPosition>("Trades", e => e.OrderId);
        var descriptor = builder.Build();

        await Assert.That(descriptor.MaterializedLinks.Count).IsEqualTo(2);
        await Assert.That(descriptor.MaterializedLinks[0]).IsEqualTo("Orders");
        await Assert.That(descriptor.MaterializedLinks[1]).IsEqualTo("Trades");
    }
}
