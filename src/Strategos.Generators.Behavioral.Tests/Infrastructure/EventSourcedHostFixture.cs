// -----------------------------------------------------------------------
// <copyright file="EventSourcedHostFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

using JasperFx.Resources;

using Marten;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Strategos.Agents.Abstractions;
using Strategos.Generators.Behavioral.Tests.Workflows;

using Wolverine;
using Wolverine.Marten;
using Wolverine.Tracking;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Runtime host fixture for the EVENT-SOURCED audit-event behavioral proofs
/// (#138 G-5). Stands up a real PostgreSQL container and a Wolverine+Marten host
/// whose Marten store is integrated with Wolverine, then runs generated sagas
/// that declare <c>Persistence = PersistenceMode.EventSourced</c>. In that mode
/// the generated handlers <c>session.Events.Append(WorkflowId, evt)</c> instead
/// of mutating a saga document, so workflow events round-trip through the Marten
/// event stream — the surface these tests inspect.
/// </summary>
/// <remarks>
/// <para>
/// This is the event-sourced counterpart to <see cref="WolverineHostFixture"/>
/// (document mode). The existing fixtures are all document-mode; the audit-event
/// vertical needs to observe the named <c>StepFailed</c> / <c>LowConfidenceRouted</c>
/// stream events, which only exist when the saga appends to the Marten stream.
/// </para>
/// <para>
/// It owns a single shared <see cref="PostgresFixture"/> container for the
/// session and registers the event-sourced fixture workflows. Lifecycle is
/// driven by TUnit via <see cref="IAsyncInitializer"/> / <see cref="IAsyncDisposable"/>.
/// Share one instance for the whole session with
/// <c>[ClassDataSource&lt;EventSourcedHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>;
/// mark consumers <c>[NotInParallel]</c> because the host + invocation log are
/// process-shared.
/// </para>
/// </remarks>
public sealed class EventSourcedHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps push
    /// their name here so a test can assert which steps ran and how many times.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts a Wolverine
    /// host with a Marten event store integrated with Wolverine and the generated
    /// event-sourced workflow registrations.
    /// </summary>
    /// <returns>A task that completes when the host is running.</returns>
    public async Task InitializeAsync()
    {
        await this.postgres.InitializeAsync();

        this.host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                // Marten owns BOTH the event store (the appended workflow events)
                // and Wolverine's durable inbox/outbox. The generated
                // Add{Name}Workflow() registrations call ConfigureMarten(...) to
                // register each event-sourced state's inline snapshot projection;
                // IntegrateWithWolverine() wires the saga/message durability.
                opts.Services
                    .AddMarten(storeOptions =>
                    {
                        storeOptions.Connection(this.postgres.ConnectionString);
                        storeOptions.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                    })
                    .IntegrateWithWolverine()
                    .ApplyAllDatabaseChangesOnStartup();

                // The event-sourced happy-path proof (Task 5.1): its generated saga
                // appends a Started + step-completed events to the Marten stream.
                opts.Services.AddEventSourcedHappyWorkflow();

                // The event-sourced failure proof (Task 5.2): its failing step routes
                // to the OnFailure chain; the trigger handler appends StepFailed.
                opts.Services.AddEventSourcedFailureProofWorkflow();

                // The event-sourced low-confidence proof (Task 5.3): its gated step
                // returns low confidence; the confidence gate appends LowConfidenceRouted.
                opts.Services.AddEventSourcedLowConfidenceWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Runs a generated event-sourced workflow saga to completion against the real
    /// host, using Wolverine's message-tracking API to deterministically await all
    /// cascaded processing.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type whose persistence is polled to confirm
    /// terminal completion (its absence signals the saga finished).
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="timeout">
    /// Optional wait budget for the cascading saga + worker activity to settle.
    /// Defaults to 30 seconds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the saga reached its terminal phase.
    /// </returns>
    public async Task<bool> RunWorkflowAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? timeout = null)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(startCommand, nameof(startCommand));

        var runtime = this.RequireHost();

        await runtime
            .TrackActivity()
            .Timeout(timeout ?? TimeSpan.FromSeconds(30))
            .PublishMessageAndWaitAsync(startCommand);

        await using var query = runtime.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var saga = await query.LoadAsync<TSaga>(workflowId);

        return saga is null;
    }

    /// <summary>
    /// Publishes a generated start command, awaits all synchronously-tracked
    /// cascaded activity, then polls until the saga reaches its terminal phase
    /// (its document removed by <c>MarkCompleted()</c>) or the budget elapses.
    /// Used for failure-routed sagas whose terminal route is released through the
    /// durable inbox/outbox (which <c>TrackActivity()</c> does not await).
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type polled for terminal completion.
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="terminalBudget">
    /// Total budget to wait for the terminal phase. Defaults to 60 seconds.
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
            // The error/dead-letter machinery may keep the tracked session busy
            // past the point the saga already routed to its terminal phase; fall
            // through to the saga-absence poll, the authoritative terminal signal.
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
    /// Reads back the raw event payloads appended to a workflow's Marten stream,
    /// in append order. The audit-event tests use this to assert that the named
    /// <c>StepFailed</c> / <c>LowConfidenceRouted</c> events (and baseline workflow
    /// events) actually landed in the stream.
    /// </summary>
    /// <param name="workflowId">The workflow/stream identity.</param>
    /// <returns>The ordered list of event payload objects in the stream.</returns>
    public async Task<IReadOnlyList<object>> ReadStreamEventsAsync(Guid workflowId)
    {
        var runtime = this.RequireHost();
        await using var query = runtime.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var events = await query.Events.FetchStreamAsync(workflowId);
        return events.Select(e => e.Data).ToArray();
    }

    /// <summary>
    /// Polls the workflow's Marten stream until an event of type
    /// <typeparamref name="TEvent"/> appears or the budget elapses. Audit events
    /// can be appended through the durable outbox slightly after the tracked
    /// publish settles, so a short poll avoids a flaky read-too-early.
    /// </summary>
    /// <typeparam name="TEvent">The audit event type to wait for.</typeparam>
    /// <param name="workflowId">The workflow/stream identity.</param>
    /// <param name="budget">The wait budget. Defaults to 30 seconds.</param>
    /// <returns>
    /// The first matching event payload, or <see langword="null"/> if none
    /// appeared within the budget.
    /// </returns>
    public async Task<TEvent?> WaitForStreamEventAsync<TEvent>(
        Guid workflowId,
        TimeSpan? budget = null)
        where TEvent : class
    {
        var deadline = budget ?? TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < deadline)
        {
            var events = await this.ReadStreamEventsAsync(workflowId);
            var match = events.OfType<TEvent>().FirstOrDefault();
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }

        return null;
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
