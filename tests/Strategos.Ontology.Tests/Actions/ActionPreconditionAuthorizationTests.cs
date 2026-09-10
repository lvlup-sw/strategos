using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Actions;

public sealed class ActionPreconditionAuthorizationTests
{
    [Test]
    public async Task Dispatch_EnforcedHardPredicate_UsesAuthoritativeFacts()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var factResolver = new FixedFactResolver(Facts(quantity: 2));
        var dispatcher = Dispatcher(inner, graph, factResolver);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Ship", enforcePreconditions: true),
            new { Quantity = -100 });

        await Assert.That(result.IsSuccess).IsTrue();
        await inner.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(value => value.ActionDescriptor!.Name == "Ship"),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_CallerRequestCannotSpoofAuthoritativeFacts()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(inner, graph, new FixedFactResolver(Facts(quantity: 0)));

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Ship", enforcePreconditions: true),
            new { Quantity = 100 });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Unsatisfied);
        await inner.DidNotReceive().DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_MissingFactResolver_FailsClosedAsIndeterminate()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(inner, graph, factResolver: null);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Ship", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task Dispatch_FailingFactResolver_FailsClosedAsIndeterminate()
    {
        var graph = BuildGraph();
        var dispatcher = Dispatcher(SuccessfulInner(), graph, new ThrowingFactResolver());

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Ship", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task Dispatch_GeneralEnforcementDisabled_DoesNotBlockNonRelationPredicate()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(inner, graph, factResolver: null);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Ship", enforcePreconditions: false),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Dispatch_RelationContainingFormula_IsAlwaysMandatoryAndComplete()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var relationResolver = Substitute.For<IActionRelationResolver>();
        relationResolver.HoldsAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<ActionPrecondition>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var dispatcher = Dispatcher(
            inner,
            graph,
            factResolver: null,
            relationResolver);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Transfer", enforcePreconditions: false),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await relationResolver.Received(1).HoldsAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<ActionPrecondition>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_SoftPredicate_NeverBlocks()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(inner, graph, factResolver: null);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Audit", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Dispatch_MissingCustomEvaluator_FailsClosedAsIndeterminate()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(inner, graph, factResolver: null);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Approve", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task Dispatch_RegisteredCustomEvaluator_SatisfiedAllowsDispatch()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(
            inner,
            graph,
            factResolver: null,
            customEvaluators: [new FixedCustomEvaluator(PredicateTruthValue.Satisfied)]);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Approve", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await inner.Received(1).DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_CustomTargetRead_MissingFactResolverFailsClosedBeforeEvaluator()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var customEvaluator = new FixedCustomEvaluator(
            PredicateTruthValue.Satisfied,
            "approval-with-facts-v1");
        var dispatcher = Dispatcher(
            inner,
            graph,
            factResolver: null,
            customEvaluators: [customEvaluator]);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "ApproveWithFacts", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await Assert.That(customEvaluator.CallCount).IsEqualTo(0);
        await inner.DidNotReceive().DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_CustomTargetRead_FailingFactResolverFailsClosedBeforeEvaluator()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var customEvaluator = new FixedCustomEvaluator(
            PredicateTruthValue.Satisfied,
            "approval-with-facts-v1");
        var dispatcher = Dispatcher(
            inner,
            graph,
            new ThrowingFactResolver(),
            customEvaluators: [customEvaluator]);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "ApproveWithFacts", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await Assert.That(customEvaluator.CallCount).IsEqualTo(0);
        await inner.DidNotReceive().DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_FailingCustomEvaluator_FailsClosedAsIndeterminate()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var dispatcher = Dispatcher(
            inner,
            graph,
            factResolver: null,
            customEvaluators: [new ThrowingCustomEvaluator()]);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "Approve", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations!.Hard.Single().TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
        await inner.DidNotReceive().DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetCandidateActionsAsync_CustomEvaluatorControlsTriStateAvailability()
    {
        var graph = BuildGraph();
        var satisfiedQuery = new OntologyQueryService(
            graph,
            objectSetProvider: null,
            actionDispatcher: null,
            eventStreamProvider: null,
            relationResolver: null,
            customEvaluators: [new FixedCustomEvaluator(PredicateTruthValue.Satisfied)]);
        var indeterminateQuery = new OntologyQueryService(
            graph,
            objectSetProvider: null,
            actionDispatcher: null,
            eventStreamProvider: null,
            relationResolver: null,
            customEvaluators: [new ThrowingCustomEvaluator()]);
        var unsatisfiedQuery = new OntologyQueryService(
            graph,
            objectSetProvider: null,
            actionDispatcher: null,
            eventStreamProvider: null,
            relationResolver: null,
            customEvaluators: [new FixedCustomEvaluator(PredicateTruthValue.Unsatisfied)]);

        var satisfied = await satisfiedQuery.GetCandidateActionsAsync(
            new ActionPrincipal("User", "user-1"),
            "runtime",
            nameof(RuntimeOrder),
            "order-1");
        var indeterminate = await indeterminateQuery.GetCandidateActionsAsync(
            new ActionPrincipal("User", "user-1"),
            "runtime",
            nameof(RuntimeOrder),
            "order-1");
        var unsatisfied = await unsatisfiedQuery.GetCandidateActionsAsync(
            new ActionPrincipal("User", "user-1"),
            "runtime",
            nameof(RuntimeOrder),
            "order-1");

        await Assert.That(satisfied.Single(candidate => candidate.Action.Name == "Approve").Availability)
            .IsEqualTo(ActionAvailability.Available);
        await Assert.That(indeterminate.Single(candidate => candidate.Action.Name == "Approve").Availability)
            .IsEqualTo(ActionAvailability.Indeterminate);
        await Assert.That(unsatisfied.Select(candidate => candidate.Action.Name))
            .DoesNotContain("Approve");
    }

    [Test]
    public async Task GetCandidateActionsAsync_ReusesCustomAtomAcrossHardAndSoftConstraints()
    {
        var graph = BuildGraph();
        var customEvaluator = new FixedCustomEvaluator(
            PredicateTruthValue.Satisfied,
            "repeat-v1");
        var query = new OntologyQueryService(
            graph,
            objectSetProvider: null,
            actionDispatcher: null,
            eventStreamProvider: null,
            relationResolver: null,
            customEvaluators: [customEvaluator]);

        var candidates = await query.GetCandidateActionsAsync(
            new ActionPrincipal("User", "user-1"),
            "runtime",
            nameof(RuntimeOrder),
            "order-1");

        var repeated = candidates.Single(candidate => candidate.Action.Name == "RepeatApproval");
        await Assert.That(repeated.Availability).IsEqualTo(ActionAvailability.Available);
        await Assert.That(repeated.Constraints).HasCount().EqualTo(3);
        await Assert.That(repeated.Constraints.All(constraint => constraint.IsSatisfied)).IsTrue();
        await Assert.That(customEvaluator.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispatch_ReusesCustomAtomAcrossApplicableHardConstraints()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var customEvaluator = new FixedCustomEvaluator(
            PredicateTruthValue.Satisfied,
            "repeat-v1");
        var dispatcher = Dispatcher(
            inner,
            graph,
            factResolver: null,
            customEvaluators: [customEvaluator]);

        var result = await dispatcher.DispatchAsync(
            Context(graph, "RepeatApproval", enforcePreconditions: true),
            new { });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(customEvaluator.CallCount).IsEqualTo(1);
        await inner.Received(1).DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_SpoofedDescriptor_IsRejectedBeforeResolvers()
    {
        var graph = BuildGraph();
        var inner = SuccessfulInner();
        var factResolver = new FixedFactResolver(Facts(quantity: 2));
        var dispatcher = Dispatcher(inner, graph, factResolver);
        var context = Context(graph, "Ship", enforcePreconditions: true) with
        {
            ActionDescriptor = new ActionDescriptor(
                new ActionSubject("runtime", nameof(RuntimeOrder)),
                "Ship",
                "spoofed"),
        };

        var result = await dispatcher.DispatchAsync(context, new { });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(factResolver.CallCount).IsEqualTo(0);
    }

    private static RelationAuthorizationActionDispatcher Dispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionFactResolver? factResolver,
        IActionRelationResolver? relationResolver = null,
        IEnumerable<ICustomActionPredicateEvaluator>? customEvaluators = null) =>
        new(
            inner,
            graph,
            factResolver,
            relationResolver,
            customEvaluators ?? [],
            NullLogger<RelationAuthorizationActionDispatcher>.Instance);

    private static IActionDispatcher SuccessfulInner()
    {
        var inner = Substitute.For<IActionDispatcher>();
        inner.DispatchAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));
        return inner;
    }

    private static ActionContext Context(
        OntologyGraph graph,
        string actionName,
        bool enforcePreconditions)
    {
        var descriptor = graph.GetObjectType("runtime", nameof(RuntimeOrder))!
            .Actions.Single(action => action.Name == actionName);
        return new ActionContext(
            new ActionPrincipal("User", "user-1"),
            "runtime",
            nameof(RuntimeOrder),
            "order-1",
            actionName,
            new ActionDispatchOptions { EnforcePreconditions = enforcePreconditions })
        {
            ActionDescriptor = descriptor,
        };
    }

    private static ActionFacts Facts(int quantity) => new(
        properties: new Dictionary<string, PredicateLiteral>
        {
            [nameof(RuntimeOrder.Quantity)] = PredicateLiteral.Integer(quantity),
        });

    private static OntologyGraph BuildGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<RuntimePredicateOntology>();
        return builder.Build();
    }

    private sealed class FixedFactResolver(ActionFacts facts) : IActionFactResolver
    {
        public int CallCount { get; private set; }

        public ValueTask<ActionFacts?> ResolveAsync(
            ActionContext context,
            CancellationToken ct = default)
        {
            CallCount++;
            return ValueTask.FromResult<ActionFacts?>(facts);
        }
    }

    private sealed class ThrowingFactResolver : IActionFactResolver
    {
        public ValueTask<ActionFacts?> ResolveAsync(
            ActionContext context,
            CancellationToken ct = default) =>
            ValueTask.FromException<ActionFacts?>(new InvalidOperationException("failed"));
    }

    private sealed class FixedCustomEvaluator(
        PredicateTruthValue result,
        string evaluatorKey = "approval-v1")
        : ICustomActionPredicateEvaluator
    {
        public string EvaluatorKey => evaluatorKey;

        public int CallCount { get; private set; }

        public ValueTask<PredicateTruthValue> EvaluateAsync(
            CustomActionPredicateContext context,
            CancellationToken ct = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingCustomEvaluator : ICustomActionPredicateEvaluator
    {
        public string EvaluatorKey => "approval-v1";

        public ValueTask<PredicateTruthValue> EvaluateAsync(
            CustomActionPredicateContext context,
            CancellationToken ct = default) =>
            ValueTask.FromException<PredicateTruthValue>(new InvalidOperationException("failed"));
    }
}

public sealed record RuntimeOrder(string Id, int Quantity);

public sealed class RuntimePredicateOntology : DomainOntology
{
    public override string DomainName => "runtime";

    protected override void Define(IOntologyBuilder builder)
    {
        var quantity = new PredicatePropertyReference(
            nameof(RuntimeOrder.Quantity),
            PredicateScalarKind.Integer);
        builder.Object<RuntimeOrder>(objectType =>
        {
            objectType.Key(order => order.Id);
            objectType.Property(order => order.Quantity);
            objectType.Action("Ship")
                .Requires(order => order.Quantity > 0);
            objectType.Action("Transfer")
                .Requires(ActionPredicate.All(
                    ActionPredicate.RelationHolds("owner"),
                    ActionPredicate.Property(
                        quantity,
                        PredicateComparisonOperator.GreaterThan,
                        PredicateLiteral.Integer(0))));
            objectType.Action("Audit")
                .RequiresSoft(order => order.Quantity > 0);
            objectType.Action("Approve")
                .Requires(ActionPredicate.Custom("approval-v1"));
            objectType.Action("ApproveWithFacts")
                .Requires(ActionPredicate.Custom(
                    "approval-with-facts-v1",
                    readSet: [ActionResource.Property(nameof(RuntimeOrder.Quantity))]));
            objectType.Action("RepeatApproval")
                .Requires(ActionPredicate.Custom("repeat-v1"))
                .Requires(ActionPredicate.Custom("repeat-v1"))
                .RequiresSoft(ActionPredicate.Custom("repeat-v1"));
        });
    }
}
