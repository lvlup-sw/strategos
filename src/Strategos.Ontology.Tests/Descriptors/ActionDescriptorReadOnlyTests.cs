using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class ActionDescriptorReadOnlyTests
{
    [Test]
    public async Task ActionDescriptor_IsReadOnlyDefault_IsFalse()
    {
        var descriptor = new ActionDescriptor("GetBalance", "Read the current balance");

        await Assert.That(descriptor.IsReadOnly).IsFalse();
    }

    [Test]
    public async Task ActionDescriptor_IsReadOnlyTrue_FlowsThroughInit()
    {
        var descriptor = new ActionDescriptor("GetBalance", "Read the current balance")
            with { IsReadOnly = true };

        await Assert.That(descriptor.IsReadOnly).IsTrue();
    }
}
