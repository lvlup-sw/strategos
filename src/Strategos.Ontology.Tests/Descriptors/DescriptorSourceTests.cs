using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class DescriptorSourceTests
{
    [Test]
    public async Task DescriptorSource_DefaultValue_IsHandAuthored()
    {
        // Arrange & Act
        var value = default(DescriptorSource);

        // Assert
        await Assert.That(value).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task DescriptorSource_HandAuthored_HasValueZero()
    {
        await Assert.That((int)DescriptorSource.HandAuthored).IsEqualTo(0);
    }

    [Test]
    public async Task DescriptorSource_Ingested_HasValueOne()
    {
        await Assert.That((int)DescriptorSource.Ingested).IsEqualTo(1);
    }
}
