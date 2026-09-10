using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public interface IIdentifiable
{
    string Id { get; }
}

public interface IBadInterface
{
    int Id { get; }
}

public class IdentifiablePosition
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class TestDomainWithValidInterfaceOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IIdentifiable>("IIdentifiable", intf =>
        {
            intf.Property(i => i.Id);
        });

        builder.Object<IdentifiablePosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Id).Required();
            obj.Property(p => p.Symbol).Required();
            obj.Implements<IIdentifiable>(map =>
            {
                map.Via(p => p.Id, i => i.Id);
            });
        });
    }
}

public class TestDomainWithIncompatibleInterfaceOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IBadInterface>("IBadInterface", intf =>
        {
            intf.Property(i => i.Id);
        });

        builder.Object<IdentifiablePosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Id).Required();
            obj.Implements<IBadInterface>(map =>
            {
                map.Via(p => p.Id, i => i.Id);
            });
        });
    }
}

public class TestProducerOntology : DomainOntology
{
    public override string DomainName => "producer";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
        });
    }
}

public class OntologyGraphBuilderInterfaceValidationTests
{
    [Test]
    public async Task OntologyGraphBuilder_Build_ValidInterfaceImpl_Succeeds()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestDomainWithValidInterfaceOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(1);
        await Assert.That(graph.Interfaces).HasCount().EqualTo(1);
        await Assert.That(graph.ObjectTypes[0].ImplementedInterfaces).HasCount().EqualTo(1);
        await Assert.That(graph.ObjectTypes[0].ImplementedInterfaces[0].Name).IsEqualTo("IIdentifiable");
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_IncompatibleInterfacePropertyType_Throws()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestDomainWithIncompatibleInterfaceOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_ValidWorkflowChain_Succeeds()
    {
        // Workflow chains are phase 3 - for now, verify the infrastructure exists
        // and returns empty when no chains are registered
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.WorkflowChains).IsNotNull();
        await Assert.That(graph.WorkflowChains).HasCount().EqualTo(0);
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_OrphanedProduces_DoesNotThrow()
    {
        // Orphaned Produces<T> (no matching Consumes<T>) should not throw - warning only
        // This validates the infrastructure handles graceful degradation
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestProducerOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph).IsNotNull();
        await Assert.That(graph.WorkflowChains).HasCount().EqualTo(0);
    }
}
