// -----------------------------------------------------------------------
// <copyright file="RoundTripHostFixture.cs" company="Levelup Software">
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
/// Real-host fixture for the DR-15 round-trip real-host proofs (task 019) on one Wolverine + Marten
/// host backed by a real PostgreSQL container. Registers the config (retry) importable family in BOTH
/// authoring forms — the JSON import (<c>AddRoundtripConfigImportWorkflow()</c>) + its C# twin
/// (<c>AddRoundtripConfigTwinWorkflow()</c>) — for a twin-equivalence run, and the fork-join JSON
/// import (<c>AddRoundtripForkImportWorkflow()</c>) for a fork import runtime proof (its C# twin is
/// blocked by a pre-existing fork terminal-detection bug; see <c>RoundTripForkWorkflow</c>). A JSON
/// import lowered through the SAME saga emitters as a C# workflow (INV-1).
/// </summary>
/// <remarks>
/// Requires a reachable Docker daemon for the Postgres container. When Postgres is unavailable the
/// fixture fails to initialize and the runtime tests do not run; the required semantic check is the
/// BUILD compiling the imported sagas, not the runtime execution here.
/// </remarks>
public sealed class RoundTripHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>Gets the shared step-invocation log every registered workflow records into.</summary>
    public WorkflowInvocationLog Invocations { get; } = new();

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
    /// Starts the Postgres container, then the Wolverine host with Marten-backed saga storage and the
    /// round-trip workflow registrations (config import + twin, fork import).
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

                // The config family in both authoring forms (twin-equivalence), the fork import
                // (runtime proof) plus its C# twin (DR-15 twin equivalence — registered so the SKIPPED
                // ForkJoinCSharpTwin_RunsIdentically_ToJsonImport goes green once strategos#155 lands),
                // and the onFailure import (bucket-(a) compile + runtime proof) — all lowered through
                // the SAME saga emitters (INV-1).
                opts.Services.AddRoundtripForkImportWorkflow();
                opts.Services.AddRoundtripForkTwinWorkflow();
                opts.Services.AddRoundtripConfigImportWorkflow();
                opts.Services.AddRoundtripConfigTwinWorkflow();
                opts.Services.AddRoundtripOnFailureImportWorkflow();

                // The two C#-authored Branch shapes, registered on THIS host rather than a second
                // Postgres container: one whose cases both rejoin a declared terminal, and one
                // mixing a rejoining case with a workflow-ending .Complete() case. Neither runs to
                // completion on the current generator — the terminal cascades back into a branch
                // path and the workflow cycles (#175) — so the behavioral proofs are skipped, but
                // registering them here keeps the shapes compiled and resolvable.
                opts.Services.AddRoundtripBranchWorkflow();
                opts.Services.AddTerminalBranchWorkflow();

                // A handled command that starts nothing, so the harness's own completion oracle
                // can be exercised against a run that demonstrably did no work. Registered by
                // explicit type so nothing else is pulled into handler discovery.
                opts.Discovery.IncludeType(typeof(HarnessProbeCommand.Handler));

                opts.Services.AddSingleton(this.Invocations);
                opts.Services.AddResourceSetupOnStartup();
            })
            .StartAsync();
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

        var runtime = this.RequireHost();
        var budget = timeout ?? TimeSpan.FromSeconds(30);

        // Baselined rather than read absolutely, so a test that runs two workflows in one body
        // attributes each one's invocations to the call that produced them.
        var invocationsBefore = this.Invocations.TotalCount;

        try
        {
            await runtime
                .TrackActivity()
                .Timeout(budget)
                .PublishMessageAndWaitAsync(startCommand);
        }
        catch (TimeoutException)
        {
            // The tracked session may settle before the saga has routed to its terminal phase; the
            // saga-absence poll below is authoritative.
        }

        var store = runtime.Services.GetRequiredService<IDocumentStore>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var documentRemoved = false;

        while (true)
        {
            await using (var query = store.QuerySession())
            {
                documentRemoved = await query.LoadAsync<TSaga>(workflowId) is null;
            }

            var stepInvocations = this.Invocations.TotalCount - invocationsBefore;

            if (documentRemoved && stepInvocations > 0)
            {
                return new WorkflowRunOutcome(
                    Completed: true,
                    DocumentRemoved: true,
                    StepInvocations: stepInvocations,
                    Diagnostic: $"the saga for {workflowId} ran {stepInvocations} step invocation(s) "
                        + "and its document was removed, so it reached its terminal phase.");
            }

            if (stopwatch.Elapsed >= budget)
            {
                return new WorkflowRunOutcome(
                    Completed: false,
                    DocumentRemoved: documentRemoved,
                    StepInvocations: stepInvocations,
                    Diagnostic: DescribeIncompleteRun(workflowId, documentRemoved, stepInvocations, budget));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
    }

    /// <summary>
    /// Explains why a run did not count as completed, separating "never ran at all" from "ran
    /// but never terminated" — two failures with the same old boolean and very different causes.
    /// </summary>
    /// <param name="workflowId">The workflow/saga identity that was waited on.</param>
    /// <param name="documentRemoved">Whether the saga document was absent at the final poll.</param>
    /// <param name="stepInvocations">Step invocations recorded while the call was in flight.</param>
    /// <param name="budget">The per-phase wait budget.</param>
    /// <returns>The diagnostic message.</returns>
    private static string DescribeIncompleteRun(
        Guid workflowId,
        bool documentRemoved,
        int stepInvocations,
        TimeSpan budget)
    {
        if (stepInvocations == 0)
        {
            return $"no step of the saga for {workflowId} ever ran within {budget}. Its document was "
                + $"{(documentRemoved ? "never present" : "present but no step executed")}, so the run "
                + "produced no work at all — an unrouted, unhandled or misdirected start command looks "
                + "exactly like this. Document absence on its own is NOT evidence of completion.";
        }

        return $"the saga for {workflowId} ran {stepInvocations} step invocation(s) but its document was "
            + $"still present after {budget}, so it never reached its terminal phase.";
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
