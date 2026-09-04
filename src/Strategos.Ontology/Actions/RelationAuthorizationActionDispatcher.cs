using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Enforces hard <see cref="PreconditionKind.RelationHolds"/> preconditions
/// before an action reaches its handler.
/// </summary>
public sealed class RelationAuthorizationActionDispatcher : IActionDispatcher
{
    private readonly IActionDispatcher inner;
    private readonly OntologyGraph graph;
    private readonly IActionRelationResolver relationResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationAuthorizationActionDispatcher"/> class.
    /// </summary>
    public RelationAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionRelationResolver relationResolver)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(relationResolver);
        this.inner = inner;
        this.graph = graph;
        this.relationResolver = relationResolver;
    }

    internal IActionDispatcher Inner => inner;

    /// <inheritdoc />
    public async Task<ActionResult> DispatchAsync(
        ActionContext context,
        object request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = context.ActionDescriptor ?? ResolveAction(context);
        if (descriptor is null)
        {
            return await inner.DispatchAsync(context, request, ct).ConfigureAwait(false);
        }

        var authorizedContext = ReferenceEquals(descriptor, context.ActionDescriptor)
            ? context
            : context with { ActionDescriptor = descriptor };

        foreach (var precondition in descriptor.Preconditions.Where(candidate =>
                     candidate.Kind == PreconditionKind.RelationHolds &&
                     candidate.Strength == ConstraintStrength.Hard))
        {
            bool holds;
            try
            {
                holds = await relationResolver.HoldsAsync(authorizedContext, precondition, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                holds = false;
            }

            if (!holds)
            {
                return new ActionResult(
                    false,
                    Error: $"Principal '{context.Principal.PrincipalType}/{context.Principal.PrincipalId}' " +
                        $"does not satisfy relation '{precondition.RelationName}' for action '{descriptor.Name}'.");
            }
        }

        return await inner.DispatchAsync(authorizedContext, request, ct).ConfigureAwait(false);
    }

    private ActionDescriptor? ResolveAction(ActionContext context)
    {
        var objectType = graph.GetObjectType(context.Domain, context.ObjectType);
        if (objectType is null)
        {
            var matches = graph.ObjectTypes
                .Where(candidate => candidate.Name == context.ObjectType)
                .Take(2)
                .ToList();
            objectType = matches.Count == 1 ? matches[0] : null;
        }

        return objectType?.Actions.FirstOrDefault(candidate => candidate.Name == context.ActionName);
    }
}
