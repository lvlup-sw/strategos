// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkHostFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

using JasperFx.Resources;

using Marten;
using Marten.Exceptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Strategos.Generators.Behavioral.Tests.Workflows;

using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
using Wolverine.Tracking;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Runtime host fixture for the diagnostic-fork lowering behavioral proofs (DR-9, #151).
/// Stands up a real PostgreSQL container and a Wolverine+Marten host with the generated
/// <c>AddDiagnosticForkProofWorkflow()</c> registration, then drives the generated saga's
/// single fork decision site (<c>Handle(ForkDiagnosticForkProofCommand)</c>) to observe
/// the anchor / permitted-trigger + evidence / maxForks guards, the WorkflowForked audit
/// event, and the compensation seeding into the merged trigger site (#140).
/// </summary>
/// <remarks>
/// <para>
/// The fork decision command carries <c>[SagaIdentity]</c>, so it routes to a live saga.
/// The proofs seed the saga at its anchor phase directly (via <see cref="SeedSagaAsync"/>)
/// and then publish the fork command, which is a deterministic way to place the saga at a
/// declared fork anchor without racing the happy path to completion.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session and mark
/// consumers <c>[NotInParallel]</c> because the host + invocation log are process-shared.
/// </para>
/// </remarks>
public sealed class DiagnosticForkHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps push their name
    /// here so a test can assert which steps ran and how many times.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts a Wolverine host with
    /// a Marten event store integrated with Wolverine and the generated diagnostic-fork
    /// workflow registration.
    /// </summary>
    /// <returns>A task that completes when the host is running.</returns>
    public async Task InitializeAsync()
    {
        await this.postgres.InitializeAsync();

        this.host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                var concurrencyCooldown = new[]
                {
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromMilliseconds(400),
                    TimeSpan.FromMilliseconds(800),
                };

                opts.OnException(ex =>
                        ex is ConcurrentUpdateException
                        || ex.GetType().Name.Contains("EventStreamUnexpected", StringComparison.Ordinal))
                    .RetryWithCooldown(concurrencyCooldown);

                opts.Services
                    .AddMarten(storeOptions =>
                    {
                        storeOptions.Connection(this.postgres.ConnectionString);
                        storeOptions.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                    })
                    .IntegrateWithWolverine()
                    .ApplyAllDatabaseChangesOnStartup();

                // The generated diagnostic-fork proof workflow registration: registers the
                // saga's step types, worker handlers, and the inline snapshot projection.
                opts.Services.AddDiagnosticForkProofWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Seeds a saga document directly at a chosen phase so a fork decision command can be
    /// published to a live saga sitting at a declared anchor, without racing the happy
    /// path to completion.
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type.</typeparam>
    /// <param name="saga">The saga document to persist.</param>
    /// <returns>A task that completes when the saga is persisted.</returns>
    public async Task SeedSagaAsync<TSaga>(TSaga saga)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(saga, nameof(saga));

        var runtime = this.RequireHost();
        await using var session = runtime.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        session.Store(saga);
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Publishes the fork decision command and awaits all synchronously-tracked cascaded
    /// activity (the guard evaluation and, on a valid fork, the seeded compensation).
    /// </summary>
    /// <param name="forkCommand">The generated fork decision command.</param>
    /// <param name="timeout">Optional wait budget. Defaults to 30 seconds.</param>
    /// <returns>A task that completes when the tracked activity settles.</returns>
    public async Task PublishForkAsync(object forkCommand, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(forkCommand, nameof(forkCommand));

        var runtime = this.RequireHost();

        try
        {
            await runtime
                .TrackActivity()
                .Timeout(timeout ?? TimeSpan.FromSeconds(30))
                .DoNotAssertOnExceptionsDetected()
                .PublishMessageAndWaitAsync(forkCommand);
        }
        catch (TimeoutException)
        {
            // The seeded compensation may release through the durable inbox/outbox past
            // the tracked window; the proofs poll the invocation log / stream / saga doc
            // afterwards, which is the authoritative signal.
        }
    }

    /// <summary>
    /// Reloads a saga document, or returns <see langword="null"/> when it has been removed
    /// by <c>MarkCompleted()</c> (a terminal fork route).
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type.</typeparam>
    /// <param name="workflowId">The saga identity.</param>
    /// <returns>The saga document, or <see langword="null"/> if absent.</returns>
    public async Task<TSaga?> LoadSagaAsync<TSaga>(Guid workflowId)
        where TSaga : class
    {
        var runtime = this.RequireHost();
        await using var query = runtime.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await query.LoadAsync<TSaga>(workflowId);
    }

    /// <summary>
    /// Polls the workflow's Marten stream until an event of type <typeparamref name="TEvent"/>
    /// appears or the budget elapses.
    /// </summary>
    /// <typeparam name="TEvent">The audit event type to wait for.</typeparam>
    /// <param name="workflowId">The workflow/stream identity.</param>
    /// <param name="budget">The wait budget. Defaults to 15 seconds.</param>
    /// <returns>The first matching event payload, or <see langword="null"/> if none appeared.</returns>
    public async Task<TEvent?> WaitForStreamEventAsync<TEvent>(
        Guid workflowId,
        TimeSpan? budget = null)
        where TEvent : class
    {
        var runtime = this.RequireHost();
        var deadline = budget ?? TimeSpan.FromSeconds(15);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < deadline)
        {
            await using var query = runtime.Services.GetRequiredService<IDocumentStore>().QuerySession();
            var events = await query.Events.FetchStreamAsync(workflowId);
            var match = events.Select(e => e.Data).OfType<TEvent>().FirstOrDefault();
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
