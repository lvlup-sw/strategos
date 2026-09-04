using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Configuration;

public class DispatcherDecoratorRegistrationTests
{
    private static IServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    [Test]
    public async Task NoDecoratorExtensions_AddsMandatoryAuthorizationChokepoints()
    {
        var services = BaseServices();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
        });

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        await Assert.That(dispatcher).IsTypeOf<AuthorityAuthorizationActionDispatcher>();
        var authority = (AuthorityAuthorizationActionDispatcher)dispatcher;
        await Assert.That(authority.Inner).IsTypeOf<RelationAuthorizationActionDispatcher>();
        var relation = (RelationAuthorizationActionDispatcher)authority.Inner;
        await Assert.That(relation.Inner).IsTypeOf<StubActionDispatcher>();
    }

    [Test]
    public async Task AddConstraintReporting_WrapsUserDispatcher()
    {
        var services = BaseServices();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddConstraintReporting();
        });

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        await Assert.That(dispatcher).IsTypeOf<ConstraintReportingActionDispatcher>();
    }

    [Test]
    public async Task AddDispatchObservation_WrapsUserDispatcher()
    {
        var services = BaseServices();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddDispatchObservation();
        });

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        await Assert.That(dispatcher).IsTypeOf<ObservableActionDispatcher>();
    }

    [Test]
    public async Task BothExtensions_ObservationOutermostConstraintReportingInner()
    {
        var services = BaseServices();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddConstraintReporting();
            options.AddDispatchObservation();
        });

        var provider = services.BuildServiceProvider();
        var outer = provider.GetRequiredService<IActionDispatcher>();

        await Assert.That(outer).IsTypeOf<ObservableActionDispatcher>();
        // Verify the full chain so a regression that drops either enforcement
        // layer is caught: Observable → ConstraintReporting → Authority → Relation → Stub.
        var observable = (ObservableActionDispatcher)outer;
        await Assert.That(observable.Inner).IsTypeOf<ConstraintReportingActionDispatcher>();
        var constraintReporting = (ConstraintReportingActionDispatcher)observable.Inner;
        await Assert.That(constraintReporting.Inner).IsTypeOf<AuthorityAuthorizationActionDispatcher>();
        var authority = (AuthorityAuthorizationActionDispatcher)constraintReporting.Inner;
        await Assert.That(authority.Inner).IsTypeOf<RelationAuthorizationActionDispatcher>();
        var relation = (RelationAuthorizationActionDispatcher)authority.Inner;
        await Assert.That(relation.Inner).IsTypeOf<StubActionDispatcher>();
    }

    [Test]
    public async Task BothExtensions_OrderingInvariantToCallSequence()
    {
        var services = BaseServices();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddDispatchObservation();
            options.AddConstraintReporting();
        });

        var provider = services.BuildServiceProvider();
        var outer = provider.GetRequiredService<IActionDispatcher>();

        await Assert.That(outer).IsTypeOf<ObservableActionDispatcher>();
        var observable = (ObservableActionDispatcher)outer;
        await Assert.That(observable.Inner).IsTypeOf<ConstraintReportingActionDispatcher>();
        var constraintReporting = (ConstraintReportingActionDispatcher)observable.Inner;
        await Assert.That(constraintReporting.Inner).IsTypeOf<AuthorityAuthorizationActionDispatcher>();
        var authority = (AuthorityAuthorizationActionDispatcher)constraintReporting.Inner;
        await Assert.That(authority.Inner).IsTypeOf<RelationAuthorizationActionDispatcher>();
        var relation = (RelationAuthorizationActionDispatcher)authority.Inner;
        await Assert.That(relation.Inner).IsTypeOf<StubActionDispatcher>();
    }

    [Test]
    public async Task AddConstraintReporting_BuildsChainResolvableFromDi()
    {
        var services = BaseServices();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddConstraintReporting();
        });

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        // Resolves successfully and routes Dispatch through the inner stub.
        var ctx = new ActionContext(
            new ActionPrincipal("User", "user-1"),
            "test-domain",
            "TestPosition",
            "p-1",
            "Noop");
        var result = await dispatcher.DispatchAsync(ctx, new { }, CancellationToken.None);
        await Assert.That(result.IsSuccess).IsTrue();
    }
}
