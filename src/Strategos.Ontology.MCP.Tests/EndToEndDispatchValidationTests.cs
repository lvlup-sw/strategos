using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Strategos.Ontology;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests;

// --- Fixture domain types ---

public class E2EPosition
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class E2EOrder
{
    public string OrderId { get; set; } = "";
    public string PositionId { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class E2EGetPositionRequest
{
    public string Id { get; set; } = "";
}

public class E2ESubmitOrderRequest
{
    public string PositionId { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class E2ERiskLimits
{
    public string LimitId { get; set; } = "";
    public decimal MaxExposure { get; set; }
}

// Uses RequiresLink so the precondition is deterministically unsatisfied
// when no known properties are supplied to the constraint reporter.
public class E2ETradingDomain : DomainOntology
{
    public override string DomainName => "e2e-trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<E2EPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.Quantity);

            obj.Action("get_position")
                .Description("Read position data without mutation")
                .Accepts<E2EGetPositionRequest>()
                .ReadOnly();

            obj.HasMany<E2EOrder>("Orders")
                .Description("Orders placed against this position");
        });

        builder.Object<E2EOrder>(obj =>
        {
            obj.Key(o => o.OrderId);
            obj.Property(o => o.PositionId).Required();
            obj.Property(o => o.Quantity);

            obj.Action("submit_order")
                .Description("Submit a new order — requires Position link to be present")
                .Accepts<E2ESubmitOrderRequest>()
                .RequiresLink("Position");

            obj.HasOne<E2EPosition>("Position");
        });

        builder.CrossDomainLink("position_to_risk")
            .From<E2EPosition>()
            .ToExternal("e2e-risk", "E2ERiskLimits")
            .Description("Position linked to its risk limit bucket");
    }
}

public class E2ERiskDomain : DomainOntology
{
    public override string DomainName => "e2e-risk";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<E2ERiskLimits>(obj =>
        {
            obj.Key(r => r.LimitId);
            obj.Property(r => r.MaxExposure);
        });
    }
}

// --- Fixture helpers shared across test methods ---

file static class E2EFixture
{
    public static OntologyGraph BuildGraph()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<E2ETradingDomain>();
        graphBuilder.AddDomain<E2ERiskDomain>();
        return graphBuilder.Build();
    }

    public static IOntologyQuery BuildRealQuery(OntologyGraph graph)
    {
        // OntologyQueryService is internal to Strategos.Ontology. Route through the
        // public DI surface (AddOntology) to obtain the real implementation without
        // requiring an InternalsVisibleTo grant. The hash is content-addressed so
        // the IOntologyQuery's internal graph Version matches _graph.Version.
        var services = new ServiceCollection();
        services.AddOntology(opts =>
        {
            opts.AddDomain<E2ETradingDomain>();
            opts.AddDomain<E2ERiskDomain>();
        });
        return services.BuildServiceProvider().GetRequiredService<IOntologyQuery>();
    }

    public static ActionDescriptor GetReadOnlyDescriptor(IOntologyQuery query) =>
        query.GetActions("E2EPosition").Single(a => a.Name == "get_position");

    public static ActionDescriptor GetMutatingDescriptor(IOntologyQuery query) =>
        query.GetActions("E2EOrder").Single(a => a.Name == "submit_order");
}

// ---------------------------------------------------------------------------
// Composite e2e acceptance tests for dispatch + validation (Task X1)
// ---------------------------------------------------------------------------

public class EndToEndDispatchValidationTests
{
    private OntologyGraph _graph = null!;
    private IOntologyQuery _query = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = E2EFixture.BuildGraph();
        _query = E2EFixture.BuildRealQuery(_graph);
    }

    [Test]
    public async Task ValidateCleanIntent_NoViolations_PassedTrueWithNonNullBlastRadiusAndNullCoverage()
    {
        var tool = new OntologyValidateTool(_query);

        var positionRef = new OntologyNodeRef("e2e-trading", "E2EPosition", "pos-1");
        var orderRef = new OntologyNodeRef("e2e-trading", "E2EOrder", "ord-1");
        var intent = new DesignIntent(
            AffectedNodes: [positionRef, orderRef],
            Actions: [new ProposedAction("get_position", positionRef, null)],
            KnownProperties: null);

        var verdict = tool.Validate(intent);
        var meta = ResponseMeta.ForGraph(_graph);
        var response = new ValidateResult(verdict, meta);

        await Assert.That(verdict.Passed).IsTrue();
        await Assert.That(verdict.BlastRadius).IsNotNull();
        await Assert.That(verdict.Coverage).IsNull();
        await Assert.That(response.Meta.OntologyVersion)
            .IsEqualTo(ResponseMeta.WireFormat(_graph.Version));
    }

    [Test]
    public async Task ValidateWithCrossDomainSeeds_BlastRadiusIsCrossDomainOrGlobal()
    {
        var tool = new OntologyValidateTool(_query);

        var positionRef = new OntologyNodeRef("e2e-trading", "E2EPosition", "pos-1");
        var riskRef = new OntologyNodeRef("e2e-risk", "E2ERiskLimits", "limit-1");
        var intent = new DesignIntent(
            AffectedNodes: [positionRef, riskRef],
            Actions: [new ProposedAction("get_position", positionRef, null)],
            KnownProperties: null);

        var verdict = tool.Validate(intent);

        await Assert.That(verdict.BlastRadius).IsNotNull();
        var scope = verdict.BlastRadius.Scope;
        // Tightened (was: CrossDomain || Global || Domain) — Domain would
        // mean cross-domain classification regressed silently.
        await Assert.That(scope == BlastRadiusScope.CrossDomain || scope == BlastRadiusScope.Global)
            .IsTrue();
    }

    [Test]
    public async Task DispatchReadOnly_InnerCalledOnce_ObserverReceivesDispatch_ReturnsSuccess()
    {
        var readOnlyDescriptor = E2EFixture.GetReadOnlyDescriptor(_query);
        await Assert.That(readOnlyDescriptor.IsReadOnly).IsTrue();

        var innerDispatcher = Substitute.For<IActionDispatcher>();
        innerDispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(IsSuccess: true));

        var observer = Substitute.For<IActionDispatchObserver>();
        observer
            .OnDispatchedAsync(Arg.Any<ActionContext>(), Arg.Any<ActionResult>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        IActionDispatcher sut = new ObservableActionDispatcher(
            innerDispatcher,
            [observer],
            NullLogger<ObservableActionDispatcher>.Instance);

        var context = new ActionContext(
            Domain: "e2e-trading",
            ObjectType: "E2EPosition",
            ObjectId: "pos-1",
            ActionName: "get_position")
        {
            ActionDescriptor = readOnlyDescriptor,
        };

        var result = await sut.DispatchReadOnlyAsync(context, new E2EGetPositionRequest { Id = "pos-1" });

        await Assert.That(result.IsSuccess).IsTrue();
        await innerDispatcher.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(c => c.ActionName == "get_position"),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
        await observer.Received(1).OnDispatchedAsync(
            Arg.Is<ActionContext>(c => c.ActionName == "get_position"),
            Arg.Is<ActionResult>(r => r.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DispatchMutating_LinkPreconditionUnsatisfied_ViolationsPopulatedOnResult()
    {
        var mutatingDescriptor = E2EFixture.GetMutatingDescriptor(_query);

        var innerDispatcher = Substitute.For<IActionDispatcher>();
        innerDispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(IsSuccess: false, Error: "business rule rejected"));

        var sut = new ConstraintReportingActionDispatcher(
            innerDispatcher,
            _query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance);

        // No known properties supplied — the RequiresLink("Position") precondition
        // is unsatisfied when the link is absent from knownProperties.
        var context = new ActionContext(
            Domain: "e2e-trading",
            ObjectType: "E2EOrder",
            ObjectId: "ord-1",
            ActionName: "submit_order")
        {
            ActionDescriptor = mutatingDescriptor,
        };

        var result = await sut.DispatchAsync(context, new E2ESubmitOrderRequest { Quantity = 0m });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.Hard.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(result.Violations.ActionName).IsEqualTo("submit_order");
    }
}
