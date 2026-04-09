using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class ObjectTypeBuilderTests
{
    [Test]
    public async Task ObjectTypeBuilder_Key_SetsKeyProperty()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.Key(p => p.Id);
        var descriptor = builder.Build();

        await Assert.That(descriptor.KeyProperty).IsNotNull();
        await Assert.That(descriptor.KeyProperty!.Name).IsEqualTo("Id");
    }

    [Test]
    public async Task ObjectTypeBuilder_Property_AddsPropertyDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.Property(p => p.Symbol).Required();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Properties.Count).IsEqualTo(1);
        await Assert.That(descriptor.Properties[0].Name).IsEqualTo("Symbol");
        await Assert.That(descriptor.Properties[0].IsRequired).IsTrue();
    }

    [Test]
    public async Task ObjectTypeBuilder_HasOne_AddsLinkWithOneToOneCardinality()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.HasOne<TestStrategy>("Strategy");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Links.Count).IsEqualTo(1);
        await Assert.That(descriptor.Links[0].Name).IsEqualTo("Strategy");
        await Assert.That(descriptor.Links[0].Cardinality).IsEqualTo(LinkCardinality.OneToOne);
        await Assert.That(descriptor.Links[0].TargetTypeName).IsEqualTo("TestStrategy");
    }

    [Test]
    public async Task ObjectTypeBuilder_HasMany_AddsLinkWithOneToManyCardinality()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.HasMany<TestTradeOrder>("Orders");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Links.Count).IsEqualTo(1);
        await Assert.That(descriptor.Links[0].Name).IsEqualTo("Orders");
        await Assert.That(descriptor.Links[0].Cardinality).IsEqualTo(LinkCardinality.OneToMany);
    }

    [Test]
    public async Task ObjectTypeBuilder_ManyToMany_AddsLinkWithManyToManyCardinality()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.ManyToMany<TestTradeOrder>("RelatedOrders");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Links.Count).IsEqualTo(1);
        await Assert.That(descriptor.Links[0].Name).IsEqualTo("RelatedOrders");
        await Assert.That(descriptor.Links[0].Cardinality).IsEqualTo(LinkCardinality.ManyToMany);
    }

    [Test]
    public async Task ObjectTypeBuilder_ManyToManyWithEdge_RecordsEdgeProperties()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.ManyToMany<TestTradeOrder>("RelatedOrders", edge =>
        {
            edge.Property<double>("Relevance");
            edge.Property<string>("Rationale");
        });
        var descriptor = builder.Build();

        await Assert.That(descriptor.Links[0].EdgeProperties.Count).IsEqualTo(2);
        await Assert.That(descriptor.Links[0].EdgeProperties[0].Name).IsEqualTo("Relevance");
        await Assert.That(descriptor.Links[0].EdgeProperties[1].Name).IsEqualTo("Rationale");
    }

    [Test]
    public async Task ObjectTypeBuilder_Action_AddsActionDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.Action("ExecuteTrade")
            .Description("Open a new position")
            .BoundToWorkflow("execute-trade");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Actions[0].Name).IsEqualTo("ExecuteTrade");
        await Assert.That(descriptor.Actions[0].BindingType).IsEqualTo(ActionBindingType.Workflow);
    }

    [Test]
    public async Task ObjectTypeBuilder_Event_AddsEventDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.Event<TestTradeExecuted>(evt =>
        {
            evt.Description("A trade was executed");
        });
        var descriptor = builder.Build();

        await Assert.That(descriptor.Events.Count).IsEqualTo(1);
        await Assert.That(descriptor.Events[0].EventType).IsEqualTo(typeof(TestTradeExecuted));
        await Assert.That(descriptor.Events[0].Description).IsEqualTo("A trade was executed");
    }

    [Test]
    public async Task ObjectTypeBuilder_Implements_RecordsInterfaceMapping()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading");

        builder.Implements<ITestSearchable>(map =>
        {
            map.Via(p => p.Symbol, s => s.Title);
        });
        var descriptor = builder.Build();

        await Assert.That(descriptor.ImplementedInterfaces.Count).IsEqualTo(1);
        await Assert.That(descriptor.ImplementedInterfaces[0].InterfaceType).IsEqualTo(typeof(ITestSearchable));
    }

    [Test]
    public async Task Build_WithValidFromStateOnAction_EmitsSelfLoopTransitionIntoLifecycle()
    {
        var builder = new ObjectTypeBuilder<TrackATestType>("test-domain");
        builder.Lifecycle<TrackATestState>(t => t.Status, lc =>
        {
            lc.State(TrackATestState.Open).Initial();
            lc.State(TrackATestState.Closed).Terminal();
        });
        builder.Action("ViewPosition").ValidFromState(TrackATestState.Open);

        var descriptor = builder.Build();

        var selfLoop = descriptor.Lifecycle!.Transitions
            .FirstOrDefault(t => t.FromState == "Open" && t.ToState == "Open" && t.TriggerActionName == "ViewPosition");
        await Assert.That(selfLoop).IsNotNull();
    }

    [Test]
    public async Task Build_WithValidFromStateForUndeclaredState_Throws()
    {
        var builder = new ObjectTypeBuilder<TrackATestType>("test-domain");
        builder.Lifecycle<TrackATestState>(t => t.Status, lc =>
        {
            lc.State(TrackATestState.Open).Initial();
            // Closed is intentionally NOT declared
        });
        builder.Action("Inspect").ValidFromState(TrackATestState.Closed);

        await Assert.That(() => builder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(() => builder.Build())
            .ThrowsException()
            .WithMessageContaining("Closed");
    }

    [Test]
    public async Task Build_WithMultipleValidFromStates_EmitsOneSelfLoopPerState()
    {
        var builder = new ObjectTypeBuilder<TrackATestType>("test-domain");
        builder.Lifecycle<TrackATestState>(t => t.Status, lc =>
        {
            lc.State(TrackATestState.Pending).Initial();
            lc.State(TrackATestState.Active).Terminal();
        });
        builder.Action("ExecuteTrade")
            .ValidFromState(TrackATestState.Pending)
            .ValidFromState(TrackATestState.Active);

        var descriptor = builder.Build();

        var pending = descriptor.Lifecycle!.Transitions
            .Any(t => t.FromState == "Pending" && t.ToState == "Pending" && t.TriggerActionName == "ExecuteTrade");
        var active = descriptor.Lifecycle!.Transitions
            .Any(t => t.FromState == "Active" && t.ToState == "Active" && t.TriggerActionName == "ExecuteTrade");
        await Assert.That(pending).IsTrue();
        await Assert.That(active).IsTrue();
    }

    [Test]
    public async Task ObjectTypeBuilder_WithExplicitName_UsesExplicitNameInDescriptor()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading", explicitName: "trading_documents");

        builder.Key(p => p.Id);
        builder.Property(p => p.Symbol).Required();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("trading_documents");
        await Assert.That(descriptor.ClrType).IsEqualTo(typeof(TestPosition));
    }

    [Test]
    public async Task ObjectTypeBuilder_WithNullExplicitName_FallsBackToTypeofTName()
    {
        var builder = new ObjectTypeBuilder<TestPosition>("Trading", explicitName: null);

        builder.Key(p => p.Id);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("TestPosition");
        await Assert.That(descriptor.ClrType).IsEqualTo(typeof(TestPosition));
    }

    [Test]
    public async Task ObjectTypeBuilder_WithExplicitName_LifecycleProjectionAppliesToThatDescriptor()
    {
        var builder = new ObjectTypeBuilder<TrackATestType>("test-domain", explicitName: "archived_items");
        builder.Lifecycle<TrackATestState>(t => t.Status, lc =>
        {
            lc.State(TrackATestState.Open).Initial();
            lc.State(TrackATestState.Closed).Terminal();
        });
        builder.Action("ViewItem").ValidFromState(TrackATestState.Open);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("archived_items");
        var selfLoop = descriptor.Lifecycle!.Transitions
            .FirstOrDefault(t => t.FromState == "Open" && t.ToState == "Open" && t.TriggerActionName == "ViewItem");
        await Assert.That(selfLoop).IsNotNull();
    }
}
