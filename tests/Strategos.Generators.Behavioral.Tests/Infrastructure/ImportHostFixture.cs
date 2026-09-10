// -----------------------------------------------------------------------
// <copyright file="ImportHostFixture.cs" company="Levelup Software">
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
/// Real-host fixture for the JSON import keystone (task 017). Registers BOTH the gate-bearing
/// JSON-imported workflow (<c>AddImportGateWorkflow()</c> — generated at build time from
/// <c>import-gate.workflow.json</c> by the source-generator import front-end) and its gate-free
/// C#-authored twin (<c>AddImportTwinWorkflow()</c>) on one Wolverine + Marten host backed by a
/// real PostgreSQL container, so a test can run both to completion and assert they behave
/// identically (DR-3: gates are inert, the imported saga is behaviorally the twin's).
/// </summary>
/// <remarks>
/// Requires a reachable Docker daemon for the Postgres container. When Postgres is unavailable the
/// fixture fails to initialize and the runtime tests do not run; the required semantic check is the
/// BUILD compiling the imported saga (which <c>JsonWorkflowImportTests</c>'s non-host tests force),
/// not the runtime execution.
/// </remarks>
public sealed class ImportHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>Gets the shared step-invocation log both workflows record into.</summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the Postgres container, then the Wolverine host with Marten-backed saga storage and
    /// both the imported and twin workflow registrations.
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

                // The gate-bearing JSON import and its gate-free C# twin, lowered through the SAME
                // saga emitters. Both share the invocation log below.
                opts.Services.AddImportGateWorkflow();
                opts.Services.AddImportTwinWorkflow();

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Runs a generated workflow saga to completion against the real host, returning whether it
    /// reached its terminal phase (the saga document is removed by <c>MarkCompleted()</c>).
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type polled for terminal completion.</typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command that kicks off the saga.</param>
    /// <param name="timeout">Optional wait budget (defaults to 30 seconds).</param>
    /// <returns><see langword="true"/> when the saga reached its terminal phase.</returns>
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
