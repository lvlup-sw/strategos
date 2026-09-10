using Microsoft.CodeAnalysis;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public sealed class TypedActionCompositionAnalyzerTests
{
    [Test]
    public async Task EmptySequenceReportsAont221InsteadOfDynamicCoverage()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("ActionCalculus.Sequential(lattice);"));

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo([OntologyDiagnosticIds.InvalidActionContract]);
    }

    [Test]
    public async Task DirectIllegalSeamReportsAont217WithCounterexample()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var xIsOne = ActionPredicate.Property(
                    value: PredicateLiteral.Integer(value: 1),
                    comparison: PredicateComparisonOperator.Equal,
                    property: x);
                var xIsTwo = ActionPredicate.Property(
                    value: PredicateLiteral.Integer(value: 2),
                    comparison: PredicateComparisonOperator.Equal,
                    property: x);
                var first = new ActionDescriptor(subject, "first", "first")
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [new ActionGuarantee(xIsOne)],
                };
                var second = new ActionDescriptor(subject, "second", "second")
                {
                    Preconditions = [new ActionPrecondition(xIsTwo, "x is two")],
                };
                ActionCalculus.Sequential(lattice, first, second);
                """),
            OntologyDiagnosticIds.IllegalActionSeam);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].GetMessage()).Contains("property|x");
    }

    [Test]
    public async Task ReorderedNamedConstructorArgumentsRemainStaticallyProvable()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("""
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var xIsOne = ActionPredicate.Property(
                    value: PredicateLiteral.Integer(value: 1),
                    comparison: PredicateComparisonOperator.Equal,
                    property: x);
                var xIsTwo = ActionPredicate.Property(
                    value: PredicateLiteral.Integer(value: 2),
                    comparison: PredicateComparisonOperator.Equal,
                    property: x);
                var stateIsActive = ActionPredicate.Property(
                    value: PredicateLiteral.Enum(
                        memberName: nameof(ModelState.Active),
                        typeName: nameof(ModelState)),
                    comparison: PredicateComparisonOperator.Equal,
                    property: new PredicatePropertyReference(
                        enumTypeName: nameof(ModelState),
                        scalarKind: PredicateScalarKind.Enum,
                        name: "state"));
                var ownerRelation = ActionPredicate.RelationHolds(
                    linkPath: ["space"],
                    relationName: "owner");
                var first = new ActionDescriptor(
                    description: "first",
                    name: "first",
                    subject: new ActionSubject(objectTypeName: "Order", domainName: "orders"))
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [new ActionGuarantee(description: "x is one", predicate: xIsOne)],
                };
                var second = new ActionDescriptor(
                    description: "second",
                    name: "second",
                    subject: new ActionSubject(objectTypeName: "Order", domainName: "orders"))
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            description: "x is two",
                            predicate: ActionPredicate.All(
                                operands: [xIsTwo, stateIsActive, ownerRelation]),
                            strength: ConstraintStrength.Hard),
                    ],
                };
                ActionCalculus.Sequential(lattice, first, second);
                """));

        await Assert.That(diagnostics.Count(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.IllegalActionSeam)).IsEqualTo(1);
        await Assert.That(diagnostics.Any(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.DynamicActionSequence)).IsFalse();
    }

    [Test]
    public async Task StaticallyBlankActionIdentitiesReportAont221InsteadOfAont220()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("""
                var blankSubject = new ActionDescriptor(
                    new ActionSubject("", "Order"),
                    "valid-name",
                    "blank subject");
                ActionCalculus.Sequential(lattice, blankSubject);

                var blankName = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    " ",
                    "blank action name");
                ActionCalculus.Sequential(lattice, blankName);

                ActionCalculus.Sequential(
                    lattice,
                    ActionCalculus.Identity(new ActionSubject("orders", "")));

                var nullSubject = new ActionDescriptor(
                    new ActionSubject(null!, "Order"),
                    "valid-name",
                    "null subject");
                ActionCalculus.Sequential(lattice, nullSubject);

                var nullName = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    null!,
                    "null action name");
                ActionCalculus.Sequential(lattice, nullName);

                var blankAuthority = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    "blank-authority",
                    "blank authority")
                {
                    RequiredAuthority = " ",
                };
                ActionCalculus.Sequential(lattice, blankAuthority);
                """));

        await Assert.That(diagnostics.Any(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.DynamicActionSequence)).IsFalse()
            .Because(string.Join(Environment.NewLine, diagnostics.Select(diagnostic =>
                diagnostic.Id + ": " + diagnostic.GetMessage())));
        await Assert.That(diagnostics.Count(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.InvalidActionContract)).IsEqualTo(6);
    }

    [Test]
    public async Task StaticallyConstructedAuthorityLatticeValidatesRequiredAuthorityNames()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("""
                var staticLattice = new AuthorityLattice(
                    [],
                    [new AuthorityDescriptor("reader")]);
                var valid = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    "valid-authority",
                    "valid authority")
                {
                    RequiredAuthority = "reader",
                };
                ActionCalculus.Sequential(staticLattice, valid);

                var missing = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    "missing-authority",
                    "missing authority")
                {
                    RequiredAuthority = "writer",
                };
                ActionCalculus.Sequential(staticLattice, missing);
                """));

        var invalid = diagnostics.Where(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.InvalidActionContract).ToArray();
        await Assert.That(invalid).HasCount().EqualTo(1);
        await Assert.That(invalid[0].GetMessage()).Contains("required authority 'writer'");
        await Assert.That(diagnostics.Any(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.DynamicActionSequence)).IsFalse();
    }

    [Test]
    public async Task DynamicAuthorityLatticeReportsRuntimeOnlyVerification()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var action = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    "requires-reader",
                    "requires reader")
                {
                    RequiredAuthority = "reader",
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("authority lattice");
    }

    [Test]
    public async Task DynamicAuthorityLatticeDoesNotSuppressStaticContractDiagnostics()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("""
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var xIsOne = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var xIsTwo = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(2));
                var impossible = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    "impossible",
                    "impossible")
                {
                    RequiredAuthority = "reader",
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.All(xIsOne, xIsTwo),
                            "contradiction"),
                    ],
                };
                ActionCalculus.Sequential(lattice, impossible);
                """));

        await Assert.That(diagnostics.Count(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.DynamicActionSequence)).IsEqualTo(1);
        await Assert.That(diagnostics.Count(diagnostic =>
            diagnostic.Id == OntologyDiagnosticIds.InvalidActionContract)).IsEqualTo(1);
    }

    [Test]
    public async Task NestedRelationCannotBeEstablishedBySameNamedLocalLinkFrame()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var relation = ActionPredicate.RelationHolds("owner", "space");
                var action = new ActionDescriptor(subject, "invent-linked-owner", "invent-linked-owner")
                {
                    Ensures = [new ActionGuarantee(relation)],
                    TouchedResources = [ActionResource.Link("owner")],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("untouched state");
    }

    [Test]
    public async Task ReplacingFirstRelationPathHopRefutesDownstreamRelationRequirement()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var relation = ActionPredicate.RelationHolds("owner", "space");
                var upstream = new ActionDescriptor(subject, "replace-space", "replace-space")
                {
                    Preconditions = [new ActionPrecondition(relation, "relation held")],
                    TouchedResources = [ActionResource.Link("space")],
                };
                var downstream = new ActionDescriptor(subject, "requires-linked-owner", "requires-linked-owner")
                {
                    Preconditions = [new ActionPrecondition(relation, "relation held")],
                };
                ActionCalculus.Sequential(lattice, upstream, downstream);
                """),
            OntologyDiagnosticIds.IllegalActionSeam);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("relation|");
    }

    [Test]
    public async Task ReplacingDirectRelationLinkRefutesDownstreamRelationRequirement()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var relation = ActionPredicate.RelationHolds("owner");
                var upstream = new ActionDescriptor(subject, "replace-owner-link", "replace-owner-link")
                {
                    Preconditions = [new ActionPrecondition(relation, "relation held")],
                    TouchedResources = [ActionResource.Link("owner")],
                };
                var downstream = new ActionDescriptor(subject, "requires-owner", "requires-owner")
                {
                    Preconditions = [new ActionPrecondition(relation, "relation held")],
                };
                ActionCalculus.Sequential(lattice, upstream, downstream);
                """),
            OntologyDiagnosticIds.IllegalActionSeam);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("relation|");
    }

    [Test]
    public async Task InconsistentScalarMetadataAcrossSeamReportsAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var upstreamFact = ActionPredicate.Property(
                    new PredicatePropertyReference("value", PredicateScalarKind.Integer),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var downstreamRequirement = ActionPredicate.Property(
                    new PredicatePropertyReference("value", PredicateScalarKind.String),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.String("one"));
                var upstream = new ActionDescriptor(subject, "upstream", "upstream")
                {
                    Ensures = [new ActionGuarantee(upstreamFact)],
                    TouchedResources = [ActionResource.Property("value")],
                };
                var downstream = new ActionDescriptor(subject, "downstream", "downstream")
                {
                    Preconditions = [new ActionPrecondition(downstreamRequirement, "string value")],
                };
                ActionCalculus.Sequential(lattice, upstream, downstream);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("inconsistent scalar metadata");
    }

    [Test]
    public async Task OpaqueActionReportsAont218()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var action = new ActionDescriptor(subject, "opaque", "opaque")
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.Custom("orders.credit.v1"),
                            "credit"),
                    ],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.OpaqueActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("orders.credit.v1");
    }

    [Test]
    public async Task SoftCustomRequirementIsExcludedFromStaticOpacity()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var action = new ActionDescriptor(subject, "soft", "soft")
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.Custom("orders.credit.v1"),
                            "credit",
                            strength: ConstraintStrength.Soft),
                    ],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.OpaqueActionContract);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task EnumLiteralTypeMismatchReportsAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var state = new PredicatePropertyReference(
                    "state",
                    PredicateScalarKind.Enum,
                    enumTypeName: "OrderState");
                var wrongEnum = ActionPredicate.Property(
                    state,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Enum("OtherState", "Active"));
                var action = new ActionDescriptor(subject, "invalid", "invalid")
                {
                    Preconditions = [new ActionPrecondition(wrongEnum, "wrong enum")],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("OtherState");
    }

    [Test]
    public async Task WrittenPropertyCannotChangeScalarDomainAcrossPreAndPostState()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var integerValue = ActionPredicate.Property(
                    new PredicatePropertyReference("value", PredicateScalarKind.Integer),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var stringValue = ActionPredicate.Property(
                    new PredicatePropertyReference("value", PredicateScalarKind.String),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.String("one"));
                var action = new ActionDescriptor(subject, "change-type", "change-type")
                {
                    Preconditions = [new ActionPrecondition(integerValue, "integer value")],
                    Ensures = [new ActionGuarantee(stringValue)],
                    TouchedResources = [ActionResource.Property("value")],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("inconsistent scalar metadata");
    }

    [Test]
    public async Task UnknownTypedEnumValuesReportAont221InsteadOfBeingTextuallyAccepted()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var badScalar = ActionPredicate.Property(
                    new PredicatePropertyReference("x", (PredicateScalarKind)99),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var badOperator = ActionPredicate.Property(
                    new PredicatePropertyReference("y", PredicateScalarKind.Integer),
                    (PredicateComparisonOperator)99,
                    PredicateLiteral.Integer(1));
                var first = new ActionDescriptor(subject, "bad-scalar", "bad-scalar")
                {
                    Preconditions = [new ActionPrecondition(badScalar, "bad")],
                };
                var second = new ActionDescriptor(subject, "bad-operator", "bad-operator")
                {
                    Preconditions = [new ActionPrecondition(badOperator, "bad")],
                };
                var third = new ActionDescriptor(subject, "bad-strength", "bad-strength")
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.True,
                            "bad",
                            (ConstraintStrength)99),
                    ],
                };
                ActionCalculus.Sequential(lattice, first, second, third);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(3);
    }

    [Test]
    public async Task ImmutableSingleAssignmentLocalsAreResolved()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var property = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var predicate = ActionPredicate.Property(
                    property,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var guarantee = new ActionGuarantee(predicate);
                var effect = new ActionPostcondition
                {
                    Kind = PostconditionKind.ModifiesProperty,
                    PropertyName = "x",
                };
                var first = new ActionDescriptor(subject, "first", "first")
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [guarantee],
                    Postconditions = [effect],
                };
                var second = new ActionDescriptor(subject, "second", "second")
                {
                    Preconditions = [new ActionPrecondition(predicate, "x is one")],
                };
                ActionCalculus.Sequential(lattice, first, second);
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task TupleReassignedLocalReceivesRuntimeOnlyVerification()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var xIsOne = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var xIsTwo = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(2));
                var action = new ActionDescriptor(subject, "one", "one")
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [new ActionGuarantee(xIsOne)],
                };
                var replacement = new ActionDescriptor(subject, "two", "two")
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [new ActionGuarantee(xIsTwo)],
                };
                var other = 0;
                (action, other) = (replacement, other);
                var downstream = new ActionDescriptor(subject, "requires-one", "requires-one")
                {
                    Preconditions = [new ActionPrecondition(xIsOne, "x is one")],
                };
                ActionCalculus.Sequential(lattice, action, downstream);
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
    }

    [Test]
    public async Task RefAliasedLocalReceivesRuntimeOnlyVerification()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var xIsOne = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var xIsTwo = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(2));
                var action = new ActionDescriptor(subject, "one", "one")
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [new ActionGuarantee(xIsOne)],
                };
                var replacement = new ActionDescriptor(subject, "two", "two")
                {
                    TouchedResources = [ActionResource.Property("x")],
                    Ensures = [new ActionGuarantee(xIsTwo)],
                };
                ref ActionDescriptor alias = ref action;
                alias = replacement;
                var downstream = new ActionDescriptor(subject, "requires-one", "requires-one")
                {
                    Preconditions = [new ActionPrecondition(xIsOne, "x is one")],
                };
                ActionCalculus.Sequential(lattice, action, downstream);
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
    }

    [Test]
    public async Task SupportedTypedLiteralFactoriesRemainStaticallyProvable()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var predicate = ActionPredicate.All(
                    ActionPredicate.Property(
                        new PredicatePropertyReference("approved", PredicateScalarKind.Boolean),
                        PredicateComparisonOperator.Equal,
                        PredicateLiteral.Boolean(true)),
                    ActionPredicate.Property(
                        new PredicatePropertyReference("count", PredicateScalarKind.Integer),
                        PredicateComparisonOperator.GreaterThan,
                        PredicateLiteral.Integer(BigInteger.Parse("999999999999999999999999999999"))),
                    ActionPredicate.Property(
                        new PredicatePropertyReference("amount", PredicateScalarKind.Decimal),
                        PredicateComparisonOperator.GreaterThanOrEqual,
                        PredicateLiteral.Decimal("10.2500")),
                    ActionPredicate.Property(
                        new PredicatePropertyReference("name", PredicateScalarKind.String),
                        PredicateComparisonOperator.NotEqual,
                        PredicateLiteral.String("")),
                    ActionPredicate.Property(
                        new PredicatePropertyReference(
                            "state",
                            PredicateScalarKind.Enum,
                            enumTypeName: nameof(ModelState)),
                        PredicateComparisonOperator.Equal,
                        PredicateLiteral.Enum(ModelState.Active)),
                    ActionPredicate.Property(
                        new PredicatePropertyReference("id", PredicateScalarKind.Symbol),
                        PredicateComparisonOperator.Equal,
                        PredicateLiteral.Symbol("123e4567-e89b-12d3-a456-426614174000")));
                var action = new ActionDescriptor(subject, "typed", "typed")
                {
                    Preconditions = [new ActionPrecondition(predicate, "typed")],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task LeadingPlusExactDecimalMatchesRuntimeCanonicalization()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var predicate = ActionPredicate.Property(
                    new PredicatePropertyReference("amount", PredicateScalarKind.Decimal),
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Decimal("+1.20"));
                var action = new ActionDescriptor(subject, "decimal", "decimal")
                {
                    Preconditions = [new ActionPrecondition(predicate, "exact decimal")],
                };
                ActionCalculus.Sequential(lattice, action);
                """));

        await Assert.That(diagnostics.Any(diagnostic =>
            diagnostic.Id is OntologyDiagnosticIds.InvalidActionContract
                or OntologyDiagnosticIds.DynamicActionSequence)).IsFalse();
    }

    [Test]
    public async Task ImmutableOperandCollectionIsResolvedButMutableArrayLocalIsDynamic()
    {
        var source = Source("""
            var subject = new ActionSubject("orders", "Order");
            var action = new ActionDescriptor(subject, "inspect", "inspect");
            ImmutableArray<ActionCompositionOperand> immutable = [action];
            ActionCalculus.Sequential(lattice, immutable);
            ActionCompositionOperand[] mutable = [action];
            ActionCalculus.Sequential(lattice, mutable);
            """);
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("mutable collection");
    }

    [Test]
    public async Task DirectImmutableFactoryCollectionIsResolved()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var action = new ActionDescriptor(subject, "inspect", "inspect");
                ActionCalculus.Sequential(
                    lattice,
                    ImmutableArray.Create<ActionCompositionOperand>(action));
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task IdentityOnlySubjectMismatchReportsAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                ActionCalculus.Sequential(
                    lattice,
                    ActionCalculus.Identity(new ActionSubject("orders", "Order")),
                    ActionCalculus.Identity(new ActionSubject("orders", "Invoice")));
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("identity subject");
    }

    [Test]
    public async Task SemanticTautologyIsReportedAsVacuousCoverage()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var equal = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var notEqual = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.NotEqual,
                    PredicateLiteral.Integer(1));
                var action = new ActionDescriptor(subject, "tautology", "tautology")
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.Any(equal, notEqual),
                            "tautology"),
                    ],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.ActionComposabilityCoverage);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).IsEqualTo(
            "Action composability coverage: 0/1 (0%): composable=0, opaque=0, vacuous=1, invalid=0, statically-unresolved=0");
    }

    [Test]
    public async Task ContradictionOutsideCustomTermReportsInvalidInsteadOfOpaque()
    {
        var source = Source("""
            var subject = new ActionSubject("orders", "Order");
            var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
            var xIsOne = ActionPredicate.Property(
                x,
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Integer(1));
            var xIsTwo = ActionPredicate.Property(
                x,
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Integer(2));
            var action = new ActionDescriptor(subject, "opaque-invalid", "opaque-invalid")
            {
                Preconditions =
                [
                    new ActionPrecondition(
                        ActionPredicate.All(
                            ActionPredicate.Custom("orders.credit.v1"),
                            xIsOne,
                            xIsTwo),
                        "invalid"),
                ],
            };
            ActionCalculus.Sequential(lattice, action);
            """);
        var invalid = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.InvalidActionContract);
        var opaque = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.OpaqueActionContract);

        await Assert.That(invalid).HasCount().EqualTo(1);
        await Assert.That(opaque).IsEmpty();
    }

    [Test]
    public async Task OpaqueGuaranteeCannotHideClosedUntouchedFrameViolation()
    {
        var source = Source("""
            var subject = new ActionSubject("orders", "Order");
            var yIsTwo = ActionPredicate.Property(
                new PredicatePropertyReference("y", PredicateScalarKind.Integer),
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Integer(2));
            var action = new ActionDescriptor(subject, "opaque-frame", "opaque-frame")
            {
                Ensures =
                [
                    new ActionGuarantee(ActionPredicate.All(
                        ActionPredicate.Custom("orders.runtime-check.v1"),
                        yIsTwo)),
                ],
            };
            ActionCalculus.Sequential(lattice, action);
            """);
        var invalid = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.InvalidActionContract);
        var opaque = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.OpaqueActionContract);

        await Assert.That(invalid).HasCount().EqualTo(1);
        await Assert.That(invalid[0].GetMessage()).Contains("untouched state");
        await Assert.That(opaque).IsEmpty();
    }

    [Test]
    public async Task OpaqueRequirementMayEstablishUntouchedGuarantee()
    {
        var source = Source("""
            var subject = new ActionSubject("orders", "Order");
            var yIsTwo = ActionPredicate.Property(
                new PredicatePropertyReference("y", PredicateScalarKind.Integer),
                PredicateComparisonOperator.Equal,
                PredicateLiteral.Integer(2));
            var action = new ActionDescriptor(subject, "opaque-frame", "opaque-frame")
            {
                Preconditions =
                [
                    new ActionPrecondition(
                        ActionPredicate.Custom("orders.runtime-check.v1"),
                        "custom requirement"),
                ],
                Ensures = [new ActionGuarantee(yIsTwo)],
            };
            ActionCalculus.Sequential(lattice, action);
            """);
        var invalid = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.InvalidActionContract);
        var opaque = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.OpaqueActionContract);

        await Assert.That(invalid).IsEmpty();
        await Assert.That(opaque).HasCount().EqualTo(1);
    }

    [Test]
    public async Task DistinctCustomArgumentsAreNotConflatedDuringOpaqueValidation()
    {
        var source = Source("""
            var subject = new ActionSubject("orders", "Order");
            var first = ActionPredicate.Custom(
                "orders.credit.v1",
                [PredicateLiteral.Integer(1)]);
            var second = ActionPredicate.Custom(
                "orders.credit.v1",
                [PredicateLiteral.Integer(2)]);
            var action = new ActionDescriptor(subject, "opaque", "opaque")
            {
                Preconditions =
                [
                    new ActionPrecondition(
                        ActionPredicate.All(first, ActionPredicate.Not(second)),
                        "opaque"),
                ],
            };
            ActionCalculus.Sequential(lattice, action);
            """);
        var invalid = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.InvalidActionContract);
        var opaque = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            source,
            OntologyDiagnosticIds.OpaqueActionContract);

        await Assert.That(invalid).IsEmpty();
        await Assert.That(opaque).HasCount().EqualTo(1);
    }

    [Test]
    public async Task DynamicHelperReportsAont220()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                ActionCalculus.Sequential(lattice, MakeAction());
                """),
            OntologyDiagnosticIds.DynamicActionSequence);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task ContradictoryContractReportsAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var x = new PredicatePropertyReference("x", PredicateScalarKind.Integer);
                var xIsOne = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(1));
                var xIsTwo = ActionPredicate.Property(
                    x,
                    PredicateComparisonOperator.Equal,
                    PredicateLiteral.Integer(2));
                var action = new ActionDescriptor(subject, "impossible", "impossible")
                {
                    Preconditions =
                    [
                        new ActionPrecondition(
                            ActionPredicate.All(xIsOne, xIsTwo),
                            "contradiction"),
                    ],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("contradictory");
    }

    [Test]
    public async Task UnsupportedExpressionReportsAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model => model.Count + 1 > 2);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("unsupported");
    }

    [Test]
    public async Task UnrelatedRequiresMethodInOntologyPrefixedNamespaceIsIgnored()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(
            Source("""
                var unrelated = new Strategos.Ontology.Extensions.UnrelatedBuilder();
                unrelated.Requires(model => model.Count + 1 > 2);
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task SupportedExpressionScalarGrammarDoesNotReportAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model =>
                    model.Approved
                    && model.Count >= 1
                    && model.Amount < 10.25m
                    && model.Total > 1
                    && model.Name != "closed"
                    && model.State == ModelState.Active
                    && model.OptionalId != null);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task RepresentationPreservingExpressionConversionsDoNotReportAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model =>
                    model.Count < 10L
                    && checked((long)model.Count) < 10L
                    && model.SmallCount >= 1
                    && model.SmallCount < 10L
                    && model.OptionalCount == 1
                    && model.OptionalAmount < 10
                    && model.OptionalState == ModelState.Active
                    && model.SmallState == ModelSmallState.Active
                    && model.OptionalSmallState == ModelSmallState.Active);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task StaticallyVisibleExpressionExclusionsAllReportAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                var minimum = 2;
                builder.Requires(model => model.CountField > 0);
                builder.Requires(model => model.Ratio > 0d);
                builder.Requires(model => model.Count > minimum);
                builder.Requires(model => model.Count > LiteralSource.Minimum);
                builder.Requires(model => model.Wrapped == LiteralSource.Wrapped);
                builder.Requires(model => model.Count > 1 + 1);
                const int localConstant = 2;
                builder.Requires(model => model.Count > localConstant);
                builder.Requires(model => model.Count > LiteralSource.ConstantMinimum);
                builder.Requires(model => model.Name.StartsWith("A"));
                builder.Requires(model => model.Count + 1 > 2);
                builder.Requires(model => model.Count == model.OtherCount);
                builder.Requires(model => true);
                builder.Requires(model => (Wrapped)model.Count == LiteralSource.Wrapped);
                builder.Requires(model => model.State == (ModelState)99);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(14);
    }

    [Test]
    public async Task SemanticChangingExpressionConversionsReportAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model => (byte)model.Count == 0);
                builder.Requires(model => checked((byte)model.Count) == 0);
                builder.Requires(model => (uint)model.Count == 0U);
                builder.Requires(model => (BigInteger)model.Amount > 1);
                builder.Requires(model => model.Count == (int)(object)0);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(5);
    }

    [Test]
    public async Task ExplicitEnumUnderlyingEqualityCanonicalizesWithoutAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model => (int)model.State == 1);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task DefinedNumericToEnumCastDoesNotReportAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model => model.State == (ModelState)1);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    public async Task AliasedEnumValuesReportAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model => model.AliasedState == AliasedModelState.Enabled);
                builder.Requires(model => model.AliasedState == (AliasedModelState)1);

                var ambiguous = PredicateLiteral.Enum(AliasedModelState.Enabled);
                var predicate = ActionPredicate.Property(
                    new PredicatePropertyReference(
                        "state",
                        PredicateScalarKind.Enum,
                        enumTypeName: nameof(AliasedModelState)),
                    PredicateComparisonOperator.Equal,
                    ambiguous);
                var action = new ActionDescriptor(
                    new ActionSubject("orders", "Order"),
                    "ambiguous",
                    "ambiguous")
                {
                    Preconditions = [new ActionPrecondition(predicate, "ambiguous")],
                };
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(3);
    }

    [Test]
    public async Task NullableNumericOrderingAgainstNullReportsAont221()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                IActionBuilder<Model> builder = null!;
                builder.Requires(model => model.OptionalCount < null);
                """),
            OntologyDiagnosticIds.InvalidActionContract);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
    }

    [Test]
    public async Task ResolvedSequenceReportsAont219Coverage()
    {
        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(
            Source("""
                var subject = new ActionSubject("orders", "Order");
                var action = new ActionDescriptor(subject, "vacuous", "vacuous");
                ActionCalculus.Sequential(lattice, action);
                """),
            OntologyDiagnosticIds.ActionComposabilityCoverage);

        await Assert.That(diagnostics).HasCount().EqualTo(1);
        await Assert.That(diagnostics[0].GetMessage()).Contains("vacuous=1");
    }

    private static string Source(string body) => $$"""
        using System;
        using System.Collections.Immutable;
        using System.Numerics;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;

        public enum ModelState
        {
            Pending,
            Active,
        }

        public enum ModelSmallState : byte
        {
            Pending,
            Active,
        }

        public enum AliasedModelState
        {
            Active = 1,
            Enabled = 1,
        }

        public readonly struct Wrapped
        {
            public Wrapped(int value) => Value = value;

            public int Value { get; }

            public static implicit operator Wrapped(int value) => new(value);

            public static bool operator ==(Wrapped left, Wrapped right) => left.Value == right.Value;

            public static bool operator !=(Wrapped left, Wrapped right) => !(left == right);

            public override bool Equals(object? value) => value is Wrapped other && this == other;

            public override int GetHashCode() => Value;
        }

        public sealed class Model
        {
            public int Count { get; set; }

            public int OtherCount { get; set; }

            public int? OptionalCount { get; set; }

            public byte SmallCount { get; set; }

            public decimal Amount { get; set; }

            public decimal? OptionalAmount { get; set; }

            public BigInteger Total { get; set; }

            public string Name { get; set; } = "";

            public ModelState State { get; set; }

            public ModelState? OptionalState { get; set; }

            public ModelSmallState SmallState { get; set; }

            public ModelSmallState? OptionalSmallState { get; set; }

            public AliasedModelState AliasedState { get; set; }

            public Guid Id { get; set; }

            public Guid? OptionalId { get; set; }

            public bool Approved { get; set; }

            public double Ratio { get; set; }

            public Wrapped Wrapped { get; set; }

            public int CountField;
        }

        namespace Strategos.Ontology.Extensions
        {
            public sealed class UnrelatedBuilder
            {
                public void Requires(
                    System.Linq.Expressions.Expression<Func<global::Model, bool>> predicate)
                {
                }
            }
        }

        public static class LiteralSource
        {
            public const int ConstantMinimum = 2;

            public static int Minimum => 2;

            public static Wrapped Wrapped => new(2);
        }

        public static class Consumer
        {
            public static void Compose(AuthorityLattice lattice)
            {
                {{body}}
            }

            private static ActionDescriptor MakeAction() =>
                new(new ActionSubject("orders", "Order"), "dynamic", "dynamic");
        }
        """;
}
