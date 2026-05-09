using Microsoft.Extensions.Logging;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Decorator that fans dispatch results out to a set of <see cref="IActionDispatchObserver"/>
/// instances after every dispatch. Each observer is invoked under its own try/catch:
/// observer exceptions are logged at warning severity but never fail the dispatch.
/// </summary>
public sealed class ObservableActionDispatcher : IActionDispatcher
{
    private readonly IActionDispatcher _inner;
    private readonly IReadOnlyList<IActionDispatchObserver> _observers;
    private readonly ILogger<ObservableActionDispatcher> _logger;

    internal IActionDispatcher Inner => _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableActionDispatcher"/> class.
    /// </summary>
    /// <param name="inner">Inner dispatcher to delegate dispatch to.</param>
    /// <param name="observers">Observers notified after each dispatch.</param>
    /// <param name="logger">Logger used to record observer failures.</param>
    public ObservableActionDispatcher(
        IActionDispatcher inner,
        IEnumerable<IActionDispatchObserver> observers,
        ILogger<ObservableActionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(observers);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _observers = observers as IReadOnlyList<IActionDispatchObserver> ?? observers.ToArray();
        _logger = logger;
    }

    public async Task<ActionResult> DispatchAsync(
        ActionContext context, object request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var result = await _inner.DispatchAsync(context, request, ct).ConfigureAwait(false);
        await NotifyObserversIsolatedAsync(context, result, ct).ConfigureAwait(false);
        return result;
    }

    private async Task NotifyObserversIsolatedAsync(
        ActionContext context, ActionResult result, CancellationToken ct)
    {
        if (_observers.Count == 0)
        {
            return;
        }

        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnDispatchedAsync(context, result, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Action dispatch observer {Observer} threw for {ObjectType}.{ActionName}",
                    observer.GetType().FullName,
                    context.ObjectType,
                    context.ActionName);
            }
        }
    }
}
