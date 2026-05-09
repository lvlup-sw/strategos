using Microsoft.Extensions.Logging.Abstractions;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Actions;

public class ConstraintReportingActionDispatcherTests
{
    private static ActionContext MakeContext(
        string actionName,
        ActionDescriptor? descriptor = null) =>
        new("CRM", "Order", "o-1", actionName)
        {
            ActionDescriptor = descriptor,
        };

    private static ActionDescriptor MakeDescriptor(string name) =>
        new(name, $"{name} description");

    private static ConstraintEvaluation MakeEvaluation(
        string expression,
        bool isSatisfied,
        ConstraintStrength strength,
        string? failureReason = null)
    {
        var precondition = new ActionPrecondition
        {
            Expression = expression,
            Description = expression,
            Kind = PreconditionKind.PropertyPredicate,
            Strength = strength,
        };
        return new ConstraintEvaluation(precondition, isSatisfied, strength, failureReason, null);
    }

    [Test]
    public async Task Dispatch_DelegatesToInner()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var query = Substitute.For<IOntologyQuery>();
        query.GetActionConstraintReport(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns([]);
        var innerResult = new ActionResult(true, Result: "ok");
        var ctx = MakeContext("Place", MakeDescriptor("Place"));
        var request = new { Quantity = 10 };
        inner.DispatchAsync(ctx, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(innerResult));
        var sut = new ConstraintReportingActionDispatcher(
            inner,
            query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, request, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await inner.Received(1).DispatchAsync(ctx, request, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_PreconditionFailure_PopulatesHardViolations()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var query = Substitute.For<IOntologyQuery>();
        var descriptor = MakeDescriptor("Ship");
        var hard = MakeEvaluation("Quantity > 0", isSatisfied: false, ConstraintStrength.Hard, "Quantity must be positive");
        query.GetActionConstraintReport("Order", Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns([new ActionConstraintReport(descriptor, IsAvailable: false, Constraints: [hard])]);
        var ctx = MakeContext("Ship", descriptor);
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(false, Error: "precondition failed")));
        var sut = new ConstraintReportingActionDispatcher(
            inner,
            query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.ActionName).IsEqualTo("Ship");
        await Assert.That(result.Violations.Hard).HasCount().EqualTo(1);
        await Assert.That(result.Violations.Soft).IsEmpty();
    }

    [Test]
    public async Task Dispatch_SoftConstraintsOnSuccess_PopulatesSoftViolations()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var query = Substitute.For<IOntologyQuery>();
        var descriptor = MakeDescriptor("Audit");
        var soft = MakeEvaluation("LinkedTo(Approval)", isSatisfied: false, ConstraintStrength.Soft);
        query.GetActionConstraintReport("Order", Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns([new ActionConstraintReport(descriptor, IsAvailable: true, Constraints: [soft])]);
        var ctx = MakeContext("Audit", descriptor);
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(true, Result: "audited")));
        var sut = new ConstraintReportingActionDispatcher(
            inner,
            query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNotNull();
        await Assert.That(result.Violations!.ActionName).IsEqualTo("Audit");
        await Assert.That(result.Violations.Hard).IsEmpty();
        await Assert.That(result.Violations.Soft).HasCount().EqualTo(1);
    }

    [Test]
    public async Task Dispatch_NoConstraintsReported_ViolationsIsNull()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var query = Substitute.For<IOntologyQuery>();
        var descriptor = MakeDescriptor("Create");
        query.GetActionConstraintReport("Order", Arg.Any<IReadOnlyDictionary<string, object?>?>())
            .Returns([new ActionConstraintReport(descriptor, IsAvailable: true, Constraints: [])]);
        var ctx = MakeContext("Create", descriptor);
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(true, Result: "created")));
        var sut = new ConstraintReportingActionDispatcher(
            inner,
            query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Violations).IsNull();
    }

    [Test]
    public async Task Dispatch_DescriptorNullActionDescriptor_DelegatesUnchanged()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var query = Substitute.For<IOntologyQuery>();
        var ctx = MakeContext("Unknown", descriptor: null);
        var innerResult = new ActionResult(true, Result: "raw");
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(innerResult));
        var sut = new ConstraintReportingActionDispatcher(
            inner,
            query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result).IsSameReferenceAs(innerResult);
        query.DidNotReceive().GetActionConstraintReport(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>?>());
    }

    [Test]
    public async Task Constructor_NullInner_Throws()
    {
        var query = Substitute.For<IOntologyQuery>();

        await Assert.That(() => new ConstraintReportingActionDispatcher(
            null!,
            query,
            NullLogger<ConstraintReportingActionDispatcher>.Instance))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_NullQuery_Throws()
    {
        var inner = Substitute.For<IActionDispatcher>();

        await Assert.That(() => new ConstraintReportingActionDispatcher(
            inner,
            null!,
            NullLogger<ConstraintReportingActionDispatcher>.Instance))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_NullLogger_Throws()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var query = Substitute.For<IOntologyQuery>();

        await Assert.That(() => new ConstraintReportingActionDispatcher(inner, query, null!))
            .Throws<ArgumentNullException>();
    }
}
