namespace Strategos.Ontology.Npgsql.Schema;

/// <summary>
/// Thrown when two DISTINCT schema inputs (e.g. two per-<c>(link, target-descriptor)</c>
/// junction names) would derive the SAME PostgreSQL identifier through
/// <see cref="JunctionIdentifier.Derive(string)"/> — a collision that, left
/// unguarded, would silently route two logical tables to one physical table.
/// </summary>
/// <remarks>
/// DR-11 (junction posture, #128). PostgreSQL truncates any identifier at 63
/// bytes (<c>NAMEDATALEN - 1</c>) WITHOUT error, so a collision is the real
/// failure mode rather than a server-side rejection.
/// <see cref="JunctionIdentifier"/> derives a deterministic hash suffix from the
/// FULL name so distinct inputs normally derive distinct identifiers; this typed
/// exception makes the residual (hash-collision) case MECHANICAL — a loud,
/// catchable error at schema-derivation time — instead of a silent table merge.
/// A programmer error in the descriptor graph, surfaced eagerly.
/// </remarks>
public sealed class OntologySchemaIdentifierException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OntologySchemaIdentifierException"/>
    /// class for two distinct inputs that derive the same identifier.
    /// </summary>
    /// <param name="firstName">The first colliding input name.</param>
    /// <param name="secondName">The second colliding input name.</param>
    /// <param name="derivedIdentifier">The identifier both inputs derive to.</param>
    public OntologySchemaIdentifierException(string firstName, string secondName, string derivedIdentifier)
        : base(
            $"Schema identifiers '{firstName}' and '{secondName}' both derive the PostgreSQL "
            + $"identifier '{derivedIdentifier}'. PostgreSQL truncates identifiers at 63 bytes "
            + $"silently, so these two distinct junction targets would collapse onto one physical "
            + $"table. Rename one descriptor/link so their derived identifiers differ.")
    {
        FirstName = firstName;
        SecondName = secondName;
        DerivedIdentifier = derivedIdentifier;
    }

    /// <summary>The first colliding input name.</summary>
    public string FirstName { get; }

    /// <summary>The second colliding input name.</summary>
    public string SecondName { get; }

    /// <summary>The PostgreSQL identifier both inputs derive to.</summary>
    public string DerivedIdentifier { get; }
}
