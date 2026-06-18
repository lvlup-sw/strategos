// -----------------------------------------------------------------------
// <copyright file="FailureHandlerHostFixture.cs" company="Levelup Software">
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
/// Runtime host fixture for the workflow-level <c>OnFailure</c> chain behavioral
/// proof (#140 Task 3.1). Stands up a real PostgreSQL container and a
/// Wolverine+Marten host wired with the generated
/// <c>AddFailureHandlerProofWorkflow()</c> registration, then runs the generated
/// saga end-to-end to observe the previously-dead <c>OnFailure(flow =&gt;
/// flow.Then&lt;NotifyFailure&gt;())</c> chain firing after a step fails.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors <see cref="CompensationHostFixture"/>: the failure-handler trigger
/// is published from the failing step's Wolverine error chain once the step throws
/// terminally, and is released through the durable inbox/outbox by Wolverine's
/// durability agent rather than being awaited synchronously by
/// <c>TrackActivity()</c>. The fixture therefore publishes the start command,
/// awaits the synchronously-cascaded activity, then polls saga-document absence
/// until the OnFailure chain settles into the terminal Failed phase.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;FailureHandlerHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class FailureHandlerHostFixture : IAsyncInitializer, IAsyncDisposable
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
    /// host with Marten-backed saga storage and the generated failure-handler
    /// workflow registration.
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

                // The generated failure-handler workflow: its FailureHandlerFailingStep
                // handler carries the generated Configure(HandlerChain) that publishes
                // the Trigger{Pascal}FailureHandlerCommand on terminal failure, and its
                // NotifyFailure handler carries the generated worker handler for the
                // ExecuteFailureHandler_..WorkerCommand (#140 Task 3.1).
                opts.Services.AddFailureHandlerProofWorkflow();

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
    /// Total budget to wait for the terminal phase, covering the failing step, the
    /// outbox-released failure-handler trigger, the OnFailure handler worker, and
    /// completion. Defaults to 60 seconds.
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

        // Publish the start command. The failing step throws; the error chain
        // publishes the trigger via the outbox. That trigger + the OnFailure handler
        // worker + completion are released by the durability agent and polled for
        // below, so the tracked publish is allowed to settle without requiring the
        // whole OnFailure cascade to be synchronous.
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
            // The error/dead-letter machinery may keep the tracked session busy past
            // the point the saga has already routed to the OnFailure chain; fall
            // through to the saga-absence poll, which is the authoritative terminal
            // signal.
        }

        var store = runtime.Services.GetRequiredService<IDocumentStore>();
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < budget)
        {
            await using var query = store.QuerySession();
            var saga = await query.LoadAsync<TSaga>(workflowId);
            if (saga is null)
            {
                // MarkCompleted() removed the saga document: terminal Failed reached
                // after the OnFailure handler step ran.
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
