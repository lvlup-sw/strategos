using System.Reflection;

using Strategos.Ontology;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// DR-15 / T19 (#125): relate/unrelate is a WRITE and must stay gated through the
/// action path (<see cref="OntologyActionTool"/> → <see cref="IActionDispatcher"/>),
/// never a direct <see cref="IObjectSetWriter"/> call from a read/traverse tool. So
/// the constraint/precondition pipeline the dispatcher enforces always runs before a
/// relation is materialized or removed.
/// </summary>
public sealed class RelateActionGatingTests
{
    public sealed record Account(string Id, string Status = "open");

    public sealed record Holder(string Id);

    public sealed record RelateRequest(string TargetId);

    private sealed class RelateDomain : DomainOntology
    {
        public override string DomainName => "rel";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<Holder>(obj => obj.Key(h => h.Id));

            builder.Object<Account>(obj =>
            {
                obj.Key(a => a.Id);
                obj.Property(a => a.Status).Required();
                obj.HasOne<Holder>("holder");

                // relate/unrelate are ACTIONS — dispatched through IActionDispatcher,
                // so a precondition (account must be open) gates them.
                obj.Action("relate_holder")
                    .Description("Attach a holder to the account")
                    .Accepts<RelateRequest>()
                    .Requires(a => a.Status == "open");

                obj.Action("unrelate_holder")
                    .Description("Detach a holder from the account")
                    .Accepts<RelateRequest>();
            });
        }
    }

    private static OntologyGraph BuildGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<RelateDomain>();
        return builder.Build();
    }

    [Test]
    public async Task ActionTool_RelateUnrelate_RemainsActionGated()
    {
        // Arrange
        var graph = BuildGraph();
        var dispatcher = Substitute.For<IActionDispatcher>();
        var provider = Substitute.For<IObjectSetProvider>();
        dispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));

        var tool = new OntologyActionTool(graph, dispatcher, provider);

        // Act — relate, then unrelate, both as ACTIONS on a single object.
        await tool.ExecuteAsync("Account", "relate_holder", new RelateRequest("h1"), domain: "rel", objectId: "a1");
        await tool.ExecuteAsync("Account", "unrelate_holder", new RelateRequest("h1"), domain: "rel", objectId: "a1");

        // Assert — BOTH went through the action dispatcher (the gate), never a direct
        // writer call.
        await dispatcher.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(c => c.ActionName == "relate_holder" && c.ObjectId == "a1"),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await dispatcher.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(c => c.ActionName == "unrelate_holder" && c.ObjectId == "a1"),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ActionTool_RelateDenied_WhenDispatcherReturnsConstraintFailure()
    {
        // Arrange — the dispatcher (the gate) denies the relate because the account is
        // not open. The action tool surfaces the denial; nothing is written.
        var graph = BuildGraph();
        var dispatcher = Substitute.For<IActionDispatcher>();
        var provider = Substitute.For<IObjectSetProvider>();
        dispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(false, Error: "precondition failed: account is not open"));

        var tool = new OntologyActionTool(graph, dispatcher, provider);

        // Act
        var result = await tool.ExecuteAsync(
            "Account", "relate_holder", new RelateRequest("h1"), domain: "rel", objectId: "a1");

        // Assert — the relate was denied at the gate.
        await Assert.That(result.Results).HasCount().EqualTo(1);
        await Assert.That(result.Results[0].IsSuccess).IsFalse();
        await Assert.That(result.Results[0].Error).Contains("precondition");
    }

    [Test]
    public async Task ReadAndTraversalTools_DoNotDependOnObjectSetWriter()
    {
        // Structural guard: the read/traverse MCP tools materialize NOTHING — their
        // constructors take only read surfaces (IObjectSetProvider), never the
        // IObjectSetWriter write surface. Writes must flow through OntologyActionTool.
        var readToolTypes = new[]
        {
            typeof(OntologyTraverseTool),
            typeof(OntologyQueryTool),
            typeof(OntologyExploreTool),
        };

        var offenders = new List<string>();
        foreach (var type in readToolTypes)
        {
            foreach (var ctor in type.GetConstructors())
            {
                foreach (var p in ctor.GetParameters())
                {
                    if (p.ParameterType == typeof(IObjectSetWriter))
                    {
                        offenders.Add($"{type.Name} ctor takes IObjectSetWriter ({p.Name})");
                    }
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType == typeof(IObjectSetWriter))
                {
                    offenders.Add($"{type.Name} field {field.Name} is IObjectSetWriter");
                }
            }
        }

        await Assert.That(offenders).IsEmpty();
    }
}
