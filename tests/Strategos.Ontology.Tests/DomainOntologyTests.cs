using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests;

public class TestDomainOntology : DomainOntology
{
    public override string DomainName => "TestDomain";

    public bool DefineCalled { get; private set; }
    public IOntologyBuilder? ReceivedBuilder { get; private set; }

    protected override void Define(IOntologyBuilder builder)
    {
        DefineCalled = true;
        ReceivedBuilder = builder;

        builder.Object<TestModel>(obj =>
        {
            obj.Key(m => m.Id);
            obj.Property(m => m.Name).Required();
        });
    }
}

public record TestModel(Guid Id, string Name);

public class DomainOntologyTests
{
    [Test]
    public async Task DomainOntology_DomainName_ReturnsSubclassValue()
    {
        var ontology = new TestDomainOntology();

        await Assert.That(ontology.DomainName).IsEqualTo("TestDomain");
    }

    [Test]
    public async Task DomainOntology_Define_ReceivesOntologyBuilder()
    {
        var ontology = new TestDomainOntology();
        var builder = new OntologyBuilder("TestDomain");

        ontology.Build(builder);

        await Assert.That(ontology.DefineCalled).IsTrue();
        await Assert.That(ontology.ReceivedBuilder).IsNotNull();
    }

    [Test]
    public async Task DomainOntology_Subclass_CanDefineObjectTypes()
    {
        var ontology = new TestDomainOntology();
        var builder = new OntologyBuilder("TestDomain");

        ontology.Build(builder);

        await Assert.That(builder.ObjectTypes.Count).IsEqualTo(1);
        await Assert.That(builder.ObjectTypes[0].Name).IsEqualTo("TestModel");
    }
}
