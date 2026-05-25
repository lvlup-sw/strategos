using System.IO.Pipelines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

public sealed class AddOntologyToolsTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "ontology_explore",
        "ontology_query",
        "ontology_action",
        "ontology_validate",
    ];

    [Test]
    public async Task AddOntologyTools_RegistersFourCallableTools()
    {
        // Arrange — register ontology tools on an MCP server builder.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var services = new ServiceCollection();
        services.AddMcpServer().AddOntologyTools(graph);
        await using var provider = services.BuildServiceProvider();
        var serverOptions = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        // In-memory duplex transport pair (no process, no network).
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var serverTransport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            serverName: "ontology-test-server",
            loggerFactory: null);

        await using var server = McpServer.Create(
            serverTransport,
            serverOptions,
            loggerFactory: null,
            serviceProvider: provider);

        var serverRun = server.RunAsync();

        var clientTransport = new StreamClientTransport(
            serverInput: clientToServer.Writer.AsStream(),
            serverOutput: serverToClient.Reader.AsStream(),
            loggerFactory: null);

        await using var client = await McpClient.CreateAsync(clientTransport);

        // Act — tools/list advertises the four ontology tools.
        var tools = await client.ListToolsAsync();
        var names = tools.Select(t => t.Name).ToList();

        // Assert
        foreach (var expected in ExpectedToolNames)
        {
            await Assert.That(names).Contains(expected);
        }

        // Act — tools/call on ontology_query routes to the registered tool.
        var result = await client.CallToolAsync(
            "ontology_query",
            new Dictionary<string, object?> { ["input"] = "{}" });

        // Assert — the call routed and returned content (no protocol error).
        await Assert.That(result).IsNotNull();
        await Assert.That(result.IsError ?? false).IsFalse();
    }
}
