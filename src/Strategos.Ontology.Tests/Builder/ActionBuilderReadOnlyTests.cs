using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class ActionBuilderReadOnlyTests
{
    private static readonly ActionSubject Subject = new("Trading", "Position");

    [Test]
    public async Task ActionBuilder_ReadOnly_FlagsBuilder_DescriptorIsReadOnlyTrue()
    {
        var builder = new ActionBuilder("GetBalance", Subject);

        builder.ReadOnly();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsReadOnly).IsTrue();
    }

    [Test]
    public async Task ActionBuilder_NoReadOnly_DescriptorIsReadOnlyFalse()
    {
        var builder = new ActionBuilder("GetBalance", Subject);

        var descriptor = builder.Build();

        await Assert.That(descriptor.IsReadOnly).IsFalse();
    }
}
