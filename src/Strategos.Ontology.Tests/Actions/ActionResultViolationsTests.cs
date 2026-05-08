// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Actions;

public class ActionResultViolationsTests
{
    [Test]
    public async Task ActionResult_NewWithoutViolations_DefaultsToNull()
    {
        var result = new ActionResult(false, null, "fail");

        await Assert.That(result.Violations).IsNull();
    }

    [Test]
    public async Task ActionResult_NewWithViolations_PreservesReport()
    {
        // Arrange
        var precondition = new ActionPrecondition
        {
            Expression = "(p.Quantity > 0)",
            Description = "Quantity must be positive",
            Kind = PreconditionKind.PropertyPredicate,
            Strength = ConstraintStrength.Hard,
        };

        var hard1 = new ConstraintEvaluation(precondition, IsSatisfied: false, ConstraintStrength.Hard, "zero quantity", null);
        var hard2 = new ConstraintEvaluation(precondition, IsSatisfied: false, ConstraintStrength.Hard, "negative quantity", null);

        var report = new ConstraintViolationReport(
            ActionName: "Ship",
            Hard: [hard1, hard2],
            Soft: [],
            SuggestedCorrection: null);

        // Act
        var result = new ActionResult(false, null, "preconditions failed", report);

        // Assert
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Hard.Count).IsEqualTo(2);
    }
}
