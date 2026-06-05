using System;
using System.Threading.Tasks;
using global::Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Npgsql;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

// ---------------------------------------------------------------------------
// DR-8 (t14): cross-provider FAILURE-MODE MATRIX — the NPGSQL half.
//
// The companion in-memory half lives in
// Strategos.Ontology.Tests/Integration/EdgeFailureModeMatrixTests. This file
// asserts the SAME four modes against the PRODUCTION PgVectorObjectSetProvider,
// proving the Npgsql backend fails IDENTICALLY (same typed errors, no silent
// drops). Every mode here is DB-GATED via [SkipIfNoPostgres]: there is no local
// Postgres in the default lane, so these SKIP unless STRATEGOS_PG_TEST_CONN names
// a reachable database (they run + assert in a provisioned DB lane / Docker).
//
//   1. Relate_NonExistentEndpoint_ThrowsTypedError — the provider's eager
//      SELECT EXISTS (T9) surfaces a typed RelationEndpointNotFoundException and
//      writes NO junction row.
//   2. SelfLoop_WhenLinkDisallows_ThrowsTypedError_NeverSilentDrop — the provider
//      refuses (x, link, x) with the SAME typed SelfLoopNotAllowedException the
//      in-memory provider raises (parity), and writes NO row.
//   3. Traverse_ZeroRelations_ReturnsEmpty_NotAllTargets — instance-anchored
//      traversal from a node with no junction rows returns EMPTY, never all
//      target-type rows (#114).
//   4. Traverse_AmbiguousMultiRegistrationWithoutOverride_ThrowsAtRuntime — the
//      provider's graph-first hop resolver (ResolveHopTargetDescriptorName)
//      REFUSES an unresolvable hop target with a typed InvalidOperationException
//      rather than mis-routing. (Compile-time half: AONT211 / T5.)
//
// INV-2: raw Npgsql throughout — no Marten/Wolverine. INV-8: identity by
// descriptor name, never typeof.
// ---------------------------------------------------------------------------
public class EdgeFailureModeMatrixNpgsqlTests
{
    private const string NodeDescriptor = "FmNode";
    private const string LinkName = "links_to";
    private const string KeyField = "Id";

    // -----------------------------------------------------------------------
    // Mode 1 — non-existent endpoint (DB-GATED)
    // -----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task Relate_NonExistentEndpoint_ThrowsTypedError()
    {
        var conn = RequireConn();
        var graph = BuildGraph(allowsSelfLoop: false);
        await using var harness = await EdgeFailureModeNpgsqlHarness.CreateAsync(conn, graph);
        await harness.SeedNodeAsync("a"); // only the source exists

        IObjectSetWriter writer = harness.Provider;

        await Assert.That(async () =>
                await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "ghost"))
            .Throws<RelationEndpointNotFoundException>();

        // No dangling junction row survives the failed relate.
        await Assert.That(await harness.JunctionRowCountAsync()).IsEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Mode 2 — self-loop (DB-GATED). The PARITY mode: pre-t14 the Npgsql provider
    // had NO self-loop guard, so this is the test that drove the provider fix.
    // -----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task SelfLoop_WhenLinkDisallows_ThrowsTypedError_NeverSilentDrop()
    {
        var conn = RequireConn();
        var graph = BuildGraph(allowsSelfLoop: false);
        await using var harness = await EdgeFailureModeNpgsqlHarness.CreateAsync(conn, graph);
        await harness.SeedNodeAsync("a");

        IObjectSetWriter writer = harness.Provider;

        // Parity with the in-memory provider: a disallowed self-loop is REFUSED
        // with the SAME typed error, never silently written.
        await Assert.That(async () =>
                await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "a"))
            .Throws<SelfLoopNotAllowedException>();

        await Assert.That(await harness.JunctionRowCountAsync()).IsEqualTo(0);
    }

    [Test]
    [SkipIfNoPostgres]
    public async Task SelfLoop_WhenLinkAllows_CreatesRow()
    {
        var conn = RequireConn();
        var graph = BuildGraph(allowsSelfLoop: true);
        await using var harness = await EdgeFailureModeNpgsqlHarness.CreateAsync(conn, graph);
        await harness.SeedNodeAsync("a");

        IObjectSetWriter writer = harness.Provider;

        // When the link permits self-loops, (x, link, x) is a legitimate row.
        await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "a");

        await Assert.That(await harness.JunctionRowCountAsync()).IsEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // Mode 3 — zero relations (DB-GATED)
    // -----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task Traverse_ZeroRelations_ReturnsEmpty_NotAllTargets()
    {
        var conn = RequireConn();
        var graph = BuildGraph(allowsSelfLoop: true);
        await using var harness = await EdgeFailureModeNpgsqlHarness.CreateAsync(conn, graph);
        await harness.SeedNodeAsync("a");
        await harness.SeedNodeAsync("b");
        await harness.SeedNodeAsync("c");

        // Relate a -> b, then traverse from "c", which has NO junction rows.
        await harness.Provider.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "b");

        var count = await harness.TraverseTargetCountAsync("c");

        // Empty — never the whole FmNode table (the #114 type-level-fetch defect).
        await Assert.That(count).IsEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Mode 4 — ambiguous / unresolvable hop target without override (DB-GATED)
    // -----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task Traverse_AmbiguousMultiRegistrationWithoutOverride_ThrowsAtRuntime()
    {
        var conn = RequireConn();

        // A link whose target is NOT graph-resolvable: empty TargetTypeName, no
        // TargetSymbolKey, no override. The provider's graph-first hop resolver
        // (ResolveHopTargetDescriptorName, reached via ResolveTraversalHop) must
        // REFUSE with a typed InvalidOperationException rather than mis-route to a
        // first-CLR-match partition. This refusal is DB-free (the resolver throws
        // before any SQL), so the live connection is only the gate.
        await Assert.That(conn).IsNotNull();

        var graph = BuildUnresolvableTargetGraph();

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTraversalHop(
                graph,
                sourceDescriptorName: NodeDescriptor,
                linkName: LinkName,
                targetDescriptorOverride: null))
            .Throws<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // Graph builders — CLR-free (SymbolKey-only) so the hop/endpoint resolution
    // can only go through the graph (INV-8), mirroring the rationale corpus.
    // -----------------------------------------------------------------------

    private static OntologyGraph BuildGraph(bool allowsSelfLoop)
    {
        var node = new ObjectTypeDescriptor
        {
            Name = NodeDescriptor,
            DomainName = "fm",
            ClrType = null,
            SymbolKey = "scip . . fm/node#FmNode",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "fm-source",
            IdAccessor = instance => ((FmNodeRow)instance).Id,
            KeyProperty = new PropertyDescriptor(KeyField, typeof(string)),
            Links =
            [
                // A self-targeting link (target = FmNode via its SymbolKey) so the
                // self-loop policy is exercisable end-to-end.
                new LinkDescriptor(LinkName, string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = "scip . . fm/node#FmNode",
                    Source = DescriptorSource.Ingested,
                    AllowsSelfLoop = allowsSelfLoop,
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

    private static OntologyGraph BuildUnresolvableTargetGraph()
    {
        var node = new ObjectTypeDescriptor
        {
            Name = NodeDescriptor,
            DomainName = "fm",
            ClrType = null,
            SymbolKey = "scip . . fm/node#FmNode",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "fm-source",
            IdAccessor = instance => ((FmNodeRow)instance).Id,
            KeyProperty = new PropertyDescriptor(KeyField, typeof(string)),
            Links =
            [
                // Target names NOTHING the graph can resolve: empty TargetTypeName,
                // no TargetSymbolKey, no override -> the hop resolver must refuse.
                new LinkDescriptor(LinkName, string.Empty, LinkCardinality.OneToMany)
                {
                    Source = DescriptorSource.Ingested,
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

    private static string RequireConn()
    {
        var conn = Environment.GetEnvironmentVariable(SkipIfNoPostgresAttribute.ConnectionEnvVar);
        if (string.IsNullOrWhiteSpace(conn))
        {
            // The skip attribute guarantees this only runs with a connection
            // string; assert defensively so a misconfigured lane fails loud.
            throw new InvalidOperationException(
                $"{SkipIfNoPostgresAttribute.ConnectionEnvVar} is not set; the gated test should "
                + "have been skipped.");
        }

        return conn;
    }

    /// <summary>A minimal CLR-free node carrier; identity flows through Id only.</summary>
    internal sealed record FmNodeRow(string Id);
}

/// <summary>
/// DR-8 (t14) DB-GATED harness for the Npgsql failure-mode matrix. Provisions a
/// CLEAN node table + self-link junction table for the failure-mode graph via the
/// SAME <see cref="SqlGenerator"/> DDL the provider's schema path uses, and exposes
/// a PRODUCTION <see cref="PgVectorObjectSetProvider"/> so the matrix drives the
/// real relate/traverse SQL paths (where the failure modes live). INV-2: raw
/// Npgsql only.
/// </summary>
internal sealed class EdgeFailureModeNpgsqlHarness : IAsyncDisposable
{
    private const string NodeDescriptor = "FmNode";
    private const string LinkName = "links_to";

    private readonly NpgsqlDataSource _dataSource;
    private readonly OntologyGraph _graph;

    // A UNIQUE schema per harness instance. The failure modes run in parallel
    // against the SAME shared Postgres, so a shared schema would race the
    // catalog on the concurrent DROP/CREATE TABLE (pg_type duplicate-key). An
    // isolated schema makes each mode hermetic without serializing the suite.
    private readonly string _schema = $"fm_{Guid.NewGuid():N}";

    private EdgeFailureModeNpgsqlHarness(NpgsqlDataSource dataSource, OntologyGraph graph)
    {
        _dataSource = dataSource;
        _graph = graph;
        var embedding = Substitute.For<IEmbeddingProvider>();
        embedding.Dimensions.Returns(3);
        Provider = new PgVectorObjectSetProvider(
            dataSource,
            embedding,
            Options.Create(new PgVectorOptions { Schema = _schema }),
            NullLogger<PgVectorObjectSetProvider>.Instance,
            graph);
    }

    public PgVectorObjectSetProvider Provider { get; }

    public static async Task<EdgeFailureModeNpgsqlHarness> CreateAsync(
        string connectionString, OntologyGraph graph)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        var harness = new EdgeFailureModeNpgsqlHarness(dataSource, graph);
        await harness.CreateSchemaAsync().ConfigureAwait(false);
        return harness;
    }

    public async Task SeedNodeAsync(string id)
    {
        var table = TableName();
        var json = $"{{\"Id\":\"{id}\"}}";
        await ExecuteAsync(
            $"INSERT INTO \"{_schema}\".\"{table}\" (data) VALUES (@data::jsonb);",
            ("data", json)).ConfigureAwait(false);
    }

    public async Task<long> JunctionRowCountAsync()
    {
        var junction = SqlGenerator.JunctionTableName(TableName(), LinkName);
        await using var cmd = _dataSource.CreateCommand(
            $"SELECT count(*) FROM \"{_schema}\".\"{junction}\";");
        var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(scalar);
    }

    public async Task<long> TraverseTargetCountAsync(string sourceId)
    {
        // Instance-anchored traversal SQL (the SAME shape the provider's read path
        // emits): count the targets reachable from the anchor along the link.
        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            _graph, NodeDescriptor, LinkName, targetDescriptorOverride: null);
        var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            _schema, hop.SourceTable, hop.SourceKeyProperty, hop.JunctionTable, hop.TargetTable);

        await using var cmd = _dataSource.CreateCommand($"SELECT count(*) FROM ({sql}) sub;");
        cmd.Parameters.AddWithValue("srcId", sourceId);
        var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(scalar);
    }

    public async ValueTask DisposeAsync()
    {
        // Drop the per-instance schema (and everything in it) before tearing down
        // the data source, so a long-lived shared Postgres does not accumulate
        // orphaned per-run schemas.
        try
        {
            await ExecuteAsync($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE;").ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // Best-effort cleanup; a failed drop must not mask a test result.
        }

        await _dataSource.DisposeAsync().ConfigureAwait(false);
    }

    private async Task CreateSchemaAsync()
    {
        await ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\";").ConfigureAwait(false);

        var table = TableName();
        await ExecuteAsync(
            $"CREATE TABLE \"{_schema}\".\"{table}\" "
            + "(id uuid PRIMARY KEY DEFAULT gen_random_uuid(), data jsonb NOT NULL);")
            .ConfigureAwait(false);

        // Self-link junction (source table == target table).
        var ddl = SqlGenerator.BuildJunctionTableDdl(_schema, table, LinkName, table);
        await ExecuteAsync(ddl).ConfigureAwait(false);
    }

    private static string TableName() => TypeMapper.ToSnakeCase(NodeDescriptor);

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
