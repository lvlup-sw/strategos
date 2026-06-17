using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// DR-11 (junction posture, #128): unit tests for the per-<c>(link,
/// target-descriptor)</c> junction-table DDL (Posture 2). Each junction table is
/// scoped to ONE resolved target descriptor, so its endpoint FK is HONEST — it
/// references exactly the descriptor's object table, never a union of partitions.
///
/// The resolved target descriptor NAME is the DR-10 graph resolution
/// (override → <c>TargetTypeName</c> → <c>TargetSymbolKey</c>), never
/// <c>typeof(TLinked)</c> — so a <c>SymbolKey</c>-only (ingested, <c>ClrType</c>
/// == null) descriptor still derives a junction table.
/// </summary>
/// <remarks>
/// These assert generated-DDL strings only — no live database (INV-2: raw Npgsql
/// + pgvector DDL, no Marten/Wolverine). Mirrors
/// <see cref="PgVectorEdgeSchemaTests"/>.
///
/// A MONOMORPHIC link (the common case) resolves to a single descriptor and
/// emits ONE junction table — same count as the pre-DR-11 lowering. A link
/// resolving (via interface narrow / multi-registration) to TWO descriptors fans
/// out into TWO junction tables, each with its own single honest FK.
/// </remarks>
public class JunctionDdlTests
{
    [Test]
    public async Task BuildJunctionTableDdl_MonomorphicLink_EmitsSingleTableNamedByResolvedDescriptor()
    {
        // A monomorphic link "WrittenBy" from Document resolves to exactly ONE
        // target descriptor, "Author" (table "author"). One junction table, named
        // by (source, link) for back-compat with the pre-DR-11 lowering, FK to the
        // resolved descriptor's object table.
        var resolved = new JunctionTableDescriptor
        {
            SourceTable = "document",
            LinkName = "WrittenBy",
            TargetDescriptorName = "Author",
            TargetTable = "author",
            IsPolymorphic = false,
        };

        var ddls = SqlGenerator.BuildJunctionTableDdlForResolvedTargets("public", [resolved]);

        // Exactly ONE table — the monomorphic count is unchanged.
        await Assert.That(ddls.Count).IsEqualTo(1);

        var ddl = ddls[0];
        // Named by (source, link) — lockstep with the relate/traverse DML name.
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"document_written_by\"");

        // A single HONEST FK to the resolved descriptor's object table.
        await Assert.That(ddl).Contains("target_id uuid NOT NULL");
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"document\" (id)");
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"author\" (id)");

        // INV-2: raw DDL only.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task BuildJunctionTableDdl_LinkResolvingToTwoDescriptors_EmitsTwoTablesEachWithSingleFk()
    {
        // A polymorphic link "Owns" from Account resolves (interface narrow /
        // multi-registration) to TWO target descriptors: "Stock" and "Bond". Each
        // descriptor gets its OWN junction table with a single HONEST FK — never
        // one table with a polymorphic/union endpoint.
        var toStock = new JunctionTableDescriptor
        {
            SourceTable = "account",
            LinkName = "Owns",
            TargetDescriptorName = "Stock",
            TargetTable = "stock",
            IsPolymorphic = true,
        };
        var toBond = new JunctionTableDescriptor
        {
            SourceTable = "account",
            LinkName = "Owns",
            TargetDescriptorName = "Bond",
            TargetTable = "bond",
            IsPolymorphic = true,
        };

        var ddls = SqlGenerator.BuildJunctionTableDdlForResolvedTargets("public", [toStock, toBond]);

        // TWO tables — one per resolved descriptor (fan-out).
        await Assert.That(ddls.Count).IsEqualTo(2);

        var joined = string.Join("\n;;\n", ddls);

        // Each table is disambiguated by its resolved descriptor name, so the two
        // are DISTINCT physical tables (no collision onto one).
        await Assert.That(joined).Contains("account_owns_stock");
        await Assert.That(joined).Contains("account_owns_bond");

        // Each carries a single honest FK to its OWN descriptor's object table.
        await Assert.That(joined).Contains("REFERENCES \"public\".\"stock\" (id)");
        await Assert.That(joined).Contains("REFERENCES \"public\".\"bond\" (id)");

        // The two derived junction names are distinct.
        var stockName = SqlGenerator.JunctionTableNameFor(toStock);
        var bondName = SqlGenerator.JunctionTableNameFor(toBond);
        await Assert.That(stockName).IsNotEqualTo(bondName);
    }

    [Test]
    public async Task BuildJunctionTableDdl_SymbolKeyOnlyTarget_DerivesNameWithoutTypeof()
    {
        // The resolved target descriptor is a SymbolKey-only (ingested,
        // ClrType == null) descriptor — DR-10 resolved its NAME from the graph's
        // SymbolKey reverse index, never typeof. The junction DDL derives its
        // identity from that resolved descriptor NAME, so a CLR-less target still
        // produces a junction table.
        var resolved = new JunctionTableDescriptor
        {
            SourceTable = "origin",
            LinkName = "toEdge",
            TargetDescriptorName = "SymEdge",
            TargetTable = "sym_edge",
            IsPolymorphic = false,
        };

        var ddls = SqlGenerator.BuildJunctionTableDdlForResolvedTargets("public", [resolved]);

        await Assert.That(ddls.Count).IsEqualTo(1);
        var ddl = ddls[0];

        // The junction references the CLR-less descriptor's object table — derived
        // from the resolved descriptor NAME, not any CLR type name.
        await Assert.That(ddl).Contains("REFERENCES \"public\".\"sym_edge\" (id)");
        await Assert.That(ddl).Contains("CREATE TABLE IF NOT EXISTS \"public\".\"origin_to_edge\"");
    }
}
