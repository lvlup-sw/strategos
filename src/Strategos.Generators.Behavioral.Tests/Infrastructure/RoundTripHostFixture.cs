// -----------------------------------------------------------------------
// <copyright file="RoundTripHostFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using JasperFx.Resources;

using Marten;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Strategos.Generators.Behavioral.Tests.Workflows;

using Wolverine;
using Wolverine.Marten;
using Wolverine.Tracking;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Real-host fixture for the DR-15 round-trip real-host proofs (task 019) on one Wolverine + Marten
/// host backed by a real PostgreSQL container. Registers the config (retry) importable family in BOTH
/// authoring forms — the JSON import (<c>AddRoundtripConfigImportWorkflow()</c>) + its C# twin
/// (<c>AddRoundtripConfigTwinWorkflow()</c>) — for a twin-equivalence run, and the fork-join JSON
/// import (<c>AddRoundtripForkImportWorkflow()</c>) for a fork import runtime proof (its C# twin is
/// blocked by a pre-existing fork terminal-detection bug; see <c>RoundTripForkWorkflow</c>). A JSON
/// import lowered through the SAME saga emitters as a C# workflow (INV-1).
/// </summary>
/// <remarks>
/// Requires a reachable Docker daemon for the Postgres container. When Postgres is unavailable the
/// fixture fails to initialize and the runtime tests do not run; the required semantic check is the
/// BUILD compiling the imported sagas, not the runtime execution here.
/// </remarks>
public sealed class RoundTripHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>Gets the shared step-invocation log every registered workflow records into.</summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the Postgres container, then the Wolverine host with Marten-backed saga storage and the
    /// round-trip workflow registrations (config import + twin, fork import).
    /// </summary>
    /// <returns>A task that completes when the host is running.</returns>
    public async Task InitializeAsync()
    {
        await this.postgres.InitializeAsync();

        this.host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Services
                    .AddMarten(storeOptions =>
                    {
                        storeOptions.Connection(this.postgres.ConnectionString);
                        storeOptions.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                    })
                    .IntegrateWithWolverine()
                    .ApplyAllDatabaseChangesOnStartup();

                // The config family in both authoring forms (twin-equivalence), the fork import
                // (runtime proof) plus its C# twin (DR-15 twin equivalence — registered so the SKIPPED
                // ForkJoinCSharpTwin_RunsIdentically_ToJsonImport goes green once strategos#155 lands),
                // and the onFailure import (bucket-(a) compile + runtime proof) — all lowered through
                // the SAME saga emitters (INV-1).
                opts.Services.AddRoundtripForkImportWorkflow();
                opts.Services.AddRoundtripForkTwinWorkflow();
                opts.Services.AddRoundtripConfigImportWorkflow();
                opts.Services.AddRoundtripConfigTwinWorkflow();
                opts.Services.AddRoundtripOnFailureImportWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Runs a generated workflow saga to completion against the real host, then POLLS until it
    /// reaches its terminal phase (its document is removed by <c>MarkCompleted()</c>) or the budget
    /// elapses. Polling is authoritative because a fork saga's parallel-path/join cascade can settle
    /// the tracked activity session before the join and terminal step have run.
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type polled for terminal completion.</typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command that kicks off the saga.</param>
    /// <param name="timeout">Optional wait budget (defaults to 30 seconds).</param>
    /// <returns><see langword="true"/> when the saga reached its terminal phase within the budget.</returns>
    public async Task<bool> RunWorkflowAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? timeout = null)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(startCommand, nameof(startCommand));

        var runtime = this.RequireHost();
        var budget = timeout ?? TimeSpan.FromSeconds(30);

        try
        {
            await runtime
                .TrackActivity()
                .Timeout(budget)
                .PublishMessageAndWaitAsync(startCommand);
        }
        catch (TimeoutException)
        {
            // The tracked session may settle before the saga has routed to its terminal phase; the
            // saga-absence poll below is authoritative.
        }

        var store = runtime.Services.GetRequiredService<IDocumentStore>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < budget)
        {
            await using var query = store.QuerySession();
            var saga = await query.LoadAsync<TSaga>(workflowId);
            if (saga is null)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }

        return false;
    }

    /// <summary>Stops and disposes the host and the shared Postgres container.</summary>
    /// <returns>A value task that completes when teardown finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.host is not null)
        {
            await this.host.StopAsync();
            this.host.Dispose();
        }

        await this.postgres.DisposeAsync();
    }

    private IHost RequireHost() =>
        this.host ?? throw new InvalidOperationException(
            "Host not initialized. Ensure InitializeAsync ran (TUnit IAsyncInitializer).");
}
