using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class WorkflowBindingReferenceTests
{
    [Test]
    public async Task Constructor_ValidWorkflowId_PreservesExactValue()
    {
        var reference = new WorkflowBindingReference("  execute-trade  ");

        await Assert.That(reference.WorkflowId).IsEqualTo("  execute-trade  ");
    }

    [Test]
    public async Task Constructor_NullWorkflowId_ThrowsArgumentNullException()
    {
        await Assert.That(() => new WorkflowBindingReference(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Constructor_EmptyOrWhiteSpaceWorkflowId_ThrowsArgumentException(string workflowId)
    {
        await Assert.That(() => new WorkflowBindingReference(workflowId))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Equality_SameWorkflowId_UsesValueSemantics()
    {
        var first = new WorkflowBindingReference("execute-trade");
        var second = new WorkflowBindingReference("execute-trade");

        await Assert.That(first).IsEqualTo(second);
    }

    [Test]
    public async Task Type_IsSealedAndWorkflowIdIsGetOnly()
    {
        var type = typeof(WorkflowBindingReference);
        var property = type.GetProperty(nameof(WorkflowBindingReference.WorkflowId));

        await Assert.That(type.IsSealed).IsTrue();
        await Assert.That(property).IsNotNull();
        await Assert.That(property!.SetMethod).IsNull();
    }
}
