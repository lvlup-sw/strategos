// -----------------------------------------------------------------------
// <copyright file="RoundTripHostFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

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
/// Real-host fixture for the DR-15 round-trip real-host proofs (task 019) on one Wolverine + Marten
/// host backed by a real PostgreSQL container. Registers the config (retry) importable family in BOTH
/// authoring forms — the JSON import (<c>AddRoundtripConfigImportWorkflow()</c>) + its C# twin
/// (<c>AddRoundtripConfigTwinWorkflow()</c>) — for a twin-equivalence run, the fork-join family in
/// both authoring forms, and the onFailure JSON import. A JSON import lowered through the SAME saga
/// emitters as a C# workflow (INV-1).
/// </summary>
/// <remarks>
/// <para>
/// Requires a reachable Docker daemon for the Postgres container. When Postgres is unavailable the
/// fixture fails to initialize and the runtime tests do not run; the required semantic check is the
/// BUILD compiling the imported sagas, not the runtime execution here.
/// </para>
/// <para>
/// The four-test class shares this fixture (<c>SharedType.PerClass</c>) and one Postgres container.
/// A prior family's leftover Wolverine inbox/outbox work and the shared invocation log are what made
/// <c>ForkJoinCSharpTwin_RunsIdentically_ToJsonImport</c> time out in a full-class run (strategos#180)
/// even though the same import passes in isolation. <see cref="RecycleHostAsync"/> is the isolation:
/// stop the host, replace the log, and start a fresh host against a new Marten/Wolverine schema on
/// the SAME container — not a second Postgres.
/// </para>
/// </remarks>
public sealed class RoundTripHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    private int hostEpoch;

    /// <summary>Gets the step-invocation log the current host's workflows record into.</summary>
    /// <remarks>
    /// <see cref="RecycleHostAsync"/> replaces this instance so a prior test's still-running
    /// writer cannot inflate the next test's counts (strategos#180).
    /// </remarks>
    public WorkflowInvocationLog Invocations { get; private set; } = new();

    /// <summary>
    /// Gets the running host's service provider, so a test can assert that a workflow's generated
    /// registration actually took effect without executing the workflow.
    /// </summary>
    /// <remarks>
    /// A workflow that does not terminate cannot be proven registered by running it, so the step
    /// types the generated <c>Add{Name}Workflow()</c> registers are resolved directly instead.
    /// </remarks>
    public IServiceProvider Services => this.RequireHost().Services;

    /// <summary>
    /// Gets the connection string of the fixture's Postgres container, so a test can read the raw
    /// <c>mt_doc_*</c> row a saga was persisted as.
    /// </summary>
    /// <remarks>
    /// Reading through Marten would deserialize the document and hide the stored representation,
    /// which is exactly what a persistence-shape assertion has to see. Recycle keeps this
    /// container; only the Marten/Wolverine schema on it changes.
    /// </remarks>
    public string ConnectionString => this.postgres.ConnectionString;

    /// <summary>
    /// Starts the Postgres container, then the Wolverine host with Marten-backed saga storage and the
    /// round-trip workflow registrations (config import + twin, fork import + twin, onFailure import).
    /// </summary>
    /// <returns>A task that completes when the host is running.</returns>
    public async Task InitializeAsync()
    {
        await this.postgres.InitializeAsync();
        await this.StartHostAsync(schemaName: null);
    }

    /// <summary>
    /// Stops the current Wolverine host and starts a fresh one on the same Postgres container,
    /// with a new invocation log and a new Marten/Wolverine schema.
    /// </summary>
    /// <returns>A task that completes when the replacement host is running.</returns>
    /// <remarks>
    /// This is the smallest isolation that makes <c>RoundTripBehavioralTests</c> deterministic
    /// (strategos#180). Stopping the host kills in-memory leftover work; a new log means a
    /// still-running writer from the old host cannot Reset()-race the next test; a new schema
    /// means durable inbox/outbox envelopes from a prior family are not redelivered into the
    /// next run. The Postgres container is reused so this is not a second container.
    /// </remarks>
    public async Task RecycleHostAsync()
    {
        await this.StopHostAsync();

        var epoch = Interlocked.Increment(ref this.hostEpoch);
        var schemaName = "rt_" + epoch.ToString(CultureInfo.InvariantCulture);
        await this.StartHostAsync(schemaName);
    }

    /// <summary>
    /// Runs a generated workflow saga to completion against the real host, then POLLS until it
    /// reaches its terminal phase (its document is removed by <c>MarkCompleted()</c>) or the budget
    /// elapses. Polling is authoritative because a fork saga's parallel-path/join cascade can settle
    /// the tracked activity session before the join and terminal step have run.
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type polled for terminal completion.</typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command that kicks off the saga.</param>
    /// <param name="timeout">Optional per-phase wait budget (defaults to 30 seconds); see the remarks for how it bounds the two-phase wait.</param>
    /// <returns><see langword="true"/> when the saga demonstrably RAN and reached its terminal phase within the budget.</returns>
    /// <remarks>
    /// <para>
    /// The wait runs in two phases, each bounded by the budget: first the tracked-activity
    /// <c>Timeout(budget)</c> wait, then — on <see cref="TimeoutException"/> — an authoritative
    /// saga-absence poll for up to a further budget. So on the failure path (the saga never
    /// routes to its terminal phase) the total wait is up to ~2× the budget, not one budget.
    /// </para>
    /// <para>
    /// Document absence ALONE is not accepted as proof of completion. A saga that was never
    /// created is absent for the whole poll, so an unrouted, unhandled or misdirected start
    /// command used to be indistinguishable from a workflow that ran and completed — and the
    /// first poll can also observe absence before the start command has even been handled.
    /// Completion therefore requires positive evidence that work happened: the shared
    /// invocation log must have grown while this call was in flight.
    /// </para>
    /// </remarks>
    public async Task<bool> RunWorkflowAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? timeout = null)
        where TSaga : class
    {
        var outcome = await this.RunWorkflowWithOutcomeAsync<TSaga>(workflowId, startCommand, timeout);
        return outcome.Completed;
    }

    /// <summary>
    /// Runs a generated workflow saga as <see cref="RunWorkflowAsync{TSaga}"/> does, and reports
    /// the full outcome: whether it completed, whether its document was removed, how many step
    /// invocations it produced, and a diagnostic distinguishing the failure modes.
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type polled for terminal completion.</typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command that kicks off the saga.</param>
    /// <param name="timeout">Optional per-phase wait budget (defaults to 30 seconds).</param>
    /// <returns>The observed outcome of the run.</returns>
    public async Task<WorkflowRunOutcome> RunWorkflowWithOutcomeAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? timeout = null)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(startCommand, nameof(startCommand));

        // One implementation of the completion oracle, shared with ApprovalHostFixture and
        // FailureHandlerHostFixture. It previously lived here as a second copy; a correction to
        // the acceptance predicate would have had to land in both, and only this one was pinned.
        var outcome = await SagaCompletionProbe.RunAsync<TSaga>(
            this.RequireHost(),
            this.Invocations,
            workflowId,
            startCommand,
            timeout ?? TimeSpan.FromSeconds(30));

        return AnnotateSharedHostInterference(outcome);
    }

    /// <summary>Stops and disposes the host and the shared Postgres container.</summary>
    /// <returns>A value task that completes when teardown finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        await this.StopHostAsync();
        await this.postgres.DisposeAsync();
    }

    /// <summary>
    /// Names the shared-host interference when a run does not complete, so a full-class timeout
    /// reports what the PerClass log actually recorded instead of an opaque false (strategos#180).
    /// </summary>
    /// <param name="outcome">The oracle's observed outcome.</param>
    /// <returns>The same outcome, with an interference clause on the failure diagnostic.</returns>
    private WorkflowRunOutcome AnnotateSharedHostInterference(WorkflowRunOutcome outcome)
    {
        if (outcome.Completed)
        {
            return outcome;
        }

        var recorded = this.Invocations.Invocations;
        if (recorded.Count == 0)
        {
            return outcome with
            {
                Diagnostic = outcome.Diagnostic
                    + " Shared-host interference: the PerClass invocation log is empty, so this run "
                    + "produced no work — a prior test's saga still occupying the Wolverine agent is "
                    + "the usual cause (strategos#180).",
            };
        }

        var distinct = recorded.Distinct(StringComparer.Ordinal).ToArray();
        return outcome with
        {
            Diagnostic = outcome.Diagnostic
                + " Shared-host interference: the PerClass invocation log recorded ["
                + string.Join(", ", distinct)
                + "] while this run was in flight (strategos#180).",
        };
    }

    private async Task StartHostAsync(string? schemaName)
    {
        this.Invocations = new WorkflowInvocationLog();

        this.host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                if (schemaName is not null)
                {
                    // Isolate durable inbox/outbox from the previous host epoch. The same
                    // physical Postgres is reused; only the schema changes.
                    opts.Durability.MessageStorageSchemaName = schemaName;
                }

                opts.Services
                    .AddMarten(storeOptions =>
                    {
                        storeOptions.Connection(this.postgres.ConnectionString);
                        storeOptions.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;
                        if (schemaName is not null)
                        {
                            storeOptions.DatabaseSchemaName = schemaName;
                        }
                    })
                    .IntegrateWithWolverine()
                    .ApplyAllDatabaseChangesOnStartup();

                // The config family in both authoring forms (twin-equivalence), the fork family
                // in both authoring forms (DR-15 twin equivalence; strategos#155 is fixed and
                // strategos#180 is the host-isolation that lets the twin run in the class),
                // and the onFailure import (bucket-(a) compile + runtime proof) — all lowered
                // through the SAME saga emitters (INV-1).
                opts.Services.AddRoundtripForkImportWorkflow();
                opts.Services.AddRoundtripForkTwinWorkflow();
                opts.Services.AddRoundtripConfigImportWorkflow();
                opts.Services.AddRoundtripConfigTwinWorkflow();
                opts.Services.AddRoundtripOnFailureImportWorkflow();

                // The C#-authored Branch shapes, registered on THIS host rather than a second
                // Postgres container: one whose cases both rejoin a declared terminal, one mixing
                // a rejoining case with a workflow-ending .Complete() case, and one carrying a
                // confidence gate on the last step of each case kind.
                opts.Services.AddRoundtripBranchWorkflow();
                opts.Services.AddTerminalBranchWorkflow();
                opts.Services.AddBranchCaseConfidenceWorkflow();

                // A handled command that starts nothing, so the harness's own completion oracle
                // can be exercised against a run that demonstrably did no work. Registered by
                // explicit type so nothing else is pulled into handler discovery.
                opts.Discovery.IncludeType(typeof(HarnessProbeCommand.Handler));

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
    }

    private async Task StopHostAsync()
    {
        if (this.host is null)
        {
            return;
        }

        await this.host.StopAsync();
        this.host.Dispose();
        this.host = null;
    }

    private IHost RequireHost() =>
        this.host ?? throw new InvalidOperationException(
            "Host not initialized. Ensure InitializeAsync ran (TUnit IAsyncInitializer).");
}
