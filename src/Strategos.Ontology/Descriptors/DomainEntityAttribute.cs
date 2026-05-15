namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Marks a hand-authored ontology type as opted-in to DR-7 strict mode.
/// When <see cref="Strict"/> is <c>true</c>, the graph-freeze emits
/// AONT203 (warning) for any property present on the ingested side of
/// the descriptor but missing from the hand-authored
/// <c>DomainOntology.Define()</c> declaration. The default
/// (<c>Strict = false</c>) preserves the loose contract — ingested-only
/// properties are absorbed without complaint.
/// </summary>
/// <remarks>
/// DR-7 (Task 25). Applied to the CLR backing type of an
/// <see cref="ObjectTypeDescriptor"/>; the graph-freeze reads the
/// attribute from <see cref="ObjectTypeDescriptor.ClrType"/> at compose
/// time. Hand-authored descriptors whose CLR type is decorated with
/// <c>[DomainEntity(Strict = true)]</c> are flagged here; this is the
/// only opt-in for AONT203.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DomainEntityAttribute : Attribute
{
    /// <summary>
    /// When <c>true</c>, ingested-only properties absent from hand
    /// <c>Define()</c> emit AONT203 at graph-freeze time. Default: <c>false</c>.
    /// </summary>
    public bool Strict { get; init; }
}
