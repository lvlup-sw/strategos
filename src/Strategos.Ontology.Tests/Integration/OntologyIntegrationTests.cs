using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Integration;

// --- Test domain model types ---

public class Position
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class TradeOrderItem
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
}

public interface IHasSymbol
{
    string Symbol { get; }
}

public class Article
{
    public string ArticleId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class Tag
{
    public string TagId { get; set; } = "";
    public string Label { get; set; } = "";
}

// --- Test domain ontologies ---

public class TradingTestOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IHasSymbol>("IHasSymbol", intf =>
        {
            intf.Property(i => i.Symbol);
        });

        builder.Object<Position>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.Quantity);
            obj.Action("close-position").Accepts<decimal>().Description("Close position");
            obj.HasMany<TradeOrderItem>("orders");
            obj.Implements<IHasSymbol>(map =>
            {
                map.Via(p => p.Symbol, i => i.Symbol);
            });
        });

        builder.Object<TradeOrderItem>(obj =>
        {
            obj.Key(o => o.OrderId);
            obj.Property(o => o.Amount).Required();
        });

        builder.CrossDomainLink("PositionToArticle")
            .From<Position>()
            .ToExternal("knowledge", "Article")
            .ManyToMany();
    }
}

public class KnowledgeTestOntology : DomainOntology
{
    public override string DomainName => "knowledge";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IHasSymbol>("IHasSymbol", intf =>
        {
            intf.Property(i => i.Symbol);
        });

        builder.Object<Article>(obj =>
        {
            obj.Key(a => a.ArticleId);
            obj.Property(a => a.Title).Required();
            obj.Property(a => a.Symbol).Required();
            obj.HasMany<Tag>("tags");
            obj.Implements<IHasSymbol>(map =>
            {
                map.Via(a => a.Symbol, i => i.Symbol);
            });
        });

        builder.Object<Tag>(obj =>
        {
            obj.Key(t => t.TagId);
            obj.Property(t => t.Label).Required();
        });
    }
}

public class TradingWithBadCrossDomainLinkOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Position>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });

        builder.CrossDomainLink("PositionToGhost")
            .From<Position>()
            .ToExternal("ghost-domain", "GhostType");
    }
}

// --- Stub providers for integration testing ---

public class IntegrationStubObjectSetProvider : IObjectSetProvider
{
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        Task.FromResult(new ObjectSetResult<T>([], 0, ObjectSetInclusion.Properties));

    public IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        AsyncEnumerable.Empty<T>();

    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct = default) where T : class =>
        Task.FromResult(new ScoredObjectSetResult<T>([], 0, ObjectSetInclusion.Properties, []));
}

public class IntegrationStubEventStreamProvider : IEventStreamProvider
{
    public IAsyncEnumerable<OntologyEvent> QueryEventsAsync(EventQuery query, CancellationToken ct = default) =>
        AsyncEnumerable.Empty<OntologyEvent>();
}

public class IntegrationStubActionDispatcher : IActionDispatcher
{
    public Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default) =>
        Task.FromResult(new ActionResult(true));
}

// --- Integration tests ---

public class OntologyIntegrationTests
{
    [Test]
    public async Task Ontology_FullRegistration_GraphFreezes()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TradingTestOntology>();
            options.AddDomain<KnowledgeTestOntology>();
            options.UseObjectSetProvider<IntegrationStubObjectSetProvider>();
            options.UseEventStreamProvider<IntegrationStubEventStreamProvider>();
            options.UseActionDispatcher<IntegrationStubActionDispatcher>();
        });

        var provider = services.BuildServiceProvider();
        var graph1 = provider.GetRequiredService<OntologyGraph>();
        var graph2 = provider.GetRequiredService<OntologyGraph>();

        // Graph is frozen (singleton, immutable)
        await Assert.That(graph1).IsSameReferenceAs(graph2);
        await Assert.That(graph1.Domains).IsNotNull();
        await Assert.That(graph1.ObjectTypes).IsNotNull();
        await Assert.That(graph1.Interfaces).IsNotNull();
        await Assert.That(graph1.CrossDomainLinks).IsNotNull();
        await Assert.That(graph1.WorkflowChains).IsNotNull();
    }

    [Test]
    public async Task Ontology_FullRegistration_CrossDomainLinksResolved()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TradingTestOntology>();
            options.AddDomain<KnowledgeTestOntology>();
        });

        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<OntologyGraph>();

        await Assert.That(graph.CrossDomainLinks).HasCount().EqualTo(1);

        var link = graph.CrossDomainLinks[0];
        await Assert.That(link.Name).IsEqualTo("PositionToArticle");
        await Assert.That(link.SourceDomain).IsEqualTo("trading");
        await Assert.That(link.TargetDomain).IsEqualTo("knowledge");
        await Assert.That(link.SourceObjectType.Name).IsEqualTo("Position");
        await Assert.That(link.TargetObjectType.Name).IsEqualTo("Article");
        await Assert.That(link.Cardinality).IsEqualTo(LinkCardinality.ManyToMany);
    }

    [Test]
    public async Task Ontology_FullRegistration_InterfaceImplementorsDiscoverable()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TradingTestOntology>();
            options.AddDomain<KnowledgeTestOntology>();
        });

        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<OntologyGraph>();

        var implementors = graph.GetImplementors("IHasSymbol");

        await Assert.That(implementors).HasCount().EqualTo(2);

        var names = implementors.Select(i => i.Name).OrderBy(n => n).ToList();
        await Assert.That(names[0]).IsEqualTo("Article");
        await Assert.That(names[1]).IsEqualTo("Position");
    }

    [Test]
    public async Task Ontology_FullRegistration_ObjectSetQueriesWork()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TradingTestOntology>();
            options.AddDomain<KnowledgeTestOntology>();
            options.UseObjectSetProvider<IntegrationStubObjectSetProvider>();
            options.UseEventStreamProvider<IntegrationStubEventStreamProvider>();
            options.UseActionDispatcher<IntegrationStubActionDispatcher>();
        });

        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<OntologyGraph>();
        var objectSetProvider = provider.GetRequiredService<IObjectSetProvider>();
        var actionDispatcher = provider.GetRequiredService<IActionDispatcher>();
        var eventStreamProvider = provider.GetRequiredService<IEventStreamProvider>();

        // Verify we can construct an ObjectSet with the resolved providers
        var positionSet = new ObjectSet<Position>(
            nameof(Position), objectSetProvider, actionDispatcher, eventStreamProvider);
        var result = await positionSet.ExecuteAsync();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).HasCount().EqualTo(0);

        // Verify the graph has the Position type
        var positionType = graph.GetObjectType("trading", "Position");
        await Assert.That(positionType).IsNotNull();
        await Assert.That(positionType!.Actions).HasCount().EqualTo(1);
        await Assert.That(positionType.Actions[0].Name).IsEqualTo("close-position");
    }

    [Test]
    public async Task Ontology_InvalidCrossDomainLink_FailsFast()
    {
        var services = new ServiceCollection();

        await Assert.That(() =>
        {
            services.AddOntology(options =>
            {
                options.AddDomain<TradingWithBadCrossDomainLinkOntology>();
            });
        })
        .ThrowsException()
        .WithExceptionType(typeof(OntologyCompositionException));
    }
}
