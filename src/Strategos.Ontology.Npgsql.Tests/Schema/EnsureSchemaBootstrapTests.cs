using global::Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Tests.Integration;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// G-7 / CL-7 (#132): the BATCH/SAFE schema-bootstrap API that removes the
/// multi-registration startup footgun. Before this, a host wanting to stand up a
/// multi-registered carrier type's tables had to hand-roll a loop over
/// <see cref="Query.IOntologyQuery.GetObjectTypeNames{T}"/> and call the
/// single-descriptor <c>EnsureSchema</c> once per name, because
/// <see cref="PgVectorObjectSetProvider.EnsureSchemaAsync{T}(string?, CancellationToken)"/>
/// with a null name THROWS for a multi-registered type. The two new entry points
/// — <see cref="IObjectSetProvider.EnsureSchemaAsync{T}(CancellationToken)"/> (ensure
/// ALL descriptors for T) and
/// <see cref="IObjectSetProvider.EnsureAllSchemasAsync(CancellationToken)"/> (ensure
/// every object descriptor in the graph) — bootstrap the layout in one call.
/// </summary>
/// <remarks>
/// These are DB-GATED via <see cref="SkipIfNoPostgresAttribute"/>: there is no
/// Postgres in the default dev/CI lane, so they SKIP (not fail) unless
/// STRATEGOS_PG_TEST_CONN names a reachable database. INV-2: raw Npgsql/pgvector
/// only (no Marten/Wolverine). INV-8: descriptors are resolved by name, never
/// <c>typeof</c>.
/// </remarks>
public class EnsureSchemaBootstrapTests
{
    // ----- Task 7.1 -------------------------------------------------------
    // SAFE EnsureSchemaAsync<T>(ct): a multi-registered type ensures BOTH
    // descriptor tables, instead of throwing on the ambiguous default overload.
    // ----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task EnsureSchemaAsync_MultiRegisteredType_EnsuresAllDescriptorTables()
    {
        var conn = RequireConn();

        // Doc is registered under TWO descriptors: "a" and "b". The pre-#132
        // EnsureSchemaAsync<Doc>(null) throws "has multiple registrations"; the
        // new safe overload ensures both.
        var graph = new OntologyGraphBuilder()
            .AddDomain<DocMultiRegistrationOntology>()
            .Build();

        await using var harness = await BootstrapHarness.CreateAsync(conn, graph);

        await harness.Provider.EnsureSchemaAsync<Doc>(CancellationToken.None);

        await Assert.That(await harness.TableExistsAsync("a")).IsTrue();
        await Assert.That(await harness.TableExistsAsync("b")).IsTrue();
    }

    // ----- Task 7.2 -------------------------------------------------------
    // Graph-wide EnsureAllSchemasAsync(ct): every object descriptor in the graph
    // gets a vertex table after ONE call.
    // ----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task EnsureAllSchemasAsync_Graph_CreatesTableForEveryObjectDescriptor()
    {
        var conn = RequireConn();

        // A graph with three distinct object descriptors across two CLR types.
        var graph = new OntologyGraphBuilder()
            .AddDomain<DocMultiRegistrationOntology>()
            .AddDomain<NoteOntology>()
            .Build();

        await using var harness = await BootstrapHarness.CreateAsync(conn, graph);

        await harness.Provider.EnsureAllSchemasAsync(CancellationToken.None);

        await Assert.That(await harness.TableExistsAsync("a")).IsTrue();
        await Assert.That(await harness.TableExistsAsync("b")).IsTrue();
        await Assert.That(await harness.TableExistsAsync("note")).IsTrue();
    }

    // ----- Task 7.3 -------------------------------------------------------
    // Validation corpus: the reported basileus scenario — a shared content carrier
    // registered under two collection partitions — bootstraps cleanly under BOTH
    // new entry points.
    // ----------------------------------------------------------------------

    [Test]
    [SkipIfNoPostgres]
    public async Task EnsureAllSchemasAsync_BasileusMultiRegistrationScenario_BootstrapsCleanly()
    {
        var conn = RequireConn();

        // SemanticDocument is the shared content carrier; it is registered under
        // two collection partitions (trading_documents / knowledge_documents),
        // exactly the shape that tripped the multi-registration footgun.
        var graph = new OntologyGraphBuilder()
            .AddDomain<BasileusDocumentOntology>()
            .Build();

        // (1) Graph-wide entry point bootstraps every partition in one call.
        await using (var harness = await BootstrapHarness.CreateAsync(conn, graph))
        {
            await harness.Provider.EnsureAllSchemasAsync(CancellationToken.None);

            await Assert.That(await harness.TableExistsAsync("trading_documents")).IsTrue();
            await Assert.That(await harness.TableExistsAsync("knowledge_documents")).IsTrue();
        }

        // (2) Safe per-type entry point ensures BOTH partitions for the carrier,
        //     no throw on the multi-registration.
        await using (var harness = await BootstrapHarness.CreateAsync(conn, graph))
        {
            await harness.Provider.EnsureSchemaAsync<SemanticDocument>(
                CancellationToken.None);

            await Assert.That(await harness.TableExistsAsync("trading_documents")).IsTrue();
            await Assert.That(await harness.TableExistsAsync("knowledge_documents")).IsTrue();
        }
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

    /// <summary>
    /// A minimal live-DB harness for the schema-bootstrap tests. Mirrors
    /// <c>EdgeFailureModeNpgsqlHarness</c>'s posture: a UNIQUE per-instance schema
    /// (the bootstrap tests run in parallel against a SHARED Postgres, so a shared
    /// schema would race the catalog on concurrent CREATE TABLE), the PRODUCTION
    /// <see cref="PgVectorObjectSetProvider"/> as the unit under test, and a CASCADE
    /// drop of the per-instance schema on dispose so a long-lived shared Postgres
    /// does not accumulate orphaned schemas.
    /// </summary>
    private sealed class BootstrapHarness : IAsyncDisposable
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly string _schema = $"bootstrap_{Guid.NewGuid():N}";

        private BootstrapHarness(NpgsqlDataSource dataSource, OntologyGraph graph)
        {
            _dataSource = dataSource;
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

        public static async Task<BootstrapHarness> CreateAsync(string connectionString, OntologyGraph graph)
        {
            var dataSource = NpgsqlDataSource.Create(connectionString);
            var harness = new BootstrapHarness(dataSource, graph);
            try
            {
                await harness.ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{harness._schema}\";")
                    .ConfigureAwait(false);
            }
            catch
            {
                await dataSource.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return harness;
        }

        /// <summary>
        /// True when a relation named <paramref name="table"/> exists in this
        /// harness's per-instance schema. Uses <c>to_regclass</c>, which returns
        /// NULL for an absent relation rather than raising.
        /// </summary>
        public async Task<bool> TableExistsAsync(string table)
        {
            await using var cmd = _dataSource.CreateCommand("SELECT to_regclass(@qualified) IS NOT NULL;");
            cmd.Parameters.AddWithValue("qualified", $"\"{_schema}\".\"{table}\"");
            var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            return scalar is true;
        }

        public async ValueTask DisposeAsync()
        {
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

        private async Task ExecuteAsync(string sql)
        {
            await using var cmd = _dataSource.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }
}

// ---------------------------------------------------------------------------
// Test fixtures — top-level + public so OntologyGraphBuilder.AddDomain<T>() can
// instantiate them (requires a public parameterless constructor).
// ---------------------------------------------------------------------------

public sealed class Doc
{
    public string Id { get; set; } = string.Empty;
}

public sealed class Note
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Standalone SemanticDocument-like content carrier for the basileus
/// multi-registration corpus (Task 7.3). A single CLR type registered under
/// multiple COLLECTION partitions — the exact shape that triggered #132.
/// </summary>
public sealed class SemanticDocument
{
    public string Id { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public class DocMultiRegistrationOntology : DomainOntology
{
    public override string DomainName => "doc-multi";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Doc>("a", obj => obj.Key(f => f.Id));
        builder.Object<Doc>("b", obj => obj.Key(f => f.Id));
    }
}

public class NoteOntology : DomainOntology
{
    public override string DomainName => "note";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Note>("note", obj => obj.Key(f => f.Id));
    }
}

public class BasileusDocumentOntology : DomainOntology
{
    public override string DomainName => "basileus-docs";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<SemanticDocument>("trading_documents", obj => obj.Key(f => f.Id));
        builder.Object<SemanticDocument>("knowledge_documents", obj => obj.Key(f => f.Id));
    }
}
