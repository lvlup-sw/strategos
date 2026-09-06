using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
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

    [Test]
    public async Task AddConstraintReporting_AuthoritativeDenialRetainsUnsatisfiedTruthValue()
    {
        var services = BaseServices();
        services.AddOntology(options =>
        {
            options.AddDomain<ReportingActionTestOntology>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.UseActionFactResolver<ZeroQuantityFactResolver>();
            options.AddConstraintReporting();
        });
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        var result = await dispatcher.DispatchAsync(
            ReportingContext(enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Unsatisfied);
    }

    [Test]
    public async Task AddConstraintReporting_AuthoritativeSuccessDoesNotManufactureHardUnknown()
    {
        var services = BaseServices();
        services.AddOntology(options =>
        {
            options.AddDomain<ReportingActionTestOntology>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.UseActionFactResolver<PositiveQuantityFactResolver>();
            options.AddConstraintReporting();
        });
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        var result = await dispatcher.DispatchAsync(
            ReportingContext(enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNull();
    }

    [Test]
    public async Task AddConstraintReporting_AuthoritativeSoftFailureIsReportedWithoutBlocking()
    {
        var services = BaseServices();
        services.AddOntology(options =>
        {
            options.AddDomain<ReportingActionTestOntology>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.UseActionFactResolver<LowQuantityFactResolver>();
            options.AddConstraintReporting();
        });
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();
        var resolver = (LowQuantityFactResolver)provider.GetRequiredService<IActionFactResolver>();
        var context = ReportingContext(enforcePreconditions: true);

        var result = await dispatcher.DispatchAsync(context, new { });

        await Assert.That(context.ActionDescriptor).IsNull();
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Hard).IsEmpty();
        await Assert.That(result.Violations.Soft.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Unsatisfied);
        await Assert.That(resolver.CallCount).IsEqualTo(1);
        await Assert.That(resolver.LastContext).IsNotNull();
        await Assert.That(resolver.LastContext!.Domain).IsEqualTo("reporting-runtime");
        await Assert.That(resolver.LastContext.ObjectType)
            .IsEqualTo(nameof(ReportingActionTarget));
        await Assert.That(resolver.LastContext.ObjectId).IsEqualTo("target-1");
        await Assert.That(resolver.LastContext.ActionName).IsEqualTo("Ship");
        await Assert.That(resolver.LastContext.ActionDescriptor).IsNotNull();
    }

    [Test]
    public async Task AddConstraintReporting_MissingFactResolverReportsNonEnforcedHardAndSoftIndeterminate()
    {
        var services = BaseServices();
        services.AddOntology(options =>
        {
            options.AddDomain<ReportingActionTestOntology>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddConstraintReporting();
        });
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        var result = await dispatcher.DispatchAsync(
            ReportingContext(enforcePreconditions: false),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await Assert.That(result.Violations.Soft.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task AddConstraintReporting_NonEnforcedHardFailureIsAuthoritativelyReportedWithoutBlocking()
    {
        var services = BaseServices();
        services.AddOntology(options =>
        {
            options.AddDomain<ReportingActionTestOntology>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.UseActionFactResolver<ZeroQuantityFactResolver>();
            options.AddConstraintReporting();
        });
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        var result = await dispatcher.DispatchAsync(
            ReportingContext(enforcePreconditions: false),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Unsatisfied);
        await Assert.That(result.Violations.Soft.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Unsatisfied);
    }

    [Test]
    public async Task AddConstraintReporting_FailingSoftCustomEvaluatorReportsIndeterminate()
    {
        var services = BaseServices();
        var logger = new RecordingLogger<RelationAuthorizationActionDispatcher>();
        services.AddSingleton<ILogger<RelationAuthorizationActionDispatcher>>(logger);
        services.AddOntology(options =>
        {
            options.AddDomain<ReportingActionTestOntology>();
            options.UseActionDispatcher<StubActionDispatcher>();
            options.AddCustomActionPredicateEvaluator<ThrowingSoftCustomEvaluator>();
            options.AddConstraintReporting();
        });
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IActionDispatcher>();

        var result = await dispatcher.DispatchAsync(
            ReportingContext(enforcePreconditions: false, actionName: "Review"),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Soft.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await Assert.That(logger.Entries.Any(entry =>
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("soft-report-v1", StringComparison.Ordinal)))
            .IsTrue();
    }

    private static ActionContext ReportingContext(
        bool enforcePreconditions,
        string actionName = "Ship") => new(
        new ActionPrincipal("User", "user-1"),
        "reporting-runtime",
        nameof(ReportingActionTarget),
        "target-1",
        actionName,
        new ActionDispatchOptions { EnforcePreconditions = enforcePreconditions });

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        internal List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

public sealed record ReportingActionTarget(string Id, int Quantity);

public sealed class ReportingActionTestOntology : DomainOntology
{
    public override string DomainName => "reporting-runtime";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ReportingActionTarget>(objectType =>
        {
            objectType.Key(target => target.Id);
            objectType.Property(target => target.Quantity);
            objectType.Action("Ship")
                .Requires(target => target.Quantity > 0)
                .RequiresSoft(target => target.Quantity >= 10);
            objectType.Action("Review")
                .RequiresSoft(ActionPredicate.Custom("soft-report-v1"));
        });
    }
}

public sealed class ZeroQuantityFactResolver : IActionFactResolver
{
    public ValueTask<ActionFacts?> ResolveAsync(
        ActionContext context,
        CancellationToken ct = default) =>
        ValueTask.FromResult<ActionFacts?>(ReportingFacts.Create(0));
}

public sealed class PositiveQuantityFactResolver : IActionFactResolver
{
    public ValueTask<ActionFacts?> ResolveAsync(
        ActionContext context,
        CancellationToken ct = default) =>
        ValueTask.FromResult<ActionFacts?>(ReportingFacts.Create(20));
}

public sealed class LowQuantityFactResolver : IActionFactResolver
{
    public int CallCount { get; private set; }

    public ActionContext? LastContext { get; private set; }

    public ValueTask<ActionFacts?> ResolveAsync(
        ActionContext context,
        CancellationToken ct = default)
    {
        CallCount++;
        LastContext = context;
        return ValueTask.FromResult<ActionFacts?>(ReportingFacts.Create(2));
    }
}

public sealed class ThrowingSoftCustomEvaluator : ICustomActionPredicateEvaluator
{
    public string EvaluatorKey => "soft-report-v1";

    public ValueTask<PredicateTruthValue> EvaluateAsync(
        CustomActionPredicateContext context,
        CancellationToken ct = default) =>
        ValueTask.FromException<PredicateTruthValue>(new InvalidOperationException("failed"));
}

internal static class ReportingFacts
{
    internal static ActionFacts Create(int quantity) => new(
        properties: new Dictionary<string, PredicateLiteral>
        {
            [nameof(ReportingActionTarget.Quantity)] = PredicateLiteral.Integer(quantity),
        });
}
