using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Diagnostics;

namespace Strategos.Ontology.Tests.Diagnostics;

public sealed class AONT218To221GraphFreezeTests
{
    [Test]
    public async Task Build_ClosedOpaqueVacuousAndSoftOpaqueActions_ReportExactCoverage()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<CoverageOntology>()
            .Build();

        var opaque = graph.NonFatalDiagnostics.Single(diagnostic => diagnostic.Id == "AONT218");
        var coverage = graph.NonFatalDiagnostics.Single(diagnostic => diagnostic.Id == "AONT219");

        await Assert.That(opaque.PropertyName).IsEqualTo("opaque");
        await Assert.That(opaque.Message).Contains("policy.hard.v1");
        await Assert.That(coverage.Message).Contains("2/4 (50%)");
        await Assert.That(coverage.Message).Contains("composable=2");
        await Assert.That(coverage.Message).Contains("opaque=1");
        await Assert.That(coverage.Message).Contains("vacuous=1");
        await Assert.That(coverage.Message).Contains("invalid=0");
        await Assert.That(coverage.Message).Contains("statically-unresolved=0");
    }

    [Test]
    public async Task Build_ZeroActionGraph_SuppressesCoverageDiagnostic()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<EmptyOntology>()
            .Build();

        await Assert.That(graph.NonFatalDiagnostics.Any(diagnostic => diagnostic.Id == "AONT219"))
            .IsFalse();
    }

    [Test]
    [Arguments(typeof(SubjectMismatchOntology), "does not match containing object")]
    [Arguments(typeof(ContradictoryOntology), "requirements are contradictory")]
    [Arguments(typeof(UnrealizableFrameOntology), "untouched state")]
    [Arguments(typeof(NullFrameEntryOntology), "action frame contains a null resource")]
    [Arguments(typeof(NullPostconditionEntryOntology), "postcondition collection contains a null entry")]
    [Arguments(typeof(DuplicateActionIdentityOntology), "Action identity 'orders/Order.ship' is declared 2 times")]
    public async Task Build_InvalidTypedContract_ReportsAont221(Type ontologyType, string expectedDetail)
    {
        OntologyCompositionException? caught = null;
        try
        {
            new OntologyGraphBuilder()
                .AddDomain((DomainOntology)Activator.CreateInstance(ontologyType)!)
                .Build();
        }
        catch (OntologyCompositionException exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsNotNull();
        var diagnostic = caught!.Diagnostics.Single(item => item.Id == "AONT221");
        await Assert.That(diagnostic.Severity).IsEqualTo(OntologyDiagnosticSeverity.Error);
        await Assert.That(diagnostic.Message).Contains(expectedDetail);
    }

    private static ActionPredicate Integer(string propertyName, int value) =>
        ActionPredicate.Property(
            new PredicatePropertyReference(propertyName, PredicateScalarKind.Integer),
            PredicateComparisonOperator.Equal,
            PredicateLiteral.Integer(value));

    private sealed class CoverageOntology : DomainOntology
    {
        public override string DomainName => "coverage";

        protected override void Define(IOntologyBuilder builder)
        {
            var subject = new ActionSubject(DomainName, "Order");
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(subject, "composable", "composable")
                    {
                        TouchedResources = [ActionResource.Property("x")],
                        Ensures = [new ActionGuarantee(Integer("x", 1))],
                    },
                    new ActionDescriptor(subject, "opaque", "opaque")
                    {
                        Preconditions =
                        [
                            new ActionPrecondition(
                                ActionPredicate.Custom("policy.hard.v1"),
                                "opaque"),
                        ],
                    },
                    new ActionDescriptor(subject, "vacuous", "vacuous"),
                    new ActionDescriptor(subject, "soft-opaque", "soft-opaque")
                    {
                        Preconditions =
                        [
                            new ActionPrecondition(
                                ActionPredicate.Custom("policy.soft.v1"),
                                "advisory",
                                ConstraintStrength.Soft),
                        ],
                        TouchedResources = [ActionResource.Property("y")],
                        Ensures = [new ActionGuarantee(Integer("y", 2))],
                    },
                ],
            });
        }
    }

    private sealed class EmptyOntology : DomainOntology
    {
        public override string DomainName => "empty";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
            });
        }
    }

    public sealed class SubjectMismatchOntology : DomainOntology
    {
        public override string DomainName => "orders";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(new ActionSubject(DomainName, "Invoice"), "ship", "ship"),
                ],
            });
        }
    }

    public sealed class ContradictoryOntology : DomainOntology
    {
        public override string DomainName => "orders";

        protected override void Define(IOntologyBuilder builder)
        {
            var subject = new ActionSubject(DomainName, "Order");
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(subject, "impossible", "impossible")
                    {
                        Preconditions =
                        [
                            new ActionPrecondition(
                                ActionPredicate.All(Integer("x", 1), Integer("x", 2)),
                                "contradiction"),
                        ],
                    },
                ],
            });
        }
    }

    public sealed class UnrealizableFrameOntology : DomainOntology
    {
        public override string DomainName => "orders";

        protected override void Define(IOntologyBuilder builder)
        {
            var subject = new ActionSubject(DomainName, "Order");
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(subject, "invent", "invent")
                    {
                        TouchedResources = [ActionResource.Property("x")],
                        Ensures = [new ActionGuarantee(Integer("y", 2))],
                    },
                ],
            });
        }
    }

    public sealed class NullFrameEntryOntology : DomainOntology
    {
        public override string DomainName => "orders";

        protected override void Define(IOntologyBuilder builder)
        {
            var subject = new ActionSubject(DomainName, "Order");
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(subject, "malformed-frame", "malformed frame")
                    {
                        TouchedResources = new ActionResource[] { null! },
                    },
                ],
            });
        }
    }

    public sealed class NullPostconditionEntryOntology : DomainOntology
    {
        public override string DomainName => "orders";

        protected override void Define(IOntologyBuilder builder)
        {
            var subject = new ActionSubject(DomainName, "Order");
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(subject, "malformed-postcondition", "malformed postcondition")
                    {
                        Postconditions = new ActionPostcondition[] { null! },
                    },
                ],
            });
        }
    }

    public sealed class DuplicateActionIdentityOntology : DomainOntology
    {
        public override string DomainName => "orders";

        protected override void Define(IOntologyBuilder builder)
        {
            var subject = new ActionSubject(DomainName, "Order");
            builder.ObjectTypeFromDescriptor(new ObjectTypeDescriptor
            {
                Name = "Order",
                DomainName = DomainName,
                ClrType = typeof(object),
                Actions =
                [
                    new ActionDescriptor(subject, "ship", "first ship"),
                    new ActionDescriptor(subject, "ship", "second ship"),
                ],
            });
        }
    }
}
