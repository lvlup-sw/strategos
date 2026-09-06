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
        ct.ThrowIfCancellationRequested();

        if (precondition.Predicate is not RelationHoldsPredicate relation)
        {
            throw new ArgumentException(
                "ObjectSetActionRelationResolver can only evaluate relation predicates.",
                nameof(precondition));
        }

        var current = ResolveDescriptor(context.Domain, context.ObjectType)
            ?? throw MissingMetadata(
                $"Action target descriptor '{context.Domain}.{context.ObjectType}' is not registered.");
        RequireIdAccessor(current, "action target");

        var sourceIdPredicate = current.IdPredicateFactory?.Invoke(context.ObjectId)
            ?? throw MissingMetadata(
                $"Action target descriptor '{current.DomainName}.{current.Name}' cannot build an identifier predicate.");

        ObjectSetExpression expression = new RootExpression(current.ClrType ?? typeof(object), current.Name);
        expression = new FilterExpression(expression, sourceIdPredicate);

        foreach (var linkName in relation.LinkPath)
        {
            if (string.IsNullOrWhiteSpace(linkName))
            {
                throw MissingMetadata(
                    $"Relation '{relation.RelationName}' contains an empty traversal path segment.");
            }

            var link = current.Links.FirstOrDefault(candidate => candidate.Name == linkName)
                ?? throw MissingMetadata(
                    $"Link '{current.DomainName}.{current.Name}.{linkName}' is not registered.");
            var target = ResolveLinkTarget(current, link);
            RequireIdAccessor(target, $"traversal target of '{current.Name}.{linkName}'");

            expression = new TraverseLinkExpression(
                expression,
                linkName,
                target.ClrType ?? typeof(object),
                target.Name);
            current = target;
        }

        var relationLink = current.Links.FirstOrDefault(candidate => candidate.Name == relation.RelationName)
            ?? throw MissingMetadata(
                $"Principal relation '{current.DomainName}.{current.Name}.{relation.RelationName}' is not registered.");
        var principalDescriptor = ResolveLinkTarget(current, relationLink);
        if (!string.Equals(
                principalDescriptor.Name,
                context.Principal.PrincipalType,
                StringComparison.Ordinal))
        {
            throw MissingMetadata(
                $"Principal relation '{current.DomainName}.{current.Name}.{relation.RelationName}' targets " +
                $"'{principalDescriptor.Name}', but the action principal is '{context.Principal.PrincipalType}'.");
        }

        RequireIdAccessor(principalDescriptor, "principal relation target");

        expression = new TraverseLinkExpression(
            expression,
            relation.RelationName,
            principalDescriptor.ClrType ?? typeof(object),
            principalDescriptor.Name);
        var principalIdPredicate = principalDescriptor.IdPredicateFactory?.Invoke(context.Principal.PrincipalId)
            ?? throw MissingMetadata(
                $"Principal descriptor '{principalDescriptor.DomainName}.{principalDescriptor.Name}' " +
                "cannot build an identifier predicate.");

        expression = new FilterExpression(expression, principalIdPredicate);

        var result = await provider.ExecuteAsync<object>(expression, ct).ConfigureAwait(false);
        return result.Items.Count > 0;
    }

    private ObjectTypeDescriptor? ResolveDescriptor(string preferredDomain, string name)
    {
        return graph.GetObjectType(preferredDomain, name);
    }

    private ObjectTypeDescriptor ResolveLinkTarget(
        ObjectTypeDescriptor source,
        LinkDescriptor link)
    {
        if (!string.IsNullOrWhiteSpace(link.TargetTypeName))
        {
            return ResolveDescriptor(source.DomainName, link.TargetTypeName)
                ?? throw MissingMetadata(
                    $"Link '{source.DomainName}.{source.Name}.{link.Name}' targets unregistered descriptor " +
                    $"'{link.TargetTypeName}'.");
        }

        if (!string.IsNullOrWhiteSpace(link.TargetSymbolKey))
        {
            var matches = graph.ObjectTypes
                .Where(candidate =>
                    string.Equals(candidate.SymbolKey, link.TargetSymbolKey, StringComparison.Ordinal))
                .ToArray();
            return matches.Length switch
            {
                1 => matches[0],
                > 1 => throw MissingMetadata(
                    $"Link '{source.DomainName}.{source.Name}.{link.Name}' has ambiguous SymbolKey target " +
                    $"'{link.TargetSymbolKey}'."),
                _ => throw MissingMetadata(
                    $"Link '{source.DomainName}.{source.Name}.{link.Name}' targets unregistered SymbolKey " +
                    $"'{link.TargetSymbolKey}'."),
            };
        }

        throw MissingMetadata(
            $"Link '{source.DomainName}.{source.Name}.{link.Name}' has no authoritative target descriptor identity.");
    }

    private static void RequireIdAccessor(ObjectTypeDescriptor descriptor, string role)
    {
        if (descriptor.IdAccessor is null)
        {
            throw MissingMetadata(
                $"The {role} descriptor '{descriptor.DomainName}.{descriptor.Name}' has no identifier accessor.");
        }
    }

    private static InvalidOperationException MissingMetadata(string message) =>
        new($"Relation evaluation is indeterminate because authoritative ontology metadata is unavailable. {message}");
}
