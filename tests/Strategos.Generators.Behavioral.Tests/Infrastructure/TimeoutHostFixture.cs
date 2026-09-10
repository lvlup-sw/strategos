// -----------------------------------------------------------------------
// <copyright file="TimeoutHostFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

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
/// Runtime host fixture for the timeout behavioral proofs (DR-4). Stands up a real
/// PostgreSQL container and a Wolverine+Marten host wired with the generated
/// <c>AddTimeoutSlowWorkflow()</c> and <c>AddTimeoutFastWorkflow()</c> registrations,
/// then runs the generated sagas end-to-end to observe the saga-level deadline race.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors <see cref="WolverineHostFixture"/> (which is pinned to the
/// happy-path workflow) but registers the two timeout workflows and exposes a
/// terminal-wait helper. The host integrates Marten with Wolverine
/// (<c>IntegrateWithWolverine()</c>) so the durable inbox/outbox releases the
/// scheduled <c>TimeoutMessage</c> deliveries that drive the deadline race.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;TimeoutHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class TimeoutHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps push their
    /// name here so a test can assert which steps ran and how many times.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts the Wolverine
    /// host with Marten-backed saga storage and the generated timeout-workflow
    /// registrations.
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

                // Both generated timeout workflows: the slow scenario (deadline
                // exceeded → route to failure) and the fast scenario (completes
                // first → timeout is a no-op).
                opts.Services.AddTimeoutSlowWorkflow();
                opts.Services.AddTimeoutFastWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Publishes a generated start command, awaits all synchronously-tracked
    /// cascaded activity, then polls until the saga reaches its terminal phase
    /// (its document is removed by <c>MarkCompleted()</c>) or the budget elapses.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type whose persistence is polled for terminal
    /// completion (its absence signals the saga finished).
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="terminalBudget">
    /// Total budget to wait for the terminal phase, covering the scheduled
    /// <c>TimeoutMessage</c> delivery and its handling. Defaults to 30 seconds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the saga reached its terminal phase within the
    /// budget; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The deadline-race timeout is a durable scheduled message released by
    /// Wolverine's background durability agent, which is NOT awaited by
    /// <c>TrackActivity()</c>. The fixture therefore polls saga-document absence
    /// after the tracked publish settles, so both the "timeout fires and routes to
    /// failure" path and the "step completes first" path are observed
    /// deterministically.
    /// </remarks>
    public async Task<bool> RunToTerminalAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? terminalBudget = null)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(startCommand, nameof(startCommand));

        var runtime = this.RequireHost();
        var budget = terminalBudget ?? TimeSpan.FromSeconds(30);

        // Publish + await the synchronously-cascaded chain (saga start → step start
        // → worker → step completed → ...). The scheduled timeout message is
        // released later by the durability agent and is polled for below.
        await runtime
            .TrackActivity()
            .Timeout(budget)
            .PublishMessageAndWaitAsync(startCommand);

        var store = runtime.Services.GetRequiredService<IDocumentStore>();
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < budget)
        {
            await using var query = store.QuerySession();
            var saga = await query.LoadAsync<TSaga>(workflowId);
            if (saga is null)
            {
                // MarkCompleted() removed the saga document: terminal reached
                // (Completed for the fast path, or Failed for the timed-out path).
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        return false;
    }

    /// <summary>
    /// Stops and disposes the host and the shared Postgres container.
    /// </summary>
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
