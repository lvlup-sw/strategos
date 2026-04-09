using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class OntologyBuilderTests
{
    [Test]
    public async Task OntologyBuilder_Object_CollectsObjectTypeDescriptor()
    {
        var builder = new OntologyBuilder("Trading");

        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });

        await Assert.That(builder.ObjectTypes.Count).IsEqualTo(1);
        await Assert.That(builder.ObjectTypes[0].Name).IsEqualTo("TestPosition");
    }

    [Test]
    public async Task OntologyBuilder_Interface_CollectsInterfaceDescriptor()
    {
        var builder = new OntologyBuilder("Trading");

        builder.Interface<ITestSearchable>("Searchable", iface =>
        {
            iface.Property(s => s.Title);
        });

        await Assert.That(builder.Interfaces.Count).IsEqualTo(1);
        await Assert.That(builder.Interfaces[0].Name).IsEqualTo("Searchable");
    }

    [Test]
    public async Task OntologyBuilder_CrossDomainLink_CollectsLinkDefinition()
    {
        var builder = new OntologyBuilder("Trading");

        builder.CrossDomainLink("KnowledgeInformsStrategy")
            .From<TestAtomicNote>()
            .ToExternal("trading", "Strategy")
            .ManyToMany();

        await Assert.That(builder.CrossDomainLinks.Count).IsEqualTo(1);
        await Assert.That(builder.CrossDomainLinks[0].Name).IsEqualTo("KnowledgeInformsStrategy");
    }

    [Test]
    public async Task OntologyBuilder_MultipleObjects_AllCollected()
    {
        var builder = new OntologyBuilder("Trading");

        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
        });
        builder.Object<TestTradeOrder>(obj =>
        {
            obj.Key(o => o.Id);
        });

        await Assert.That(builder.ObjectTypes.Count).IsEqualTo(2);
        await Assert.That(builder.ObjectTypes[0].Name).IsEqualTo("TestPosition");
        await Assert.That(builder.ObjectTypes[1].Name).IsEqualTo("TestTradeOrder");
    }

    [Test]
    public async Task OntologyBuilder_ObjectWithExplicitName_RegistersDescriptorWithThatName()
    {
        var builder = new OntologyBuilder("Trading");

        builder.Object<TestPosition>("trading_documents", obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });

        await Assert.That(builder.ObjectTypes.Count).IsEqualTo(1);
        await Assert.That(builder.ObjectTypes[0].Name).IsEqualTo("trading_documents");
        await Assert.That(builder.ObjectTypes[0].ClrType).IsEqualTo(typeof(TestPosition));
    }
}
