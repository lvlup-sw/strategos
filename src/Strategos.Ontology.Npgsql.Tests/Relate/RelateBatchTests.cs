using global::Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Tests.Integration;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Relate;

/// <summary>
/// DR-13 (R4, #130): the plain + attributed relate is collapsed from THREE
/// commands (two eager <c>SELECT EXISTS</c> probes + one <c>INSERT</c>, each on
/// its own connection round-trip and no shared snapshot) into a SINGLE
/// self-validating statement issued as one <see cref="NpgsqlBatch"/>. The
/// statement resolves both endpoints, performs the idempotent insert in a
/// data-modifying CTE (which Postgres executes exactly once), and RETURNS the two
/// endpoint-existence flags — read back to surface the typed
/// <see cref="RelationEndpointNotFoundException"/>. Because all CTE sub-statements
/// share ONE snapshot (a single statement), the pre-DR-13 TOCTOU window between
/// the probes and the insert is closed: a concurrent endpoint delete can no longer
/// turn an expected typed error into a silent no-op.
/// </summary>
/// <remarks>
/// <see cref="RelateAsync_IssuesSingleBatch"/> is a SQL-SHAPE test (no DB). The
/// concurrency test is DB-GATED via <see cref="SkipIfNoPostgresAttribute"/> — it
/// runs only in a provisioned Postgres lane.
/// </remarks>
public class RelateBatchTests
{
    [Test]
    public async Task RelateAsync_IssuesSingleBatch()
    {
        // The relate is ONE self-validating statement: resolve both endpoints,
        // insert in a data-modifying CTE, and return the existence flags. One
        // statement => one snapshot => no probe/insert TOCTOU gap, and one batch
        // round-trip instead of three.
        var sql = SqlGenerator.BuildValidatingRelateInsertSql(
            schema: "public",
            junctionTableName: "document_written_by",
            sourceTableName: "document",
            sourceKeyProperty: "Id",
            targetTableName: "author",
            targetKeyProperty: "Id");

        // Endpoint resolution is folded into the SAME statement (CTEs), not a
        // separate probe round-trip.
        await Assert.That(sql).Contains("data->>'Id' = @srcId");
        await Assert.That(sql).Contains("data->>'Id' = @tgtId");

        // The insert is a data-modifying CTE (executed exactly once, same snapshot
        // as the existence probe), still idempotent on the junction unique key.
        await Assert.That(sql).Contains("INSERT INTO \"public\".\"document_written_by\" (source_id, target_id)");
        await Assert.That(sql).Contains("ON CONFLICT (source_id, target_id) DO NOTHING");

        // The statement RETURNS the two endpoint-existence flags so the typed error
        // is sourced from the probe rows of the SAME atomic statement.
        await Assert.That(sql).Contains("AS src_exists");
        await Assert.That(sql).Contains("AS tgt_exists");

        // Parameterized — business ids never interpolated.
        await Assert.That(sql).Contains("@srcId");
        await Assert.That(sql).Contains("@tgtId");

        // INV-2: raw Npgsql/pgvector only.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    [Test]
    [SkipIfNoPostgres]
    public async Task RelateAsync_ConcurrentEndpointDelete_RaisesTypedError_NotSilentNoOp()
    {
        var conn = RequireConn();
        var graph = BuildGraph();
        await using var harness = await EdgeFailureModeNpgsqlHarness.CreateAsync(conn, graph);

        // Seed only the source. The target is ABSENT at relate time — the
        // single-statement relate's existence flags must surface the typed error
        // rather than the pre-DR-13 silent no-op (the insert's subquery resolving
        // zero rows). This is the static stand-in for the concurrent-delete race;
        // under the single-snapshot statement the two are indistinguishable.
        await harness.SeedNodeAsync("a");

        IObjectSetWriter writer = harness.Provider;

        await Assert.That(async () =>
                await writer.RelateAsync("FmNode", "a", "links_to", "FmNode", "ghost"))
            .Throws<RelationEndpointNotFoundException>();

        // And no dangling junction row was written.
        await Assert.That(await harness.JunctionRowCountAsync()).IsEqualTo(0);
    }

    private static string RequireConn()
    {
        var conn = Environment.GetEnvironmentVariable(SkipIfNoPostgresAttribute.ConnectionEnvVar);
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(
                $"{SkipIfNoPostgresAttribute.ConnectionEnvVar} is not set; the gated test should "
                + "have been skipped.");
        }

        return conn;
    }

    private static OntologyGraph BuildGraph()
    {
        var node = new ObjectTypeDescriptor
        {
            Name = "FmNode",
            DomainName = "fm",
            ClrType = null,
            SymbolKey = "scip . . fm/node#FmNode",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "fm-source",
            IdAccessor = instance => ((FmNodeRow)instance).Id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links =
            [
                new LinkDescriptor("links_to", string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = "scip . . fm/node#FmNode",
                    Source = DescriptorSource.Ingested,
                    AllowsSelfLoop = true,
                },
            ],
        };

        var objectTypes = new[] { node };
        return new OntologyGraph(
            domains: [new DomainDescriptor("fm") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    private sealed record FmNodeRow(string Id);
}
