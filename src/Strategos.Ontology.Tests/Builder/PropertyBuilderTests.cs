using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class PropertyBuilderTests
{
    [Test]
    public async Task PropertyBuilder_Build_ProducesDescriptorWithName()
    {
        var builder = new PropertyBuilder("Symbol", typeof(string));

        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("Symbol");
        await Assert.That(descriptor.PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task PropertyBuilder_Required_SetsIsRequired()
    {
        var builder = new PropertyBuilder("Symbol", typeof(string));

        builder.Required();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsRequired).IsTrue();
    }

    [Test]
    public async Task PropertyBuilder_Computed_SetsIsComputed()
    {
        var builder = new PropertyBuilder("PnL", typeof(decimal));

        builder.Computed();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsComputed).IsTrue();
    }

    [Test]
    public async Task PropertyBuilder_ChainedCalls_AllApplied()
    {
        var builder = new PropertyBuilder("Symbol", typeof(string));

        builder.Required().Computed();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsRequired).IsTrue();
        await Assert.That(descriptor.IsComputed).IsTrue();
    }

    [Test]
    public async Task PropertyBuilder_Vector_SetsKindAndDimensions()
    {
        var builder = new PropertyBuilder("Embedding", typeof(float[]));

        builder.Vector(1536);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Kind).IsEqualTo(PropertyKind.Vector);
        await Assert.That(descriptor.VectorDimensions).IsEqualTo(1536);
    }

    [Test]
    public async Task PropertyBuilder_Vector_ZeroDimensions_ThrowsArgumentOutOfRange()
    {
        var builder = new PropertyBuilder("Embedding", typeof(float[]));

        await Assert.That(() => builder.Vector(0))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentOutOfRangeException));
    }

    [Test]
    public async Task PropertyBuilder_Vector_NegativeDimensions_ThrowsArgumentOutOfRange()
    {
        var builder = new PropertyBuilder("Embedding", typeof(float[]));

        await Assert.That(() => builder.Vector(-1))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentOutOfRangeException));
    }
}
