namespace Strategos.Ontology.Descriptors;

public sealed record ActionPostcondition
{
    public required PostconditionKind Kind { get; init; }

    public string? PropertyName { get; init; }

    public string? LinkName { get; init; }

    public string? EventTypeName { get; init; }

    /// <summary>
    /// Simple object type name within the action's owning domain that this
    /// postcondition's link or creation targets. Null when the postcondition
    /// does not name a specific target type. Set by
    /// <c>ActionBuilder&lt;T&gt;.CreatesLinked&lt;TTarget&gt;</c> from
    /// <c>typeof(TTarget).Name</c>; resolution to a descriptor must be
    /// domain-qualified at the call site.
    /// </summary>
    public string? TargetTypeName { get; init; }
}
