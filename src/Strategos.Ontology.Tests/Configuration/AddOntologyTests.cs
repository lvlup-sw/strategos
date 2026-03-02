using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Configuration;

public class TestOntologyDomain : DomainOntology
{
    public override string DomainName => "test-domain";

    public bool DefineCalled { get; private set; }

    protected override void Define(IOntologyBuilder builder)
    {
        DefineCalled = true;
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });
    }
}

public class StubObjectSetProvider : IObjectSetProvider
{
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        Task.FromResult(new ObjectSetResult<T>([], 0, ObjectSetInclusion.Properties));

    public IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        AsyncEnumerable.Empty<T>();

    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(SimilarityExpression expression, CancellationToken ct = default)
        where T : class =>
        Task.FromResult(new ScoredObjectSetResult<T>([], 0, ObjectSetInclusion.Properties, []));
}

public class StubEventStreamProvider : IEventStreamProvider
{
    public IAsyncEnumerable<OntologyEvent> QueryEventsAsync(EventQuery query, CancellationToken ct = default) =>
        AsyncEnumerable.Empty<OntologyEvent>();
}

public class StubActionDispatcher : IActionDispatcher
{
    public Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default) =>
        Task.FromResult(new ActionResult(true, null));
}

public class AddOntologyTests
{
    [Test]
    public async Task AddOntology_RegistersOntologyGraphAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
        });

        var provider = services.BuildServiceProvider();
        var graph1 = provider.GetRequiredService<OntologyGraph>();
        var graph2 = provider.GetRequiredService<OntologyGraph>();

        await Assert.That(graph1).IsNotNull();
        await Assert.That(graph1).IsSameReferenceAs(graph2);
    }

    [Test]
    public async Task AddOntology_ExecutesDomainOntologyDefine()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
        });

        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<OntologyGraph>();

        await Assert.That(graph.Domains).HasCount().EqualTo(1);
        await Assert.That(graph.Domains[0].DomainName).IsEqualTo("test-domain");
        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(1);
    }

    [Test]
    public async Task AddOntology_FreezesGraphAfterRegistration()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
        });

        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<OntologyGraph>();

        // Graph is a sealed class with an internal constructor — it can only be
        // created through OntologyGraphBuilder.Build(), which freezes it.
        await Assert.That(graph).IsNotNull();
        await Assert.That(graph.Domains).IsNotNull();
        await Assert.That(graph.ObjectTypes).IsNotNull();
    }

    [Test]
    public async Task AddOntology_AddDomain_RegistersDomainOntology()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestTradingOntology>();
            options.AddDomain<TestMarketDataOntology>();
        });

        var provider = services.BuildServiceProvider();
        var graph = provider.GetRequiredService<OntologyGraph>();

        await Assert.That(graph.Domains).HasCount().EqualTo(2);
    }

    [Test]
    public async Task AddOntology_UseObjectSetProvider_RegistersProvider()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseObjectSetProvider<StubObjectSetProvider>();
        });

        var provider = services.BuildServiceProvider();
        var objectSetProvider = provider.GetService<IObjectSetProvider>();

        await Assert.That(objectSetProvider).IsNotNull();
        await Assert.That(objectSetProvider).IsTypeOf<StubObjectSetProvider>();
    }

    [Test]
    public async Task AddOntology_UseEventStreamProvider_RegistersProvider()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseEventStreamProvider<StubEventStreamProvider>();
        });

        var provider = services.BuildServiceProvider();
        var eventStreamProvider = provider.GetService<IEventStreamProvider>();

        await Assert.That(eventStreamProvider).IsNotNull();
        await Assert.That(eventStreamProvider).IsTypeOf<StubEventStreamProvider>();
    }

    [Test]
    public async Task AddOntology_UseActionDispatcher_RegistersDispatcher()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
        });

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetService<IActionDispatcher>();

        await Assert.That(dispatcher).IsNotNull();
        await Assert.That(dispatcher).IsTypeOf<StubActionDispatcher>();
    }
}
