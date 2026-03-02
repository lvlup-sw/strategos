// =============================================================================
// <copyright file="InterfacePropertyMappingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

// --- Test domain models for interface property mapping tests ---

public interface IMappableInterface
{
    string DisplayName { get; }
    int Score { get; }
}

public record MappedEntity(string Name, int Rating, string Description);

public class InterfacePropertyMappingValidOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IMappableInterface>("Mappable", iface =>
        {
            iface.Property(i => i.DisplayName);
            iface.Property(i => i.Score);
        });

        builder.Object<MappedEntity>(obj =>
        {
            obj.Key(e => e.Name);
            obj.Property(e => e.Name).Required();
            obj.Property(e => e.Rating);
            obj.Implements<IMappableInterface>(map =>
            {
                map.Via(e => e.Name, i => i.DisplayName);
                map.Via(e => e.Rating, i => i.Score);
            });
        });
    }
}

public class InterfacePropertyMappingMissingViaOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IMappableInterface>("Mappable", iface =>
        {
            iface.Property(i => i.DisplayName);
            iface.Property(i => i.Score);
        });

        builder.Object<MappedEntity>(obj =>
        {
            obj.Key(e => e.Name);
            obj.Property(e => e.Name).Required();
            obj.Property(e => e.Rating);
            // Only maps one property, missing Score
            obj.Implements<IMappableInterface>(map =>
            {
                map.Via(e => e.Name, i => i.DisplayName);
            });
        });
    }
}

public class InterfacePropertyMappingNameMatchOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IIdentifiable>("IIdentifiable", iface =>
        {
            iface.Property(i => i.Id);
        });

        builder.Object<IdentifiablePosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Id).Required();
            obj.Property(p => p.Symbol).Required();
            obj.Implements<IIdentifiable>(map =>
            {
                // No Via() needed because property names match directly
            });
        });
    }
}

public class InterfacePropertyMappingTests
{
    [Test]
    public async Task ValidateInterfaceImplementations_ViaMappingCoversProperty_NoDiagnostic()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InterfacePropertyMappingValidOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(1);
        await Assert.That(graph.ObjectTypes[0].ImplementedInterfaces).HasCount().EqualTo(1);
    }

    [Test]
    public async Task ValidateInterfaceImplementations_MissingViaMapping_Throws()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InterfacePropertyMappingMissingViaOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task ValidateInterfaceImplementations_NameMatch_NoViaMappingNeeded()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InterfacePropertyMappingNameMatchOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(1);
        await Assert.That(graph.ObjectTypes[0].ImplementedInterfaces).HasCount().EqualTo(1);
    }

    [Test]
    public async Task InterfacePropertyMappings_PersistedInDescriptor()
    {
        var builder = new ObjectTypeBuilder<MappedEntity>("test");
        builder.Property(e => e.Name).Required();
        builder.Property(e => e.Rating);
        builder.Implements<IMappableInterface>(map =>
        {
            map.Via(e => e.Name, i => i.DisplayName);
            map.Via(e => e.Rating, i => i.Score);
        });

        var descriptor = builder.Build();

        await Assert.That(descriptor.InterfacePropertyMappings).HasCount().EqualTo(2);
        await Assert.That(descriptor.InterfacePropertyMappings[0].SourcePropertyName).IsEqualTo("Name");
        await Assert.That(descriptor.InterfacePropertyMappings[0].TargetPropertyName).IsEqualTo("DisplayName");
        await Assert.That(descriptor.InterfacePropertyMappings[0].InterfaceName).IsEqualTo("IMappableInterface");
    }
}
