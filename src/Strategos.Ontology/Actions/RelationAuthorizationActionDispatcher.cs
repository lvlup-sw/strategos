using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Compatibility facade for dispatch-time action requirement enforcement.
/// Relation-bearing hard formulas are always enforced; when general
/// precondition enforcement is enabled, every hard formula is enforced.
/// </summary>
public sealed class RelationAuthorizationActionDispatcher : IActionDispatcher
{
    private readonly IActionDispatcher inner;
    private readonly ActionPreconditionAuthorizationActionDispatcher dispatcher;

    /// <summary>
    /// Initializes the compatibility facade with a relation evaluator.
    /// </summary>
    public RelationAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionRelationResolver relationResolver)
        : this(
            inner,
            graph,
            factResolver: null,
            relationResolver: relationResolver,
            customEvaluators: [],
            logger: NullLogger<RelationAuthorizationActionDispatcher>.Instance,
            reportSoftConstraints: false)
    {
    }

    /// <summary>
    /// Initializes the compatibility facade with a relation evaluator and logger.
    /// </summary>
    public RelationAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionRelationResolver relationResolver,
        ILogger<RelationAuthorizationActionDispatcher> logger)
        : this(
            inner,
            graph,
            factResolver: null,
            relationResolver: relationResolver,
            customEvaluators: [],
            logger: logger,
            reportSoftConstraints: false)
    {
    }

    internal RelationAuthorizationActionDispatcher(
        IActionDispatcher inner,
        OntologyGraph graph,
        IActionFactResolver? factResolver,
        IActionRelationResolver? relationResolver,
        IEnumerable<ICustomActionPredicateEvaluator> customEvaluators,
        ILogger<RelationAuthorizationActionDispatcher> logger,
        bool reportSoftConstraints = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(customEvaluators);
        ArgumentNullException.ThrowIfNull(logger);

        this.inner = inner;
        dispatcher = new ActionPreconditionAuthorizationActionDispatcher(
            inner,
            graph,
            factResolver,
            relationResolver,
            customEvaluators,
            logger,
            reportSoftConstraints);
    }

    internal IActionDispatcher Inner => inner;

    /// <inheritdoc />
    public Task<ActionResult> DispatchAsync(
        ActionContext context,
        object request,
        CancellationToken ct = default) =>
        dispatcher.DispatchAsync(context, request, ct);
}
