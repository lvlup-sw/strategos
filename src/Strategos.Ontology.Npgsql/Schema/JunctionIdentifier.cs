namespace Strategos.Ontology.Npgsql.Schema;

/// <summary>
/// Derives a PostgreSQL-safe identifier for a per-<c>(link, target-descriptor)</c>
/// junction table (DR-11, #128), guarding the 63-byte identifier limit
/// (<c>NAMEDATALEN - 1</c>) deterministically.
/// </summary>
/// <remarks>
/// The junction-named facade over the general <see cref="PgIdentifier"/> guard
/// (#130 R3a): every generated identifier — vertex, junction and
/// association-object table, <c>{role}_id</c> endpoint column, index — derives
/// through <see cref="PgIdentifier.Derive(string)"/>, and this type delegates to
/// it unchanged so the junction call sites and their tests keep their name.
/// </remarks>
internal static class JunctionIdentifier
{
    /// <inheritdoc cref="PgIdentifier.Derive(string)"/>
    internal static string Derive(string name) => PgIdentifier.Derive(name);

    /// <inheritdoc cref="PgIdentifier.Derive(string, string)"/>
    internal static string Derive(string first, string second) => PgIdentifier.Derive(first, second);
}
