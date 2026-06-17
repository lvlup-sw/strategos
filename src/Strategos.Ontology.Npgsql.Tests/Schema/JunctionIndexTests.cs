using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// DR-13 (R1, #130): unit tests for the REVERSE junction index. The junction
/// table's <c>UNIQUE (source_id, target_id)</c> constraint already backs FORWARD
/// traversal (source → target: <c>j.source_id = s.id</c>), but a composite index
/// is prefix-ordered, so it does NOT serve a lookup keyed on <c>target_id</c>
/// alone. Reverse traversal (target → source) and any FK-cascade probe on the
/// target endpoint therefore fall back to a sequential scan. R1 adds an explicit
/// <c>(target_id, source_id)</c> index so both traversal directions are
/// index-backed.
/// </summary>
/// <remarks>
/// These assert generated-DDL strings only — no live database (INV-2: raw Npgsql
/// + pgvector DDL, no Marten/Wolverine). Mirrors <see cref="JunctionDdlTests"/>
/// and <see cref="PgVectorEdgeSchemaTests"/>.
/// </remarks>
public class JunctionIndexTests
{
    [Test]
    public async Task JunctionDdl_EmitsReverseIndex_TargetSourceComposite()
    {
        // The original DR-7 junction DDL must carry a reverse composite index so
        // a target → source traversal is index-backed, not a seq scan.
        var ddl = SqlGenerator.BuildJunctionTableDdl(
            schema: "public",
            sourceTableName: "document",
            linkName: "WrittenBy",
            targetTableName: "author");

        // The reverse index is on (target_id, source_id) — the mirror of the
        // forward UNIQUE (source_id, target_id) constraint.
        await Assert.That(ddl).Contains("CREATE INDEX IF NOT EXISTS");
        await Assert.That(ddl).Contains("ON \"public\".\"document_written_by\" (target_id, source_id)");

        // The forward composite is still present (the UNIQUE constraint).
        await Assert.That(ddl).Contains("UNIQUE (source_id, target_id)");

        // INV-2: raw DDL only.
        await Assert.That(ddl).DoesNotContain("Marten");
        await Assert.That(ddl).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task ResolvedJunctionDdl_EmitsReverseIndex_PerTable()
    {
        // The DR-11 per-(link, target-descriptor) junction DDL must also carry a
        // reverse index per emitted table — the polymorphic fan-out tables get the
        // same reverse-traversal index the monomorphic table does.
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

        await Assert.That(ddls.Count).IsEqualTo(2);

        var stockName = SqlGenerator.JunctionTableNameFor(toStock);
        var bondName = SqlGenerator.JunctionTableNameFor(toBond);

        // Each table carries its OWN reverse index, keyed on (target_id, source_id),
        // scoped to its physical table name.
        await Assert.That(ddls[0]).Contains($"ON \"public\".\"{stockName}\" (target_id, source_id)");
        await Assert.That(ddls[1]).Contains($"ON \"public\".\"{bondName}\" (target_id, source_id)");
    }
}
