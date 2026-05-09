// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Actions;

public class ConstraintViolationReportTests
{
    [Test]
    public async Task ConstraintViolationReport_Construction_PreservesAllFields()
    {
        // Arrange
        var precondition = new ActionPrecondition
        {
            Expression = "(p.Quantity > 0)",
            Description = "Quantity must be positive",
            Kind = PreconditionKind.PropertyPredicate,
            Strength = ConstraintStrength.Hard,
        };

        var hard1 = new ConstraintEvaluation(precondition, IsSatisfied: false, ConstraintStrength.Hard, "Quantity is 0", null);
        var hard2 = new ConstraintEvaluation(precondition, IsSatisfied: false, ConstraintStrength.Hard, "Quantity is negative", null);
        var soft1 = new ConstraintEvaluation(precondition, IsSatisfied: false, ConstraintStrength.Soft, "Optional link missing", null);

        // Act
        var report = new ConstraintViolationReport(
            ActionName: "Ship",
            Hard: [hard1, hard2],
            Soft: [soft1],
            SuggestedCorrection: "Set Quantity to a positive value");

        // Assert
        await Assert.That(report.ActionName).IsEqualTo("Ship");
        await Assert.That(report.Hard.Count).IsEqualTo(2);
        await Assert.That(report.Soft.Count).IsEqualTo(1);
        await Assert.That(report.SuggestedCorrection).IsEqualTo("Set Quantity to a positive value");
    }
}
