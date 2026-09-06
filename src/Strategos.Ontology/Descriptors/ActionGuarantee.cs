namespace Strategos.Ontology.Descriptors;

/// <summary>An explicit post-state fact promised by a successfully completed action.</summary>
public sealed record ActionGuarantee
{
    /// <summary>Initializes a typed action guarantee.</summary>
    public ActionGuarantee(ActionPredicate predicate, string? description = null)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Description = description;
    }

    /// <summary>Gets the typed post-state predicate.</summary>
    public ActionPredicate Predicate { get; }

    /// <summary>Gets optional presentation-only prose.</summary>
    public string? Description { get; }

    /// <summary>Gets the canonical display projection.</summary>
    public string Expression => Predicate.Expression;

    /// <summary>Gets whether the guarantee is opaque to build-time composition.</summary>
    public bool IsOpaque => Predicate.ContainsCustom;
}
