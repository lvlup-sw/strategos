// =============================================================================
// <copyright file="ThrowingStreamingHandler.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// <see cref="IStreamingHandler"/> test double whose first token callback throws.
/// Used to assert the orchestrator wraps handler faults as
/// <c>AgentStreamingException</c> (AGAG009) without mutating caller state (DR-4).
/// </summary>
internal sealed class ThrowingStreamingHandler : IStreamingHandler
{
    public Task OnTokenReceivedAsync(
        string token,
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("handler boom");

    public Task OnResponseCompletedAsync(
        string fullResponse,
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("handler boom");
}
