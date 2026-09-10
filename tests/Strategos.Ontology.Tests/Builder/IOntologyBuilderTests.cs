using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public class IOntologyBuilderTests
{
    [Test]
    public async Task IOntologyBuilder_Object_AcceptsConfigAction()
    {
        var substitute = Substitute.For<IOntologyBuilder>();

        substitute.Object<TestPosition>(Arg.Any<Action<IObjectTypeBuilder<TestPosition>>>());

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task IOntologyBuilder_Interface_AcceptsConfigAction()
    {
        var substitute = Substitute.For<IOntologyBuilder>();

        substitute.Interface<ITestSearchable>(
            "Searchable",
            Arg.Any<Action<IInterfaceBuilder<ITestSearchable>>>());

        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task IOntologyBuilder_CrossDomainLink_ReturnsLinkBuilder()
    {
        var substitute = Substitute.For<IOntologyBuilder>();
        var linkBuilder = Substitute.For<ICrossDomainLinkBuilder>();
        substitute.CrossDomainLink("KnowledgeInformsStrategy").Returns(linkBuilder);

        var result = substitute.CrossDomainLink("KnowledgeInformsStrategy");

        await Assert.That(result).IsEqualTo(linkBuilder);
    }
}
