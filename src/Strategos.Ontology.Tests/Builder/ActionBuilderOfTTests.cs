using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class TestTool
{
    public Task DoSomethingAsync() => Task.CompletedTask;
}

public enum TestPositionStatus
{
    Pending,
    Active,
    Closed,
}

public record TestPositionWithStatus(
    Guid Id,
    string Symbol,
    decimal Quantity,
    decimal UnrealizedPnL,
    TestPositionStatus Status,
    string DisplayDescription);

public record TestTradeExecutedEvent(Guid OrderId);

public class ActionBuilderOfTTests
{
    [Test]
    public async Task Build_ProducesDescriptorWithName()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("ExecuteTrade");
    }

    [Test]
    public async Task Description_SetsDescription()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Description("Open a new position");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Description).IsEqualTo("Open a new position");
    }

    [Test]
    public async Task Accepts_SetsAcceptsType()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Accepts<TestTradeExecutionRequest>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.AcceptsType).IsEqualTo(typeof(TestTradeExecutionRequest));
    }

    [Test]
    public async Task Returns_SetsReturnsType()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Returns<TestTradeExecutionResult>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.ReturnsType).IsEqualTo(typeof(TestTradeExecutionResult));
    }

    [Test]
    public async Task BoundToWorkflow_SetsBindingAndWorkflowName()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.BoundToWorkflow("execute-trade");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflowName).IsEqualTo("execute-trade");
    }

    [Test]
    public async Task BoundToTool_SetsBindingAndToolReference()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetQuote");

        builder.BoundToTool("MarketData", "GetQuoteAsync");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
        await Assert.That(descriptor.BoundToolName).IsEqualTo("MarketData");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("GetQuoteAsync");
    }

    [Test]
    public async Task Requires_AddsPreconditionWithPropertyPredicate()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Requires(p => p.Status == TestPositionStatus.Active);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Preconditions[0].Kind).IsEqualTo(PreconditionKind.PropertyPredicate);
        await Assert.That(descriptor.Preconditions[0].Description).Contains("Status");
    }

    [Test]
    public async Task Requires_MultipleCallsAddMultiplePreconditions()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Requires(p => p.Status == TestPositionStatus.Active);
        builder.Requires(p => p.Quantity > 0);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RequiresLink_AddsPreconditionWithLinkExists()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.RequiresLink("Strategy");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Preconditions[0].Kind).IsEqualTo(PreconditionKind.LinkExists);
        await Assert.That(descriptor.Preconditions[0].LinkName).IsEqualTo("Strategy");
    }

    [Test]
    public async Task Modifies_AddsPostconditionWithModifiesProperty()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Modifies(p => p.Quantity);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Postconditions[0].Kind).IsEqualTo(PostconditionKind.ModifiesProperty);
        await Assert.That(descriptor.Postconditions[0].PropertyName).IsEqualTo("Quantity");
    }

    [Test]
    public async Task Modifies_MultipleCallsAddMultiplePostconditions()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.Modifies(p => p.Quantity);
        builder.Modifies(p => p.UnrealizedPnL);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(2);
        await Assert.That(descriptor.Postconditions[0].PropertyName).IsEqualTo("Quantity");
        await Assert.That(descriptor.Postconditions[1].PropertyName).IsEqualTo("UnrealizedPnL");
    }

    [Test]
    public async Task CreatesLinked_AddsPostconditionWithCreatesLink()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.CreatesLinked<TestTradeOrder>("Orders");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Postconditions[0].Kind).IsEqualTo(PostconditionKind.CreatesLink);
        await Assert.That(descriptor.Postconditions[0].LinkName).IsEqualTo("Orders");
    }

    [Test]
    public async Task EmitsEvent_AddsPostconditionWithEmitsEvent()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder.EmitsEvent<TestTradeExecutedEvent>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Postconditions[0].Kind).IsEqualTo(PostconditionKind.EmitsEvent);
        await Assert.That(descriptor.Postconditions[0].EventTypeName).IsEqualTo("TestTradeExecutedEvent");
    }

    [Test]
    public async Task FluentChaining_AllMethodsChainCorrectly()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade");

        builder
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

        await Assert.That(descriptor.Name).IsEqualTo("ExecuteTrade");
        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(3);
        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(4);
    }

    [Test]
    public async Task DefaultPreconditionsAndPostconditions_AreEmpty()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("SimpleAction");

        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(0);
        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NonGenericInterface_ChainedMethodsReturnSameBuilder()
    {
        IActionBuilder builder = new ActionBuilder<TestPositionWithStatus>("Test");

        var result = builder.Description("desc");

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task BoundToTool_Expression_SetsToolNameAndMethod()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetQuote");

        builder.BoundToTool<TestTool>(t => t.DoSomethingAsync);
        var descriptor = builder.Build();

        await Assert.That(descriptor.BoundToolName).IsEqualTo("TestTool");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("DoSomethingAsync");
    }

    [Test]
    public async Task BoundToTool_Expression_SetsBindingTypeToTool()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetQuote");

        builder.BoundToTool<TestTool>(t => t.DoSomethingAsync);
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
    }
}
