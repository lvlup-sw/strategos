// =============================================================================
// <copyright file="PropertyKindTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

// --- Test domain models for PropertyKind tests ---

public record PropKindPosition(Guid Id, string Symbol, decimal Quantity);

public record PropKindOrder(Guid Id, string Symbol);

public class PropKindPortfolio
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public PropKindPosition? MainPosition { get; set; }
    public decimal TotalValue { get; set; }
}

public class PropKindOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PropKindPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.Quantity);
        });

        builder.Object<PropKindOrder>(obj =>
        {
            obj.Key(o => o.Id);
            obj.Property(o => o.Symbol).Required();
        });

        builder.Object<PropKindPortfolio>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Name).Required();
            obj.Property(p => p.MainPosition!);
            obj.Property(p => p.TotalValue).Computed();
        });
    }
}

public class PropertyKindTests
{
    [Test]
    public async Task PropertyKind_ScalarProperty_InferredAsScalar()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<PropKindOntology>();

        var graph = graphBuilder.Build();
        var position = graph.ObjectTypes.First(ot => ot.Name == "PropKindPosition");
        var symbolProp = position.Properties.First(p => p.Name == "Symbol");

        await Assert.That(symbolProp.Kind).IsEqualTo(PropertyKind.Scalar);
    }

    [Test]
    public async Task PropertyKind_PropertyTypeMatchesObjectType_InferredAsReference()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<PropKindOntology>();

        var graph = graphBuilder.Build();
        var portfolio = graph.ObjectTypes.First(ot => ot.Name == "PropKindPortfolio");
        var mainPosProp = portfolio.Properties.First(p => p.Name == "MainPosition");

        await Assert.That(mainPosProp.Kind).IsEqualTo(PropertyKind.Reference);
    }

    [Test]
    public async Task PropertyKind_ComputedProperty_InferredAsComputed()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<PropKindOntology>();

        var graph = graphBuilder.Build();
        var portfolio = graph.ObjectTypes.First(ot => ot.Name == "PropKindPortfolio");
        var totalValueProp = portfolio.Properties.First(p => p.Name == "TotalValue");

        await Assert.That(totalValueProp.Kind).IsEqualTo(PropertyKind.Computed);
    }

    [Test]
    public async Task PropertyKind_CollectionOfObjectType_InferredAsScalar()
    {
        // Collections are not directly matched as references at the property level;
        // they are modeled via Links. So a generic collection type stays Scalar.
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<PropKindOntology>();

        var graph = graphBuilder.Build();
        var position = graph.ObjectTypes.First(ot => ot.Name == "PropKindPosition");
        var quantityProp = position.Properties.First(p => p.Name == "Quantity");

        await Assert.That(quantityProp.Kind).IsEqualTo(PropertyKind.Scalar);
    }

    [Test]
    public async Task PropertyKind_Default_IsScalar()
    {
        var descriptor = new PropertyDescriptor("Test", typeof(string));

        await Assert.That(descriptor.Kind).IsEqualTo(PropertyKind.Scalar);
    }
}
