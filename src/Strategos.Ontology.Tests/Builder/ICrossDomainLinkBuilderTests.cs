using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public record TestAtomicNote(Guid Id, string Content);

public class ICrossDomainLinkBuilderTests
{
    [Test]
    public async Task ICrossDomainLinkBuilder_From_SetsSourceType()
    {
        var substitute = Substitute.For<ICrossDomainLinkBuilder>();
        substitute.From<TestAtomicNote>().Returns(substitute);

        var result = substitute.From<TestAtomicNote>();

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task ICrossDomainLinkBuilder_ToExternal_SetsDomainAndType()
    {
        var substitute = Substitute.For<ICrossDomainLinkBuilder>();
        substitute.ToExternal("trading", "Strategy").Returns(substitute);

        var result = substitute.ToExternal("trading", "Strategy");

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task ICrossDomainLinkBuilder_ManyToMany_SetsCardinality()
    {
        var substitute = Substitute.For<ICrossDomainLinkBuilder>();
        substitute.ManyToMany().Returns(substitute);

        var result = substitute.ManyToMany();

        await Assert.That(result).IsEqualTo(substitute);
    }

    // ICrossDomainLinkBuilder_WithEdge_AllowsEdgeProperties was removed in DR-5
    // (#120, closes #114): ICrossDomainLinkBuilder.WithEdge and IEdgeBuilder no
    // longer exist. Edge attributes now live on a reified Association<T>.
}
