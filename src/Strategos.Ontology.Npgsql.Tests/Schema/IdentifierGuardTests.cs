using System.Text;
using Strategos.Ontology.Npgsql.Schema;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// DR-11 (junction posture, #128): unit tests for the deterministic
/// identifier-length guard. PostgreSQL silently TRUNCATES any identifier at 63
/// BYTES (<c>NAMEDATALEN - 1</c>), so two distinct per-<c>(link, target-descriptor)</c>
/// junction names whose first 63 bytes coincide would collapse onto the SAME
/// physical table — a SILENT COLLISION, not an error. <see cref="JunctionIdentifier"/>
/// makes that failure mode mechanical: it truncates over-long names to
/// <c>(63 - suffix)</c> bytes and appends a deterministic hash suffix derived from
/// the FULL name, and it throws a typed <see cref="OntologySchemaIdentifierException"/>
/// when two distinct inputs would still derive the same identifier.
/// </summary>
/// <remarks>
/// These assert the DERIVED identifier strings only — no live database (INV-2:
/// raw Npgsql posture, pure/deterministic SQL-identity logic). The 63-byte limit
/// is measured in UTF-8 BYTES, not chars, mirroring Postgres' own
/// <c>NAMEDATALEN</c> accounting.
/// </remarks>
public class IdentifierGuardTests
{
    private const int PostgresIdentifierByteLimit = 63;

    [Test]
    public async Task IdentifierGuard_NameExceeds63Bytes_TruncatesWithDeterministicHashSuffix()
    {
        // A junction name comfortably over the 63-byte limit.
        var longName = new string('a', 80);

        var derived = JunctionIdentifier.Derive(longName);

        // 1. The derived identifier fits Postgres' 63-byte cap, so it can never be
        //    silently truncated (and thus never collide) by the server.
        await Assert.That(Encoding.UTF8.GetByteCount(derived))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);

        // 2. It is shorter than the input — the over-long name WAS truncated, not
        //    passed through unchanged.
        await Assert.That(derived.Length).IsLessThan(longName.Length);

        // 3. The suffix is a deterministic hash of the FULL name: deriving the same
        //    input twice yields the same identifier (so DDL and DML never drift).
        var again = JunctionIdentifier.Derive(longName);
        await Assert.That(derived).IsEqualTo(again);

        // 4. A name already within the limit passes through verbatim — short,
        //    honest identifiers are not gratuitously hashed.
        var shortName = "document_written_by";
        await Assert.That(JunctionIdentifier.Derive(shortName)).IsEqualTo(shortName);
    }

    [Test]
    public async Task IdentifierGuard_TwoLongDistinctDescriptors_DoNotCollide()
    {
        // Two DISTINCT over-long junction names whose first 63 bytes are IDENTICAL
        // — exactly the case Postgres' silent truncation would collapse into one
        // table. The deterministic hash suffix is derived from the FULL name, so
        // the two derived identifiers must differ.
        var sharedPrefix = new string('x', 70);
        var first = sharedPrefix + "_alpha";
        var second = sharedPrefix + "_beta";

        var derivedFirst = JunctionIdentifier.Derive(first);
        var derivedSecond = JunctionIdentifier.Derive(second);

        // Both fit the byte cap.
        await Assert.That(Encoding.UTF8.GetByteCount(derivedFirst))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);
        await Assert.That(Encoding.UTF8.GetByteCount(derivedSecond))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);

        // The two distinct inputs derive DISTINCT identifiers — no silent collision.
        await Assert.That(derivedFirst).IsNotEqualTo(derivedSecond);
    }

    [Test]
    public async Task IdentifierGuard_TwoInputsDerivingSameIdentifier_ThrowsTyped()
    {
        // F7: force the residual collision MECHANICALLY (no brute-force search). The
        // two-arg Derive throws OntologySchemaIdentifierException when two DISTINCT
        // inputs derive the SAME identifier. A deterministic such pair:
        //   N    = an over-long name (> 63 bytes), and
        //   D    = Derive(N), which is exactly 63 ASCII bytes (54-byte truncated
        //          prefix + '_' + 8 hex chars).
        // Derive(N) == D by construction, and because D already fits the 63-byte cap
        // Derive(D) returns D verbatim — so Derive(N) == Derive(D) == D while N != D.
        // That is precisely the (first != second) AND (derivedFirst == derivedSecond)
        // condition the guard rejects.
        var longName = new string('a', 80);
        var derivedLong = JunctionIdentifier.Derive(longName);

        // Sanity: the derived identifier is a DISTINCT input that sits within the cap
        // (so it passes through verbatim) yet collides with longName's derivation.
        await Assert.That(derivedLong).IsNotEqualTo(longName);
        await Assert.That(Encoding.UTF8.GetByteCount(derivedLong))
            .IsLessThanOrEqualTo(PostgresIdentifierByteLimit);
        await Assert.That(JunctionIdentifier.Derive(derivedLong)).IsEqualTo(derivedLong);

        // The two distinct inputs derive the same identifier — the guard makes it a
        // loud, typed, catchable error rather than a silent table merge.
        await Assert.That(() => JunctionIdentifier.Derive(longName, derivedLong))
            .Throws<OntologySchemaIdentifierException>();
    }
}
