using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

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
            obj.Action("close-position")
                .Accepts<decimal>()
                .Description("Close position")
                .Requires(position => position.Quantity > 0m);
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
            obj.Action("cancel-order")
                .Requires(order => order.Amount > 0m);
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

    public Task EnsureSchemaAsync<T>(CancellationToken ct = default) where T : class => Task.CompletedTask;

    public Task EnsureAllSchemasAsync(CancellationToken ct = default) => Task.CompletedTask;
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

public class IntegrationSinglePositionObjectSetProvider : IObjectSetProvider
{
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class
    {
        IReadOnlyList<T> items = typeof(T) == typeof(Position)
            ? [(T)(object)new Position { Id = "position-1", Symbol = "STRAT", Quantity = 1m }]
            : typeof(T) == typeof(TradeOrderItem)
                ? [(T)(object)new TradeOrderItem { OrderId = "order-42", Amount = 25m }]
                : [];

        return Task.FromResult(new ObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties));
    }

    public IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        AsyncEnumerable.Empty<T>();

    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct = default) where T : class =>
        Task.FromResult(new ScoredObjectSetResult<T>([], 0, ObjectSetInclusion.Properties, []));

    public Task EnsureSchemaAsync<T>(CancellationToken ct = default) where T : class => Task.CompletedTask;

    public Task EnsureAllSchemasAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class IntegrationRecordingActionDispatcher : IActionDispatcher
{
    public ActionContext? LastContext { get; private set; }

    public Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default)
    {
        LastContext = context;
        return Task.FromResult(new ActionResult(true));
    }
}

public sealed class IntegrationPositionFactResolver : IActionFactResolver
{
    public ActionContext? LastContext { get; private set; }

    public ValueTask<ActionFacts?> ResolveAsync(
        ActionContext context,
        CancellationToken ct = default)
    {
        LastContext = context;
        if (!string.Equals(context.ObjectId, "position-1", StringComparison.Ordinal))
        {
            if (!string.Equals(context.ObjectId, "order-42", StringComparison.Ordinal))
            {
                return ValueTask.FromResult<ActionFacts?>(null);
            }

            return ValueTask.FromResult<ActionFacts?>(new ActionFacts(
                properties:
                [
                    KeyValuePair.Create(
                        nameof(TradeOrderItem.Amount),
                        PredicateLiteral.Decimal(25m)),
                ]));
        }

        return ValueTask.FromResult<ActionFacts?>(new ActionFacts(
            properties:
            [
                KeyValuePair.Create(
                    nameof(Position.Quantity),
                    PredicateLiteral.Decimal(1m)),
            ]));
    }
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
            new ActionSubject("trading", nameof(Position)),
            objectSetProvider,
            actionDispatcher,
            eventStreamProvider);
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
    public async Task Ontology_QueryObjectSetApply_UsesDomainQualifiedSubjectThroughDispatcherGuard()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TradingTestOntology>();
            options.AddDomain<KnowledgeTestOntology>();
            options.UseObjectSetProvider<IntegrationSinglePositionObjectSetProvider>();
            options.UseEventStreamProvider<IntegrationStubEventStreamProvider>();
            options.UseActionDispatcher<IntegrationRecordingActionDispatcher>();
            options.UseActionFactResolver<IntegrationPositionFactResolver>();
        });

        using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<IOntologyQuery>();
        var positionSet = query.GetObjectSet<Position>("Position");

        var results = await positionSet.ApplyAsync(
            new ActionPrincipal("User", "user-1"),
            "close-position",
            new { },
            new ActionDispatchOptions { EnforcePreconditions = true });

        var innerDispatcher = provider.GetRequiredService<IntegrationRecordingActionDispatcher>();
        var factResolver = (IntegrationPositionFactResolver)provider.GetRequiredService<IActionFactResolver>();
        await Assert.That(results).HasCount().EqualTo(1);
        await Assert.That(results[0].IsSuccess).IsTrue();
        await Assert.That(innerDispatcher.LastContext).IsNotNull();
        await Assert.That(innerDispatcher.LastContext!.Domain).IsEqualTo("trading");
        await Assert.That(innerDispatcher.LastContext.ObjectType).IsEqualTo("Position");
        await Assert.That(innerDispatcher.LastContext.ObjectId).IsEqualTo("position-1");
        await Assert.That(innerDispatcher.LastContext.ActionName).IsEqualTo("close-position");
        await Assert.That(innerDispatcher.LastContext.ActionDescriptor).IsNotNull();
        await Assert.That(innerDispatcher.LastContext.ActionDescriptor!.Name).IsEqualTo("close-position");
        await Assert.That(factResolver.LastContext).IsNotNull();
        await Assert.That(factResolver.LastContext!.ObjectId).IsEqualTo("position-1");
    }

    [Test]
    public async Task Ontology_QueryTraversalApply_PreservesTargetDescriptorAndProjectsTargetId()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TradingTestOntology>();
            options.AddDomain<KnowledgeTestOntology>();
            options.UseObjectSetProvider<IntegrationSinglePositionObjectSetProvider>();
            options.UseEventStreamProvider<IntegrationStubEventStreamProvider>();
            options.UseActionDispatcher<IntegrationRecordingActionDispatcher>();
            options.UseActionFactResolver<IntegrationPositionFactResolver>();
        });

        using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<IOntologyQuery>();
        var orders = query
            .GetObjectSet<Position>(nameof(Position))
            .TraverseLink<TradeOrderItem>("orders")
            .Where(order => order.Amount > 0m)
            .Include(ObjectSetInclusion.Actions);

        var traversal = (TraverseLinkExpression)((FilterExpression)((IncludeExpression)orders.Expression).Source).Source;
        await Assert.That(traversal.TargetDescriptorName).IsEqualTo(nameof(TradeOrderItem));

        var results = await orders.ApplyAsync(
            new ActionPrincipal("User", "user-1"),
            "cancel-order",
            new { },
            new ActionDispatchOptions { EnforcePreconditions = true });

        var innerDispatcher = provider.GetRequiredService<IntegrationRecordingActionDispatcher>();
        var factResolver = (IntegrationPositionFactResolver)provider.GetRequiredService<IActionFactResolver>();
        await Assert.That(results).HasCount().EqualTo(1);
        await Assert.That(results[0].IsSuccess).IsTrue();
        await Assert.That(innerDispatcher.LastContext).IsNotNull();
        await Assert.That(innerDispatcher.LastContext!.Domain).IsEqualTo("trading");
        await Assert.That(innerDispatcher.LastContext.ObjectType).IsEqualTo(nameof(TradeOrderItem));
        await Assert.That(innerDispatcher.LastContext.ObjectId).IsEqualTo("order-42");
        await Assert.That(innerDispatcher.LastContext.ActionName).IsEqualTo("cancel-order");
        await Assert.That(innerDispatcher.LastContext.ActionDescriptor).IsNotNull();
        await Assert.That(innerDispatcher.LastContext.ActionDescriptor!.Name).IsEqualTo("cancel-order");
        await Assert.That(factResolver.LastContext).IsNotNull();
        await Assert.That(factResolver.LastContext!.Domain).IsEqualTo("trading");
        await Assert.That(factResolver.LastContext.ObjectType).IsEqualTo(nameof(TradeOrderItem));
        await Assert.That(factResolver.LastContext.ObjectId).IsEqualTo("order-42");
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
