using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public record TestDerivedPosition(
    Guid Id,
    string Symbol,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal UnrealizedPnL,
    decimal PortfolioWeight);

public class PropertyBuilderOfTTests
{
    [Test]
    public async Task Required_SetsIsRequired()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("Symbol", typeof(string));

        builder.Required();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsRequired).IsTrue();
    }

    [Test]
    public async Task Computed_SetsIsComputed()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("UnrealizedPnL", typeof(decimal));

        builder.Computed();
        var descriptor = builder.Build();

        await Assert.That(descriptor.IsComputed).IsTrue();
    }

    [Test]
    public async Task DerivedFrom_AddsSingleLocalSource()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("UnrealizedPnL", typeof(decimal));

        builder.DerivedFrom(p => p.Quantity);
        var descriptor = builder.Build();

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(1);
        await Assert.That(descriptor.DerivedFrom[0].Kind).IsEqualTo(DerivationSourceKind.Local);
        await Assert.That(descriptor.DerivedFrom[0].PropertyName).IsEqualTo("Quantity");
    }

    [Test]
    public async Task DerivedFrom_AddsMultipleLocalSources()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("UnrealizedPnL", typeof(decimal));

        builder.DerivedFrom(p => p.Quantity, p => p.AverageCost, p => p.CurrentPrice);
        var descriptor = builder.Build();

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(3);
        await Assert.That(descriptor.DerivedFrom[0].PropertyName).IsEqualTo("Quantity");
        await Assert.That(descriptor.DerivedFrom[1].PropertyName).IsEqualTo("AverageCost");
        await Assert.That(descriptor.DerivedFrom[2].PropertyName).IsEqualTo("CurrentPrice");
    }

    [Test]
    public async Task DerivedFrom_MultipleCalls_ArAdditive()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("UnrealizedPnL", typeof(decimal));

        builder.DerivedFrom(p => p.Quantity);
        builder.DerivedFrom(p => p.AverageCost);
        var descriptor = builder.Build();

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(2);
    }

    [Test]
    public async Task DerivedFromExternal_AddsExternalSource()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("PortfolioWeight", typeof(decimal));

        builder.DerivedFromExternal("trading", "Portfolio", "TotalValue");
        var descriptor = builder.Build();

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(1);
        await Assert.That(descriptor.DerivedFrom[0].Kind).IsEqualTo(DerivationSourceKind.External);
        await Assert.That(descriptor.DerivedFrom[0].ExternalDomain).IsEqualTo("trading");
        await Assert.That(descriptor.DerivedFrom[0].ExternalObjectType).IsEqualTo("Portfolio");
        await Assert.That(descriptor.DerivedFrom[0].ExternalPropertyName).IsEqualTo("TotalValue");
    }

    [Test]
    public async Task FluentChaining_RequiredComputedDerivedFrom()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("UnrealizedPnL", typeof(decimal));

        builder
            .Computed()
            .DerivedFrom(p => p.Quantity, p => p.AverageCost, p => p.CurrentPrice);

        var descriptor = builder.Build();

        await Assert.That(descriptor.IsComputed).IsTrue();
        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(3);
    }

    [Test]
    public async Task FluentChaining_MixedLocalAndExternal()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("PortfolioWeight", typeof(decimal));

        builder
            .Computed()
            .DerivedFrom(p => p.UnrealizedPnL)
            .DerivedFromExternal("trading", "Portfolio", "TotalValue");

        var descriptor = builder.Build();

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(2);
        await Assert.That(descriptor.DerivedFrom[0].Kind).IsEqualTo(DerivationSourceKind.Local);
        await Assert.That(descriptor.DerivedFrom[1].Kind).IsEqualTo(DerivationSourceKind.External);
    }

    [Test]
    public async Task DefaultDerivedFrom_IsEmpty()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("Symbol", typeof(string));

        var descriptor = builder.Build();

        await Assert.That(descriptor.DerivedFrom.Count).IsEqualTo(0);
    }

    [Test]
    public async Task NonGenericInterface_ChainedMethodsWork()
    {
        IPropertyBuilder builder = new PropertyBuilder<TestDerivedPosition>("Symbol", typeof(string));

        var result = builder.Required();

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Vector_SetsKindAndDimensions()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("Embedding", typeof(float[]));

        builder.Vector(768);
        var descriptor = builder.Build();

        await Assert.That(descriptor.Kind).IsEqualTo(PropertyKind.Vector);
        await Assert.That(descriptor.VectorDimensions).IsEqualTo(768);
    }

    [Test]
    public async Task Vector_ZeroDimensions_ThrowsArgumentOutOfRange()
    {
        var builder = new PropertyBuilder<TestDerivedPosition>("Embedding", typeof(float[]));

        await Assert.That(() => builder.Vector(0))
            .ThrowsException()
            .WithExceptionType(typeof(ArgumentOutOfRangeException));
    }

    [Test]
    public async Task Vector_NonGenericInterface_VectorWorks()
    {
        IPropertyBuilder builder = new PropertyBuilder<TestDerivedPosition>("Embedding", typeof(float[]));

        var result = builder.Vector(1536);

        await Assert.That(result).IsNotNull();
    }
}
