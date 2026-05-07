using System.Text.RegularExpressions;
using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests;

public class OntologyGraphVersionTests
{
    private static readonly Regex LowercaseHex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    [Test]
    public async Task Version_OnEmptyGraph_ReturnsLowercaseSha256Hex()
    {
        var graph = new OntologyGraphBuilder().Build();

        await Assert.That(graph.Version).IsNotNull();
        await Assert.That(graph.Version.Length).IsEqualTo(64);
        await Assert.That(LowercaseHex64.IsMatch(graph.Version)).IsTrue();
    }

    [Test]
    public async Task Version_OnEmptyGraph_DoesNotIncludeSha256Prefix()
    {
        // Wire-format note: the "sha256:" prefix is added at the MCP _meta-emission
        // boundary (Track B's ResponseMeta factory), NOT on the property itself.
        // OntologyGraph.Version is bare hex.
        var graph = new OntologyGraphBuilder().Build();

        await Assert.That(graph.Version.StartsWith("sha256:")).IsFalse();
    }

    [Test]
    public async Task Version_BuiltTwice_ReturnsSameHash()
    {
        var graphA = new OntologyGraphBuilder().Build();
        var graphB = new OntologyGraphBuilder().Build();

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }
}
