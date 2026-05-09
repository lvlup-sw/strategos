using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests;

public class DesignIntentTypesTests
{
    [Test]
    public async Task OntologyNodeRef_Construction_PreservesFields()
    {
        var nodeRef = new OntologyNodeRef(Domain: "trading", ObjectTypeName: "Order", Key: "ord-123");

        await Assert.That(nodeRef.Domain).IsEqualTo("trading");
        await Assert.That(nodeRef.ObjectTypeName).IsEqualTo("Order");
        await Assert.That(nodeRef.Key).IsEqualTo("ord-123");
    }

    [Test]
    public async Task ProposedAction_Construction_PreservesFields()
    {
        var subject = new OntologyNodeRef(Domain: "trading", ObjectTypeName: "Order", Key: "ord-456");
        var args = new Dictionary<string, object?> { ["quantity"] = 10 };
        var action = new ProposedAction("Ship", subject, args);

        await Assert.That(action.ActionName).IsEqualTo("Ship");
        await Assert.That(action.Subject).IsEqualTo(subject);
        await Assert.That(action.Arguments).IsNotNull();
        await Assert.That(action.Arguments!["quantity"]).IsEqualTo(10);
    }

    [Test]
    public async Task ProposedAction_NullArguments_IsPermitted()
    {
        var subject = new OntologyNodeRef(Domain: "trading", ObjectTypeName: "Order", Key: "ord-789");
        var action = new ProposedAction("Cancel", subject, null);

        await Assert.That(action.Arguments).IsNull();
    }

    [Test]
    public async Task CoverageReport_Construction_PreservesFields()
    {
        var uncovered = new List<OntologyNodeRef>
        {
            new OntologyNodeRef(Domain: "billing", ObjectTypeName: "Invoice", Key: "inv-001"),
        };
        var report = new CoverageReport(8, 10, uncovered);

        await Assert.That(report.CoveredNodes).IsEqualTo(8);
        await Assert.That(report.TotalNodes).IsEqualTo(10);
        await Assert.That(report.Uncovered).HasCount().EqualTo(1);
        await Assert.That(report.Uncovered[0].ObjectTypeName).IsEqualTo("Invoice");
    }

    [Test]
    public async Task DesignIntent_Construction_PreservesAllFields()
    {
        var nodeRef = new OntologyNodeRef(Domain: "catalog", ObjectTypeName: "Product", Key: "prod-001");
        var action = new ProposedAction("Activate", nodeRef, null);
        var knownProps = new Dictionary<string, object?> { ["status"] = "draft" };

        var intent = new DesignIntent(
            new List<OntologyNodeRef> { nodeRef },
            new List<ProposedAction> { action },
            knownProps);

        await Assert.That(intent.AffectedNodes).HasCount().EqualTo(1);
        await Assert.That(intent.AffectedNodes[0]).IsEqualTo(nodeRef);
        await Assert.That(intent.Actions).HasCount().EqualTo(1);
        await Assert.That(intent.Actions[0].ActionName).IsEqualTo("Activate");
        await Assert.That(intent.KnownProperties).IsNotNull();
        await Assert.That(intent.KnownProperties!["status"]).IsEqualTo("draft");
    }

    [Test]
    public async Task DesignIntent_NullKnownProperties_IsPermitted()
    {
        var nodeRef = new OntologyNodeRef(Domain: "trading", ObjectTypeName: "Order", Key: "ord-001");
        var intent = new DesignIntent(
            new List<OntologyNodeRef> { nodeRef },
            new List<ProposedAction>(),
            null);

        await Assert.That(intent.KnownProperties).IsNull();
    }

    [Test]
    public async Task IOntologyCoverageProvider_GetCoverage_ReturnsNullable()
    {
        // Verifies the interface contract: GetCoverage returns CoverageReport?
        // We use NSubstitute to build a stub without production code.
        var provider = Substitute.For<IOntologyCoverageProvider>();
        var nodeRef = new OntologyNodeRef(Domain: "trading", ObjectTypeName: "Order", Key: "ord-001");
        var intent = new DesignIntent(
            new List<OntologyNodeRef> { nodeRef },
            new List<ProposedAction>(),
            null);

        provider.GetCoverage(intent).Returns((CoverageReport?)null);

        var result = provider.GetCoverage(intent);

        await Assert.That(result).IsNull();
    }
}
