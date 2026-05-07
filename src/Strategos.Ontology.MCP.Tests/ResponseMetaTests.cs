using System.Text.Json;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

namespace Strategos.Ontology.MCP.Tests;

public class ResponseMetaTests
{
    [Test]
    public async Task ResponseMeta_Construction_StoresOntologyVersion()
    {
        // Arrange & Act
        var meta = new ResponseMeta("sha256:abc");

        // Assert — value flows through unchanged via the positional record.
        await Assert.That(meta.OntologyVersion).IsEqualTo("sha256:abc");
    }

    [Test]
    public async Task ResponseMeta_JsonSerialization_EmitsOntologyVersionPropertyName()
    {
        // Arrange — wire-format mapping at the result-record level (B6/B7/B8)
        // uses [JsonPropertyName("_meta")] on the Meta member; the inner
        // ResponseMeta property keeps its PascalCase JSON name.
        var wrap = new Wrap(new ResponseMeta("sha256:abc"));

        // Act
        var json = JsonSerializer.Serialize(wrap);

        // Assert
        await Assert.That(json).Contains("\"OntologyVersion\"");
        await Assert.That(json).Contains("sha256:abc");
    }

    [Test]
    public async Task ForGraph_StampsSha256PrefixOnGraphVersion()
    {
        // Arrange — bare hex on the graph; the wire form gains the prefix.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();

        // Act
        var meta = ResponseMeta.ForGraph(graph);

        // Assert
        await Assert.That(meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
        await Assert.That(graph.Version.StartsWith("sha256:")).IsFalse();
    }

    [Test]
    public async Task WireFormat_IsIdempotent_DoesNotDoublePrefix()
    {
        // The internal helper must not double-prefix already-prefixed values.
        var bare = "abcdef0123";
        var once = ResponseMeta.WireFormat(bare);
        var twice = ResponseMeta.WireFormat(once);

        await Assert.That(once).IsEqualTo("sha256:" + bare);
        await Assert.That(twice).IsEqualTo(once);
    }

    private sealed record Wrap(ResponseMeta Meta);
}
