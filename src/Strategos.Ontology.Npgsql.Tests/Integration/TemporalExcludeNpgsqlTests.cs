using System;
using System.Text.Json;
using System.Threading.Tasks;
using global::Npgsql;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests.Integration;

// ---------------------------------------------------------------------------
// DR-16 (T21, #126): DB-GATED execution proof for the temporal EXCLUDE
// constraint. The unit tests (TemporalConstraintTests) assert the DDL SHAPE; this
// file provisions the real schema via the SAME SqlGenerator DDL and drives raw
// INSERTs to prove the GiST exclusion actually FIRES — and that a retracted
// (system-closed) row does NOT block a fresh assertion (the soft-delete axis,
// INV-7). DB-GATED via [SkipIfNoPostgres]: no local Postgres in the default lane,
// so these SKIP unless STRATEGOS_PG_TEST_CONN names a reachable database.
// INV-2: raw Npgsql only.
// ---------------------------------------------------------------------------
public class TemporalExcludeNpgsqlTests
{
    [Test]
    [SkipIfNoPostgres]
    public async Task Exclude_RejectsSecondOverlappingAssertion_AndAllowsAfterRetraction()
    {
        await using var harness = await TemporalExcludeHarness.CreateAsync(RequireConn());

        // First assertion: Person p1 employed by Company c1, valid from 2026-01-01,
        // open-ended, currently asserted (system_to NULL). Succeeds.
        await harness.AssertEmploymentAsync(
            "p1", "c1", validFrom: "2026-01-01T00:00:00Z", validTo: null);

        // Second assertion: SAME endpoints, OVERLAPPING valid-time, also currently
        // asserted. The partial GiST EXCLUDE rejects it with a unique/exclusion
        // violation (SQLSTATE 23P01).
        var violation = await Assert.That(async () =>
            await harness.AssertEmploymentAsync(
                "p1", "c1", validFrom: "2026-06-01T00:00:00Z", validTo: null))
            .Throws<PostgresException>();
        await Assert.That(violation!.SqlState).IsEqualTo("23P01");

        // Retract the first assertion (close its system interval — NO physical
        // delete). Now a fresh overlapping assertion is allowed: the partial
        // constraint only sees CURRENTLY-asserted (system_to IS NULL) rows.
        await harness.RetractAllOpenAsync("p1", "c1");
        await harness.AssertEmploymentAsync(
            "p1", "c1", validFrom: "2026-06-01T00:00:00Z", validTo: null);

        // The retracted row was NOT deleted: the table still holds both the closed
        // and the fresh open row (INV-7 — soft-delete via interval close).
        await Assert.That(await harness.RowCountAsync()).IsEqualTo(2L);
        await Assert.That(await harness.OpenRowCountAsync()).IsEqualTo(1L);
    }

    [Test]
    [SkipIfNoPostgres]
    public async Task AsOfTransactionTime_ReadsBackHistoricalSet()
    {
        await using var harness = await TemporalExcludeHarness.CreateAsync(RequireConn());

        // Assert, capture the as-of-now transaction instant, then retract.
        await harness.AssertEmploymentAsync(
            "p1", "c1", validFrom: "2026-01-01T00:00:00Z", validTo: null);
        var afterAssert = DateTimeOffset.UtcNow;
        await Task.Delay(10);
        await harness.RetractAllOpenAsync("p1", "c1");

        // As of the captured instant, the relationship was KNOWN — the historical
        // reconstruction (BuildAsOfTransactionTimeSql) returns it ...
        await Assert.That(await harness.AsOfTransactionCountAsync(afterAssert)).IsEqualTo(1L);
        // ... but as of NOW it has been retracted, so the as-of-now read omits it.
        await Assert.That(await harness.AsOfTransactionCountAsync(DateTimeOffset.UtcNow)).IsEqualTo(0L);
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
}

/// <summary>
/// DR-16 (T21) DB-gated harness for the temporal EXCLUDE proof. Provisions a
/// Person + Company endpoint table and the Employment association table via the
/// SAME <see cref="SqlGenerator.BuildAssociationObjectTableDdl"/> the provider's
/// schema path uses, in an isolated per-instance schema. INV-2: raw Npgsql only.
/// </summary>
internal sealed class TemporalExcludeHarness : IAsyncDisposable
{
    private const string AssocTable = "employment";

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schema = $"tmp_{Guid.NewGuid():N}";

    private TemporalExcludeHarness(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public static async Task<TemporalExcludeHarness> CreateAsync(string connectionString)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        var harness = new TemporalExcludeHarness(dataSource);
        try
        {
            await harness.CreateSchemaAsync().ConfigureAwait(false);
        }
        catch
        {
            await dataSource.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return harness;
    }

    public async Task AssertEmploymentAsync(string personId, string companyId, string validFrom, string? validTo)
    {
        // Resolve endpoint surrogate ids by business id, write the association row
        // with the user-asserted valid interval. system_from defaults to now();
        // system_to stays NULL (open assertion).
        var json = JsonSerializer.Serialize(new { Id = $"{personId}-{companyId}-{validFrom}" });
        await ExecuteAsync(
            $"INSERT INTO \"{_schema}\".\"{AssocTable}\" "
            + "(data, \"employee_id\", \"employer_id\", valid_from, valid_to) "
            + "SELECT @data::jsonb, p.id, c.id, @validFrom::timestamptz, "
            + "  CASE WHEN @validTo IS NULL THEN NULL ELSE @validTo::timestamptz END "
            + $"FROM \"{_schema}\".\"person\" p, \"{_schema}\".\"company\" c "
            + "WHERE p.data->>'Id' = @personId AND c.data->>'Id' = @companyId;",
            ("data", json),
            ("validFrom", validFrom),
            ("validTo", (object?)validTo ?? DBNull.Value),
            ("personId", personId),
            ("companyId", companyId)).ConfigureAwait(false);
    }

    public async Task RetractAllOpenAsync(string personId, string companyId)
    {
        // Soft-delete: CLOSE the system interval of the open rows for this endpoint
        // pair. No physical delete (INV-7). In the in-memory model this is an
        // appended close event; the live projection materializes it as a system_to
        // set, which is the projection's terminal effect either way.
        await ExecuteAsync(
            $"UPDATE \"{_schema}\".\"{AssocTable}\" a SET system_to = now() "
            + $"FROM \"{_schema}\".\"person\" p, \"{_schema}\".\"company\" c "
            + "WHERE a.\"employee_id\" = p.id AND a.\"employer_id\" = c.id "
            + "AND p.data->>'Id' = @personId AND c.data->>'Id' = @companyId "
            + "AND a.system_to IS NULL;",
            ("personId", personId),
            ("companyId", companyId)).ConfigureAwait(false);
    }

    public Task<long> RowCountAsync() =>
        ScalarAsync($"SELECT count(*) FROM \"{_schema}\".\"{AssocTable}\";");

    public Task<long> OpenRowCountAsync() =>
        ScalarAsync($"SELECT count(*) FROM \"{_schema}\".\"{AssocTable}\" WHERE system_to IS NULL;");

    public async Task<long> AsOfTransactionCountAsync(DateTimeOffset asOfTx)
    {
        var sql = SqlGenerator.BuildAsOfTransactionTimeSql(_schema, AssocTable);
        await using var cmd = _dataSource.CreateCommand($"SELECT count(*) FROM ({sql}) sub;");
        cmd.Parameters.AddWithValue("asOfTx", asOfTx);
        var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(scalar);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await ExecuteAsync($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE;").ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // Best-effort cleanup.
        }

        await _dataSource.DisposeAsync().ConfigureAwait(false);
    }

    private async Task CreateSchemaAsync()
    {
        await ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{_schema}\";").ConfigureAwait(false);

        foreach (var endpoint in new[] { "person", "company" })
        {
            await ExecuteAsync(
                $"CREATE TABLE \"{_schema}\".\"{endpoint}\" "
                + "(id uuid PRIMARY KEY DEFAULT gen_random_uuid(), data jsonb NOT NULL);")
                .ConfigureAwait(false);
        }

        await ExecuteAsync("INSERT INTO \"" + _schema + "\".\"person\" (data) VALUES "
            + "(@d::jsonb);", ("d", JsonSerializer.Serialize(new { Id = "p1" }))).ConfigureAwait(false);
        await ExecuteAsync("INSERT INTO \"" + _schema + "\".\"company\" (data) VALUES "
            + "(@d::jsonb);", ("d", JsonSerializer.Serialize(new { Id = "c1" }))).ConfigureAwait(false);

        var association = new ObjectTypeDescriptor("Employment", typeof(object), "hr")
        {
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("Employee", "Person"),
                new AssociationEndpoint("Employer", "Company"),
            ],
        };

        // The SAME DDL the provider emits — including the btree_gist extension, the
        // temporal EXCLUDE, and the as-of-now partial index.
        var ddl = SqlGenerator.BuildAssociationObjectTableDdl(_schema, association);
        await ExecuteAsync(ddl).ConfigureAwait(false);
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt64(scalar);
    }

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
