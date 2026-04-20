using System.Text.Json;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Tests for the <see cref="ResponseMeta"/> record — per-response metadata
/// threaded through every ontology MCP tool result.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.5
/// </summary>
public class ResponseMetaTests
{
    [Test]
    public async Task ResponseMeta_Construction_StoresOntologyVersion()
    {
        var meta = new ResponseMeta("sha256:abc");

        await Assert.That(meta.OntologyVersion).IsEqualTo("sha256:abc");
    }

    [Test]
    public async Task ResponseMeta_JsonSerialization_EmitsUnderscoreMetaFriendlyForm()
    {
        var wrap = new Wrap(new ResponseMeta("v1"));

        var json = JsonSerializer.Serialize(wrap);

        await Assert.That(json).Contains("OntologyVersion");
    }

    private sealed record Wrap(ResponseMeta Meta);
}
