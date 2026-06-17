using System.IO.Pipelines;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Strategos.Ontology;
using Strategos.Ontology.MCP.Hosting;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// DR-14 (#113): the ontology MCP tools registered by <see cref="OntologyMcpServerBuilderExtensions"/>
/// must dispatch against the DI-resolved <see cref="IObjectSetProvider"/>, not a stub. These tests
/// drive the tool end-to-end over the in-memory MCP duplex transport against an in-memory provider
/// double (NO live database).
/// </summary>
public sealed class ProviderBoundDispatchTests
{
    /// <summary>
    /// Boots an MCP client/server pair over an in-memory duplex transport with the ontology tools
    /// registered against the supplied <paramref name="services"/>. The caller seeds DI before
    /// invoking. Returns the connected client; the returned disposables keep the transport alive.
    /// </summary>
    private static async Task<(McpClient Client, IAsyncDisposable[] Disposables, Task ServerRun)> ConnectAsync(
        ServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        var serverOptions = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var serverTransport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            serverName: "ontology-test-server",
            loggerFactory: null);

        var server = McpServer.Create(
            serverTransport,
            serverOptions,
            loggerFactory: null,
            serviceProvider: provider);

        var serverRun = server.RunAsync();

        var clientTransport = new StreamClientTransport(
            serverInput: clientToServer.Writer.AsStream(),
            serverOutput: serverToClient.Reader.AsStream(),
            loggerFactory: null);

        var client = await McpClient.CreateAsync(clientTransport);

        return (client, [client, server, serverTransport, provider], serverRun);
    }

    [Test]
    public async Task AddOntologyTools_RegistersProviderBoundHandlers_NotStub()
    {
        // Arrange — a graph-aware in-memory provider seeded with one TestPosition row.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var objectSets = new InMemoryObjectSetProvider(graph);
        objectSets.Seed(new TestPosition { Id = "P-1", Symbol = "ACME", Quantity = 10m }, "ACME position");

        var services = new ServiceCollection();
        services.AddSingleton(graph);
        services.AddSingleton<IObjectSetProvider>(objectSets);
        services.AddMcpServer().AddOntologyTools(graph);

        var (client, disposables, serverRun) = await ConnectAsync(services);

        try
        {
            // Act — call ontology_query for the seeded type.
            var result = await client.CallToolAsync(
                "ontology_query",
                new Dictionary<string, object?> { ["objectType"] = "TestPosition" });

            // Assert — the handler is NOT the stub. The stub echoed {"tool":"ontology_query"}
            // with no provider contact; a provider-bound handler returns the seeded row's symbol.
            await Assert.That(result.IsError ?? false).IsFalse();
            var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
            await Assert.That(text).DoesNotContain("\"tool\":\"ontology_query\"");
            await Assert.That(text).Contains("ACME");

            // The server loop must not have faulted while serving the call — a faulted
            // ServerRun would otherwise be silently discarded.
            await Assert.That(serverRun.IsFaulted).IsFalse();
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
    public async Task Query_ExecutesAgainstConfiguredProvider_ReturnsRealRows()
    {
        // Arrange — seed two rows into the configured in-memory provider.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var objectSets = new InMemoryObjectSetProvider(graph);
        objectSets.Seed(new TestPosition { Id = "P-1", Symbol = "ACME", Quantity = 10m }, "ACME position");
        objectSets.Seed(new TestPosition { Id = "P-2", Symbol = "WIDGET", Quantity = 5m }, "WIDGET position");

        var services = new ServiceCollection();
        services.AddSingleton(graph);
        services.AddSingleton<IObjectSetProvider>(objectSets);
        services.AddMcpServer().AddOntologyTools(graph);

        var (client, disposables, serverRun) = await ConnectAsync(services);

        try
        {
            // Act
            var result = await client.CallToolAsync(
                "ontology_query",
                new Dictionary<string, object?> { ["objectType"] = "TestPosition" });

            // Assert — structured content carries the two seeded rows plus the INV-3 _meta envelope.
            await Assert.That(result.IsError ?? false).IsFalse();
            await Assert.That(result.StructuredContent.HasValue).IsTrue();

            var structured = result.StructuredContent!.Value;
            var items = structured.GetProperty("items");
            await Assert.That(items.GetArrayLength()).IsEqualTo(2);

            // INV-3: every tool result preserves the _meta envelope (ontologyVersion).
            await Assert.That(structured.TryGetProperty("_meta", out var meta)).IsTrue();
            await Assert.That(meta.TryGetProperty("ontologyVersion", out _)).IsTrue();

            // The real rows came back — both seeded symbols are present.
            var raw = structured.GetRawText();
            await Assert.That(raw).Contains("ACME");
            await Assert.That(raw).Contains("WIDGET");

            // The server loop must not have faulted while serving the call.
            await Assert.That(serverRun.IsFaulted).IsFalse();
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
    public async Task AddOntologyTools_NoArgs_ResolvesGraphFromDiAndDispatches()
    {
        // Arrange — the DI-resolved overload reads the OntologyGraph registered in the same
        // service collection, so the host configures everything (graph + provider) via DI.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var objectSets = new InMemoryObjectSetProvider(graph);
        objectSets.Seed(new TestPosition { Id = "P-1", Symbol = "ACME", Quantity = 10m }, "ACME position");

        var services = new ServiceCollection();
        services.AddSingleton(graph);
        services.AddSingleton<IObjectSetProvider>(objectSets);
        services.AddMcpServer().AddOntologyTools();

        var (client, disposables, serverRun) = await ConnectAsync(services);

        try
        {
            var result = await client.CallToolAsync(
                "ontology_query",
                new Dictionary<string, object?> { ["objectType"] = "TestPosition" });

            await Assert.That(result.IsError ?? false).IsFalse();
            var raw = result.StructuredContent!.Value.GetRawText();
            await Assert.That(raw).Contains("ACME");

            // The server loop must not have faulted while serving the call.
            await Assert.That(serverRun.IsFaulted).IsFalse();
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
    public async Task AddOntologyTools_NoArgs_WithoutRegisteredGraph_Throws()
    {
        // The DI-resolved overload requires an OntologyGraph in the collection; absent one, it
        // fails loudly at registration rather than silently shipping uncallable tools.
        var services = new ServiceCollection();
        var builder = services.AddMcpServer();

        await Assert.That(() => builder.AddOntologyTools()).Throws<InvalidOperationException>();
    }
}
