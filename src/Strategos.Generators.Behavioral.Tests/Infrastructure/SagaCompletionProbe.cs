// -----------------------------------------------------------------------
// <copyright file="SagaCompletionProbe.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;

using Marten;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.Tracking;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Publishes a generated start command on a running host and reports what the run
/// actually did, so "the workflow completed" is backed by evidence rather than by the
/// absence of a document.
/// </summary>
/// <remarks>
/// <para>
/// The Marten saga document being gone is the signal that <c>MarkCompleted()</c> ran —
/// but on its own it proves nothing, because a saga that was never created is absent
/// for the whole poll too. An unrouted, unhandled or misdirected start command looks
/// exactly like a workflow that ran and finished. Completion therefore additionally
/// requires that the shared invocation log GREW while the call was in flight.
/// </para>
/// <para>
/// The delta is measured rather than the absolute count, so a test body that starts
/// more than one workflow attributes each run's invocations to the call that produced
/// them, and so a workflow left running by an earlier test cannot manufacture evidence
/// for this one.
/// </para>
/// </remarks>
internal static class SagaCompletionProbe
{
    /// <summary>
    /// Publishes <paramref name="startCommand"/>, waits for the tracked cascade to
    /// settle, then polls the saga document until the run either completes or the
    /// budget elapses.
    /// </summary>
    /// <typeparam name="TSaga">The generated saga document type polled for terminal completion.</typeparam>
    /// <param name="host">The running Wolverine + Marten host.</param>
    /// <param name="invocations">The shared step-invocation log the fixture's steps record into.</param>
    /// <param name="workflowId">The workflow/saga identity to wait on.</param>
    /// <param name="startCommand">The generated start command that kicks off the saga.</param>
    /// <param name="budget">The per-phase wait budget.</param>
    /// <returns>The observed outcome of the run.</returns>
    /// <remarks>
    /// The wait runs in two phases, each bounded by <paramref name="budget"/>: first the
    /// tracked-activity <c>Timeout(budget)</c> wait, then — on <see cref="TimeoutException"/> — an
    /// authoritative saga-absence poll for up to a further budget. On the failure path (the saga
    /// never routes to its terminal phase) the total wait is therefore up to ~2× the budget, not
    /// one budget. Callers sizing a suite-wide timeout need that figure, not the nominal one.
    /// </remarks>
    public static async Task<WorkflowRunOutcome> RunAsync<TSaga>(
        IHost host,
        WorkflowInvocationLog invocations,
        Guid workflowId,
        object startCommand,
        TimeSpan budget)
        where TSaga : class
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(invocations);
        ArgumentNullException.ThrowIfNull(startCommand);

        var invocationsBefore = invocations.TotalCount;

        try
        {
            await host
                .TrackActivity()
                .Timeout(budget)
                .PublishMessageAndWaitAsync(startCommand);
        }
        catch (TimeoutException)
        {
            // The tracked session can settle before the saga has routed to its terminal
            // phase; the document poll below is the authoritative signal.
        }

        var store = host.Services.GetRequiredService<IDocumentStore>();
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            bool documentRemoved;
            await using (var query = store.QuerySession())
            {
                documentRemoved = await query.LoadAsync<TSaga>(workflowId) is null;
            }

            var stepInvocations = invocations.TotalCount - invocationsBefore;

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
                    Diagnostic: Describe(workflowId, documentRemoved, stepInvocations, budget));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
    }

    /// <summary>
    /// Explains why a run did not count as completed, separating "never ran at all"
    /// from "ran but never terminated" — two failures a single boolean cannot tell
    /// apart, with very different causes.
    /// </summary>
    /// <param name="workflowId">The workflow/saga identity that was waited on.</param>
    /// <param name="documentRemoved">Whether the saga document was absent at the final poll.</param>
    /// <param name="stepInvocations">Step invocations recorded while the call was in flight.</param>
    /// <param name="budget">The per-phase wait budget.</param>
    /// <returns>The diagnostic message.</returns>
    private static string Describe(
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
}
