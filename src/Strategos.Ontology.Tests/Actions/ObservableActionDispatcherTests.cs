using Microsoft.Extensions.Logging.Abstractions;
using Strategos.Ontology.Actions;

namespace Strategos.Ontology.Tests.Actions;

public class ObservableActionDispatcherTests
{
    private static ActionContext MakeContext() =>
        new("CRM", "Order", "o-1", "Place");

    [Test]
    public async Task Dispatch_AfterCompletion_InvokesAllRegisteredObservers()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var ctx = MakeContext();
        var innerResult = new ActionResult(true, Result: "ok");
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(innerResult));
        var observerA = Substitute.For<IActionDispatchObserver>();
        var observerB = Substitute.For<IActionDispatchObserver>();
        var sut = new ObservableActionDispatcher(
            inner,
            new[] { observerA, observerB },
            NullLogger<ObservableActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result).IsSameReferenceAs(innerResult);
        await observerA.Received(1).OnDispatchedAsync(ctx, innerResult, Arg.Any<CancellationToken>());
        await observerB.Received(1).OnDispatchedAsync(ctx, innerResult, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_ObserverThrows_DoesNotFailDispatch()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var ctx = MakeContext();
        var innerResult = new ActionResult(true, Result: "ok");
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(innerResult));

        var throwing = Substitute.For<IActionDispatchObserver>();
        throwing.OnDispatchedAsync(Arg.Any<ActionContext>(), Arg.Any<ActionResult>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("boom"));
        var tracking = Substitute.For<IActionDispatchObserver>();

        var sut = new ObservableActionDispatcher(
            inner,
            new[] { throwing, tracking },
            NullLogger<ObservableActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result).IsSameReferenceAs(innerResult);
        await tracking.Received(1).OnDispatchedAsync(ctx, innerResult, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_NoObservers_DelegatesToInnerCleanly()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var ctx = MakeContext();
        var innerResult = new ActionResult(true, Result: "ok");
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(innerResult));
        var sut = new ObservableActionDispatcher(
            inner,
            Array.Empty<IActionDispatchObserver>(),
            NullLogger<ObservableActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result).IsSameReferenceAs(innerResult);
        await inner.Received(1).DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Dispatch_OnInnerFailure_StillNotifiesObservers()
    {
        var inner = Substitute.For<IActionDispatcher>();
        var ctx = MakeContext();
        var failure = new ActionResult(false, Error: "rejected");
        inner.DispatchAsync(ctx, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(failure));
        var observer = Substitute.For<IActionDispatchObserver>();
        var sut = new ObservableActionDispatcher(
            inner,
            new[] { observer },
            NullLogger<ObservableActionDispatcher>.Instance);

        var result = await sut.DispatchAsync(ctx, new { }, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await observer.Received(1).OnDispatchedAsync(ctx, failure, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Constructor_NullInner_Throws()
    {
        await Assert.That(() => new ObservableActionDispatcher(
            null!,
            Array.Empty<IActionDispatchObserver>(),
            NullLogger<ObservableActionDispatcher>.Instance))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_NullObservers_Throws()
    {
        var inner = Substitute.For<IActionDispatcher>();

        await Assert.That(() => new ObservableActionDispatcher(
            inner,
            null!,
            NullLogger<ObservableActionDispatcher>.Instance))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_NullLogger_Throws()
    {
        var inner = Substitute.For<IActionDispatcher>();

        await Assert.That(() => new ObservableActionDispatcher(
            inner,
            Array.Empty<IActionDispatchObserver>(),
            null!))
            .Throws<ArgumentNullException>();
    }
}
