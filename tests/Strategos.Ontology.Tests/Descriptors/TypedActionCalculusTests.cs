using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;

using Strategos.Ontology.ActionLogic;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public sealed class TypedActionCalculusTests
{
    private static readonly ActionSubject Subject = new("orders", "Order");
    private static readonly AuthorityLattice EmptyLattice = new([], []);

    [Test]
    public async Task ForgettingWrittenPropertyFromConjunctionPreservesOtherFact()
    {
        var action = Action(
            "write-x",
            requires: ActionPredicate.All(Integer("x", 1), Integer("y", 2)),
            frame: [ActionResource.Property("x")]);

        var composite = ActionCalculus.Sequential(EmptyLattice, action);

        await Assert.That(composite.FinalGuarantee).IsEqualTo(Integer("y", 2));
    }

    [Test]
    public async Task ForgettingWrittenPropertyFromDisjunctionProducesTrue()
    {
        var action = Action(
            "write-x",
            requires: ActionPredicate.Any(Integer("x", 1), Integer("y", 2)),
            frame: [ActionResource.Property("x")]);

        var composite = ActionCalculus.Sequential(EmptyLattice, action);

        await Assert.That(composite.FinalGuarantee).IsEqualTo(ActionPredicate.True);
    }

    [Test]
    public async Task ModifiesPropertyDoesNotImplyResultingValue()
    {
        var upstream = new ActionDescriptor(Subject, "modify", "modify")
        {
            TouchedResources = [ActionResource.Property("Status")],
            Postconditions =
            [
                new ActionPostcondition
                {
                    Kind = PostconditionKind.ModifiesProperty,
                    PropertyName = "Status",
                },
            ],
        };
        var downstream = Action(
            "activate-dependent",
            requires: String("Status", "Active"));

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, downstream);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Refuted);
        await Assert.That(analysis.Contract!.VerificationStatus)
            .IsEqualTo(ActionContractVerificationStatus.Refuted);
        await Assert.That(analysis.Seams.Single().Counterexample.Single().Resource)
            .IsEqualTo("property|Status");
        await Assert.That(() => ActionCalculus.Sequential(EmptyLattice, upstream, downstream))
            .Throws<ActionCompositionException>();
    }

    [Test]
    public async Task CreatesLinkSoundlyImpliesLinkExists()
    {
        var upstream = new ActionDescriptor(Subject, "attach", "attach")
        {
            TouchedResources = [ActionResource.Link("invoice")],
            Postconditions =
            [
                new ActionPostcondition
                {
                    Kind = PostconditionKind.CreatesLink,
                    LinkName = "invoice",
                },
            ],
        };
        var downstream = Action("send", requires: ActionPredicate.LinkExists("invoice"));

        var composite = ActionCalculus.Sequential(EmptyLattice, upstream, downstream);

        await Assert.That(composite.Seams.Single().Status)
            .IsEqualTo(ActionCompositionSeamStatus.Proven);
    }

    [Test]
    public async Task GuaranteeAboutUntouchedStateMustFollowFromRequirement()
    {
        var invalid = Action(
            "invent-y",
            ensures: Integer("y", 2),
            frame: [ActionResource.Property("x")]);
        var valid = Action(
            "preserve-y",
            requires: Integer("y", 2),
            ensures: Integer("y", 2),
            frame: [ActionResource.Property("x")]);

        var invalidProof = ActionContractProofEngine.Analyze(invalid);
        var validProof = ActionContractProofEngine.Analyze(valid);

        await Assert.That(invalidProof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(invalidProof.Reason).Contains("untouched state");
        await Assert.That(validProof.Kind).IsEqualTo(ActionContractProofKind.Closed);
    }

    [Test]
    public async Task NestedRelationCannotBeEstablishedBySameNamedLocalLinkFrame()
    {
        var relationOnLinkedTarget = ActionPredicate.RelationHolds("owner", "space");
        var invalid = Action(
            "invent-linked-owner",
            ensures: relationOnLinkedTarget,
            frame: [ActionResource.Link("owner")]);
        var downstream = Action(
            "requires-linked-owner",
            requires: relationOnLinkedTarget);

        var proof = ActionContractProofEngine.Analyze(invalid);
        var composition = ActionCalculus.AnalyzeSequential(EmptyLattice, invalid, downstream);

        await Assert.That(proof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(proof.Reason).Contains("untouched state");
        await Assert.That(composition.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
    }

    [Test]
    public async Task ReplacingFirstRelationPathHopForgetsTheRelationFact()
    {
        var relationOnLinkedTarget = ActionPredicate.RelationHolds("owner", "space");
        var upstream = Action(
            "replace-space",
            requires: relationOnLinkedTarget,
            frame: [ActionResource.Link("space")]);
        var downstream = Action(
            "requires-linked-owner",
            requires: relationOnLinkedTarget);

        var composition = ActionCalculus.AnalyzeSequential(
            EmptyLattice,
            upstream,
            downstream);

        await Assert.That(composition.Status).IsEqualTo(ActionCompositionAnalysisStatus.Refuted);
        await Assert.That(composition.Seams.Single().Status)
            .IsEqualTo(ActionCompositionSeamStatus.Refuted);
    }

    [Test]
    public async Task ReplacingDirectRelationLinkForgetsTheRelationFact()
    {
        var directRelation = ActionPredicate.RelationHolds("owner");
        var upstream = Action(
            "replace-local-owner-link",
            requires: directRelation,
            frame: [ActionResource.Link("owner")]);
        var downstream = Action(
            "requires-direct-owner",
            requires: directRelation);

        var composition = ActionCalculus.AnalyzeSequential(
            EmptyLattice,
            upstream,
            downstream);

        await Assert.That(composition.Status).IsEqualTo(ActionCompositionAnalysisStatus.Refuted);
        await Assert.That(composition.Seams.Single().Status)
            .IsEqualTo(ActionCompositionSeamStatus.Refuted);
    }

    [Test]
    public async Task ContradictoryRequirementsAndGuaranteesAreRejected()
    {
        var contradictoryRequirement = Action(
            "bad-requires",
            requires: ActionPredicate.All(Integer("x", 1), Integer("x", 2)));
        var contradictoryGuarantee = Action(
            "bad-ensures",
            ensures: ActionPredicate.All(Integer("x", 1), Integer("x", 2)),
            frame: [ActionResource.Property("x")]);

        var requirementProof = ActionContractProofEngine.Analyze(contradictoryRequirement);
        var guaranteeProof = ActionContractProofEngine.Analyze(contradictoryGuarantee);

        await Assert.That(requirementProof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(requirementProof.Reason).Contains("requirements are contradictory");
        await Assert.That(guaranteeProof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(guaranteeProof.Reason).Contains("guarantees are contradictory");
    }

    [Test]
    public async Task SoftCustomRequirementDoesNotExcludeClosedContractFromProof()
    {
        var action = new ActionDescriptor(Subject, "soft-custom", "soft-custom")
        {
            Preconditions =
            [
                new ActionPrecondition(
                    ActionPredicate.Custom("advisory-score"),
                    "advisory score",
                    ConstraintStrength.Soft),
            ],
            Ensures = [new ActionGuarantee(Integer("x", 1))],
            TouchedResources = [ActionResource.Property("x")],
        };

        var proof = ActionContractProofEngine.Analyze(action);

        await Assert.That(proof.Kind).IsEqualTo(ActionContractProofKind.Closed);
        await Assert.That(proof.HasNontrivialEffectiveGuarantee).IsTrue();
    }

    [Test]
    public async Task ArbitraryBooleanAndNumericFormulasAreProvedWithoutDistribution()
    {
        var xIsOneOrTwo = ActionPredicate.Any(Integer("x", 1), Integer("x", 2));
        var upstream = Action(
            "choose",
            ensures: ActionPredicate.All(
                xIsOneOrTwo,
                ActionPredicate.Not(Boolean("disabled", true))),
            frame:
            [
                ActionResource.Property("x"),
                ActionResource.Property("disabled"),
            ]);
        var downstream = Action(
            "consume",
            requires: ActionPredicate.All(
                ActionPredicate.Not(Integer("x", 3)),
                Boolean("disabled", false)));

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, downstream);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Proven);
        await Assert.That(analysis.Seams.Single().Status)
            .IsEqualTo(ActionCompositionSeamStatus.Proven);
    }

    [Test]
    public async Task IntegerAndExactDecimalBoundariesRemainLossless()
    {
        var hugeInteger = BigInteger.Parse("999999999999999999999999999999999999999999");
        const string exact = "1000000000000000000000000000000000000000.00000000000000000001";
        var upstream = Action(
            "raise",
            ensures: ActionPredicate.All(
                Compare("count", PredicateScalarKind.Integer, PredicateComparisonOperator.GreaterThan, PredicateLiteral.Integer(hugeInteger)),
                Compare("price", PredicateScalarKind.Decimal, PredicateComparisonOperator.GreaterThan, PredicateLiteral.Decimal(exact))),
            frame:
            [
                ActionResource.Property("count"),
                ActionResource.Property("price"),
            ]);
        var downstream = Action(
            "consume",
            requires: ActionPredicate.All(
                Compare("count", PredicateScalarKind.Integer, PredicateComparisonOperator.NotEqual, PredicateLiteral.Integer(hugeInteger)),
                Compare("price", PredicateScalarKind.Decimal, PredicateComparisonOperator.NotEqual, PredicateLiteral.Decimal(exact))));

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, downstream);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Proven);
    }

    [Test]
    public async Task NullAndDiscreteOtherCellsProduceAStableMinimalWitness()
    {
        var upstream = Action("unconstrained");
        var downstream = Action(
            "requires-both",
            requires: ActionPredicate.Any(
                Compare(
                    "owner",
                    PredicateScalarKind.String,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Null,
                    isNullable: true),
                String("state", "ready")));

        var first = ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, downstream);
        var second = ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, downstream);

        await Assert.That(first.Status).IsEqualTo(ActionCompositionAnalysisStatus.Refuted);
        await Assert.That(first.Seams.Single().Counterexample)
            .IsEquivalentTo(second.Seams.Single().Counterexample);
        await Assert.That(first.Seams.Single().Counterexample.Select(fact => fact.Resource))
            .IsEquivalentTo(["property|owner", "property|state"]);
    }

    [Test]
    public async Task IntegerCounterexampleDisplaysAreCultureInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        customCulture.NumberFormat.NegativeSign = "~";
        try
        {
            CultureInfo.CurrentCulture = customCulture;
            var downstream = Action(
                "requires-count",
                requires: Compare(
                    "count",
                    PredicateScalarKind.Integer,
                    PredicateComparisonOperator.GreaterThan,
                    PredicateLiteral.Integer(-2)));

            var analysis = ActionCalculus.AnalyzeSequential(
                EmptyLattice,
                Action("unconstrained"),
                downstream);

            await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Refuted);
            await Assert.That(analysis.Seams.Single().Counterexample.Single().Value)
                .IsEqualTo("<-2");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public async Task NullNeverSatisfiesNumericOrdering()
    {
        var greaterThanOne = Compare(
            "score",
            PredicateScalarKind.Integer,
            PredicateComparisonOperator.GreaterThan,
            PredicateLiteral.Integer(1),
            isNullable: true);
        var notNull = Compare(
            "score",
            PredicateScalarKind.Integer,
            PredicateComparisonOperator.NotEqual,
            PredicateLiteral.Null,
            isNullable: true);
        var upstream = Action(
            "score",
            ensures: greaterThanOne,
            frame: [ActionResource.Property("score")]);
        var downstream = Action("consume-score", requires: notNull);

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, upstream, downstream);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Proven);
    }

    [Test]
    public async Task WrittenPropertyCannotChangeScalarDomainAcrossPreAndPostState()
    {
        var action = Action(
            "change-type",
            requires: Integer("value", 1),
            ensures: String("value", "one"),
            frame: [ActionResource.Property("value")]);

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, action);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(analysis.Errors.Single()).Contains("inconsistent scalar metadata");
    }

    [Test]
    public async Task AdjacentActionsCannotDisagreeOnAResourceScalarDomain()
    {
        var upstream = Action(
            "integer-value",
            ensures: Integer("value", 1),
            frame: [ActionResource.Property("value")]);
        var downstream = Action(
            "string-value",
            requires: String("value", "one"));

        var analysis = ActionCalculus.AnalyzeSequential(
            EmptyLattice,
            upstream,
            downstream);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(analysis.Errors.Single()).Contains("inconsistent scalar metadata");
    }

    [Test]
    public async Task NestedCompositionIsAssociativeAfterFlattening()
    {
        var first = Action(
            "first",
            ensures: Integer("x", 1),
            frame: [ActionResource.Property("x")]);
        var second = Action(
            "second",
            requires: Integer("x", 1),
            ensures: Integer("y", 2),
            frame: [ActionResource.Property("y")]);
        var third = Action(
            "third",
            requires: ActionPredicate.All(Integer("x", 1), Integer("y", 2)));

        var left = ActionCalculus.Sequential(
            EmptyLattice,
            ActionCalculus.Sequential(EmptyLattice, first, second),
            third);
        var right = ActionCalculus.Sequential(
            EmptyLattice,
            first,
            ActionCalculus.Sequential(EmptyLattice, second, third));

        await Assert.That(left.Actions.Select(action => action.Name))
            .IsEquivalentTo(right.Actions.Select(action => action.Name));
        await Assert.That(left.FirstRequirement).IsEqualTo(right.FirstRequirement);
        await Assert.That(left.FinalGuarantee).IsEqualTo(right.FinalGuarantee);
        await Assert.That(left.Seams.Select(seam => seam.Status))
            .IsEquivalentTo(right.Seams.Select(seam => seam.Status));
    }

    [Test]
    public async Task InterveningWriteForgetsAnEarlierFact()
    {
        var first = Action(
            "first",
            ensures: Integer("x", 1),
            frame: [ActionResource.Property("x")]);
        var second = Action(
            "second",
            ensures: Integer("y", 2),
            frame: [ActionResource.Property("x"), ActionResource.Property("y")]);
        var third = Action("third", requires: Integer("x", 1));

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, first, second, third);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Refuted);
        await Assert.That(analysis.Seams[0].Status).IsEqualTo(ActionCompositionSeamStatus.Proven);
        await Assert.That(analysis.Seams[1].Status).IsEqualTo(ActionCompositionSeamStatus.Refuted);
    }

    [Test]
    public async Task IdentityIsEmptyButTrueTrueActionIsStillConcrete()
    {
        var identity = ActionCalculus.Sequential(EmptyLattice, ActionCalculus.Identity(Subject));
        var ordinary = ActionCalculus.Sequential(EmptyLattice, Action("ordinary"));
        var flattened = ActionCalculus.Sequential(
            EmptyLattice,
            ActionCalculus.Identity(Subject),
            ordinary,
            ActionCalculus.Identity(Subject));

        await Assert.That(identity.IsIdentity).IsTrue();
        await Assert.That(identity.Actions).IsEmpty();
        await Assert.That(ordinary.IsIdentity).IsFalse();
        await Assert.That(ordinary.Actions).HasSingleItem();
        await Assert.That(flattened.Actions).HasSingleItem();
    }

    [Test]
    public async Task ImmutableOperandCollectionsUseTheMaterializedSequenceOverload()
    {
        ImmutableArray<ActionCompositionOperand> operands =
        [
            Action("first"),
            Action("second"),
        ];

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, operands);
        var composite = ActionCalculus.Sequential(EmptyLattice, operands);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Proven);
        await Assert.That(composite.Actions.Select(action => action.Name))
            .IsEquivalentTo(["first", "second"]);
    }

    [Test]
    public async Task SameObjectNameInDifferentDomainsDoesNotCompose()
    {
        var other = new ActionDescriptor(new ActionSubject("billing", "Order"), "bill", "bill");

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, Action("ship"), other);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(analysis.Errors.Single()).Contains("do not match");
    }

    [Test]
    public async Task CustomPredicateProducesPartialVerificationAndExplicitExclusion()
    {
        var custom = ActionPredicate.Custom(
            "orders.credit-approved.v1",
            [PredicateLiteral.Symbol("standard")],
            [ActionResource.Property("CreditStatus")]);
        var opaque = Action("opaque", requires: custom);
        var closed = Action("closed");

        var composite = ActionCalculus.Sequential(EmptyLattice, opaque, closed);

        await Assert.That(composite.VerificationStatus)
            .IsEqualTo(ActionContractVerificationStatus.PartiallyVerified);
        await Assert.That(composite.OpaqueExclusions.Single().EvaluatorKeys.Single())
            .IsEqualTo("orders.credit-approved.v1");
        await Assert.That(composite.Seams.Single().Status)
            .IsEqualTo(ActionCompositionSeamStatus.Opaque);
    }

    [Test]
    public async Task ClosedContradictionInsideOpaqueContractIsStillInvalid()
    {
        var custom = ActionPredicate.Custom("orders.runtime-check.v1");
        var contradictoryRequirement = Action(
            "opaque-bad-requirement",
            requires: ActionPredicate.All(custom, Integer("x", 1), Integer("x", 2)));
        var contradictoryGuarantee = Action(
            "opaque-bad-guarantee",
            ensures: ActionPredicate.All(custom, Integer("x", 1), Integer("x", 2)),
            frame: [ActionResource.Property("x")]);

        var requirementProof = ActionContractProofEngine.Analyze(contradictoryRequirement);
        var guaranteeProof = ActionContractProofEngine.Analyze(contradictoryGuarantee);

        await Assert.That(requirementProof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(requirementProof.Reason).Contains("independently");
        await Assert.That(guaranteeProof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(guaranteeProof.Reason).Contains("independently");
    }

    [Test]
    public async Task OpaqueGuaranteeCannotHideClosedUntouchedFrameViolation()
    {
        var custom = ActionPredicate.Custom("orders.runtime-check.v1");
        var invalid = Action(
            "opaque-unrealizable-frame",
            ensures: ActionPredicate.All(custom, Integer("y", 2)));
        var potentiallyValid = Action(
            "opaque-requirement-may-establish-y",
            requires: custom,
            ensures: Integer("y", 2));

        var invalidProof = ActionContractProofEngine.Analyze(invalid);
        var opaqueProof = ActionContractProofEngine.Analyze(potentiallyValid);

        await Assert.That(invalidProof.Kind).IsEqualTo(ActionContractProofKind.Invalid);
        await Assert.That(invalidProof.Reason).Contains("untouched state");
        await Assert.That(invalidProof.Reason).Contains("independently");
        await Assert.That(opaqueProof.Kind).IsEqualTo(ActionContractProofKind.Opaque);
    }

    [Test]
    public async Task DirectCalculusRejectsProofRelevantMalformedContractShapes()
    {
        var missingFrame = new ActionDescriptor(Subject, "missing-frame", "missing-frame")
        {
            Preconditions = [new ActionPrecondition(Integer("x", 1), "x is one")],
            Postconditions =
            [
                new ActionPostcondition
                {
                    Kind = PostconditionKind.ModifiesProperty,
                    PropertyName = "x",
                },
            ],
        };
        var unknownStrength = new ActionDescriptor(Subject, "unknown-strength", "unknown-strength")
        {
            Preconditions =
            [
                new ActionPrecondition(
                    Integer("x", 1),
                    "invalid strength",
                    (ConstraintStrength)999),
            ],
        };
        var unknownEffect = new ActionDescriptor(Subject, "unknown-effect", "unknown-effect")
        {
            Postconditions = [new ActionPostcondition { Kind = (PostconditionKind)999 }],
        };

        var missingFrameResult = ActionCalculus.AnalyzeSequential(EmptyLattice, missingFrame);
        var unknownStrengthResult = ActionCalculus.AnalyzeSequential(EmptyLattice, unknownStrength);
        var unknownEffectResult = ActionCalculus.AnalyzeSequential(EmptyLattice, unknownEffect);

        await Assert.That(missingFrameResult.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(missingFrameResult.Errors.Single()).Contains("outside the declared frame");
        await Assert.That(unknownStrengthResult.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(unknownStrengthResult.Errors.Single()).Contains("unknown constraint strength");
        await Assert.That(unknownEffectResult.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(unknownEffectResult.Errors.Single()).Contains("unknown postcondition kind");
    }

    [Test]
    public async Task NonthrowingAnalysisReturnsInvalidForUnknownAuthority()
    {
        var action = new ActionDescriptor(Subject, "admin-only", "admin-only")
        {
            RequiredAuthority = "missing-authority",
        };

        var analysis = ActionCalculus.AnalyzeSequential(EmptyLattice, action);

        await Assert.That(analysis.Status).IsEqualTo(ActionCompositionAnalysisStatus.Invalid);
        await Assert.That(analysis.Errors.Single()).Contains("Unknown authority 'missing-authority'");
        await Assert.That(() => ActionCalculus.Sequential(EmptyLattice, action))
            .Throws<ActionCompositionException>();
    }

    [Test]
    public async Task SemanticTautologyIsVacuousForCoverage()
    {
        var xIsOne = Integer("x", 1);
        var action = Action(
            "tautology",
            ensures: ActionPredicate.Any(xIsOne, ActionPredicate.Not(xIsOne)),
            frame: [ActionResource.Property("x")]);

        var proof = ActionContractProofEngine.Analyze(action);

        await Assert.That(proof.Kind).IsEqualTo(ActionContractProofKind.Closed);
        await Assert.That(proof.HasNontrivialEffectiveGuarantee).IsFalse();
    }

    [Test]
    public async Task WitnessMinimizationUsesExactValidityRatherThanKleeneApproximation()
    {
        var p = LogicFormula.BooleanAtom(new LogicResource("p", LogicScalarKind.Boolean, false));
        var required = LogicFormula.BooleanAtom(new LogicResource("required", LogicScalarKind.Boolean, false));
        var formula = LogicFormula.All(LogicFormula.Any(p, LogicFormula.Not(p)), required);

        var decision = FiniteDomainSolver.IsSatisfiable(formula);

        await Assert.That(decision.Kind).IsEqualTo(LogicDecisionKind.Satisfiable);
        await Assert.That(decision.Witness.Select(pair => pair.Key)).IsEquivalentTo(["required"]);
    }

    [Test]
    public async Task DiscreteOtherWitnessCannotBeConfusedWithAQuotedLiteral()
    {
        var resource = new LogicResource("value", LogicScalarKind.String, false);
        var reservedLookingLiteral = new LogicLiteral(LogicLiteralKind.String, "<other-string>");
        var equal = FiniteDomainSolver.IsSatisfiable(LogicFormula.Comparison(
            resource,
            LogicComparisonOperator.Equal,
            reservedLookingLiteral));
        var unequal = FiniteDomainSolver.IsSatisfiable(LogicFormula.Comparison(
            resource,
            LogicComparisonOperator.NotEqual,
            reservedLookingLiteral));

        await Assert.That(equal.Witness.Single().Value).IsEqualTo("\"<other-string>\"");
        await Assert.That(unequal.Witness.Single().Value).IsEqualTo("<other-string>");
    }

    [Test]
    public async Task ForgettingThroughNegationIsExact()
    {
        var x = new LogicResource("x", LogicScalarKind.Integer, false);
        var y = new LogicResource("y", LogicScalarKind.Integer, false);
        var xIsOne = LogicFormula.Comparison(
            x,
            LogicComparisonOperator.Equal,
            new LogicLiteral(LogicLiteralKind.Integer, "1"));
        var yIsTwo = LogicFormula.Comparison(
            y,
            LogicComparisonOperator.Equal,
            new LogicLiteral(LogicLiteralKind.Integer, "2"));
        var formula = LogicFormula.Not(LogicFormula.Any(xIsOne, yIsTwo));

        var succeeded = FiniteDomainSolver.TryForget(
            formula,
            ["x"],
            out var projected,
            out var failureReason);
        var expected = LogicFormula.Not(yIsTwo);

        await Assert.That(succeeded).IsTrue();
        await Assert.That(failureReason).IsNull();
        await Assert.That(FiniteDomainSolver.Implies(projected, expected).Kind)
            .IsEqualTo(LogicDecisionKind.Unsatisfiable);
        await Assert.That(FiniteDomainSolver.Implies(expected, projected).Kind)
            .IsEqualTo(LogicDecisionKind.Unsatisfiable);
    }

    [Test]
    public async Task ProjectionAndCompositionHonorPreCanceledTokens()
    {
        var resource = new LogicResource("x", LogicScalarKind.Integer, false);
        var formula = LogicFormula.Comparison(
            resource,
            LogicComparisonOperator.Equal,
            new LogicLiteral(LogicLiteralKind.Integer, "1"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.That(() => FiniteDomainSolver.IsSatisfiable(
                LogicFormula.Opaque("runtime-only"),
                source.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(() => FiniteDomainSolver.TryForget(
                LogicFormula.Opaque("runtime-only"),
                ["x"],
                out _,
                out _,
                source.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(() => FiniteDomainSolver.TryForget(
                formula,
                ["x"],
                out _,
                out _,
                source.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(() => ActionCalculus.AnalyzeSequential(
                EmptyLattice,
                source.Token,
                Action("canceled")))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task EnumerableCompositionStopsMaterializingWhenCancellationIsObserved()
    {
        using var source = new CancellationTokenSource();

        IEnumerable<ActionCompositionOperand> Operands()
        {
            yield return Action("first");
            source.Cancel();
            yield return Action("second");
            throw new InvalidOperationException("Enumeration continued after cancellation.");
        }

        await Assert.That(() => ActionCalculus.AnalyzeSequential(
                EmptyLattice,
                source.Token,
                Operands()))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task KernelHasNoMachineWordAtomLimit()
    {
        var formula = LogicFormula.All(Enumerable.Range(0, 96)
            .Select(index => LogicFormula.BooleanAtom(new LogicResource(
                $"atom-{index:D3}",
                LogicScalarKind.Boolean,
                false))));

        var decision = FiniteDomainSolver.IsSatisfiable(formula);

        await Assert.That(decision.Kind).IsEqualTo(LogicDecisionKind.Satisfiable);
        await Assert.That(decision.Witness).HasCount(96);
    }

    [Test]
    public async Task MixedDomainProofKernelMatchesExhaustiveReferenceOracle()
    {
        var count = new LogicResource("count", LogicScalarKind.Integer, true);
        var price = new LogicResource("price", LogicScalarKind.Decimal, false);
        var state = new LogicResource("state", LogicScalarKind.String, false);
        var owner = new LogicResource("owner", LogicScalarKind.Symbol, false);
        var phase = new LogicResource("phase", LogicScalarKind.Enum, false, "Phase");
        var link = LogicFormula.BooleanAtom(new LogicResource("link|invoice", LogicScalarKind.Boolean, false));
        var relation = LogicFormula.BooleanAtom(new LogicResource("relation|approver", LogicScalarKind.Boolean, false));

        var countIsNull = Comparison(count, LogicComparisonOperator.Equal, LogicLiteral.Null);
        var countBelowZero = Comparison(count, LogicComparisonOperator.LessThan, IntegerLiteral(0));
        var countAtLeastTwo = Comparison(count, LogicComparisonOperator.GreaterThanOrEqual, IntegerLiteral(2));
        var priceBelowTenth = Comparison(price, LogicComparisonOperator.LessThan, DecimalLiteral("0.1"));
        var priceAtLeastTwoTenths = Comparison(price, LogicComparisonOperator.GreaterThanOrEqual, DecimalLiteral("0.2"));
        var stateReady = Comparison(state, LogicComparisonOperator.Equal, StringLiteral("ready"));
        var ownerNotAlpha = Comparison(owner, LogicComparisonOperator.NotEqual, SymbolLiteral("alpha"));
        var phaseReady = Comparison(phase, LogicComparisonOperator.Equal, EnumLiteral("Phase", "Ready"));

        var formulas = new (string Name, LogicFormula Formula, Func<OracleState, bool> Evaluate)[]
        {
            ("true", LogicFormula.True, _ => true),
            ("count-null", countIsNull, value => value.Count is null),
            ("count-negative", countBelowZero, value => value.Count is < 0),
            ("count-ge-two", countAtLeastTwo, value => value.Count is >= 2),
            ("price-lt-tenth", priceBelowTenth, value => value.Price < PredicateDecimal.Parse("0.1")),
            ("price-ge-two-tenths", priceAtLeastTwoTenths, value => value.Price >= PredicateDecimal.Parse("0.2")),
            ("state-ready", stateReady, value => value.State == "ready"),
            ("owner-not-alpha", ownerNotAlpha, value => value.Owner != "alpha"),
            ("phase-ready", phaseReady, value => value.Phase == "Ready"),
            ("link", link, value => value.Link),
            ("relation", relation, value => value.Relation),
            ("not-state-or-link", LogicFormula.Not(LogicFormula.Any(stateReady, link)), value => value.State != "ready" && !value.Link),
            ("numeric-choice", LogicFormula.Any(LogicFormula.All(countBelowZero, priceBelowTenth), LogicFormula.All(countAtLeastTwo, priceAtLeastTwoTenths)), value => (value.Count is < 0 && value.Price < PredicateDecimal.Parse("0.1")) || (value.Count is >= 2 && value.Price >= PredicateDecimal.Parse("0.2"))),
            ("mixed", LogicFormula.All(LogicFormula.Any(countIsNull, stateReady, phaseReady), LogicFormula.Not(LogicFormula.All(ownerNotAlpha, relation))), value => (value.Count is null || value.State == "ready" || value.Phase == "Ready") && !(value.Owner != "alpha" && value.Relation)),
        };
        var states = OracleStates().ToArray();
        var failures = new List<string>();

        foreach (var antecedent in formulas)
        {
            foreach (var consequent in formulas)
            {
                var expected = states.All(value =>
                    !antecedent.Evaluate(value) || consequent.Evaluate(value));
                var actual = FiniteDomainSolver.Implies(
                    antecedent.Formula,
                    consequent.Formula).Kind == LogicDecisionKind.Unsatisfiable;
                if (actual != expected)
                {
                    failures.Add($"{antecedent.Name} => {consequent.Name}: expected {expected}, got {actual}");
                }
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task BooleanProofKernelMatchesExhaustiveReferenceOracle()
    {
        var x = LogicFormula.BooleanAtom(new LogicResource("x", LogicScalarKind.Boolean, false));
        var y = LogicFormula.BooleanAtom(new LogicResource("y", LogicScalarKind.Boolean, false));
        var formulas = new (LogicFormula Formula, Func<bool, bool, bool> Evaluate)[]
        {
            (LogicFormula.True, (_, _) => true),
            (LogicFormula.False, (_, _) => false),
            (x, (xValue, _) => xValue),
            (y, (_, yValue) => yValue),
            (LogicFormula.Not(x), (xValue, _) => !xValue),
            (LogicFormula.All(x, y), (xValue, yValue) => xValue && yValue),
            (LogicFormula.Any(x, y), (xValue, yValue) => xValue || yValue),
            (LogicFormula.Not(LogicFormula.Any(x, y)), (xValue, yValue) => !xValue && !yValue),
            (LogicFormula.Any(LogicFormula.All(x, y), LogicFormula.All(LogicFormula.Not(x), y)), (_, yValue) => yValue),
        };

        foreach (var antecedent in formulas)
        {
            foreach (var consequent in formulas)
            {
                var expected = new[] { false, true }
                    .SelectMany(xValue => new[] { false, true }
                        .Select(yValue => (xValue, yValue)))
                    .All(values => !antecedent.Evaluate(values.xValue, values.yValue)
                        || consequent.Evaluate(values.xValue, values.yValue));
                var actual = FiniteDomainSolver.Implies(
                    antecedent.Formula,
                    consequent.Formula);

                await Assert.That(actual.Kind == LogicDecisionKind.Unsatisfiable)
                    .IsEqualTo(expected);
            }
        }
    }

    private static ActionDescriptor Action(
        string name,
        ActionPredicate? requires = null,
        ActionPredicate? ensures = null,
        IReadOnlyList<ActionResource>? frame = null) => new(Subject, name, name)
        {
            Preconditions = requires is null
                ? []
                : [new ActionPrecondition(requires, requires.Expression)],
            Ensures = ensures is null
                ? []
                : [new ActionGuarantee(ensures)],
            TouchedResources = frame ?? [],
        };

    private static ActionPredicate Integer(string property, int value) =>
        Compare(
            property,
            PredicateScalarKind.Integer,
            PredicateComparisonOperator.Equal,
            PredicateLiteral.Integer(value));

    private static ActionPredicate String(string property, string value) =>
        Compare(
            property,
            PredicateScalarKind.String,
            PredicateComparisonOperator.Equal,
            PredicateLiteral.String(value));

    private static ActionPredicate Boolean(string property, bool value) =>
        Compare(
            property,
            PredicateScalarKind.Boolean,
            PredicateComparisonOperator.Equal,
            PredicateLiteral.Boolean(value));

    private static ActionPredicate Compare(
        string property,
        PredicateScalarKind scalarKind,
        PredicateComparisonOperator comparison,
        PredicateLiteral literal,
        bool isNullable = false) => ActionPredicate.Property(
            new PredicatePropertyReference(property, scalarKind, isNullable),
            comparison,
            literal);

    private static LogicFormula Comparison(
        LogicResource resource,
        LogicComparisonOperator comparison,
        LogicLiteral literal) => LogicFormula.Comparison(resource, comparison, literal);

    private static LogicLiteral IntegerLiteral(int value) =>
        new(LogicLiteralKind.Integer, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static LogicLiteral DecimalLiteral(string value) => new(LogicLiteralKind.Decimal, value);

    private static LogicLiteral StringLiteral(string value) => new(LogicLiteralKind.String, value);

    private static LogicLiteral SymbolLiteral(string value) => new(LogicLiteralKind.Symbol, value);

    private static LogicLiteral EnumLiteral(string typeName, string value) =>
        new(LogicLiteralKind.Enum, value, typeName);

    private static IEnumerable<OracleState> OracleStates()
    {
        int?[] counts = [null, -1, 0, 1, 2, 3];
        PredicateDecimal[] prices =
        [
            PredicateDecimal.Parse("-1"),
            PredicateDecimal.Parse("0.1"),
            PredicateDecimal.Parse("0.15"),
            PredicateDecimal.Parse("0.2"),
            PredicateDecimal.Parse("1"),
        ];
        string[] states = ["ready", "other"];
        string[] owners = ["alpha", "beta"];
        string[] phases = ["Ready", "Closed"];
        bool[] booleans = [false, true];
        foreach (var count in counts)
        {
            foreach (var price in prices)
            {
                foreach (var state in states)
                {
                    foreach (var owner in owners)
                    {
                        foreach (var phase in phases)
                        {
                            foreach (var link in booleans)
                            {
                                foreach (var relation in booleans)
                                {
                                    yield return new OracleState(
                                        count,
                                        price,
                                        state,
                                        owner,
                                        phase,
                                        link,
                                        relation);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private sealed record OracleState(
        int? Count,
        PredicateDecimal Price,
        string State,
        string Owner,
        string Phase,
        bool Link,
        bool Relation);
}
