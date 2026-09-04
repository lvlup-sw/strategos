using System.Linq.Expressions;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Evaluates relation preconditions through provider-neutral object-set
/// expressions.
/// </summary>
public sealed class ObjectSetActionRelationResolver : IActionRelationResolver
{
    private readonly OntologyGraph graph;
    private readonly IObjectSetProvider provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectSetActionRelationResolver"/> class.
    /// </summary>
    public ObjectSetActionRelationResolver(OntologyGraph graph, IObjectSetProvider provider)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(provider);
        this.graph = graph;
        this.provider = provider;
    }

    /// <inheritdoc />
    public async Task<bool> HoldsAsync(
        ActionContext context,
        ActionPrecondition precondition,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(precondition);

        if (precondition.Kind != PreconditionKind.RelationHolds ||
            string.IsNullOrWhiteSpace(precondition.RelationName))
        {
            return false;
        }

        var current = ResolveDescriptor(context.Domain, context.ObjectType);
        if (current?.IdAccessor is null)
        {
            return false;
        }

        ObjectSetExpression expression = new RootExpression(current.ClrType ?? typeof(object), current.Name);
        expression = new FilterExpression(expression, IdPredicate(current.IdAccessor, context.ObjectId));

        foreach (var linkName in precondition.LinkPath)
        {
            if (string.IsNullOrWhiteSpace(linkName))
            {
                return false;
            }

            var link = current.Links.FirstOrDefault(candidate => candidate.Name == linkName);
            var target = link is null ? null : ResolveDescriptor(current.DomainName, link.TargetTypeName);
            if (target is null)
            {
                return false;
            }

            expression = new TraverseLinkExpression(
                expression,
                linkName,
                target.ClrType ?? typeof(object),
                target.Name);
            current = target;
        }

        var relation = current.Links.FirstOrDefault(candidate => candidate.Name == precondition.RelationName);
        var principalDescriptor = relation is null
            ? null
            : ResolveDescriptor(current.DomainName, relation.TargetTypeName);
        if (principalDescriptor is null ||
            principalDescriptor.Name != context.Principal.PrincipalType ||
            principalDescriptor.IdAccessor is null)
        {
            return false;
        }

        expression = new TraverseLinkExpression(
            expression,
            precondition.RelationName,
            principalDescriptor.ClrType ?? typeof(object),
            principalDescriptor.Name);
        expression = new FilterExpression(
            expression,
            IdPredicate(principalDescriptor.IdAccessor, context.Principal.PrincipalId));

        var result = await provider.ExecuteAsync<object>(expression, ct).ConfigureAwait(false);
        return result.Items.Count > 0;
    }

    private ObjectTypeDescriptor? ResolveDescriptor(string preferredDomain, string name)
    {
        var exact = graph.GetObjectType(preferredDomain, name);
        if (exact is not null)
        {
            return exact;
        }

        var matches = graph.ObjectTypes.Where(candidate => candidate.Name == name).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static LambdaExpression IdPredicate(Func<object, object?> idAccessor, string expectedId)
    {
        Func<object, bool> predicate = item => string.Equals(
            idAccessor(item)?.ToString() ?? string.Empty,
            expectedId,
            StringComparison.Ordinal);
        var parameter = Expression.Parameter(typeof(object), "item");
        var invoke = Expression.Invoke(Expression.Constant(predicate), parameter);
        return Expression.Lambda<Func<object, bool>>(invoke, parameter);
    }
}
