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

public enum TrackATestState
{
    Open,
    Closed,
    Pending,
    Active,
}

public sealed class TrackATestType
{
    public TrackATestState Status { get; set; }
}

public class ActionBuilderOfTTests
{
    private static readonly ActionSubject Subject = new("Trading", "TestPositionWithStatus");
    private static readonly ActionSubject TrackSubject = new("Trading", "TrackATestType");

    [Test]
    public async Task RequiresAuthority_StoresNamedRequirement()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);
        builder.RequiresAuthority("portfolio.write");
        var descriptor = builder.Build();

        await Assert.That(descriptor.RequiredAuthority).IsEqualTo("portfolio.write");
    }

    [Test]
    public async Task Build_ProducesDescriptorWithName()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("ExecuteTrade");
    }

    [Test]
    public async Task Description_SetsDescription()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.Description("Open a new position");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Description).IsEqualTo("Open a new position");
    }

    [Test]
    public async Task Accepts_SetsAcceptsType()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.Accepts<TestTradeExecutionRequest>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.AcceptsType).IsEqualTo(typeof(TestTradeExecutionRequest));
    }

    [Test]
    public async Task Returns_SetsReturnsType()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.Returns<TestTradeExecutionResult>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.ReturnsType).IsEqualTo(typeof(TestTradeExecutionResult));
    }

    [Test]
    public async Task BoundToWorkflowString_SetsBindingAndReference()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.BoundToWorkflow("execute-trade");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflow).IsEqualTo(new WorkflowBindingReference("execute-trade"));
    }

    [Test]
    public async Task BoundToWorkflowReference_SetsBindingAndReference()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);
        var workflow = new WorkflowBindingReference("execute-trade");

        builder.BoundToWorkflow(workflow);
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflow).IsEqualTo(workflow);
    }

    [Test]
    public async Task BoundToWorkflowReferenceNull_Throws()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        await Assert.That(() => builder.BoundToWorkflow((WorkflowBindingReference)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task BoundToWorkflowStringWhiteSpace_Throws()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        await Assert.That(() => builder.BoundToWorkflow("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task BoundToTool_SetsBindingAndToolReference()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetQuote", Subject);

        builder.BoundToTool("MarketData", "GetQuoteAsync");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
        await Assert.That(descriptor.BoundToolName).IsEqualTo("MarketData");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("GetQuoteAsync");
    }

    [Test]
    public async Task Requires_AddsPreconditionWithPropertyPredicate()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.Requires(p => p.Status == TestPositionStatus.Active);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Preconditions[0].Predicate).IsTypeOf<PropertyComparisonPredicate>();
        await Assert.That(descriptor.Preconditions[0].Description).Contains("Status");
    }

    [Test]
    public async Task Requires_MultipleCallsAddMultiplePreconditions()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.Requires(p => p.Status == TestPositionStatus.Active);
        builder.Requires(p => p.Quantity > 0);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RequiresLink_AddsPreconditionWithLinkExists()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.RequiresLink("Strategy");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Preconditions[0].Predicate).IsTypeOf<LinkExistsPredicate>();
        await Assert.That(((LinkExistsPredicate)descriptor.Preconditions[0].Predicate).LinkName)
            .IsEqualTo("Strategy");
    }

    [Test]
    public async Task RequiresRelation_AddsStructuredPrincipalRelationPrecondition()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.RequiresRelation("owner", "Portfolio", "Space");
        var precondition = builder.Build().Preconditions.Single();
        var relation = (RelationHoldsPredicate)precondition.Predicate;

        await Assert.That(relation.RelationName).IsEqualTo("owner");
        await Assert.That(relation.LinkPath).IsEquivalentTo(["Portfolio", "Space"]);
        await Assert.That(precondition.IsOpaque).IsFalse();
    }

    [Test]
    public async Task Modifies_AddsPostconditionWithModifiesProperty()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.Modifies(p => p.Quantity);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Postconditions[0].Kind).IsEqualTo(PostconditionKind.ModifiesProperty);
        await Assert.That(descriptor.Postconditions[0].PropertyName).IsEqualTo("Quantity");
    }

    [Test]
    public async Task Modifies_MultipleCallsAddMultiplePostconditions()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

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
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.CreatesLinked<TestTradeOrder>("Orders");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Postconditions[0].Kind).IsEqualTo(PostconditionKind.CreatesLink);
        await Assert.That(descriptor.Postconditions[0].LinkName).IsEqualTo("Orders");
    }

    [Test]
    public async Task EmitsEvent_AddsPostconditionWithEmitsEvent()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

        builder.EmitsEvent<TestTradeExecutedEvent>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Postconditions[0].Kind).IsEqualTo(PostconditionKind.EmitsEvent);
        await Assert.That(descriptor.Postconditions[0].EventTypeName).IsEqualTo("TestTradeExecutedEvent");
    }

    [Test]
    public async Task FluentChaining_AllMethodsChainCorrectly()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("ExecuteTrade", Subject);

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
        var builder = new ActionBuilder<TestPositionWithStatus>("SimpleAction", Subject);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(0);
        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Idempotent_WriteAction_OptsIntoRetrySafety()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("UpsertPosition", Subject);
        builder.Idempotent();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsReadOnly).IsFalse();
        await Assert.That(descriptor.Idempotent).IsTrue();
    }

    [Test]
    public async Task ReadOnly_ActionIsIdempotentByConstruction()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetPosition", Subject);
        builder.ReadOnly();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsReadOnly).IsTrue();
        await Assert.That(descriptor.Idempotent).IsTrue();
    }

    [Test]
    public async Task NonGenericInterface_ChainedMethodsReturnSameBuilder()
    {
        IActionBuilder builder = new ActionBuilder<TestPositionWithStatus>("Test", Subject);

        var result = builder.Description("desc");

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task BoundToTool_Expression_SetsToolNameAndMethod()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetQuote", Subject);

        builder.BoundToTool<TestTool>(t => t.DoSomethingAsync);
        var descriptor = builder.Build();

        await Assert.That(descriptor.BoundToolName).IsEqualTo("TestTool");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("DoSomethingAsync");
    }

    [Test]
    public async Task BoundToTool_Expression_SetsBindingTypeToTool()
    {
        var builder = new ActionBuilder<TestPositionWithStatus>("GetQuote", Subject);

        builder.BoundToTool<TestTool>(t => t.DoSomethingAsync);
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
    }

    [Test]
    public async Task ValidFromState_RecordsStateNameOnBuilder()
    {
        var builder = new ActionBuilder<TrackATestType>("ClosePosition", TrackSubject);

        builder.ValidFromState(TrackATestState.Open);

        await Assert.That(builder.ValidFromStates).Contains("Open");
    }
}
