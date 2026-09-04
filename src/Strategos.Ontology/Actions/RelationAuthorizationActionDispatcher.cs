using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<RelationAuthorizationActionDispatcher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationAuthorizationActionDispatcher"/> class.
    /// </summary>
    public RelationAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionRelationResolver relationResolver)
        : this(inner, graph, relationResolver, NullLogger<RelationAuthorizationActionDispatcher>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationAuthorizationActionDispatcher"/> class.
    /// </summary>
    /// <param name="inner">The dispatcher invoked after relation checks pass.</param>
    /// <param name="graph">The authoritative ontology graph.</param>
    /// <param name="relationResolver">The provider-neutral relation evaluator.</param>
    /// <param name="logger">The logger for fail-closed resolver failures.</param>
    public RelationAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionRelationResolver relationResolver,
        ILogger<RelationAuthorizationActionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(relationResolver);
        ArgumentNullException.ThrowIfNull(logger);
        this.inner = inner;
        this.graph = graph;
        this.relationResolver = relationResolver;
        this.logger = logger;
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

        var descriptor = ResolveAction(context);
        if (descriptor is null)
        {
            return DeniedUnknownAction(context);
        }

        if (context.ActionDescriptor is not null && !ReferenceEquals(context.ActionDescriptor, descriptor))
        {
            return DeniedUnknownAction(context);
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
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Relation resolver failed closed for action {Domain}/{ObjectType}/{ActionName} and relation {RelationName}.",
                    context.Domain,
                    context.ObjectType,
                    context.ActionName,
                    precondition.RelationName);
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
        return objectType?.Actions.FirstOrDefault(candidate => candidate.Name == context.ActionName);
    }

    private static ActionResult DeniedUnknownAction(ActionContext context) => new(
        false,
        Error: $"Action '{context.Domain}/{context.ObjectType}/{context.ActionName}' is not present in the authoritative ontology graph.");
}
