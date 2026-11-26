using System.Diagnostics;
using System.IO.Pipelines;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.MCP.Hosting;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// Drives <c>ontology_validate</c> over the MCP SDK the hosting package registers,
/// with no authenticated principal. A tools/call whose design intent carries a
/// JSON number of <c>1</c> followed by a long fractional zero run must return
/// within a linear budget. The handler does not consult <c>RequestContext.User</c>.
/// </summary>
public sealed class OntologyValidateDecimalCostTests
{
    private const int TrailingFractionalZeros = 50_000;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(250);

    [Test]
    public async Task ToolsCall_ShortDecimalKnownProperty_ReturnsCanonicalVerdict()
    {
        var (elapsed, result, facts) = await CallValidateAsync(JsonNumber("1.50"), onArguments: false);

        await Assert.That(result.IsError ?? false).IsFalse();
        await Assert.That(elapsed).IsLessThan(Budget);
        await Assert.That(facts).IsNotNull();
        await Assert.That(facts!.Properties["price"].ToString()).IsEqualTo("1.5");
    }

    [Test]
    public async Task ToolsCall_KnownPropertyTrailingZeros_IsBoundedWithoutPrincipal()
    {
        var (elapsed, result, facts) = await CallValidateAsync(
            JsonNumber(TrailingZeroLiteral(TrailingFractionalZeros)),
            onArguments: false);

        await Assert.That(result.IsError ?? false).IsFalse();
        await AssertBoundedFact(elapsed, facts, "1");
    }

    [Test]
    public async Task ToolsCall_ActionArgumentTrailingZeros_IsBoundedWithoutPrincipal()
    {
        var (elapsed, result, facts) = await CallValidateAsync(
            JsonNumber(TrailingZeroLiteral(TrailingFractionalZeros)),
            onArguments: true);

        await Assert.That(result.IsError ?? false).IsFalse();
        await AssertBoundedFact(elapsed, facts, "1");
    }

    private static async Task AssertBoundedFact(TimeSpan elapsed, ActionFacts? facts, string canonical)
    {
        await Assert.That(facts).IsNotNull();
        if (facts!.Properties.TryGetValue("price", out var literal))
        {
            await Assert.That(literal.Kind is PredicateLiteralKind.Decimal or PredicateLiteralKind.Integer).IsTrue();
            await Assert.That(literal.ToString()).IsEqualTo(canonical);
        }

        await Assert.That(elapsed).IsLessThan(Budget);
    }

    private static async Task<(TimeSpan Elapsed, CallToolResult Result, ActionFacts? Facts)> CallValidateAsync(
        JsonElement number,
        bool onArguments)
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        ActionFacts? captured = null;
        var query = Substitute.For<IOntologyQuery>();
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<ActionFacts?>())
            .Returns(call =>
            {
                captured = call.ArgAt<ActionFacts?>(1);
                return Array.Empty<ActionConstraintReport>();
            });
        query
            .GetActionConstraintReport(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActionFacts?>())
            .Returns(call =>
            {
                captured = call.ArgAt<ActionFacts?>(2);
                return Array.Empty<ActionConstraintReport>();
            });
        query
            .EstimateBlastRadius(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<BlastRadiusOptions?>())
            .Returns(new BlastRadius(
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<OntologyNodeRef>(),
                Array.Empty<CrossDomainHop>(),
                BlastRadiusScope.Local));
        query
            .DetectPatternViolations(Arg.Any<IReadOnlyList<OntologyNodeRef>>(), Arg.Any<DesignIntent>())
            .Returns(Array.Empty<PatternViolation>());

        var services = new ServiceCollection();
        services.AddSingleton(graph);
        services.AddSingleton(query);
        services.AddMcpServer().AddOntologyTools(graph);

        await using var provider = services.BuildServiceProvider();
        var serverOptions = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverTransport = new StreamServerTransport(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            serverName: "ontology-validate-cost",
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

        var intent = IntentElement(number, onArguments);
        var stopwatch = Stopwatch.StartNew();
        var result = await client.CallToolAsync(
            "ontology_validate",
            new Dictionary<string, object?> { ["intent"] = intent });
        stopwatch.Stop();
        await Assert.That(serverRun.IsFaulted).IsFalse();
        return (stopwatch.Elapsed, result, captured);
    }

    private static JsonElement IntentElement(JsonElement number, bool onArguments)
    {
        var price = number.GetRawText();
        var known = onArguments ? "{}" : "{\"price\":" + price + "}";
        var arguments = onArguments ? ",\"arguments\":{\"price\":" + price + "}" : string.Empty;
        var json =
            "{\"affectedNodes\":[{\"domain\":\"trading\",\"objectTypeName\":\"Order\",\"key\":\"ord-1\"}]," +
            "\"actions\":[{\"actionName\":\"Ship\",\"subject\":{\"domain\":\"trading\",\"objectTypeName\":\"Order\",\"key\":\"ord-1\"}" +
            arguments + "}]," +
            "\"knownProperties\":" + known + "}";
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement JsonNumber(string literal)
    {
        using var document = JsonDocument.Parse("{\"v\":" + literal + "}");
        return document.RootElement.GetProperty("v").Clone();
    }

    private static string TrailingZeroLiteral(int zeros) => "1." + new string('0', zeros);
}
