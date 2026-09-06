// Copyright (c) Levelup Software. All rights reserved.

using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

// --- Test domain models for constraint report tests ---

public enum ReportTestStatus
{
    Pending,
    Active,
    Closed,
}

public record ReportItem(
    Guid Id,
    string Name,
    decimal Quantity,
    ReportTestStatus Status);

public record ReportTarget(Guid Id, string Label);

public class ConstraintReportOntology : DomainOntology
{
    public override string DomainName => "report-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ReportTarget>(obj =>
        {
            obj.Key(t => t.Id);
            obj.Property(t => t.Label);
        });

        builder.Object<ReportItem>(obj =>
        {
            obj.Key(i => i.Id);
            obj.Property(i => i.Name).Required();
            obj.Property(i => i.Quantity);
            obj.Property(i => i.Status);
            obj.HasMany<ReportTarget>("Targets");

            // Action with no preconditions
            obj.Action("Create")
                .Description("Create a new item");

            // Action with hard property predicate
            obj.Action("Activate")
                .Description("Activate the item")
                .Requires(i => i.Status == ReportTestStatus.Pending);

            // Action with hard Quantity > 0 constraint
            obj.Action("Ship")
                .Description("Ship the item")
                .Requires(i => i.Quantity > 0);

            // Action with soft link constraint
            obj.Action("Audit")
                .Description("Audit the item")
                .RequiresLinkSoft("Targets");

            // Action with mixed constraints: hard Status + soft Quantity
            obj.Action("Archive")
                .Description("Archive the item")
                .Requires(i => i.Status == ReportTestStatus.Active)
                .RequiresSoft(i => i.Quantity > 0);

            // Action with hard constraint that will be unsatisfied
            obj.Action("Close")
                .Description("Close the item")
                .Requires(i => i.Status == ReportTestStatus.Active)
                .RequiresLink("Targets");
        });
    }
}

public static class ReportTestGraphFactory
{
    public static OntologyGraph Build()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<ConstraintReportOntology>();
        return graphBuilder.Build();
    }

    public static IOntologyQuery CreateQueryService()
    {
        return new OntologyQueryService(Build());
    }
}

// --- Tests ---

public class ActionConstraintReportTests
{
    [Test]
    public async Task GetActionConstraintReport_AllSatisfied_IsAvailableTrue()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var knownProps = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Status"] = PredicateLiteral.Enum(ReportTestStatus.Active),
            },
            links: new Dictionary<string, bool> { ["Targets"] = true });

        var reports = query.GetActionConstraintReport("ReportItem", knownProps);

        var closeReport = reports.Single(r => r.Action.Name == "Close");
        await Assert.That(closeReport.IsAvailable).IsTrue();
        await Assert.That(closeReport.Constraints.All(c => c.IsSatisfied)).IsTrue();
    }

    [Test]
    public async Task GetActionConstraintReport_PropertyPredicateUnsatisfied_HasFailureReason()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var knownProps = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Quantity"] = PredicateLiteral.Decimal(0m),
        });

        var reports = query.GetActionConstraintReport("ReportItem", knownProps);

        var shipReport = reports.Single(r => r.Action.Name == "Ship");
        var unsatisfied = shipReport.Constraints.Single(c => !c.IsSatisfied);
        await Assert.That(unsatisfied.FailureReason).IsNotNull();
        await Assert.That(unsatisfied.FailureReason!).Contains("Quantity");
    }

    [Test]
    public async Task GetActionConstraintReport_PropertyPredicateUnsatisfied_HasExpectedShape()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var knownProps = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Quantity"] = PredicateLiteral.Decimal(0m),
        });

        var reports = query.GetActionConstraintReport("ReportItem", knownProps);

        var shipReport = reports.Single(r => r.Action.Name == "Ship");
        var unsatisfied = shipReport.Constraints.Single(c => !c.IsSatisfied);
        await Assert.That(unsatisfied.ExpectedShape).IsNotNull();
        await Assert.That(unsatisfied.ExpectedShape!.ContainsKey("Quantity")).IsTrue();
    }

    [Test]
    public async Task GetActionConstraintReport_LinkExistsUnknown_SoftConstraintStillAvailable()
    {
        var query = ReportTestGraphFactory.CreateQueryService();

        // No knownProperties at all -- soft link constraint is unknown
        var reports = query.GetActionConstraintReport("ReportItem");

        var auditReport = reports.Single(r => r.Action.Name == "Audit");
        // Soft constraint not satisfied but action still available
        await Assert.That(auditReport.IsAvailable).IsTrue();
        await Assert.That(auditReport.Constraints[0].Strength).IsEqualTo(ConstraintStrength.Soft);
        await Assert.That(auditReport.Constraints[0].TruthValue)
            .IsEqualTo(PredicateTruthValue.Indeterminate);
    }

    [Test]
    public async Task GetActionConstraintReport_NoPreconditions_AllAvailable()
    {
        var query = ReportTestGraphFactory.CreateQueryService();

        var reports = query.GetActionConstraintReport("ReportItem");

        var createReport = reports.Single(r => r.Action.Name == "Create");
        await Assert.That(createReport.IsAvailable).IsTrue();
        await Assert.That(createReport.Constraints).IsEmpty();
    }

    [Test]
    public async Task GetActionConstraintReport_MultipleConstraints_PartialSatisfaction()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var knownProps = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Status"] = PredicateLiteral.Enum(ReportTestStatus.Active),
            ["Quantity"] = PredicateLiteral.Decimal(0m),
        });

        var reports = query.GetActionConstraintReport("ReportItem", knownProps);

        var archiveReport = reports.Single(r => r.Action.Name == "Archive");
        // Hard constraint (Status == Active) satisfied, soft (Quantity > 0) unsatisfied
        await Assert.That(archiveReport.IsAvailable).IsTrue();

        var satisfied = archiveReport.Constraints.Where(c => c.IsSatisfied).ToList();
        var unsatisfied = archiveReport.Constraints.Where(c => !c.IsSatisfied).ToList();
        await Assert.That(satisfied.Count).IsEqualTo(1);
        await Assert.That(unsatisfied.Count).IsEqualTo(1);
        await Assert.That(unsatisfied[0].Strength).IsEqualTo(ConstraintStrength.Soft);
    }

    [Test]
    public async Task GetActionConstraintReport_UnknownType_ReturnsEmpty()
    {
        var query = ReportTestGraphFactory.CreateQueryService();

        var reports = query.GetActionConstraintReport("NonExistentType");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task GetActionConstraintReport_HardConstraintUnsatisfied_IsAvailableFalse()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var knownProps = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Status"] = PredicateLiteral.Enum(ReportTestStatus.Pending),
        });

        var reports = query.GetActionConstraintReport("ReportItem", knownProps);

        var closeReport = reports.Single(r => r.Action.Name == "Close");
        // Close requires Status == Active (hard) -- Pending doesn't match
        await Assert.That(closeReport.IsAvailable).IsFalse();

        var hardUnsatisfied = closeReport.Constraints
            .Where(c => !c.IsSatisfied && c.Strength == ConstraintStrength.Hard)
            .ToList();
        await Assert.That(hardUnsatisfied.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task GetCandidateActions_UnknownHardFact_IsIndeterminateAndRemainsValid()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var facts = new ActionFacts(properties: new Dictionary<string, PredicateLiteral>
        {
            ["Status"] = PredicateLiteral.Enum(ReportTestStatus.Active),
        });

        var candidates = query.GetCandidateActions("ReportItem", facts);
        var valid = query.GetValidActions("ReportItem", facts);

        var close = candidates.Single(candidate => candidate.Action.Name == "Close");
        await Assert.That(close.Availability).IsEqualTo(ActionAvailability.Indeterminate);
        await Assert.That(valid.Select(action => action.Name)).Contains("Close");
    }

    [Test]
    public async Task GetCandidateActions_ProvenUnavailableActionIsExcludedButConstraintReportRetainsIt()
    {
        var query = ReportTestGraphFactory.CreateQueryService();
        var facts = new ActionFacts(
            properties: new Dictionary<string, PredicateLiteral>
            {
                ["Status"] = PredicateLiteral.Enum(ReportTestStatus.Pending),
            },
            links: new Dictionary<string, bool> { ["Targets"] = false });

        var candidates = query.GetCandidateActions("ReportItem", facts);
        var reports = query.GetActionConstraintReport("ReportItem", facts);

        await Assert.That(candidates.Select(candidate => candidate.Action.Name))
            .DoesNotContain("Close");
        await Assert.That(reports.Single(report => report.Action.Name == "Close").Availability)
            .IsEqualTo(ActionAvailability.Unavailable);
    }
}
