// -----------------------------------------------------------------------
// <copyright file="PostgresFixtureSmokeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Data;

using Npgsql;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Smoke test proving the behavioral-test harness can stand up a real
/// PostgreSQL database via Testcontainers and open a connection to it. This
/// is the acceptance-unlock for later DR-9 tasks that compile and RUN the
/// generated Wolverine+Marten saga against this database.
/// </summary>
/// <remarks>
/// Marked <see cref="NotInParallelAttribute"/> because the suite asserts
/// against a single shared container (a process-shared resource). The
/// container itself is shared for the whole test session via
/// <c>[ClassDataSource(Shared = SharedType.PerTestSession)]</c>.
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public sealed class PostgresFixtureSmokeTests
{
    private readonly PostgresFixture fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresFixtureSmokeTests"/> class.
    /// </summary>
    /// <param name="fixture">
    /// The shared Postgres container fixture, injected by TUnit from the
    /// class-level <c>[ClassDataSource]</c> and shared across the entire test
    /// session.
    /// </param>
    public PostgresFixtureSmokeTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// Verifies the fixture's container starts and a connection can be opened
    /// against its connection string: <c>SELECT 1</c> returns <c>1</c> and the
    /// connection reaches the <see cref="ConnectionState.Open"/> state.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PostgresFixture_StartsContainer_ConnectionOpens()
    {
        await using var connection = new NpgsqlConnection(this.fixture.ConnectionString);

        await connection.OpenAsync();

        await Assert.That(connection.State).IsEqualTo(ConnectionState.Open);

        await using var command = new NpgsqlCommand("SELECT 1;", connection);
        var result = await command.ExecuteScalarAsync();

        await Assert.That(result).IsEqualTo(1);
    }
}
