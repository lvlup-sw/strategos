using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Retrieval;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// PR-C Task 28: <see cref="OntologyQueryTool"/> constructor — optional
/// <see cref="IKeywordSearchProvider"/> injection.
/// </summary>
public sealed class OntologyQueryToolConstructorTests
{
    [Test]
    public async Task Ctor_WithoutKeywordProvider_Constructs()
    {
        // The 2.5.0 ctor surface (no provider) must continue to construct so
        // that every existing call site remains source-compatible (DIM-3).
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var objectSetProvider = Substitute.For<IObjectSetProvider>();
        var eventStreamProvider = Substitute.For<IEventStreamProvider>();

        var tool = new OntologyQueryTool(
            graph,
            objectSetProvider,
            eventStreamProvider,
            NullLogger<OntologyQueryTool>.Instance);

        await Assert.That(tool).IsNotNull();
    }

    [Test]
    public async Task Ctor_WithKeywordProvider_StoresField()
    {
        // The 2.6.0 ctor accepts a provider and stores it for later hybrid use.
        // We can't observe the field directly without breaking encapsulation,
        // so we assert via the side-channel: a hybrid call that needs the
        // provider succeeds (covered in OntologyQueryToolHybridTests). Here
        // we simply assert the ctor with the provider compiles and returns
        // an instance.
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var objectSetProvider = Substitute.For<IObjectSetProvider>();
        var eventStreamProvider = Substitute.For<IEventStreamProvider>();
        var keywordProvider = Substitute.For<IKeywordSearchProvider>();

        var tool = new OntologyQueryTool(
            graph,
            objectSetProvider,
            eventStreamProvider,
            NullLogger<OntologyQueryTool>.Instance,
            keywordProvider);

        await Assert.That(tool).IsNotNull();
    }
}
