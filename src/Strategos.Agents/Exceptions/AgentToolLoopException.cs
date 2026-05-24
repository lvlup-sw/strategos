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
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG005"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG005;

    /// <summary>Maximum iterations that was hit.</summary>
    public int MaxIterations { get; }

    /// <summary>Partial trace of tool-call messages observed before the limit fired.</summary>
    public IReadOnlyList<ChatMessage> PartialTrace { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentToolLoopException"/> class.
    /// </summary>
    /// <param name="maxIterations">The configured tool-iteration maximum that was hit.</param>
    /// <param name="partialTrace">The tool-call messages observed before the limit fired.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxIterations"/> is negative or zero.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partialTrace"/> is <see langword="null"/>.</exception>
    public AgentToolLoopException(int maxIterations, IReadOnlyList<ChatMessage> partialTrace)
        : base($"Tool-invocation loop exceeded configured maximum of {maxIterations} iterations. Diagnostic: {AgentDiagnostics.AGAG005}.")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
        ArgumentNullException.ThrowIfNull(partialTrace);

        MaxIterations = maxIterations;

        // Defensive copy: callers must observe a stable trace even if the source
        // collection is later mutated (INV-7 immutable state).
        PartialTrace = partialTrace.ToArray();
    }
}
