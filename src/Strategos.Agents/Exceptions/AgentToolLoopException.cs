// =============================================================================
// <copyright file="AgentToolLoopException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when the chat-tool iteration count exceeds the configured maximum (DR-8).
/// </summary>
public sealed class AgentToolLoopException : AgentException
{
    public override string Diagnostic => AgentDiagnostics.AGAG005;

    /// <summary>Maximum iterations that was hit.</summary>
    public int MaxIterations { get; }

    /// <summary>Partial trace of tool-call messages observed before the limit fired.</summary>
    public IReadOnlyList<ChatMessage> PartialTrace { get; }

    public AgentToolLoopException(int maxIterations, IReadOnlyList<ChatMessage> partialTrace)
        : base($"Tool-invocation loop exceeded configured maximum of {maxIterations} iterations. Diagnostic: {AgentDiagnostics.AGAG005}.")
    {
        MaxIterations = maxIterations;
        PartialTrace = partialTrace;
    }
}
