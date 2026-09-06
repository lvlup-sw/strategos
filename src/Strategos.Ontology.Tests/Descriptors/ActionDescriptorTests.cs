using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class ActionDescriptorTests
{
    private static readonly ActionSubject Subject = new("Trading", "Position");

    [Test]
    public async Task ActionBindingType_HasExpectedValues()
    {
        await Assert.That(Enum.IsDefined(ActionBindingType.Workflow)).IsTrue();
        await Assert.That(Enum.IsDefined(ActionBindingType.Tool)).IsTrue();
        await Assert.That(Enum.IsDefined(ActionBindingType.Unbound)).IsTrue();
    }

    [Test]
    public async Task ActionDescriptor_Create_HasNameAndDescription()
    {
        var descriptor = new ActionDescriptor(Subject, "ExecuteTrade", "Open a new position");

        await Assert.That(descriptor.Subject).IsEqualTo(Subject);
        await Assert.That(descriptor.Name).IsEqualTo("ExecuteTrade");
        await Assert.That(descriptor.Description).IsEqualTo("Open a new position");
    }

    [Test]
    public async Task ActionDescriptor_BoundToWorkflow_SetsBindingType()
    {
        var descriptor = new ActionDescriptor(Subject, "ExecuteTrade", "Open a new position")
        {
            BindingType = ActionBindingType.Workflow,
            BoundWorkflowName = "execute-trade",
        };

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Workflow);
        await Assert.That(descriptor.BoundWorkflowName).IsEqualTo("execute-trade");
    }

    [Test]
    public async Task ActionDescriptor_BoundToTool_SetsToolReference()
    {
        var descriptor = new ActionDescriptor(Subject, "ExecuteTrade", "Open a new position")
        {
            BindingType = ActionBindingType.Tool,
            BoundToolName = "trading-tool",
            BoundToolMethod = "execute",
        };

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Tool);
        await Assert.That(descriptor.BoundToolName).IsEqualTo("trading-tool");
        await Assert.That(descriptor.BoundToolMethod).IsEqualTo("execute");
    }

    [Test]
    public async Task ActionDescriptor_Unbound_DefaultBinding()
    {
        var descriptor = new ActionDescriptor(Subject, "ExecuteTrade", "Open a new position");

        await Assert.That(descriptor.BindingType).IsEqualTo(ActionBindingType.Unbound);
        await Assert.That(descriptor.BoundWorkflowName).IsNull();
        await Assert.That(descriptor.BoundToolName).IsNull();
        await Assert.That(descriptor.BoundToolMethod).IsNull();
    }
}
