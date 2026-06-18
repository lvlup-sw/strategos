// -----------------------------------------------------------------------
// <copyright file="ContextHostFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using JasperFx.Resources;

using Marten;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Strategos.Generators.Behavioral.Tests.Workflows;
using Strategos.Ontology.ObjectSets;

using Wolverine;
using Wolverine.Marten;
using Wolverine.Tracking;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Runtime host fixture for the context-assembly behavioral proof (DR-6 T016).
/// Stands up a real PostgreSQL container and a Wolverine+Marten host wired with
/// the generated <c>AddContextFlowWorkflow()</c> registration, then runs the
/// generated saga end-to-end so the lowered <c>{Step}ContextAssembler</c> is
/// exercised on the real container.
/// </summary>
/// <remarks>
/// <para>
/// Dedicated rather than reusing <see cref="WolverineHostFixture"/> because the
/// generated <c>EnrichStepContextAssembler</c> takes an
/// <c>IObjectSetProvider</c> dependency; this fixture registers the recording
/// <see cref="StubObjectSetProvider"/> (and the shared <see cref="ContextProbe"/>)
/// so the assembler resolves and its <c>ExecuteSimilarityAsync</c> call is
/// observable. The generated DI registration only wires the assembler type
/// itself; the provider is the host's responsibility, exactly as a real ontology
/// host would register its backend.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;ContextHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class ContextHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps push their
    /// name here so a test can assert which steps ran and how many times.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Gets the shared observation probe. The stub provider records its
    /// <c>ExecuteSimilarityAsync</c> expression here and the context-aware step
    /// records the assembled context it received.
    /// </summary>
    public ContextProbe Probe { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts the Wolverine
    /// host with Marten-backed saga storage, the generated context workflow, and
    /// the stub object-set provider that backs the generated assembler.
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

                // The generated context workflow: AddContextFlowWorkflow() registers
                // the steps, worker handlers, AND the lowered EnrichStepContextAssembler
                // (DR-6). The assembler's IObjectSetProvider dependency is the host's
                // to supply — here the recording stub below.
                opts.Services.AddContextFlowWorkflow();

                // The recording object-set provider that backs the generated
                // assembler's retrieval source, plus the shared probe both it and the
                // context-aware step write their observations to.
                opts.Services.AddSingleton(this.Probe);
                opts.Services.AddSingleton<IObjectSetProvider>(_ => new StubObjectSetProvider(this.Probe));

                // The shared invocation log injected into instrumented steps.
                opts.Services.AddSingleton(this.Invocations);

                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    /// <summary>
    /// Runs the generated context workflow saga to completion against the real
    /// host, awaiting all cascaded saga + worker activity via Wolverine's
    /// message-tracking API.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type whose persistence is polled to confirm
    /// terminal completion (its absence signals the saga finished).
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="timeout">Optional wait budget. Defaults to 30 seconds.</param>
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
