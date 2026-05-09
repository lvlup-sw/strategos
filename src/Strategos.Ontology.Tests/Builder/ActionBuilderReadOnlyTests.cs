using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public class ActionBuilderReadOnlyTests
{
    [Test]
    public async Task ActionBuilder_ReadOnly_FlagsBuilder_DescriptorIsReadOnlyTrue()
    {
        var builder = new ActionBuilder("GetBalance");

        builder.ReadOnly();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsReadOnly).IsTrue();
    }

    [Test]
    public async Task ActionBuilder_NoReadOnly_DescriptorIsReadOnlyFalse()
    {
        var builder = new ActionBuilder("GetBalance");

        var descriptor = builder.Build();

        await Assert.That(descriptor.IsReadOnly).IsFalse();
    }
}
