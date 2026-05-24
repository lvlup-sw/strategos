// =============================================================================
// <copyright file="AgentStreamingException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when a streaming response handler fails mid-stream (DR-8).
/// </summary>
public sealed class AgentStreamingException : AgentException
{
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG009"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG009;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStreamingException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the streaming failure.</param>
    public AgentStreamingException(string message)
        : base($"{message} Diagnostic: {AgentDiagnostics.AGAG009}.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStreamingException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the streaming failure.</param>
    /// <param name="innerException">The exception that caused this streaming failure.</param>
    public AgentStreamingException(string message, Exception innerException)
        : base($"{message} Diagnostic: {AgentDiagnostics.AGAG009}.", innerException)
    {
    }
}
