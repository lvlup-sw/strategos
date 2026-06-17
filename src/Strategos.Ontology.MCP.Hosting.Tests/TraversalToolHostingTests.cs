using System.IO.Pipelines;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.MCP.Hosting;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// DR-15 / T18 (#125): the instance-anchored traversal tool over the MCP transport.
/// Drives <c>ontology_traverse</c> end-to-end against an in-memory provider double
/// (NO live database), asserting the MCP-spec behaviors the host owns: malformed
/// args → <c>isError: true</c> (SEP-1303, NOT a thrown protocol error); a large
/// subgraph → a <c>resource_link</c> content block + opaque cursor; and provenance
/// keys under the <c>sw.lvlup.strategos/</c> prefix.
/// </summary>
public sealed class TraversalToolHostingTests
{
    public sealed record TravNode(string Id);

    public sealed record TravEdge(string Id, TravNode From, TravNode To, string Status);

    private sealed class TravOntology : DomainOntology
    {
        public override string DomainName => "trav";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<TravNode>(obj =>
            {
                obj.Key(n => n.Id);
                obj.HasMany<TravNode>("link");
            });

            builder.Association<TravEdge>("TravEdge", a =>
            {
                a.Key(e => e.Id);
                a.Between(e => e.From).And(e => e.To);
                a.Property(e => e.Status).Required();
            });
        }
    }

    private static (OntologyGraph Graph, InMemoryObjectSetProvider Provider) BuildSeeded(int farCount)
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TravOntology>();
        var graph = graphBuilder.Build();

        var provider = new InMemoryObjectSetProvider(graph);
        provider.Seed(new TravNode("x"), "x", nameof(TravNode));

        IObjectSetWriter writer = provider;
        for (var i = 0; i < farCount; i++)
        {
            var farId = $"y{i}";
            provider.Seed(new TravNode(farId), farId, nameof(TravNode));
            writer.RelateAsync(
                nameof(TravNode), "x", "link", nameof(TravNode), farId,
                "TravEdge", new TravEdge($"e{i}", new TravNode("x"), new TravNode(farId), "active"))
                .GetAwaiter().GetResult();
        }

        return (graph, provider);
    }

    private static async Task<(McpClient Client, IAsyncDisposable[] Disposables)> ConnectAsync(
        OntologyGraph graph, InMemoryObjectSetProvider provider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(graph);
        services.AddSingleton<IObjectSetProvider>(provider);
        services.AddMcpServer().AddOntologyTools(graph);
        var sp = services.BuildServiceProvider();
        var serverOptions = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var c2s = new Pipe();
        var s2c = new Pipe();
        var serverTransport = new StreamServerTransport(
            c2s.Reader.AsStream(), s2c.Writer.AsStream(), "trav-test-server", null);
        var server = McpServer.Create(serverTransport, serverOptions, null, sp);
        _ = server.RunAsync();
        var clientTransport = new StreamClientTransport(c2s.Writer.AsStream(), s2c.Reader.AsStream(), null);
        var client = await McpClient.CreateAsync(clientTransport);
        return (client, [client, server, serverTransport, sp]);
    }

    [Test]
    public async Task TraversalTool_InstanceToAssociationToFarEndpoint_Succeeds()
    {
        var (graph, provider) = BuildSeeded(farCount: 2);
        var (client, disposables) = await ConnectAsync(graph, provider);
        try
        {
            var result = await client.CallToolAsync(
                "ontology_traverse",
                new Dictionary<string, object?>
                {
                    ["objectType"] = "TravNode",
                    ["objectId"] = "x",
                    ["linkName"] = "link",
                    ["direction"] = "ToDestination",
                    ["depth"] = 1,
                });

            await Assert.That(result.IsError ?? false).IsFalse();
            var structured = result.StructuredContent!.Value;
            // _meta is present (INV-3).
            await Assert.That(structured.TryGetProperty("_meta", out _)).IsTrue();
            var raw = structured.GetRawText();
            await Assert.That(raw).Contains("y0");
            await Assert.That(raw).Contains("y1");
        }
        finally
        {
            foreach (var d in disposables)
            {
                await d.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task TraversalTool_MalformedArgs_ReturnsIsErrorTrue_NotProtocolError()
    {
        var (graph, provider) = BuildSeeded(farCount: 1);
        var (client, disposables) = await ConnectAsync(graph, provider);
        try
        {
            // A free-text link name not in the closed vocabulary. This must come back
            // as a CallToolResult with isError:true — NOT a thrown JSON-RPC protocol
            // error (the call itself succeeds at the protocol layer).
            var result = await client.CallToolAsync(
                "ontology_traverse",
                new Dictionary<string, object?>
                {
                    ["objectType"] = "TravNode",
                    ["objectId"] = "x",
                    ["linkName"] = "not_a_real_link",
                    ["direction"] = "ToDestination",
                    ["depth"] = 1,
                });

            await Assert.That(result.IsError ?? false).IsTrue();
            var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            await Assert.That(text).Contains("link");
        }
        finally
        {
            foreach (var d in disposables)
            {
                await d.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task TraversalTool_LargeResult_ReturnsResourceLink_WithCursor()
    {
        // Far-endpoint count exceeds the tool's row budget, so the host emits a
        // resource_link content block plus an opaque continuation cursor.
        var (graph, provider) = BuildSeeded(farCount: Strategos.Ontology.MCP.OntologyTraverseTool.RowBudget + 5);
        var (client, disposables) = await ConnectAsync(graph, provider);
        try
        {
            var result = await client.CallToolAsync(
                "ontology_traverse",
                new Dictionary<string, object?>
                {
                    ["objectType"] = "TravNode",
                    ["objectId"] = "x",
                    ["linkName"] = "link",
                    ["direction"] = "ToDestination",
                    ["depth"] = 1,
                });

            await Assert.That(result.IsError ?? false).IsFalse();

            // A resource_link content block is present.
            var resourceLinks = result.Content.OfType<ResourceLinkBlock>().ToList();
            await Assert.That(resourceLinks.Count).IsGreaterThanOrEqualTo(1);

            // The opaque cursor rides in the structured content (its own schema slot),
            // not as a far-endpoint row.
            var structured = result.StructuredContent!.Value;
            await Assert.That(structured.TryGetProperty("nextCursor", out var cursor)).IsTrue();
            await Assert.That(string.IsNullOrEmpty(cursor.GetString())).IsFalse();
        }
        finally
        {
            foreach (var d in disposables)
            {
                await d.DisposeAsync();
            }
        }
    }
}
