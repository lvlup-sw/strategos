using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Actions;

public class DispatchReadOnlyAsyncTests
{
    private sealed class CapturingDispatcher : IActionDispatcher
    {
        private readonly ActionResult _result;

        public CapturingDispatcher(ActionResult result) => _result = result;

        public ActionContext? CapturedContext { get; private set; }

        public object? CapturedRequest { get; private set; }

        public int CallCount { get; private set; }

        public Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default)
        {
            CapturedContext = context;
            CapturedRequest = request;
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    [Test]
    public async Task DispatchReadOnlyAsync_OnReadOnlyAction_DelegatesToDispatchAsync()
    {
        var expected = new ActionResult(true, Result: "ok");
        var dispatcher = new CapturingDispatcher(expected);
        var descriptor = new ActionDescriptor("GetPosition", "Read position") { IsReadOnly = true };
        var context = new ActionContext("CRM", "Contact", "c-1", "GetPosition")
        {
            ActionDescriptor = descriptor,
        };
        var request = new { Probe = 1 };

        var result = await ((IActionDispatcher)dispatcher).DispatchReadOnlyAsync(
            context, request, CancellationToken.None);

        await Assert.That(result).IsSameReferenceAs(expected);
        await Assert.That(dispatcher.CallCount).IsEqualTo(1);
        await Assert.That(dispatcher.CapturedContext).IsSameReferenceAs(context);
        await Assert.That(dispatcher.CapturedRequest).IsSameReferenceAs(request);
    }

    [Test]
    public async Task DispatchReadOnlyAsync_OnNonReadOnlyAction_ReturnsFailureWithoutCallingInner()
    {
        var dispatcher = new CapturingDispatcher(new ActionResult(true, Result: "should-not-see"));
        var descriptor = new ActionDescriptor("UpdatePosition", "Mutates position") { IsReadOnly = false };
        var context = new ActionContext("CRM", "Contact", "c-1", "UpdatePosition")
        {
            ActionDescriptor = descriptor,
        };
        var request = new { Probe = 1 };

        var result = await ((IActionDispatcher)dispatcher).DispatchReadOnlyAsync(
            context, request, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!).Contains("UpdatePosition");
        await Assert.That(dispatcher.CallCount).IsEqualTo(0);
    }

    [Test]
    public async Task DispatchReadOnlyAsync_OnMissingDescriptor_ReturnsFailureWithoutCallingInner()
    {
        var dispatcher = new CapturingDispatcher(new ActionResult(true));
        var context = new ActionContext("CRM", "Contact", "c-1", "Unknown");
        var request = new { Probe = 1 };

        var result = await ((IActionDispatcher)dispatcher).DispatchReadOnlyAsync(
            context, request, CancellationToken.None);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(dispatcher.CallCount).IsEqualTo(0);
    }
}
