// -----------------------------------------------------------------------
// <copyright file="ValidationHostFixture.cs" company="Levelup Software">
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
/// Runtime host fixture for the validation-guard behavioral proof (#143, G-6 6.1
/// backfill). Stands up a real PostgreSQL container and a Wolverine+Marten host
/// wired with the generated <c>AddValidationProofWorkflow()</c> registration, then
/// runs the generated saga end-to-end to observe the lowered <c>.ValidateState(...)</c>
/// Guard-Then-Dispatch path: when the guard predicate is false the saga transitions to
/// the <c>ValidationFailed</c> phase WITHOUT dispatching the guarded step's worker, and
/// without calling <c>MarkCompleted()</c> — so the saga document PERSISTS carrying the
/// failed phase.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the compensation/timeout fixtures, the validation-failed terminal does NOT
/// delete the saga document (there is no <c>MarkCompleted()</c> on the guard-failed
/// path). Terminal observation is therefore the persisted saga's <c>Phase</c>, exposed
/// here as its <c>CurrentPhaseName</c> string, rather than saga-document absence.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;ValidationHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class ValidationHostFixture : IAsyncInitializer, IAsyncDisposable
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
    /// host with Marten-backed saga storage and the generated validation-workflow
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

                // The generated validation workflow: its ValidationGuardedStep start
                // handler carries the yield-based Guard-Then-Dispatch guard lowered from
                // .ValidateState(s => s.IsAuthorized, "..."). When the guard fails it sets
                // Phase = ValidationFailed and yield-breaks before dispatching the worker.
                opts.Services.AddValidationProofWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Publishes the generated start command and awaits all synchronously-tracked
    /// cascaded activity, then loads the persisted saga and returns its current phase
    /// name. The guard-failed path does not delete the saga, so the persisted phase is
    /// the authoritative terminal signal.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type (e.g. <c>ValidationProofSaga</c>) whose phase is
    /// inspected after the run.
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="timeout">
    /// Optional wait budget for the cascading saga activity to settle. Defaults to
    /// 30 seconds.
    /// </param>
    /// <returns>
    /// The persisted saga's <c>CurrentPhaseName</c>, or <see langword="null"/> if no
    /// saga document was persisted (which would itself be a regression signal).
    /// </returns>
    public async Task<string?> RunAndGetPhaseAsync<TSaga>(
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

        if (saga is null)
        {
            return null;
        }

        // The generated saga exposes `public string CurrentPhaseName => Phase.ToString();`
        // (SagaPropertiesEmitter), so we can read the terminal phase name without
        // referencing the generated phase enum type.
        var phaseNameProperty = typeof(TSaga).GetProperty("CurrentPhaseName")
            ?? throw new InvalidOperationException(
                $"Generated saga '{typeof(TSaga).Name}' is missing the CurrentPhaseName property.");

        return (string?)phaseNameProperty.GetValue(saga);
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
