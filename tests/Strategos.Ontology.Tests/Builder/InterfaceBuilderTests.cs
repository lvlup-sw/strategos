using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public class InterfaceBuilderTests
{
    [Test]
    public async Task InterfaceBuilder_Build_ProducesDescriptorWithProperties()
    {
        var builder = new InterfaceBuilder<ITestSearchable>("Searchable");

        builder.Property(s => s.Title);
        builder.Property(s => s.Description);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("Searchable");
        await Assert.That(descriptor.InterfaceType).IsEqualTo(typeof(ITestSearchable));
        await Assert.That(descriptor.Properties.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InterfaceBuilder_Property_AddsToPropertyList()
    {
        var builder = new InterfaceBuilder<ITestSearchable>("Searchable");

        builder.Property(s => s.Title);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Properties.Count).IsEqualTo(1);
        await Assert.That(descriptor.Properties[0].Name).IsEqualTo("Title");
    }

    [Test]
    public async Task InterfaceMapping_Via_RecordsMappingPair()
    {
        var mapping = new InterfaceMapping<TestSearchableObject, ITestSearchable>();

        mapping.Via(p => p.Symbol, s => s.Title);

        var mappings = mapping.GetMappings();
        await Assert.That(mappings.Count).IsEqualTo(1);
        await Assert.That(mappings[0].SourceName).IsEqualTo("Symbol");
        await Assert.That(mappings[0].TargetName).IsEqualTo("Title");
    }
}
