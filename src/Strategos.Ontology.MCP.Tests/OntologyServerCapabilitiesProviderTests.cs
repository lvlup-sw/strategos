namespace Strategos.Ontology.MCP.Tests;

public class OntologyServerCapabilitiesProviderTests
{
    [Test]
    public async Task GetServerCapabilities_ReturnsCurrentGraphVersion()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var provider = new OntologyServerCapabilitiesProvider(graph);

        // Act
        var capabilities = provider.GetServerCapabilities();

        // Assert — wire-format prefixed version, same envelope as ResponseMeta
        await Assert.That(capabilities.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task OntologyServerCapabilitiesProvider_NullGraph_Throws()
    {
        // The constructor must reject a null graph eagerly so callers see the
        // bug at composition time rather than later at GetServerCapabilities().
        await Assert.That(() => new OntologyServerCapabilitiesProvider(null!))
            .Throws<ArgumentNullException>();
    }
}
