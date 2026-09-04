using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>Enforces named authority requirements at the dispatch boundary.</summary>
public sealed class AuthorityAuthorizationActionDispatcher : IActionDispatcher
{
    private readonly IActionDispatcher inner;
    private readonly OntologyGraph graph;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorityAuthorizationActionDispatcher"/> class.
    /// </summary>
    /// <param name="inner">The dispatcher invoked after authority checks pass.</param>
    /// <param name="graph">The authoritative ontology graph.</param>
    public AuthorityAuthorizationActionDispatcher(IActionDispatcher inner, OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(graph);
        this.inner = inner;
        this.graph = graph;
    }

    internal IActionDispatcher Inner => inner;

    /// <inheritdoc />
    public Task<ActionResult> DispatchAsync(
        ActionContext context,
        object request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = ResolveAction(context);
        if (descriptor is null)
        {
            return Task.FromResult(DeniedUnknownAction(context));
        }

        if (context.ActionDescriptor is not null && !ReferenceEquals(context.ActionDescriptor, descriptor))
        {
            return Task.FromResult(DeniedUnknownAction(context));
        }

        var authorizedContext = ReferenceEquals(descriptor, context.ActionDescriptor)
            ? context
            : context with { ActionDescriptor = descriptor };
        if (descriptor.RequiredAuthority is null)
        {
            return inner.DispatchAsync(authorizedContext, request, ct);
        }

        var lattice = graph.GetAuthorityLattice(context.Domain);
        var authorized = context.Principal.GrantedAuthorities.Any(granted =>
            IsSufficientGrant(lattice, granted, descriptor.RequiredAuthority));
        if (!authorized)
        {
            return Task.FromResult(new ActionResult(
                false,
                Error: $"Principal '{context.Principal.PrincipalType}/{context.Principal.PrincipalId}' "
                    + $"does not hold authority satisfying '{descriptor.RequiredAuthority}' "
                    + $"for action '{descriptor.Name}'."));
        }

        return inner.DispatchAsync(authorizedContext, request, ct);
    }

    private static bool IsSufficientGrant(
        AuthorityLattice lattice,
        string granted,
        string required)
    {
        try
        {
            return lattice.Satisfies(granted, required);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private ActionDescriptor? ResolveAction(ActionContext context)
    {
        var objectType = graph.GetObjectType(context.Domain, context.ObjectType);
        return objectType?.Actions.FirstOrDefault(action => action.Name == context.ActionName);
    }

    private static ActionResult DeniedUnknownAction(ActionContext context) => new(
        false,
        Error: $"Action '{context.Domain}/{context.ObjectType}/{context.ActionName}' is not present in the authoritative ontology graph.");
}
