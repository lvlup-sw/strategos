using Strategos.Ontology.Builder;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Actions;

public sealed class RelationAuthorizationTests
{
    private static readonly ActionPrincipal Owner = new(nameof(AuthorizationUser), "user-1");

    [Test]
    public async Task DispatchAsync_MissingRelation_RefusesBeforeInnerDispatcher()
    {
        var graph = BuildGraph();
        var inner = Substitute.For<IActionDispatcher>();
        var resolver = Substitute.For<IActionRelationResolver>();
        resolver.HoldsAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<ActionPrecondition>(),
                Arg.Any<CancellationToken>())
            .Returns(false);
        var dispatcher = new RelationAuthorizationActionDispatcher(inner, graph, resolver);

        var result = await dispatcher.DispatchAsync(Context(graph), new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("hard requirements");
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Unsatisfied);
        await inner.DidNotReceive().DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DispatchAsync_HeldRelation_ForwardsResolvedDescriptorToInnerDispatcher()
    {
        var graph = BuildGraph();
        var inner = Substitute.For<IActionDispatcher>();
        inner.DispatchAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));
        var resolver = Substitute.For<IActionRelationResolver>();
        resolver.HoldsAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<ActionPrecondition>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var dispatcher = new RelationAuthorizationActionDispatcher(inner, graph, resolver);

        var result = await dispatcher.DispatchAsync(Context(graph) with { ActionDescriptor = null }, new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await inner.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(value => value.ActionDescriptor!.Name == "update"),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DispatchAsync_CallerSuppliedUnprotectedDescriptor_CannotBypassGraphRelation()
    {
        var graph = BuildGraph();
        var inner = Substitute.For<IActionDispatcher>();
        var resolver = Substitute.For<IActionRelationResolver>();
        var dispatcher = new RelationAuthorizationActionDispatcher(inner, graph, resolver);
        var context = Context(graph) with
        {
            ActionDescriptor = new ActionDescriptor(
                new ActionSubject("authorization", nameof(AuthorizationOrder)),
                "update",
                "spoofed"),
        };

        var result = await dispatcher.DispatchAsync(context, new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await resolver.DidNotReceive().HoldsAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<ActionPrecondition>(),
            Arg.Any<CancellationToken>());
        await inner.DidNotReceive().DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ObjectSetResolver_EmitsProviderTranslatableIdentifierPredicates()
    {
        var graph = BuildGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        ObjectSetExpression? captured = null;
        provider.ExecuteAsync<object>(Arg.Do<ObjectSetExpression>(value => captured = value), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>([], 0, ObjectSetInclusion.Properties));
        var resolver = new ObjectSetActionRelationResolver(graph, provider);

        var holds = await resolver.HoldsAsync(Context(graph), RelationPrecondition());

        var finalFilter = (FilterExpression)captured!;
        await Assert.That(holds).IsFalse();
        await Assert.That(finalFilter.Predicate.Body.NodeType).IsEqualTo(System.Linq.Expressions.ExpressionType.Equal);
    }

    [Test]
    public async Task ObjectSetResolver_DoesNotRetargetUnknownDomainToUniqueGlobalName()
    {
        var graph = BuildGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var resolver = new ObjectSetActionRelationResolver(graph, provider);
        var context = Context(graph) with { Domain = "other-domain" };

        await Assert.That(async () => await resolver.HoldsAsync(context, RelationPrecondition()))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("authoritative ontology metadata");
        await provider.DidNotReceive().ExecuteAsync<object>(
            Arg.Any<ObjectSetExpression>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ObjectSetResolver_TraversesTargetPathAndPrincipalRelation()
    {
        var graph = BuildGraph();
        var provider = await SeedProviderAsync(graph, includeOwnerRelation: true);
        var resolver = new ObjectSetActionRelationResolver(graph, provider);

        var holds = await resolver.HoldsAsync(Context(graph), RelationPrecondition());

        await Assert.That(holds).IsTrue();
    }

    [Test]
    public async Task GetValidActionsAsync_ExcludesActionWhenCallingPrincipalDoesNotHoldRelation()
    {
        var graph = BuildGraph();
        var provider = await SeedProviderAsync(graph, includeOwnerRelation: false);
        var query = new OntologyQueryService(
            graph,
            provider,
            Substitute.For<IActionDispatcher>(),
            Substitute.For<IEventStreamProvider>());

        var actions = await query.GetValidActionsAsync(
            Owner,
            "authorization",
            nameof(AuthorizationOrder),
            "order-1");

        await Assert.That(actions.Select(action => action.Name)).IsEquivalentTo(["view"]);
    }

    [Test]
    public async Task GetValidActionsAsync_IncludesActionWhenCallingPrincipalHoldsRelation()
    {
        var graph = BuildGraph();
        var provider = await SeedProviderAsync(graph, includeOwnerRelation: true);
        var query = new OntologyQueryService(
            graph,
            provider,
            Substitute.For<IActionDispatcher>(),
            Substitute.For<IEventStreamProvider>());

        var actions = await query.GetValidActionsAsync(
            Owner,
            "authorization",
            nameof(AuthorizationOrder),
            "order-1");

        await Assert.That(actions.Select(action => action.Name)).IsEquivalentTo(["update", "view"]);
    }

    [Test]
    public async Task GetCandidateActionsAsync_MissingRelationResolver_RetainsIndeterminateAction()
    {
        var graph = BuildGraph();
        var query = new OntologyQueryService(graph);

        var candidates = await query.GetCandidateActionsAsync(
            Owner,
            "authorization",
            nameof(AuthorizationOrder),
            "order-1");
        var valid = await query.GetValidActionsAsync(
            Owner,
            "authorization",
            nameof(AuthorizationOrder),
            "order-1");

        var update = candidates.Single(candidate => candidate.Action.Name == "update");
        await Assert.That(update.Availability).IsEqualTo(ActionAvailability.Indeterminate);
        await Assert.That(valid.Select(action => action.Name)).Contains("update");
    }

    [Test]
    public async Task GetCandidateActionsAsync_DefaultResolverMissingSymbolIdentity_IsIndeterminateAndRetained()
    {
        var graph = BuildSymbolKeyOnlyGraphWithoutIdentityInfrastructure();
        var provider = Substitute.For<IObjectSetProvider>();
        var query = new OntologyQueryService(
            graph,
            provider,
            Substitute.For<IActionDispatcher>(),
            Substitute.For<IEventStreamProvider>());
        var principal = new ActionPrincipal("PolyUser", "user-1");

        var candidates = await query.GetCandidateActionsAsync(
            principal,
            "poly",
            "PolyOrder",
            "order-1");
        var valid = await query.GetValidActionsAsync(
            principal,
            "poly",
            "PolyOrder",
            "order-1");

        var inspect = candidates.Single(candidate => candidate.Action.Name == "inspect");
        await Assert.That(inspect.Availability).IsEqualTo(ActionAvailability.Indeterminate);
        await Assert.That(inspect.Constraints.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await Assert.That(valid.Select(action => action.Name)).Contains("inspect");
        await provider.DidNotReceive().ExecuteAsync<object>(
            Arg.Any<ObjectSetExpression>(),
            Arg.Any<CancellationToken>());
    }

    private static ActionContext Context(OntologyGraph graph) =>
        new(Owner, "authorization", nameof(AuthorizationOrder), "order-1", "update")
        {
            ActionDescriptor = graph
                .GetObjectType("authorization", nameof(AuthorizationOrder))!
                .Actions.Single(action => action.Name == "update"),
        };

    private static ActionPrecondition RelationPrecondition() =>
        new(
            ActionPredicate.RelationHolds("owner", "space"),
            "caller owns the order space");

    private static OntologyGraph BuildGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<AuthorizationOntology>();
        return builder.Build();
    }

    private static OntologyGraph BuildSymbolKeyOnlyGraphWithoutIdentityInfrastructure()
    {
        var action = new ActionDescriptor(
            new ActionSubject("poly", "PolyOrder"),
            "inspect",
            "inspect")
        {
            Preconditions = [new ActionPrecondition(ActionPredicate.RelationHolds("owner"), "owner")],
        };
        var order = new ObjectTypeDescriptor
        {
            Name = "PolyOrder",
            DomainName = "poly",
            ClrType = null,
            SymbolKey = "scip-typescript ./orders.ts#Order",
            Links = [new LinkDescriptor("owner", "PolyUser", LinkCardinality.OneToOne)],
            Actions = [action],
            // Deliberately no IdAccessor or IdPredicateFactory: a default relation
            // resolver cannot authoritatively identify the action target.
        };
        var user = new ObjectTypeDescriptor
        {
            Name = "PolyUser",
            DomainName = "poly",
            ClrType = null,
            SymbolKey = "scip-typescript ./users.ts#User",
        };
        ObjectTypeDescriptor[] objectTypes = [order, user];
        return new OntologyGraph(
            domains: [new DomainDescriptor("poly") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    private static async Task<InMemoryObjectSetProvider> SeedProviderAsync(
        OntologyGraph graph,
        bool includeOwnerRelation)
    {
        var provider = new InMemoryObjectSetProvider(graph);
        provider.Seed(new AuthorizationOrder("order-1"), string.Empty);
        provider.Seed(new AuthorizationSpace("space-1"), string.Empty);
        provider.Seed(new AuthorizationUser("user-1"), string.Empty);
        await provider.RelateAsync(
            nameof(AuthorizationOrder),
            "order-1",
            "space",
            nameof(AuthorizationSpace),
            "space-1");

        if (includeOwnerRelation)
        {
            await provider.RelateAsync(
                nameof(AuthorizationSpace),
                "space-1",
                "owner",
                nameof(AuthorizationUser),
                "user-1");
        }

        return provider;
    }
}

public sealed record AuthorizationOrder(string Id);

public sealed record AuthorizationSpace(string Id);

public sealed record AuthorizationUser(string Id);

public sealed class AuthorizationOntology : DomainOntology
{
    public override string DomainName => "authorization";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AuthorizationOrder>(objectType =>
        {
            objectType.Key(order => order.Id);
            objectType.HasOne<AuthorizationSpace>("space");
            objectType.Action("update")
                .RequiresRelation("owner", "space")
                .BoundToWorkflow("update-order");
            objectType.Action("view")
                .ReadOnly()
                .BoundToWorkflow("view-order");
        });

        builder.Object<AuthorizationSpace>(objectType =>
        {
            objectType.Key(space => space.Id);
            objectType.HasOne<AuthorizationUser>("owner");
        });

        builder.Object<AuthorizationUser>(objectType => objectType.Key(user => user.Id));
    }
}
