// =============================================================================
// <copyright file="InverseLinkTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests;

// --- Test domain models for inverse link tests ---

public record InvDepartment(Guid Id, string Name);

public record InvEmployee(Guid Id, string Name);

public record InvProject(Guid Id, string Title);

// --- Ontology with valid bidirectional inverse links ---

public class InverseLinkValidOntology : DomainOntology
{
    public override string DomainName => "org";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<InvDepartment>(obj =>
        {
            obj.Key(d => d.Id);
            obj.Property(d => d.Name).Required();
            obj.HasMany<InvEmployee>("Employees").Inverse("Department");
        });

        builder.Object<InvEmployee>(obj =>
        {
            obj.Key(e => e.Id);
            obj.Property(e => e.Name).Required();
            obj.HasOne<InvDepartment>("Department").Inverse("Employees");
        });
    }
}

// --- Ontology with missing inverse target ---

public class InverseLinkMissingTargetOntology : DomainOntology
{
    public override string DomainName => "org";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<InvDepartment>(obj =>
        {
            obj.Key(d => d.Id);
            obj.Property(d => d.Name).Required();
            obj.HasMany<InvEmployee>("Employees").Inverse("Department");
        });

        builder.Object<InvEmployee>(obj =>
        {
            obj.Key(e => e.Id);
            obj.Property(e => e.Name).Required();
            // No link named "Department" -- inverse target is missing
        });
    }
}

// --- Ontology with asymmetric inverse ---

public class InverseLinkAsymmetricOntology : DomainOntology
{
    public override string DomainName => "org";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<InvDepartment>(obj =>
        {
            obj.Key(d => d.Id);
            obj.Property(d => d.Name).Required();
            obj.HasMany<InvEmployee>("Employees").Inverse("Department");
        });

        builder.Object<InvEmployee>(obj =>
        {
            obj.Key(e => e.Id);
            obj.Property(e => e.Name).Required();
            // Has the link but declares an inverse pointing to wrong link
            obj.HasOne<InvDepartment>("Department").Inverse("WrongLinkName");
        });
    }
}

// --- Tests ---

public class InverseLinkTests
{
    [Test]
    public async Task HasMany_WithInverse_SetsInverseLinkName()
    {
        var builder = new ObjectTypeBuilder<InvDepartment>("org");
        builder.HasMany<InvEmployee>("Employees").Inverse("Department");

        var descriptor = builder.Build();

        await Assert.That(descriptor.Links[0].InverseLinkName).IsEqualTo("Department");
    }

    [Test]
    public async Task HasOne_WithInverse_SetsInverseLinkName()
    {
        var builder = new ObjectTypeBuilder<InvEmployee>("org");
        builder.HasOne<InvDepartment>("Department").Inverse("Employees");

        var descriptor = builder.Build();

        await Assert.That(descriptor.Links[0].InverseLinkName).IsEqualTo("Employees");
    }

    [Test]
    public async Task InverseLink_MissingTargetLink_ThrowsOnBuild()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InverseLinkMissingTargetOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task InverseLink_BidirectionalConsistency_Validates()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InverseLinkValidOntology>();

        var graph = graphBuilder.Build();

        var dept = graph.ObjectTypes.First(ot => ot.Name == "InvDepartment");
        var emp = graph.ObjectTypes.First(ot => ot.Name == "InvEmployee");

        await Assert.That(dept.Links[0].InverseLinkName).IsEqualTo("Department");
        await Assert.That(emp.Links[0].InverseLinkName).IsEqualTo("Employees");
    }

    [Test]
    public async Task InverseLink_AsymmetricDeclaration_ThrowsOnBuild()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InverseLinkAsymmetricOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task GetInverseLinks_ReturnsMatchingInverse()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InverseLinkValidOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        var inverseLinks = query.GetInverseLinks("InvDepartment", "Employees");

        await Assert.That(inverseLinks).HasCount().EqualTo(1);
        await Assert.That(inverseLinks[0].Name).IsEqualTo("Department");
    }

    [Test]
    public async Task GetInverseLinks_NoInverse_ReturnsEmpty()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<InverseLinkValidOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        var inverseLinks = query.GetInverseLinks("InvDepartment", "NonExistentLink");

        await Assert.That(inverseLinks).HasCount().EqualTo(0);
    }
}
