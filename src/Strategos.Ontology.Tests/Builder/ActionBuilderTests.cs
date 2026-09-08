using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class ActionBuilderTests
{
    private static readonly ActionSubject Subject = new("Trading", "Position");

    [Test]
    public async Task ActionBuilder_Build_ProducesDescriptorWithName()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("ExecuteTrade");
    }

    [Test]
    public async Task ActionBuilder_Description_SetsDescription()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.Description("Open a new position");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Description).IsEqualTo("Open a new position");
    }

    [Test]
    public async Task ActionBuilder_Accepts_SetsAcceptsType()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.Accepts<TestTradeExecutionRequest>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.AcceptsType).IsEqualTo(typeof(TestTradeExecutionRequest));
    }

    [Test]
    public async Task ActionBuilder_Returns_SetsReturnsType()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.Returns<TestTradeExecutionResult>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.ReturnsType).IsEqualTo(typeof(TestTradeExecutionResult));
    }

    [Test]
    public async Task ActionBuilder_BoundToWorkflowString_SetsBindingAndReference()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.BoundToWorkflow("execute-trade");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflow).IsEqualTo(new WorkflowBindingReference("execute-trade"));
    }

    [Test]
    public async Task ActionBuilder_BoundToWorkflowReference_SetsBindingAndReference()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);
        var workflow = new WorkflowBindingReference("execute-trade");

        builder.BoundToWorkflow(workflow);
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflow).IsEqualTo(workflow);
    }

    [Test]
    public async Task ActionBuilder_BoundToWorkflowReferenceNull_Throws()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        await Assert.That(() => builder.BoundToWorkflow((WorkflowBindingReference)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ActionBuilder_BoundToWorkflowStringWhiteSpace_Throws()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        await Assert.That(() => builder.BoundToWorkflow("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ActionBuilder_BoundToTool_SetsBindingAndToolReference()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.BoundToTool("trading-tool", "execute");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
        await Assert.That(descriptor.BoundToolName).IsEqualTo("trading-tool");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("execute");
    }

    [Test]
    public async Task ActionBuilder_BoundToWorkflowThenBoundToTool_ClearsWorkflowCarrier()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.BoundToWorkflow("execute-trade").BoundToTool("trading-tool", "execute");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
        await Assert.That(descriptor.BoundWorkflow).IsNull();
        await Assert.That(descriptor.BoundToolName).IsEqualTo("trading-tool");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("execute");
    }

    [Test]
    public async Task ActionBuilder_BoundToToolThenBoundToWorkflow_ClearsToolCarrier()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        builder.BoundToTool("trading-tool", "execute").BoundToWorkflow("execute-trade");
        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflow).IsEqualTo(new WorkflowBindingReference("execute-trade"));
        await Assert.That(descriptor.BoundToolName).IsNull();
        await Assert.That(descriptor.BoundToolMethod).IsNull();
    }

    [Test]
    public async Task ActionBuilder_Unbound_DefaultsToUnbound()
    {
        var builder = new ActionBuilder("ExecuteTrade", Subject);

        var descriptor = builder.Build();

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Unbound);
    }
}
