using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Actions;

public sealed class ActionPredicateEvaluatorTests
{
    private static readonly PredicatePropertyReference Quantity = new(
        "Quantity",
        PredicateScalarKind.Integer);

    private static readonly PredicatePropertyReference Label = new(
        "Label",
        PredicateScalarKind.String,
        isNullable: true);

    [Test]
    public async Task Evaluate_StrongKleeneConjunction_FalseDominatesUnknown()
    {
        var predicate = ActionPredicate.All(
            ActionPredicate.Property(
                Quantity,
                PredicateComparisonOperator.GreaterThan,
                PredicateLiteral.Integer(0)),
            ActionPredicate.LinkExists("Owner"));
        var facts = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Quantity"] = PredicateLiteral.Integer(-1),
            });

        var result = new ActionPredicateEvaluator().Evaluate(predicate, facts);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Unsatisfied);
    }

    [Test]
    public async Task Evaluate_StrongKleeneDisjunction_TrueDominatesUnknown()
    {
        var predicate = ActionPredicate.Any(
            ActionPredicate.Property(
                Quantity,
                PredicateComparisonOperator.GreaterThan,
                PredicateLiteral.Integer(0)),
            ActionPredicate.LinkExists("Owner"));
        var facts = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Quantity"] = PredicateLiteral.Integer(1),
            });

        var result = new ActionPredicateEvaluator().Evaluate(predicate, facts);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Satisfied);
    }

    [Test]
    public async Task Evaluate_StrongKleeneNegation_PreservesUnknown()
    {
        var predicate = ActionPredicate.Not(ActionPredicate.LinkExists("Owner"));

        var result = new ActionPredicateEvaluator().Evaluate(predicate, ActionFacts.Empty);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task Evaluate_ExplicitNull_IsDifferentFromMissingFact()
    {
        var predicate = ActionPredicate.Property(
            Label,
            PredicateComparisonOperator.Equal,
            PredicateLiteral.Null);
        var explicitNull = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Label"] = PredicateLiteral.Null,
            });
        var evaluator = new ActionPredicateEvaluator();

        var knownResult = evaluator.Evaluate(predicate, explicitNull);
        var missingResult = evaluator.Evaluate(predicate, ActionFacts.Empty);

        await Assert.That(knownResult).IsEqualTo(PredicateTruthValue.Satisfied);
        await Assert.That(missingResult).IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task ActionFacts_SnapshotsMutableInputs()
    {
        var properties = new Dictionary<string, PredicateLiteral>
        {
            ["Quantity"] = PredicateLiteral.Integer(1),
        };
        var links = new Dictionary<string, bool> { ["Owner"] = true };
        var facts = new ActionFacts(properties, links);

        properties["Quantity"] = PredicateLiteral.Integer(2);
        links["Owner"] = false;

        await Assert.That(facts.Properties["Quantity"])
            .IsEqualTo(PredicateLiteral.Integer(1));
        await Assert.That(facts.Links["Owner"]).IsTrue();
    }

    [Test]
    public async Task Evaluate_ExactDecimalComparison_DoesNotUseFloatingPoint()
    {
        var property = new PredicatePropertyReference("Amount", PredicateScalarKind.Decimal);
        var predicate = ActionPredicate.Property(
            property,
            PredicateComparisonOperator.GreaterThan,
            PredicateLiteral.Decimal("9007199254740993.000000000000000001"));
        var facts = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Amount"] = PredicateLiteral.Decimal("9007199254740993.000000000000000002"),
            });

        var result = new ActionPredicateEvaluator().Evaluate(predicate, facts);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Satisfied);
    }

    [Test]
    public async Task Evaluate_NullableNumericOrdering_ExplicitNullIsUnsatisfied()
    {
        var predicate = ActionPredicate.Property(
            new PredicatePropertyReference(
                "Score",
                PredicateScalarKind.Integer,
                isNullable: true),
            PredicateComparisonOperator.GreaterThan,
            PredicateLiteral.Integer(1));
        var facts = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Score"] = PredicateLiteral.Null,
        });

        var result = new ActionPredicateEvaluator().Evaluate(predicate, facts);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Unsatisfied);
    }

    [Test]
    public async Task EvaluateAsync_NestedRelationFormula_EvaluatesWholeFormula()
    {
        var relationResolver = Substitute.For<IActionRelationResolver>();
        relationResolver.HoldsAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<ActionPrecondition>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var evaluator = new ActionPredicateEvaluator(relationResolver);
        var predicate = ActionPredicate.All(
            ActionPredicate.RelationHolds("owner"),
            ActionPredicate.Property(
                Quantity,
                PredicateComparisonOperator.GreaterThan,
                PredicateLiteral.Integer(0)));
        var facts = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Quantity"] = PredicateLiteral.Integer(1),
            });

        var result = await evaluator.EvaluateAsync(
            predicate,
            facts,
            Context(),
            request: null);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Satisfied);
        await relationResolver.Received(1).HoldsAsync(
            Arg.Any<ActionContext>(),
            Arg.Is<ActionPrecondition>(value => value.Predicate is RelationHoldsPredicate),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EvaluateAsync_RepeatedRelationAtom_UsesOneCoherentValuation()
    {
        var relationResolver = Substitute.For<IActionRelationResolver>();
        relationResolver.HoldsAsync(
                Arg.Any<ActionContext>(),
                Arg.Any<ActionPrecondition>(),
                Arg.Any<CancellationToken>())
            .Returns(false, true);
        var relation = ActionPredicate.RelationHolds("owner");
        var quantityPositive = ActionPredicate.Property(
            Quantity,
            PredicateComparisonOperator.GreaterThan,
            PredicateLiteral.Integer(0));
        var predicate = ActionPredicate.All(
            relation,
            ActionPredicate.Any(ActionPredicate.Not(relation), quantityPositive));
        var facts = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Quantity"] = PredicateLiteral.Integer(0),
        });

        var result = await new ActionPredicateEvaluator(relationResolver).EvaluateAsync(
            predicate,
            facts,
            Context(),
            request: null);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Unsatisfied);
        await relationResolver.Received(1).HoldsAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<ActionPrecondition>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EvaluateAsync_MissingCustomEvaluator_IsIndeterminate()
    {
        var predicate = ActionPredicate.Custom("approval-v1");

        var result = await new ActionPredicateEvaluator().EvaluateAsync(
            predicate,
            ActionFacts.Empty,
            Context(),
            request: null);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task EvaluateAsync_RegisteredCustomEvaluator_ReceivesTypedInputs()
    {
        var customEvaluator = new SatisfiedCustomEvaluator();
        var predicate = ActionPredicate.Custom(
            "approval-v1",
            arguments: [PredicateLiteral.Symbol("supervisor")],
            readSet: [ActionResource.Property("Quantity")]);
        var facts = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Quantity"] = PredicateLiteral.Integer(3),
        });

        var result = await new ActionPredicateEvaluator(customEvaluators: [customEvaluator])
            .EvaluateAsync(predicate, facts, Context(), request: null);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Satisfied);
        await Assert.That(customEvaluator.LastContext).IsNotNull();
        await Assert.That(customEvaluator.LastContext!.Predicate.Arguments.Single())
            .IsEqualTo(PredicateLiteral.Symbol("supervisor"));
        await Assert.That(customEvaluator.LastContext.Facts.Properties).HasCount().EqualTo(1);
        await Assert.That(customEvaluator.LastContext.Facts.Properties["Quantity"])
            .IsEqualTo(PredicateLiteral.Integer(3));
    }

    [Test]
    public async Task EvaluateAsync_CustomEvaluator_MissingDeclaredFactIsIndeterminateWithoutInvocation()
    {
        var customEvaluator = new SatisfiedCustomEvaluator();
        var predicate = ActionPredicate.Custom(
            "approval-v1",
            readSet:
            [
                ActionResource.Property("Quantity"),
                ActionResource.Link("Owner"),
            ]);
        var partialFacts = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Quantity"] = PredicateLiteral.Integer(3),
        });

        var result = await new ActionPredicateEvaluator(customEvaluators: [customEvaluator])
            .EvaluateAsync(predicate, partialFacts, Context(), request: null);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Indeterminate);
        await Assert.That(customEvaluator.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task EvaluateAsync_CustomEvaluator_ReceivesOnlyDeclaredTargetFacts()
    {
        var customEvaluator = new SatisfiedCustomEvaluator();
        var predicate = ActionPredicate.Custom(
            "approval-v1",
            readSet:
            [
                ActionResource.Property("Quantity"),
                ActionResource.Link("Owner"),
                ActionResource.External("credit-service"),
                ActionResource.Event("ApprovalChanged"),
            ]);
        var facts = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Quantity"] = PredicateLiteral.Integer(3),
                ["UndeclaredProperty"] = PredicateLiteral.String("secret"),
            },
            links: new Dictionary<string, bool>
            {
                ["Owner"] = false,
                ["UndeclaredLink"] = true,
            });

        var result = await new ActionPredicateEvaluator(customEvaluators: [customEvaluator])
            .EvaluateAsync(predicate, facts, Context(), request: null);

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Satisfied);
        await Assert.That(customEvaluator.LastContext).IsNotNull();
        await Assert.That(customEvaluator.LastContext!.Facts.Properties.Keys)
            .IsEquivalentTo(["Quantity"]);
        await Assert.That(customEvaluator.LastContext.Facts.Links.Keys)
            .IsEquivalentTo(["Owner"]);
        await Assert.That(customEvaluator.LastContext.Facts.Links["Owner"]).IsFalse();
    }

    [Test]
    public async Task EvaluateAsync_CustomEvaluatorFailure_IsIndeterminate()
    {
        var predicate = ActionPredicate.Custom("approval-v1");
        var evaluator = new ActionPredicateEvaluator(
            customEvaluators: [new ThrowingCustomEvaluator()]);

        var result = await evaluator.EvaluateAsync(
            predicate,
            ActionFacts.Empty,
            Context(),
            request: new { Approved = true });

        await Assert.That(result).IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task Constructor_DuplicateCustomEvaluatorKeys_AreRejected()
    {
        await Assert.That(() => new ActionPredicateEvaluator(
                customEvaluators: [new SatisfiedCustomEvaluator(), new SatisfiedCustomEvaluator()]))
            .Throws<InvalidOperationException>();
    }

    private static ActionContext Context() => new(
        new ActionPrincipal("User", "user-1"),
        "orders",
        "Order",
        "order-1",
        "Ship");

    private sealed class ThrowingCustomEvaluator : ICustomActionPredicateEvaluator
    {
        public string EvaluatorKey => "approval-v1";

        public ValueTask<PredicateTruthValue> EvaluateAsync(
            CustomActionPredicateContext context,
            CancellationToken ct = default) =>
            ValueTask.FromException<PredicateTruthValue>(new InvalidOperationException("failed"));
    }

    private sealed class SatisfiedCustomEvaluator : ICustomActionPredicateEvaluator
    {
        public string EvaluatorKey => "approval-v1";

        public CustomActionPredicateContext? LastContext { get; private set; }

        public int CallCount { get; private set; }

        public ValueTask<PredicateTruthValue> EvaluateAsync(
            CustomActionPredicateContext context,
            CancellationToken ct = default)
        {
            CallCount++;
            LastContext = context;
            return ValueTask.FromResult(PredicateTruthValue.Satisfied);
        }
    }
}
