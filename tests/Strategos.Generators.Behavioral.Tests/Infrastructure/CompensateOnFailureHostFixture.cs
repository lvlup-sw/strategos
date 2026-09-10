// -----------------------------------------------------------------------
// <copyright file="CompensateOnFailureHostFixture.cs" company="Levelup Software">
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
/// Runtime host fixture for the Compensate↔OnFailure interop behavioral proof
/// (#140 Task 3.2). Stands up a real PostgreSQL container and a Wolverine+Marten
/// host wired with the generated <c>AddCompensateOnFailureProofWorkflow()</c>
/// registration, then runs the generated saga end-to-end to observe the fixed
/// ordering: step compensation runs FIRST, then the workflow OnFailure chain.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors <see cref="CompensationHostFixture"/>: the trigger is published
/// from the failing step's Wolverine error chain and released through the durable
/// inbox/outbox by Wolverine's durability agent. The fixture publishes the start
/// command, awaits the synchronously-cascaded activity, then polls saga-document
/// absence until the merged compensation-then-OnFailure chain settles into the
/// terminal Failed phase.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;CompensateOnFailureHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class CompensateOnFailureHostFixture : IAsyncInitializer, IAsyncDisposable
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
    /// host with Marten-backed saga storage and the generated interop workflow
    /// registration.
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

                // The generated interop workflow declares BOTH a step-level
                // .Compensate<CofRollbackStep>() AND a workflow-level OnFailure chain.
                // Its single merged Handle(Trigger...) dispatches the compensation
                // rollback first; the rollback's completed handler then chains into
                // the OnFailure chain (#140 Task 3.2).
                opts.Services.AddCompensateOnFailureProofWorkflow();

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
    /// outbox-released trigger, the rollback worker, the OnFailure handler worker,
    /// and completion. Defaults to 60 seconds.
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
            // The error/dead-letter machinery may keep the tracked session busy past
            // the point the saga has already routed into the compensation-then-
            // OnFailure chain; fall through to the saga-absence poll, which is the
            // authoritative terminal signal.
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
