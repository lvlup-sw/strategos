namespace Strategos.Ontology.Actions;

/// <summary>
/// Dispatches actions to ontology objects.
/// </summary>
public interface IActionDispatcher
{
    /// <summary>
    /// Dispatches an action to the target identified by the context.
    /// </summary>
    Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default);

    /// <summary>
    /// Dispatches an action that must be declared <c>ReadOnly()</c>. Returns a
    /// failure <see cref="ActionResult"/> when the supplied <see cref="ActionContext.ActionDescriptor"/>
    /// is missing or its <c>IsReadOnly</c> flag is not <c>true</c>; otherwise delegates
    /// to <see cref="DispatchAsync"/>.
    /// </summary>
    Task<ActionResult> DispatchReadOnlyAsync(
        ActionContext context, object request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ActionDescriptor?.IsReadOnly is not true)
        {
            return Task.FromResult(new ActionResult(
                IsSuccess: false,
                Error: $"Action '{context.ActionName}' is not read-only."));
        }

        return DispatchAsync(context, request, ct);
    }
}
