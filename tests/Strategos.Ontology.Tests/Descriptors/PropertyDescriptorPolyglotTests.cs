using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class PropertyDescriptorPolyglotTests
{
    [Test]
    public async Task ReferenceSymbolKey_DefaultValue_IsNull()
    {
        var descriptor = new PropertyDescriptor("OwnerId", typeof(Guid));

        await Assert.That(descriptor.ReferenceSymbolKey).IsNull();
    }

    [Test]
    public async Task Source_DefaultValue_IsHandAuthored()
    {
        var descriptor = new PropertyDescriptor("OwnerId", typeof(Guid));

        await Assert.That(descriptor.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task Ctor_ReferenceTypeBySymbolKey_StoresSymbolKey()
    {
        // For reference-typed properties whose target type is identified
        // by SCIP rather than CLR (ingestion path), ReferenceSymbolKey
        // rides alongside PropertyType.
        var descriptor = new PropertyDescriptor("Owner", typeof(object))
        {
            ReferenceSymbolKey = "scip-typescript . ./src/user.ts#User",
            Source = DescriptorSource.Ingested,
            Kind = PropertyKind.Reference,
        };

        await Assert.That(descriptor.ReferenceSymbolKey).IsEqualTo("scip-typescript . ./src/user.ts#User");
        await Assert.That(descriptor.Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(descriptor.Kind).IsEqualTo(PropertyKind.Reference);
    }
}
