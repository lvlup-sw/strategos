// -----------------------------------------------------------------------
// <copyright file="WorkflowInvocationLog.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Thread-safe, process-shared record of step invocations for behavioral
/// tests. Instrumented workflow steps push their name here when executed so a
/// test can observe runtime behavior (which steps ran, how many times, in what
/// order) without mocking the saga or the message bus.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a DI singleton on the host so every step resolved by the
/// generated <c>Add{Name}Workflow()</c> registration shares the same instance.
/// Later behavioral tasks (retry/timeout/compensation/confidence) reuse this to
/// assert invocation counts (e.g. a flaky step retried N times).
/// </para>
/// <para>
/// A single host is shared for the whole test session, so
/// <see cref="Reset"/> clears the log at the start of each test to isolate it
/// from prior runs.
/// </para>
/// </remarks>
public sealed class WorkflowInvocationLog
{
    private readonly ConcurrentQueue<string> invocations = new();

    /// <summary>
    /// Gets the ordered sequence of step names recorded so far.
    /// </summary>
    public IReadOnlyList<string> Invocations => this.invocations.ToArray();

    /// <summary>
    /// Gets the total number of step invocations recorded.
    /// </summary>
    public int TotalCount => this.invocations.Count;

    /// <summary>
    /// Records that the named step executed.
    /// </summary>
    /// <param name="stepName">The step name (typically <c>nameof(TStep)</c>).</param>
    public void Record(string stepName)
    {
        ArgumentNullException.ThrowIfNull(stepName, nameof(stepName));

        this.invocations.Enqueue(stepName);
    }

    /// <summary>
    /// Counts how many times the named step has been recorded.
    /// </summary>
    /// <param name="stepName">The step name to count.</param>
    /// <returns>The number of recorded invocations for that step.</returns>
    public int CountFor(string stepName)
    {
        ArgumentNullException.ThrowIfNull(stepName, nameof(stepName));

        return this.invocations.Count(name => string.Equals(name, stepName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Clears all recorded invocations. Called at the start of each test to
    /// isolate it from runs that shared the same session-scoped host.
    /// </summary>
    public void Reset()
    {
        this.invocations.Clear();
    }
}
