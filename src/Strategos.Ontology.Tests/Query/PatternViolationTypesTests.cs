// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

public class PatternViolationTypesTests
{
    [Test]
    public async Task PatternViolation_Construction_PreservesAllFields()
    {
        var subject = new OntologyNodeRef(Domain: "trading", ObjectTypeName: "Order", Key: "ord-99");

        var warning = new PatternViolation(
            PatternName: "RequiresApproval",
            Description: "Order exceeds approval threshold",
            Subject: subject,
            Severity: ViolationSeverity.Warning);

        var error = new PatternViolation(
            PatternName: "MissingLineItems",
            Description: "Order has no line items",
            Subject: subject,
            Severity: ViolationSeverity.Error);

        await Assert.That(warning.PatternName).IsEqualTo("RequiresApproval");
        await Assert.That(warning.Description).IsEqualTo("Order exceeds approval threshold");
        await Assert.That(warning.Subject).IsEqualTo(subject);
        await Assert.That(warning.Severity).IsEqualTo(ViolationSeverity.Warning);

        await Assert.That(error.PatternName).IsEqualTo("MissingLineItems");
        await Assert.That(error.Description).IsEqualTo("Order has no line items");
        await Assert.That(error.Subject).IsEqualTo(subject);
        await Assert.That(error.Severity).IsEqualTo(ViolationSeverity.Error);
    }
}
