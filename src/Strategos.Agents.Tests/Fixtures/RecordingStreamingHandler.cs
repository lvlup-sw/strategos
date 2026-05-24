// =============================================================================
// <copyright file="RecordingStreamingHandler.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// Test double for <see cref="IStreamingHandler"/> that records every token and
/// completion callback in arrival order, capturing the workflow id and step name
/// observed on each call (DR-3 ordering / context-sourcing assertions).
/// </summary>
internal sealed class RecordingStreamingHandler : IStreamingHandler
{
    private readonly List<string> _tokens = new();

    /// <summary>Gets the tokens delivered via <see cref="OnTokenReceivedAsync"/>, in order.</summary>
    public IReadOnlyList<string> Tokens => _tokens;

    /// <summary>Gets the full response delivered via <see cref="OnResponseCompletedAsync"/> (null until completion).</summary>
    public string? CompletedResponse { get; private set; }

    /// <summary>Gets the count of completion callbacks observed.</summary>
    public int CompletionCount { get; private set; }

    /// <summary>Gets the workflow id observed on the most recent callback.</summary>
    public Guid? ObservedWorkflowId { get; private set; }

    /// <summary>Gets the step name observed on the most recent callback.</summary>
    public string? ObservedStepName { get; private set; }

    /// <summary>Gets the relative ordering trace ("token:..." then "complete:...").</summary>
    public IReadOnlyList<string> CallOrder => _callOrder;

    private readonly List<string> _callOrder = new();

    /// <inheritdoc/>
    public Task OnTokenReceivedAsync(
        string token,
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        _tokens.Add(token);
        _callOrder.Add($"token:{token}");
        ObservedWorkflowId = workflowId;
        ObservedStepName = stepName;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnResponseCompletedAsync(
        string fullResponse,
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        CompletedResponse = fullResponse;
        CompletionCount++;
        _callOrder.Add($"complete:{fullResponse}");
        ObservedWorkflowId = workflowId;
        ObservedStepName = stepName;
        return Task.CompletedTask;
    }
}
