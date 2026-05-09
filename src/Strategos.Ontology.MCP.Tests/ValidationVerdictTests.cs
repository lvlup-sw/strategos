using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests;

public class ValidationVerdictTests
{
    [Test]
    public async Task ValidationVerdict_Construction_PreservesAllFields()
    {
        var precondition = new ActionPrecondition
        {
            Expression = "balance > 0",
            Description = "Balance must be positive",
            Kind = PreconditionKind.PropertyPredicate,
            Strength = ConstraintStrength.Hard,
        };
        var hardEval = new ConstraintEvaluation(
            precondition,
            IsSatisfied: false,
            Strength: ConstraintStrength.Hard,
            FailureReason: "balance was zero",
            ExpectedShape: null);
        var softPrecondition = new ActionPrecondition
        {
            Expression = "preferred",
            Description = "Preferred status",
            Kind = PreconditionKind.PropertyPredicate,
            Strength = ConstraintStrength.Soft,
        };
        var softEval = new ConstraintEvaluation(
            softPrecondition,
            IsSatisfied: false,
            Strength: ConstraintStrength.Soft,
            FailureReason: "preferred status missing",
            ExpectedShape: null);

        var nodeRef = new OntologyNodeRef("trading", "Order", "ord-1");
        var blastRadius = new BlastRadius(
            DirectlyAffected: new List<OntologyNodeRef> { nodeRef },
            TransitivelyAffected: new List<OntologyNodeRef>(),
            CrossDomainHops: new List<CrossDomainHop>(),
            Scope: BlastRadiusScope.Local);

        var patternViolation = new PatternViolation(
            "OrphanedNode",
            "Node has no incoming edges",
            nodeRef,
            ViolationSeverity.Error);

        var coverage = new CoverageReport(1, 2, new List<OntologyNodeRef>());

        var verdict = new ValidationVerdict(
            Passed: false,
            HardViolations: new List<ConstraintEvaluation> { hardEval },
            SoftWarnings: new List<ConstraintEvaluation> { softEval },
            BlastRadius: blastRadius,
            PatternViolations: new List<PatternViolation> { patternViolation },
            Coverage: coverage);

        await Assert.That(verdict.Passed).IsFalse();
        await Assert.That(verdict.HardViolations).HasCount().EqualTo(1);
        await Assert.That(verdict.HardViolations[0]).IsEqualTo(hardEval);
        await Assert.That(verdict.SoftWarnings).HasCount().EqualTo(1);
        await Assert.That(verdict.SoftWarnings[0]).IsEqualTo(softEval);
        await Assert.That(verdict.BlastRadius).IsEqualTo(blastRadius);
        await Assert.That(verdict.PatternViolations).HasCount().EqualTo(1);
        await Assert.That(verdict.PatternViolations[0]).IsEqualTo(patternViolation);
        await Assert.That(verdict.Coverage).IsEqualTo(coverage);
    }
}
