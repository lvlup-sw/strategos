namespace Strategos.Ontology.Actions;

/// <summary>
/// Observer notified after each action dispatch completes. Implementations
/// must not throw — <see cref="ObservableActionDispatcher"/> isolates each
/// observer and logs (but swallows) exceptions so observation cannot fail a
/// dispatch.
/// </summary>
public interface IActionDispatchObserver
{
    /// <summary>
    /// Invoked after the inner dispatcher has produced a result for
    /// <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The action context that was dispatched.</param>
    /// <param name="result">The result produced by the inner dispatcher.</param>
    /// <param name="ct">Cancellation token associated with the dispatch.</param>
    /// <returns>A task that completes when the observer has finished its work.</returns>
    Task OnDispatchedAsync(ActionContext context, ActionResult result, CancellationToken ct);
}
