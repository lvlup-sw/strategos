namespace Strategos.Ontology.Descriptors;

public sealed record ActionPrecondition
{
    /// <summary>
    /// Initializes a typed action precondition.
    /// </summary>
    public ActionPrecondition(
        ActionPredicate predicate,
        string description,
        ConstraintStrength strength = ConstraintStrength.Hard)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Strength = strength;
    }

    /// <summary>Gets the typed predicate.</summary>
    public ActionPredicate Predicate { get; }

    /// <summary>Gets presentation-only prose describing the constraint.</summary>
    public string Description { get; }

    /// <summary>Gets the canonical display projection. It is never reparsed.</summary>
    public string Expression => Predicate.Expression;

    /// <summary>
    /// Gets whether this precondition is opaque to build-time composition.
    /// Opaque preconditions remain runtime concerns and are not treated as
    /// members of the decidable predicate fragment.
    /// </summary>
    public bool IsOpaque => Predicate.ContainsCustom;

    /// <summary>Gets whether the constraint blocks dispatch.</summary>
    public ConstraintStrength Strength { get; }
}
