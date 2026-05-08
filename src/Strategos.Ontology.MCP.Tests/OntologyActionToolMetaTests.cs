using System.Text.Json;
using Strategos.Ontology.Actions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyActionToolMetaTests
{
    [Test]
    public async Task Action_ResultCarriesMetaWithGraphVersion()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var dispatcher = Substitute.For<IActionDispatcher>();
        dispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));

        var provider = Substitute.For<IObjectSetProvider>();
        var tool = new OntologyActionTool(graph, dispatcher, provider);

        // Act — single-object dispatch
        var result = await tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "execute_trade",
            request: new { },
            domain: "trading",
            objectId: "p1");

        // Assert
        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task Action_BatchResult_CarriesMeta()
    {
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var dispatcher = Substitute.For<IActionDispatcher>();
        dispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));

        var provider = Substitute.For<IObjectSetProvider>();
        provider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>([], 0, ObjectSetInclusion.Properties));

        var tool = new OntologyActionTool(graph, dispatcher, provider);

        var result = await tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "execute_trade",
            request: new { },
            domain: "trading");

        await Assert.That(result.Meta).IsNotNull();
        await Assert.That(result.Meta.OntologyVersion).IsEqualTo("sha256:" + graph.Version);
    }

    [Test]
    public async Task Action_ErrorPaths_CarryMeta()
    {
        // Both error paths (unknown object type, unknown action) must still
        // stamp Meta — caches downstream key on it regardless of success.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var dispatcher = Substitute.For<IActionDispatcher>();
        var provider = Substitute.For<IObjectSetProvider>();
        var tool = new OntologyActionTool(graph, dispatcher, provider);
        var expected = "sha256:" + graph.Version;

        // Unknown object type
        var r1 = await tool.ExecuteAsync(
            objectType: "DoesNotExist",
            action: "noop",
            request: new { },
            domain: "trading");
        await Assert.That(r1.Meta).IsNotNull();
        await Assert.That(r1.Meta.OntologyVersion).IsEqualTo(expected);

        // Unknown action on a real type
        var r2 = await tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "no_such_action",
            request: new { },
            domain: "trading");
        await Assert.That(r2.Meta).IsNotNull();
        await Assert.That(r2.Meta.OntologyVersion).IsEqualTo(expected);
    }

    [Test]
    public async Task ActionToolResult_Json_KeysMetaAsUnderscoreMeta()
    {
        // Wire-format proof.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var dispatcher = Substitute.For<IActionDispatcher>();
        dispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));

        var provider = Substitute.For<IObjectSetProvider>();
        var tool = new OntologyActionTool(graph, dispatcher, provider);

        var result = await tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "execute_trade",
            request: new { },
            domain: "trading",
            objectId: "p1");

        var json = JsonSerializer.Serialize(result);
        await Assert.That(json).Contains("\"_meta\"");
    }
}
