namespace Strategos.Ontology.Actions;

public interface IActionDispatchObserver
{
    Task OnDispatchedAsync(ActionContext context, ActionResult result, CancellationToken ct);
}
