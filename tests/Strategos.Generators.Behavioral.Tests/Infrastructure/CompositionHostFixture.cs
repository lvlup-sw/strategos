// -----------------------------------------------------------------------
// <copyright file="CompositionHostFixture.cs" company="Levelup Software">
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
/// Runtime host fixture for the composition behavioral proofs (epic #135, DR-10,
/// T019/T020/T021). Stands up a real PostgreSQL container and a Wolverine+Marten
/// host wired with the generated composition-workflow registrations, then runs the
/// generated sagas end-to-end to observe how all four lowered resilience
/// capabilities (retry, timeout, compensation, confidence) COMPOSE on a single
/// step and behave at the edges.
/// </summary>
/// <remarks>
/// <para>
/// This is the union of the per-capability fixtures: some composition scenarios
/// complete on the synchronously-tracked cascade (transient-then-success,
/// low-confidence route), while others (always-throws -> compensation; timeout
/// firing mid-retry) complete via a durably-released message (the
/// outbox-published compensation trigger, or the scheduled timeout) that
/// <c>TrackActivity()</c> does not await. The fixture therefore publishes the
/// start command (tolerating detected exceptions from the failure paths), then
/// polls saga-document absence — the authoritative terminal signal, because the
/// generated terminal handlers call <c>MarkCompleted()</c> which removes the saga
/// document.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;CompositionHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class CompositionHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps push their
    /// name here so a test can assert which steps ran and how many times.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Gets the shared timeout-vs-retry race probe (T020). The slow retried step
    /// records each attempt-start timestamp here so a test can confirm the deadline
    /// fired mid-retry.
    /// </summary>
    public CompositionRaceProbe Race { get; } = new();

    /// <summary>
    /// Gets the shared immutable-input probe (T021). The retried step records the
    /// input state instance it received on each attempt so a test can assert INV-7.
    /// </summary>
    public CompositionImmutableProbe Immutable { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts the Wolverine
    /// host with Marten-backed saga storage and the generated composition-workflow
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

                // The three T019 precedence workflows: each generated saga carries
                // all four lowered capabilities on its middle step (retry +
                // compensation on the worker chain, timeout cascade + saga handler,
                // confidence gate in the completed handler).
                opts.Services.AddCompositionTransientWorkflow();
                opts.Services.AddCompositionLowConfidenceWorkflow();
                opts.Services.AddCompositionFailWorkflow();

                // The T020 race + T021 immutable-input workflows.
                opts.Services.AddCompositionRaceWorkflow();
                opts.Services.AddCompositionImmutableWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddSingleton(this.Race);
                opts.Services.AddSingleton(this.Immutable);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Publishes a generated start command, awaits all synchronously-tracked
    /// cascaded activity (tolerating detected exceptions from the compensation /
    /// timeout failure paths), then polls until the saga reaches its terminal phase
    /// (its document is removed by <c>MarkCompleted()</c>) or the budget elapses.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type whose persistence is polled for terminal
    /// completion (its absence signals the saga finished).
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="terminalBudget">
    /// Total budget to wait for the terminal phase, covering retry exhaustion, the
    /// outbox-released compensation trigger, the scheduled timeout, the rollback
    /// worker, and completion. Defaults to 60 seconds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the saga reached its terminal phase within the
    /// budget; otherwise <see langword="false"/>.
    /// </returns>
    public async Task<bool> RunToTerminalAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? terminalBudget = null)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(startCommand, nameof(startCommand));

        var runtime = this.RequireHost();
        var budget = terminalBudget ?? TimeSpan.FromSeconds(60);

        try
        {
            await runtime
                .TrackActivity()
                .Timeout(budget)
                .DoNotAssertOnExceptionsDetected()
                .PublishMessageAndWaitAsync(startCommand);
        }
        catch (TimeoutException)
        {
            // The retry / scheduled-timeout machinery may keep the tracked session
            // busy past the point the saga has already routed to its terminal phase;
            // fall through to the saga-absence poll, which is authoritative.
        }

        var store = runtime.Services.GetRequiredService<IDocumentStore>();
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < budget)
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
