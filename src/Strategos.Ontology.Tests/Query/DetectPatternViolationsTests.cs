using Strategos.Ontology.Builder;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

// --- Fixtures ---

public class PvTrade
{
    public string Id { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal UnrealizedPnL { get; set; }
}

public class PvTradeRequest
{
    public decimal Quantity { get; set; }
}

public class PvLinkSource
{
    public string Id { get; set; } = "";
}

public class PvLinkTarget
{
    public string Id { get; set; } = "";
}

public interface IPvAuditable
{
    string Id { get; }
}

public enum PvLifecycleStatus
{
    Draft,
    Active,
    Closed,
}

public class PvLifecycleEntity
{
    public string Id { get; set; } = "";
    public PvLifecycleStatus Status { get; set; }
}

// 1) Computed.Write fixture: Trade has UnrealizedPnL as Computed; intent
//    proposes an action whose Arguments name "UnrealizedPnL" — the agent is
//    trying to override a computed property.
public sealed class PvComputedOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PvTrade>(o =>
        {
            o.Key(t => t.Id);
            o.Property(t => t.Quantity);
            o.Property(t => t.AverageCost);
            o.Property(t => t.CurrentPrice);
            o.Property(t => t.UnrealizedPnL)
                .Computed()
                .DerivedFrom(t => t.Quantity, t => t.AverageCost, t => t.CurrentPrice);
            o.Action("Adjust").Description("Adjust the trade");
        });
    }
}

// 2) Link.MissingExtensionPoint fixture: target type declares an
//    AcceptsExternalLinks point that requires source interface IPvAuditable;
//    source type does NOT implement IPvAuditable.
public sealed class PvExtensionPointOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IPvAuditable>("Auditable", iface =>
        {
            iface.Property(a => a.Id);
        });

        builder.Object<PvLinkSource>(o =>
        {
            o.Key(s => s.Id);
            o.HasOne<PvLinkTarget>("Out");
            o.Action("CreateLink")
                .Description("Create a link to a target")
                .CreatesLinked<PvLinkTarget>("Out");
        });

        builder.Object<PvLinkTarget>(o =>
        {
            o.Key(t => t.Id);
            o.AcceptsExternalLinks("AuditedFrom", ext =>
            {
                ext.FromInterface<IPvAuditable>();
                ext.Description("Sources must be auditable");
            });
        });
    }
}

// 3) Action.PreconditionPropertyMissing fixture: precondition references a
//    property that DOES exist on AcceptsType (clean case). The positive case
//    uses PvPreconditionMissingFixture defined at the bottom of this file.
public sealed class PvPreconditionPresentOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PvTrade>(o =>
        {
            o.Key(t => t.Id);
            o.Property(t => t.Quantity);
            // Precondition expression references "Quantity", which exists on PvTradeRequest.
            // Note: the lambda parameter is on PvTradeRequest, not PvTrade.
            o.Action("Submit")
                .Description("Submit a trade")
                .Accepts<PvTradeRequest>()
                .Requires(t => t.Quantity > 0);
        });
    }
}

// 4) Lifecycle.UnreachableInitial fixture: declare an Initial state with NO
//    incoming transition. (Triggering action does not produce the Initial.)
public sealed class PvUnreachableInitialOntology : DomainOntology
{
    public override string DomainName => "lifecycle";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PvLifecycleEntity>(o =>
        {
            o.Key(e => e.Id);
            o.Property(e => e.Status);
            o.Action("Activate").Description("Activate");
            o.Action("Close").Description("Close");
            o.Lifecycle(e => e.Status, (Action<ILifecycleBuilder<PvLifecycleStatus>>)(lifecycle =>
            {
                lifecycle.State(PvLifecycleStatus.Draft).Initial();
                lifecycle.State(PvLifecycleStatus.Active);
                lifecycle.State(PvLifecycleStatus.Closed).Terminal();

                // Notice: NO transition lands on Draft. Active->Closed only.
                lifecycle.Transition(PvLifecycleStatus.Draft, PvLifecycleStatus.Active)
                    .TriggeredByAction("Activate");
                lifecycle.Transition(PvLifecycleStatus.Active, PvLifecycleStatus.Closed)
                    .TriggeredByAction("Close");
            }));
        });
    }
}

// 4-clean) Lifecycle with Initial state that IS reachable via a transition.
public sealed class PvReachableInitialOntology : DomainOntology
{
    public override string DomainName => "lifecycle";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PvLifecycleEntity>(o =>
        {
            o.Key(e => e.Id);
            o.Property(e => e.Status);
            o.Action("Reset").Description("Reset");
            o.Action("Activate").Description("Activate");
            o.Action("Close").Description("Close");
            o.Lifecycle(e => e.Status, (Action<ILifecycleBuilder<PvLifecycleStatus>>)(lifecycle =>
            {
                lifecycle.State(PvLifecycleStatus.Draft).Initial();
                lifecycle.State(PvLifecycleStatus.Active);
                lifecycle.State(PvLifecycleStatus.Closed).Terminal();

                // Self-loop into Initial via Reset action makes Draft reachable.
                lifecycle.Transition(PvLifecycleStatus.Active, PvLifecycleStatus.Draft)
                    .TriggeredByAction("Reset");
                lifecycle.Transition(PvLifecycleStatus.Draft, PvLifecycleStatus.Active)
                    .TriggeredByAction("Activate");
                lifecycle.Transition(PvLifecycleStatus.Active, PvLifecycleStatus.Closed)
                    .TriggeredByAction("Close");
            }));
        });
    }
}

// Empty/minimal fixture for negative tests on patterns 1-3.
public sealed class PvCleanOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PvTrade>(o =>
        {
            o.Key(t => t.Id);
            o.Property(t => t.Quantity);
            o.Action("Submit")
                .Description("Submit a trade");
        });
    }
}

// --- Tests ---

public class DetectPatternViolationsTests
{
    private static IOntologyQuery CreateQuery(params DomainOntology[] domains)
    {
        var graphBuilder = new OntologyGraphBuilder();
        foreach (var d in domains)
        {
            graphBuilder.AddDomain(d);
        }

        return new OntologyQueryService(graphBuilder.Build());
    }

    // === Computed.Write ===

    [Test]
    public async Task DetectPatternViolations_WriteToComputedProperty_ReturnsErrorViolation()
    {
        var query = CreateQuery(new PvComputedOntology());
        var subject = new OntologyNodeRef("trading", "PvTrade", "trade-1");
        var action = new ProposedAction(
            ActionName: "Adjust",
            Subject: subject,
            Arguments: new Dictionary<string, object?> { ["UnrealizedPnL"] = 42.0m });
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        var violation = violations.FirstOrDefault(v => v.PatternName == "Computed.Write");
        await Assert.That(violation).IsNotNull();
        await Assert.That(violation!.Severity).IsEqualTo(ViolationSeverity.Error);
        await Assert.That(violation.Subject).IsEqualTo(subject);
    }

    [Test]
    public async Task DetectPatternViolations_NoComputedWrite_ReturnsNoComputedViolation()
    {
        var query = CreateQuery(new PvComputedOntology());
        var subject = new OntologyNodeRef("trading", "PvTrade", "trade-1");
        var action = new ProposedAction(
            ActionName: "Adjust",
            Subject: subject,
            Arguments: new Dictionary<string, object?> { ["Quantity"] = 5.0m });
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        await Assert.That(violations.Any(v => v.PatternName == "Computed.Write")).IsFalse();
    }

    // === Link.MissingExtensionPoint ===

    [Test]
    public async Task DetectPatternViolations_LinkWithoutMatchingExtensionPoint_ReturnsErrorViolation()
    {
        var query = CreateQuery(new PvExtensionPointOntology());
        var subject = new OntologyNodeRef("trading", "PvLinkSource", "src-1");
        var action = new ProposedAction(
            ActionName: "CreateLink",
            Subject: subject,
            Arguments: null);
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        var violation = violations.FirstOrDefault(v => v.PatternName == "Link.MissingExtensionPoint");
        await Assert.That(violation).IsNotNull();
        await Assert.That(violation!.Severity).IsEqualTo(ViolationSeverity.Error);
    }

    [Test]
    public async Task DetectPatternViolations_LinkTargetWithoutExtensionPointConstraint_ReturnsNoViolation()
    {
        // Use the clean ontology where the action does not create a link to a
        // type with extension-point constraints.
        var query = CreateQuery(new PvCleanOntology());
        var subject = new OntologyNodeRef("trading", "PvTrade", "trade-1");
        var action = new ProposedAction("Submit", subject, null);
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        await Assert.That(violations.Any(v => v.PatternName == "Link.MissingExtensionPoint")).IsFalse();
    }

    // === Action.PreconditionPropertyMissing ===

    [Test]
    public async Task DetectPatternViolations_PreconditionReferencesMissingProperty_ReturnsErrorViolation()
    {
        var query = CreateQuery(new PvPreconditionMissingFixture());
        var subject = new OntologyNodeRef("trading", "PvTrade", "trade-1");
        var action = new ProposedAction("Submit", subject, null);
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        var violation = violations.FirstOrDefault(v => v.PatternName == "Action.PreconditionPropertyMissing");
        await Assert.That(violation).IsNotNull();
        await Assert.That(violation!.Severity).IsEqualTo(ViolationSeverity.Error);
    }

    [Test]
    public async Task DetectPatternViolations_PreconditionPropertyOnAcceptsType_ReturnsNoPreconditionViolation()
    {
        var query = CreateQuery(new PvPreconditionPresentOntology());
        var subject = new OntologyNodeRef("trading", "PvTrade", "trade-1");
        var action = new ProposedAction("Submit", subject, null);
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        await Assert.That(violations.Any(v => v.PatternName == "Action.PreconditionPropertyMissing"))
            .IsFalse();
    }

    // === Lifecycle.UnreachableInitial ===

    [Test]
    public async Task DetectPatternViolations_UnreachableInitialState_ReturnsWarningViolation()
    {
        var query = CreateQuery(new PvUnreachableInitialOntology());
        var subject = new OntologyNodeRef("lifecycle", "PvLifecycleEntity", null);
        var intent = new DesignIntent([subject], [], null);

        var violations = query.DetectPatternViolations([subject], intent);

        var violation = violations.FirstOrDefault(v => v.PatternName == "Lifecycle.UnreachableInitial");
        await Assert.That(violation).IsNotNull();
        await Assert.That(violation!.Severity).IsEqualTo(ViolationSeverity.Warning);
    }

    [Test]
    public async Task DetectPatternViolations_ReachableInitialState_ReturnsNoLifecycleViolation()
    {
        var query = CreateQuery(new PvReachableInitialOntology());
        var subject = new OntologyNodeRef("lifecycle", "PvLifecycleEntity", null);
        var intent = new DesignIntent([subject], [], null);

        var violations = query.DetectPatternViolations([subject], intent);

        await Assert.That(violations.Any(v => v.PatternName == "Lifecycle.UnreachableInitial")).IsFalse();
    }

    // === All-clean ===

    [Test]
    public async Task DetectPatternViolations_AllPatternsClean_ReturnsEmpty()
    {
        var query = CreateQuery(new PvCleanOntology());
        var subject = new OntologyNodeRef("trading", "PvTrade", "trade-1");
        var action = new ProposedAction("Submit", subject, null);
        var intent = new DesignIntent([subject], [action], null);

        var violations = query.DetectPatternViolations([subject], intent);

        await Assert.That(violations).IsEmpty();
    }
}

// Fixture used only by the precondition-missing positive test. We need a
// precondition expression that names a property NOT on AcceptsType. Since
// expression-tree comparisons take the parameter-bound type directly, we
// build it where the lambda parameter (PvTrade) has Quantity but AcceptsType
// (PvAuditOnlyRequest) does not.
public class PvAuditOnlyRequest
{
    public string AuditTag { get; set; } = "";
}

public sealed class PvPreconditionMissingFixture : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<PvTrade>(o =>
        {
            o.Key(t => t.Id);
            o.Property(t => t.Quantity);
            // Precondition expression references PvTrade.Quantity but AcceptsType
            // is PvAuditOnlyRequest which does NOT have Quantity.
            o.Action("Submit")
                .Description("Submit")
                .Accepts<PvAuditOnlyRequest>()
                .Requires(t => t.Quantity > 0);
        });
    }
}
