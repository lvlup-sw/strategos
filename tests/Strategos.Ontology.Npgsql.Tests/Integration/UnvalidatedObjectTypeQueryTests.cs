using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Pgvector.Npgsql;

using Strategos.Ontology.Builder;
using Strategos.Ontology.Events;
using Strategos.Ontology.MCP;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

/// <summary>
/// Spec for the ontology_query read of caller-supplied object type names.
/// An object type that is not a registered descriptor must be refused with
/// <see cref="InvalidOperationException"/> before any SQL reads that name in
/// the provider schema (the default is <c>public</c>). The refusal has to name
/// the requested object type and say it is not registered, and the relation
/// that was not authorized must not be read — including when a check would
/// pass for a different descriptor and the read would still use the caller's name.
/// </summary>
/// <remarks>
/// Paths that do not share this read are intentionally not asserted here:
/// <c>traverseLink</c> (graph-checked traversal lowering), <c>interfaceName</c>
/// and a raw <c>filter</c> (the SQL translator rejects those nodes before a
/// command is executed), <c>QueryEventsAsync</c> (event-stream provider, not
/// this table lowering), and explicit-name writes.
/// </remarks>
[NotInParallel("unvalidated-object-type-public")]
public sealed class UnvalidatedObjectTypeQueryTests
{
    private const string EventRelation = "mt_events";
    private const string DocumentRelation = "mt_doc_ordersaga";
    private const string LedgerRelation = "ledger_entry";
    private const string ShelfRelation = "shelf_label";

    [Test]
    public async Task PgVectorOptions_DefaultsSchemaToPublic()
    {
        var options = new PgVectorOptions();

        await Assert.That(options.Schema).IsEqualTo("public");
    }

    [Test]
    public async Task QueryAsync_UnregisteredEventTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();

        await harness.AssertQueryRefusedAsync(EventRelation, EventRelation, () =>
            harness.Tool.QueryAsync(EventRelation));
    }

    [Test]
    public async Task QueryAsync_UnregisteredDocumentTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();

        // Two rows, and the lowering has no LIMIT, so a read records both.
        await harness.AssertQueryRefusedAsync(DocumentRelation, DocumentRelation, () =>
            harness.Tool.QueryAsync(DocumentRelation));
    }

    [Test]
    public async Task QueryAsync_PascalCaseAliasOfEventTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();

        // "MtEvents" snake-cases to the physical event table. The requested name
        // is still not a registered descriptor.
        await harness.AssertQueryRefusedAsync("MtEvents", EventRelation, () =>
            harness.Tool.QueryAsync("MtEvents"));
    }

    [Test]
    public async Task QueryAsync_PhysicalNameOfRegisteredDescriptor_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();

        // "ledger_entry" is the physical table of descriptor LedgerEntry, not the
        // descriptor name. Authorizing the descriptor must not authorize this alias.
        await harness.AssertQueryRefusedAsync(LedgerRelation, LedgerRelation, () =>
            harness.Tool.QueryAsync(LedgerRelation));
    }

    [Test]
    public async Task QueryAsync_IncludeOnUnregisteredTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();

        await harness.AssertQueryRefusedAsync(DocumentRelation, DocumentRelation, () =>
            harness.Tool.QueryAsync(DocumentRelation, include: "Full"));
    }

    [Test]
    public async Task QueryAsync_DomainDoesNotAuthorizeUnregisteredTable()
    {
        await using var harness = await QueryHarness.OpenAsync();

        await harness.AssertQueryRefusedAsync(EventRelation, EventRelation, () =>
            harness.Tool.QueryAsync(EventRelation, domain: "books"));
    }

    [Test]
    public async Task QueryAsync_WrongDomainDoesNotReadRegisteredTable()
    {
        await using var harness = await QueryHarness.OpenAsync();

        // LedgerEntry is registered in "books". A different domain does not
        // authorize that descriptor, so its table must not be read either.
        await harness.AssertQueryRefusedAsync("LedgerEntry", LedgerRelation, () =>
            harness.Tool.QueryAsync("LedgerEntry", domain: "other"));
    }

    [Test]
    public async Task QueryAsync_SemanticQuery_UnregisteredTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();

        await harness.AssertQueryRefusedAsync(EventRelation, EventRelation, () =>
            harness.Tool.QueryAsync(EventRelation, semanticQuery: "workflow events"));
    }

    [Test]
    public async Task ExecuteAsync_UnregisteredTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();
        var expression = new RootExpression(typeof(object), DocumentRelation);

        await harness.AssertCallRefusedAsync(DocumentRelation, DocumentRelation, async () =>
        {
            var result = await harness.Provider.ExecuteAsync<object>(expression);
            return JsonSerializer.Serialize(result.Items);
        });
    }

    [Test]
    public async Task StreamAsync_UnregisteredTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();
        var expression = new RootExpression(typeof(object), EventRelation);

        await harness.AssertCallRefusedAsync(EventRelation, EventRelation, async () =>
        {
            var items = new List<object>();
            await foreach (var item in harness.Provider.StreamAsync<object>(expression))
            {
                items.Add(item);
            }

            return JsonSerializer.Serialize(items);
        });
    }

    [Test]
    public async Task ExecuteSimilarityAsync_UnregisteredTable_IsRefusedBeforeRead()
    {
        await using var harness = await QueryHarness.OpenAsync();
        var expression = new SimilarityExpression(
            new RootExpression(typeof(object), EventRelation),
            queryText: "workflow events",
            topK: 5,
            minRelevance: 0.0,
            queryVector: QueryHarness.ProbeVector);

        await harness.AssertCallRefusedAsync(EventRelation, EventRelation, async () =>
        {
            var result = await harness.Provider.ExecuteSimilarityAsync<object>(expression);
            return JsonSerializer.Serialize(result.Items);
        });
    }

    [Test]
    public async Task QueryAsync_RegisteredDescriptor_ReadsOnlyThatTable()
    {
        await using var harness = await QueryHarness.OpenAsync();

        var ledger = (QueryResult)await harness.Tool.QueryAsync("LedgerEntry", domain: "books");
        var ledgerJson = JsonSerializer.Serialize(ledger.Items);
        var ledgerReads = await harness.ReadsOfAsync(LedgerRelation);

        await Assert.That(ledgerJson).Contains("ledger_entry");
        await Assert.That(ledgerJson).DoesNotContain("mt_events");
        await Assert.That(ledgerJson).DoesNotContain("mt_doc_ordersaga");
        await Assert.That(ledgerJson).DoesNotContain("shelf_label");
        await Assert.That(ledgerReads).IsNotEmpty();
        foreach (var relation in ledgerReads)
        {
            await Assert.That(relation).IsEqualTo(LedgerRelation);
        }

        await harness.ClearAuditAsync();

        var shelf = (QueryResult)await harness.Tool.QueryAsync("ShelfLabel", domain: "books");
        var shelfJson = JsonSerializer.Serialize(shelf.Items);
        var shelfReads = await harness.ReadsOfAsync(ShelfRelation);

        await Assert.That(shelfJson).Contains("shelf_label");
        await Assert.That(shelfJson).DoesNotContain("ledger_entry");
        await Assert.That(shelfJson).DoesNotContain("mt_events");
        await Assert.That(shelfReads).IsNotEmpty();
        foreach (var relation in shelfReads)
        {
            await Assert.That(relation).IsEqualTo(ShelfRelation);
        }

        await Assert.That(await harness.ReadsOfAsync(EventRelation)).IsEmpty();
        await Assert.That(await harness.ReadsOfAsync(DocumentRelation)).IsEmpty();
    }

    /// <summary>
    /// Books-domain descriptors. ShelfLabel is registered first so a fix that
    /// substitutes "the first descriptor" for an unknown name is observable.
    /// </summary>
    public sealed class BooksDomain : DomainOntology
    {
        public override string DomainName => "books";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<ShelfLabel>(obj => obj.Key(label => label.Id));
            builder.Object<LedgerEntry>(obj => obj.Key(entry => entry.Id));
        }
    }

    /// <summary>Second registered descriptor. Its table must not satisfy a query for another name.</summary>
    public sealed class ShelfLabel
    {
        public string Id { get; set; } = "";
    }

    /// <summary>Registered descriptor whose physical table is <c>ledger_entry</c>.</summary>
    public sealed class LedgerEntry
    {
        public string Id { get; set; } = "";
    }

    private sealed class QueryHarness : IAsyncDisposable
    {
        internal static readonly float[] ProbeVector = [0.1f, 0.2f, 0.3f];

        private readonly NpgsqlDataSource _dataSource;

        private QueryHarness(NpgsqlDataSource dataSource, PgVectorObjectSetProvider provider, OntologyQueryTool tool)
        {
            _dataSource = dataSource;
            Provider = provider;
            Tool = tool;
        }

        internal PgVectorObjectSetProvider Provider { get; }

        internal OntologyQueryTool Tool { get; }

        public async ValueTask DisposeAsync() => await _dataSource.DisposeAsync().ConfigureAwait(false);

        internal static async Task<QueryHarness> OpenAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable(SkipIfNoPostgresAttribute.ConnectionEnvVar);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"{SkipIfNoPostgresAttribute.ConnectionEnvVar} must name a Postgres database for this test.");
            }

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector();
            var dataSource = builder.Build();
            await EnsureBaitAsync(dataSource).ConfigureAwait(false);
            await ClearAuditAsync(dataSource).ConfigureAwait(false);

            var graph = new OntologyGraphBuilder().AddDomain<BooksDomain>().Build();
            var options = Options.Create(new PgVectorOptions { ConnectionString = connectionString });
            var provider = new PgVectorObjectSetProvider(
                dataSource,
                new FixedEmbeddingProvider(),
                options,
                NullLogger<PgVectorObjectSetProvider>.Instance,
                graph);
            var tool = new OntologyQueryTool(
                graph,
                provider,
                Substitute.For<IEventStreamProvider>(),
                NullLogger<OntologyQueryTool>.Instance);

            return new QueryHarness(dataSource, provider, tool);
        }

        internal Task AssertQueryRefusedAsync(
            string objectType,
            string relation,
            Func<Task<QueryResultUnion>> query) =>
            AssertCallRefusedAsync(objectType, relation, async () =>
            {
                var result = await query().ConfigureAwait(false);
                return JsonSerializer.Serialize(result);
            });

        internal async Task AssertCallRefusedAsync(
            string objectType,
            string relation,
            Func<Task<string>> invoke)
        {
            string? payload = null;
            Exception? error = null;
            try
            {
                payload = await invoke().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            var reads = await ReadsOfAsync(relation).ConfigureAwait(false);
            await Assert.That(reads).IsEmpty()
                .Because(
                    $"object type '{objectType}' must be refused before any read of "
                    + $"\"public\".\"{relation}\"; observed {reads.Count} read(s); payload={payload ?? "<none>"}");

            await Assert.That(error).IsNotNull()
                .Because(
                    $"object type '{objectType}' must be refused; the call returned a result. payload={payload ?? "<none>"}");
            await Assert.That(error).IsTypeOf<InvalidOperationException>();
            await Assert.That(error!.Message).Contains(objectType);
            await Assert.That(error.Message).Contains("not registered");
        }

        internal async Task<IReadOnlyList<string>> ReadsOfAsync(string relation)
        {
            await using var command = _dataSource.CreateCommand(
                "SELECT relation FROM read_audit WHERE relation = @relation ORDER BY id");
            command.Parameters.AddWithValue("relation", relation);
            var reads = new List<string>();
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                reads.Add(reader.GetString(0));
            }

            return reads;
        }

        internal Task ClearAuditAsync() => ClearAuditAsync(_dataSource);

        private static async Task ClearAuditAsync(NpgsqlDataSource dataSource)
        {
            await using var command = dataSource.CreateCommand("DELETE FROM read_audit");
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        private static async Task EnsureBaitAsync(NpgsqlDataSource dataSource)
        {
            const string sql =
                """
                CREATE EXTENSION IF NOT EXISTS vector;
                DROP VIEW IF EXISTS mt_events;
                DROP VIEW IF EXISTS mt_doc_ordersaga;
                DROP VIEW IF EXISTS ledger_entry;
                DROP VIEW IF EXISTS shelf_label;
                DROP FUNCTION IF EXISTS note_relation_read(text);
                DROP TABLE IF EXISTS read_audit;

                CREATE TABLE read_audit (
                    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    relation text NOT NULL
                );

                CREATE FUNCTION note_relation_read(relation text) RETURNS jsonb
                LANGUAGE plpgsql AS $fn$
                BEGIN
                    INSERT INTO read_audit(relation) VALUES (relation);
                    RETURN jsonb_build_object('marker', relation);
                END;
                $fn$;

                CREATE VIEW mt_events AS
                SELECT
                    '11111111-1111-1111-1111-111111111111'::uuid AS id,
                    note_relation_read('mt_events') AS data,
                    '[0.1,0.2,0.3]'::vector AS embedding;

                CREATE VIEW mt_doc_ordersaga AS
                SELECT
                    rows.id,
                    note_relation_read('mt_doc_ordersaga') AS data,
                    '[0.1,0.2,0.3]'::vector AS embedding
                FROM (VALUES
                    ('22222222-2222-2222-2222-222222222221'::uuid),
                    ('22222222-2222-2222-2222-222222222222'::uuid)
                ) AS rows(id);

                CREATE VIEW ledger_entry AS
                SELECT
                    '33333333-3333-3333-3333-333333333333'::uuid AS id,
                    note_relation_read('ledger_entry') AS data,
                    '[0.1,0.2,0.3]'::vector AS embedding;

                CREATE VIEW shelf_label AS
                SELECT
                    '44444444-4444-4444-4444-444444444444'::uuid AS id,
                    note_relation_read('shelf_label') AS data,
                    '[0.1,0.2,0.3]'::vector AS embedding;
                """;

            await using var command = dataSource.CreateCommand(sql);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private sealed class FixedEmbeddingProvider : IEmbeddingProvider
    {
        public int Dimensions => QueryHarness.ProbeVector.Length;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(QueryHarness.ProbeVector);

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => QueryHarness.ProbeVector).ToArray());
    }
}
