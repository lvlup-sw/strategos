using System.Security.Cryptography;
using System.Text;

namespace Strategos.Ontology.Npgsql.Schema;

/// <summary>
/// Derives a PostgreSQL-safe identifier for a per-<c>(link, target-descriptor)</c>
/// junction table (DR-11, #128), guarding the 63-byte identifier limit
/// (<c>NAMEDATALEN - 1</c>) deterministically.
/// </summary>
/// <remarks>
/// PostgreSQL truncates any identifier longer than 63 BYTES SILENTLY, so two
/// distinct junction names that share a 63-byte prefix would collapse onto one
/// physical table — a collision, not a server error. <see cref="Derive(string)"/>
/// keeps short names verbatim and, for over-long names, truncates to
/// <c>(63 - suffix)</c> bytes and appends a deterministic hash suffix computed
/// from the FULL name. The suffix is derived from a fixed (process-independent)
/// hash so the same input always yields the same identifier — DDL and the
/// relate/traverse DML can never drift. The two-arg
/// <see cref="Derive(string, string)"/> overload makes the residual hash-collision
/// case MECHANICAL by throwing a typed
/// <see cref="OntologySchemaIdentifierException"/>. INV-2: pure, deterministic
/// SQL-identity logic — no live database.
/// </remarks>
internal static class JunctionIdentifier
{
    /// <summary>The PostgreSQL identifier byte cap (<c>NAMEDATALEN - 1</c>).</summary>
    private const int MaxIdentifierBytes = 63;

    /// <summary>
    /// The deterministic hash suffix length in hex chars (ASCII, so 1 byte each).
    /// Eight hex chars (32 bits) keeps the collision probability negligible while
    /// leaving the bulk of the 63-byte budget for the human-readable prefix.
    /// </summary>
    private const int HashHexLength = 8;

    /// <summary>The full suffix is <c>'_' + HashHexLength</c> ASCII bytes.</summary>
    private const int SuffixBytes = 1 + HashHexLength;

    /// <summary>
    /// Derives a 63-byte-safe PostgreSQL identifier for <paramref name="name"/>.
    /// A name already within the byte cap is returned VERBATIM; an over-long name
    /// is truncated to <c>(63 - suffix)</c> UTF-8 bytes (on a char boundary) and a
    /// deterministic <c>_{hash}</c> suffix computed from the FULL name is appended,
    /// so two distinct over-long names sharing a prefix derive distinct
    /// identifiers.
    /// </summary>
    /// <param name="name">The logical junction identifier (already snake_cased).</param>
    internal static string Derive(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Encoding.UTF8.GetByteCount(name) <= MaxIdentifierBytes)
        {
            return name;
        }

        var suffix = "_" + DeterministicHashHex(name);
        var prefix = TruncateToBytes(name, MaxIdentifierBytes - SuffixBytes);
        return prefix + suffix;
    }

    /// <summary>
    /// Derives identifiers for two DISTINCT junction names and throws
    /// <see cref="OntologySchemaIdentifierException"/> when they collide — the
    /// mechanical guard for the silent 63-byte-truncation collision. Two EQUAL
    /// inputs are not a collision (they are the same table) and pass through.
    /// </summary>
    /// <param name="first">The first junction name.</param>
    /// <param name="second">The second junction name.</param>
    /// <returns>The derived identifier for <paramref name="first"/>.</returns>
    internal static string Derive(string first, string second)
    {
        var derivedFirst = Derive(first);
        var derivedSecond = Derive(second);

        if (!string.Equals(first, second, StringComparison.Ordinal)
            && string.Equals(derivedFirst, derivedSecond, StringComparison.Ordinal))
        {
            throw new OntologySchemaIdentifierException(first, second, derivedFirst);
        }

        return derivedFirst;
    }

    // A process-INDEPENDENT short hash of the full name. string.GetHashCode is
    // randomized per process (and thus unusable for a stable SQL identifier), so
    // we take the leading bytes of a SHA-256 digest and render them as lowercase
    // hex. Determinism is the contract — the DDL and DML must derive the SAME
    // identifier across runs.
    private static string DeterministicHashHex(string name)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        var sb = new StringBuilder(HashHexLength);
        for (var i = 0; sb.Length < HashHexLength; i++)
        {
            sb.Append(digest[i].ToString("x2"));
        }

        return sb.ToString(0, HashHexLength);
    }

    // Truncates to at most maxBytes UTF-8 bytes WITHOUT splitting a Unicode
    // scalar (an identifier with a torn code unit would be invalid UTF-8). Walks
    // by Rune (scalar value), accumulating each scalar's UTF-8 byte cost, and
    // stops before the budget is exceeded. For the snake_cased ASCII identifiers
    // this layer handles, each Rune is a single byte; the Rune walk just keeps
    // any non-ASCII descriptor name byte-safe too.
    private static string TruncateToBytes(string value, int maxBytes)
    {
        var bytes = 0;
        var sb = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (bytes + runeBytes > maxBytes)
            {
                break;
            }

            bytes += runeBytes;
            sb.Append(rune.ToString());
        }

        return sb.ToString();
    }
}
