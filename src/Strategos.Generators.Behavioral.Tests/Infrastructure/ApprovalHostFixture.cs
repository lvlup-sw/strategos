// -----------------------------------------------------------------------
// <copyright file="ApprovalHostFixture.cs" company="Levelup Software">
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

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Runtime host fixture for the C#-authored approval checkpoint. Stands up a real
/// PostgreSQL container and a Wolverine + Marten host carrying the generated
/// <c>AddCreditLimitReviewWorkflow()</c> and <c>AddPurchaseRequisitionReviewWorkflow()</c>
/// registrations, so an <c>AwaitApproval</c> can be driven end to end on both the approved
/// and the rejected route.
/// </summary>
/// <remarks>
/// <para>
/// A generated workflow only ever runs if some host fixture calls its generated
/// <c>Add{Pascal}Workflow()</c>. Without this fixture the approval saga compiles and its
/// step types resolve — which is all the existing JSON-import approval proof asserts —
/// and no saga is ever started.
/// </para>
/// <para>
/// Two workflows share the one host and the one container: the credit-limit review, whose
/// broker approves, and the purchase-requisition review, whose broker refuses so the
/// multi-step rejection chain is actually walked.
/// </para>
/// <para>
/// The approval decisions themselves are brokered by
/// <see cref="CreditOfficerApprovalDecisionHandler"/> and
/// <see cref="PurchasingManagerApprovalDecisionHandler"/>, registered here BY EXPLICIT TYPE
/// rather than by naming convention so nothing else in the assembly is drawn into
/// handler discovery alongside them.
/// </para>
/// <para>
/// Lifecycle is driven by TUnit via <see cref="IAsyncInitializer"/> /
/// <see cref="IAsyncDisposable"/>. Share one instance for the whole session with
/// <c>[ClassDataSource&lt;ApprovalHostFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// </remarks>
public sealed class ApprovalHostFixture : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgresFixture postgres = new();

    private IHost? host;

    /// <summary>
    /// Gets the shared step-invocation log. Instrumented workflow steps — and the
    /// approval-decision broker — push their name here so a test can assert which steps
    /// ran, how many times, and in what order.
    /// </summary>
    public WorkflowInvocationLog Invocations { get; } = new();

    /// <summary>
    /// Starts the shared Postgres container, then builds and starts the Wolverine host
    /// with Marten-backed saga storage and the generated approval workflow registration.
    /// </summary>
    /// <returns>A task that completes when the host is running.</returns>
    public async Task InitializeAsync()
    {
        await this.postgres.InitializeAsync();

        try
        {
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

                    opts.Services.AddCreditLimitReviewWorkflow();
                    opts.Services.AddPurchaseRequisitionReviewWorkflow();

                    // The brokers that answer each saga's request-approval event. Registered
                    // by explicit type so handler discovery pulls in these two classes and
                    // nothing else from the test assembly.
                    opts.Discovery.IncludeType(typeof(CreditOfficerApprovalDecisionHandler));
                    opts.Discovery.IncludeType(typeof(PurchasingManagerApprovalDecisionHandler));

                    opts.Services.AddSingleton(this.Invocations);
                    opts.Services.AddResourceSetupOnStartup();
                })
                .StartAsync();
        }
        catch
        {
            // If host startup throws after the container is up, dispose the Postgres
            // fixture so a failed Initialize does not leak a container for the run.
            await this.postgres.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Runs a generated workflow saga and reports the full outcome: whether it completed,
    /// whether its document was removed, and how many step invocations it produced.
    /// </summary>
    /// <typeparam name="TSaga">
    /// The generated saga document type polled for terminal completion.
    /// </typeparam>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command.</param>
    /// <param name="timeout">The per-phase wait budget. Defaults to 30 seconds.</param>
    /// <returns>The observed outcome of the run.</returns>
    /// <remarks>
    /// Document absence alone is not accepted as completion: the probe additionally
    /// requires that the shared invocation log grew while the call was in flight, so a
    /// saga that was never created cannot be mistaken for one that finished.
    /// </remarks>
    public Task<WorkflowRunOutcome> RunWorkflowWithOutcomeAsync<TSaga>(
        Guid workflowId,
        object startCommand,
        TimeSpan? timeout = null)
        where TSaga : class =>
        SagaCompletionProbe.RunAsync<TSaga>(
            this.RequireHost(),
            this.Invocations,
            workflowId,
            startCommand,
            timeout ?? TimeSpan.FromSeconds(30));

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
