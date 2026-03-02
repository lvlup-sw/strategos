// =============================================================================
// <copyright file="ObjectKindTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

// --- Test domain models for ObjectKind tests ---

public record ObjKindEntity(Guid Id, string Name);

public record ObjKindProcess(Guid Id, string WorkflowId);

public class ObjKindOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ObjKindEntity>(obj =>
        {
            obj.Key(e => e.Id);
            obj.Property(e => e.Name).Required();
        });

        builder.Object<ObjKindProcess>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.WorkflowId).Required();
            obj.Kind(ObjectKind.Process);
        });
    }
}

public class ObjectKindTests
{
    [Test]
    public async Task ObjectKind_Entity_Default()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<ObjKindOntology>();

        var graph = graphBuilder.Build();
        var entity = graph.ObjectTypes.First(ot => ot.Name == "ObjKindEntity");

        await Assert.That(entity.Kind).IsEqualTo(ObjectKind.Entity);
    }

    [Test]
    public async Task ObjectKind_Process_SetExplicitly()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<ObjKindOntology>();

        var graph = graphBuilder.Build();
        var process = graph.ObjectTypes.First(ot => ot.Name == "ObjKindProcess");

        await Assert.That(process.Kind).IsEqualTo(ObjectKind.Process);
    }

    [Test]
    public async Task ObjectKind_NotSet_DefaultsToEntity()
    {
        var descriptor = new ObjectTypeDescriptor("Test", typeof(ObjKindEntity), "test");

        await Assert.That(descriptor.Kind).IsEqualTo(ObjectKind.Entity);
    }
}
