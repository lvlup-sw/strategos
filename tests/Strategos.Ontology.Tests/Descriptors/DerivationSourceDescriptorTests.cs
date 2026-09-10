using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class DerivationSourceDescriptorTests
{
    [Test]
    public async Task DerivationSourceKind_HasExpectedValues()
    {
        await Assert.That(Enum.GetValues<DerivationSourceKind>()).HasCount().EqualTo(2);
        await Assert.That(DerivationSourceKind.Local).IsEqualTo((DerivationSourceKind)0);
        await Assert.That(DerivationSourceKind.External).IsEqualTo((DerivationSourceKind)1);
    }

    [Test]
    public async Task DerivationSource_Local_Record()
    {
        var source = new DerivationSource
        {
            Kind = DerivationSourceKind.Local,
            PropertyName = "Quantity",
        };

        await Assert.That(source.Kind).IsEqualTo(DerivationSourceKind.Local);
        await Assert.That(source.PropertyName).IsEqualTo("Quantity");
        await Assert.That(source.ExternalDomain).IsNull();
        await Assert.That(source.ExternalObjectType).IsNull();
        await Assert.That(source.ExternalPropertyName).IsNull();
    }

    [Test]
    public async Task DerivationSource_External_Record()
    {
        var source = new DerivationSource
        {
            Kind = DerivationSourceKind.External,
            ExternalDomain = "trading",
            ExternalObjectType = "Portfolio",
            ExternalPropertyName = "TotalValue",
        };

        await Assert.That(source.Kind).IsEqualTo(DerivationSourceKind.External);
        await Assert.That(source.PropertyName).IsNull();
        await Assert.That(source.ExternalDomain).IsEqualTo("trading");
        await Assert.That(source.ExternalObjectType).IsEqualTo("Portfolio");
        await Assert.That(source.ExternalPropertyName).IsEqualTo("TotalValue");
    }

    [Test]
    public async Task PropertyDescriptor_DefaultDerivedFrom_IsEmpty()
    {
        var descriptor = new PropertyDescriptor("Test", typeof(string));

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(0);
        await Assert.That(descriptor.TransitiveDerivedFrom.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PropertyDescriptor_WithDerivedFrom_StoresSources()
    {
        var sources = new[]
        {
            new DerivationSource { Kind = DerivationSourceKind.Local, PropertyName = "Quantity" },
            new DerivationSource { Kind = DerivationSourceKind.Local, PropertyName = "AverageCost" },
        };

        var descriptor = new PropertyDescriptor("UnrealizedPnL", typeof(decimal), IsComputed: true)
        {
            DerivedFrom = sources,
        };

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(2);
    }
}
