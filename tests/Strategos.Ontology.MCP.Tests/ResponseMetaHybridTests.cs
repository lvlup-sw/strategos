using System.Text.Json;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// PR-C Task 27: <see cref="ResponseMeta.Hybrid"/> extension surface.
/// </summary>
public sealed class ResponseMetaHybridTests
{
    [Test]
    public async Task Default_HybridIsNull()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();

        var meta = ResponseMeta.ForGraph(graph);

        await Assert.That(meta.Hybrid).IsNull();
    }

    [Test]
    public async Task JsonShape_HybridNull_KeyAbsent()
    {
        // Backward-compat with 2.5.0 snapshots: when Hybrid is null the wire payload
        // must not contain a "hybrid" key (design §6.5 hard requirement).
        var meta = new ResponseMeta("sha256:abc");

        var json = JsonSerializer.Serialize(meta);

        await Assert.That(json).Contains("\"ontologyVersion\":\"sha256:abc\"");
        await Assert.That(json).DoesNotContain("\"hybrid\"");
    }

    [Test]
    public async Task JsonShape_HybridSet_KeyPresent()
    {
        var meta = new ResponseMeta("sha256:abc")
        {
            Hybrid = new HybridMeta(Hybrid: true, FusionMethod: "reciprocal"),
        };

        var json = JsonSerializer.Serialize(meta);

        // Outer key (on ResponseMeta) plus inner Hybrid.Hybrid.
        await Assert.That(json).Contains("\"hybrid\":{");
        await Assert.That(json).Contains("\"fusionMethod\":\"reciprocal\"");
    }

    [Test]
    public async Task With_HybridSet_PreservesOntologyVersion()
    {
        // Records' with-expression must keep OntologyVersion intact when extending Hybrid.
        var baseMeta = new ResponseMeta("sha256:abc");
        var withHybrid = baseMeta with { Hybrid = new HybridMeta(Hybrid: false, Degraded: "sparse-failed") };

        await Assert.That(withHybrid.OntologyVersion).IsEqualTo("sha256:abc");
        await Assert.That(withHybrid.Hybrid).IsNotNull();
        await Assert.That(withHybrid.Hybrid!.Degraded).IsEqualTo("sparse-failed");
    }
}
