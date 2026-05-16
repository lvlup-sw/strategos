using System.Text.Json;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// PR-C Task 26: <see cref="HybridMeta"/> sub-record — defaults & JSON wire shape.
/// </summary>
public sealed class HybridMetaTests
{
    [Test]
    public async Task Defaults_HybridFalse_FusionMethodNull_AllOptionalsNull()
    {
        // Positional record; all optional params default to null. Hybrid=false
        // represents the degraded shapes (no-keyword-provider, sparse-failed) per §6.5.
        var meta = new HybridMeta(Hybrid: false);

        await Assert.That(meta.Hybrid).IsFalse();
        await Assert.That(meta.FusionMethod).IsNull();
        await Assert.That(meta.Degraded).IsNull();
        await Assert.That(meta.DenseTopScore).IsNull();
        await Assert.That(meta.SparseTopScore).IsNull();
        await Assert.That(meta.BmSaturationThreshold).IsNull();
    }

    [Test]
    public async Task JsonShape_HealthyReciprocal_EmitsExpectedKeys()
    {
        var meta = new HybridMeta(
            Hybrid: true,
            FusionMethod: "reciprocal",
            Degraded: null,
            DenseTopScore: 0.91,
            SparseTopScore: 17.4,
            BmSaturationThreshold: 18.0);

        var json = JsonSerializer.Serialize(meta);

        // Pinned wire keys from design §6.5.
        await Assert.That(json).Contains("\"hybrid\":true");
        await Assert.That(json).Contains("\"fusionMethod\":\"reciprocal\"");
        await Assert.That(json).Contains("\"denseTopScore\":0.91");
        await Assert.That(json).Contains("\"sparseTopScore\":17.4");
        await Assert.That(json).Contains("\"bmSaturationThreshold\":18");
        // Degraded is null on healthy shape; null-optionals must be omitted.
        await Assert.That(json).DoesNotContain("\"degraded\"");
    }

    [Test]
    public async Task JsonShape_OmitsNullOptionals()
    {
        // Degraded shape: only "hybrid" and "degraded" keys present (per §6.5 example).
        var meta = new HybridMeta(
            Hybrid: false,
            Degraded: "sparse-failed");

        var json = JsonSerializer.Serialize(meta);

        await Assert.That(json).Contains("\"hybrid\":false");
        await Assert.That(json).Contains("\"degraded\":\"sparse-failed\"");
        await Assert.That(json).DoesNotContain("\"fusionMethod\"");
        await Assert.That(json).DoesNotContain("\"denseTopScore\"");
        await Assert.That(json).DoesNotContain("\"sparseTopScore\"");
        await Assert.That(json).DoesNotContain("\"bmSaturationThreshold\"");
    }
}
