// -----------------------------------------------------------------------
// <copyright file="WolverineHostFixture.cs" company="Levelup Software">
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
/// Reusable runtime host fixture that compiles a Strategos workflow (the
/// source generator emits its Wolverine+Marten saga) and RUNS it end-to-end
/// against a real PostgreSQL container.
/// </summary>
/// <remarks>
/// <para>
/// This is the engine that later behavioral tasks (retry/timeout/compensation/
/// confidence) build on. It owns:
/// <list type="bullet">
///   <item><description>
///     A single shared <see cref="PostgresFixture"/> container for the session.
///   </description></item>
///   <item><description>
///     A single Wolverine host wired with Marten-backed saga storage and the
///     durable inbox/outbox (via <c>IntegrateWithWolverine()</c>), plus the
///     generated <c>AddHappyPathWorkflow()</c> registration.
///   </description></item>
///   <item><description>
///     A shared <see cref="WorkflowInvocationLog"/> singleton so instrumented
///     steps can be observed at runtime.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session
/// with
/// <c>[ClassDataSource&lt;WolverineHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class WolverineHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps push
    /// their name here so a test can assert which steps ran and how many times.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts the
    /// Wolverine host with Marten-backed saga storage and the generated
    /// workflow registration.
    /// </summary>
    /// <returns>A task that completes when the host is running.</returns>
    public async Task InitializeAsync()
    {
        await this.postgres.InitializeAsync();

        this.host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                // Marten owns saga storage + the durable inbox/outbox. The
                // generated AddHappyPathWorkflow() registers the saga's step
                // types, worker handlers, and the workflow definition; the
                // saga document type itself is discovered by Wolverine's type
                // scanning of the assembly under test.
                opts.Services
                    .AddMarten(storeOptions =>
                    {
                        storeOptions.Connection(this.postgres.ConnectionString);

                        // Dev-style schema provisioning: create the saga +
                        // Wolverine envelope tables on first use.
                        storeOptions.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                    })
                    .IntegrateWithWolverine()
                    .ApplyAllDatabaseChangesOnStartup();

                opts.Services.AddHappyPathWorkflow();

                // The retry-proof workflow (DR-2 T011): its generated
                // RetryFlakyStepHandler carries the static Configure(HandlerChain)
                // retry policy lowered from .WithRetry(2). Registered alongside the
                // happy-path workflow on the same host; both share the singleton
                // invocation log below.
                opts.Services.AddRetryProofWorkflow();

                // The confidence-gate workflows (DR-5 T014). Each generated saga
                // carries the confidence-gated completed handler that compares the
                // step result confidence to the 0.85 threshold and either routes to
                // the lowered OnLowConfidence handler step (low-confidence case) or
                // proceeds on the primary path (high-confidence case). Both share the
                // singleton invocation log below.
                opts.Services.AddLowConfidenceWorkflow();
                opts.Services.AddHighConfidenceWorkflow();

                // The shared invocation log injected into instrumented steps.
                opts.Services.AddSingleton(this.Invocations);

                // Provision Wolverine's own durable storage objects on startup.
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Runs the happy-path fixture workflow saga to completion against the real
    /// host. Convenience overload pinned to <see cref="HappyPathSaga"/>.
    /// </summary>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">
    /// The generated start command (<c>StartHappyPathCommand</c>) that kicks off
    /// the saga.
    /// </param>
    /// <param name="timeout">
    /// Optional wait budget for the cascading saga + worker activity to settle.
    /// Defaults to 30 seconds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the saga reached its terminal phase.
    /// </returns>
    public Task<bool> RunWorkflowAsync(
        Guid workflowId,
        object startCommand,
        TimeSpan? timeout = null) =>
        this.RunWorkflowAsync<HappyPathSaga>(workflowId, startCommand, timeout);

    /// <summary>
    /// Runs a generated workflow saga to completion against the real host.
    /// Reusable by later behavioral tasks (retry/timeout/compensation/
    /// confidence) over any generated saga type.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type (e.g. <c>HappyPathSaga</c>) whose
    /// persistence is polled to confirm terminal completion.
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">
    /// The generated start command (e.g. <c>StartHappyPathCommand</c>) that
    /// kicks off the saga.
    /// </param>
    /// <param name="timeout">
    /// Optional wait budget for the cascading saga + worker activity to settle.
    /// Defaults to 30 seconds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the saga reached its terminal phase — the
    /// generated final-step handler calls <c>MarkCompleted()</c>, which removes
    /// the persisted saga document; an absent document after the run is the
    /// completion signal.
    /// </returns>
    /// <remarks>
    /// Uses Wolverine's message-tracking API to deterministically await all
    /// cascaded processing (saga start → step start → worker → step completed →
    /// next step → … → MarkCompleted) rather than sleeping on a poll loop.
    /// </remarks>
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

        // The saga document is deleted by MarkCompleted() on the terminal
        // step, so its absence after the tracked run settles means the workflow
        // reached its Completed phase.
        await using var query = runtime.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var saga = await query.LoadAsync<TSaga>(workflowId);

        return saga is null;
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
