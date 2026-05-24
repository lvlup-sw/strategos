// =============================================================================
// <copyright file="AgentToolSourceException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when an in-process tool source fails to resolve its AIFunctions (DR-8).
/// </summary>
public sealed class AgentToolSourceException : AgentException
{
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG007"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG007;

    /// <summary>Name of the tool-source type that failed to resolve, when known.</summary>
    public string? SourceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentToolSourceException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the resolution failure.</param>
    /// <param name="sourceType">The tool-source type that failed to resolve its functions.</param>
    public AgentToolSourceException(string message, string? sourceType)
        : base($"{message} Source: {sourceType ?? "<unknown>"}. Diagnostic: {AgentDiagnostics.AGAG007}.")
    {
        SourceType = sourceType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentToolSourceException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the resolution failure.</param>
    /// <param name="sourceType">The tool-source type that failed to resolve its functions.</param>
    /// <param name="innerException">The exception that caused this resolution failure.</param>
    public AgentToolSourceException(string message, string? sourceType, Exception innerException)
        : base($"{message} Source: {sourceType ?? "<unknown>"}. Diagnostic: {AgentDiagnostics.AGAG007}.", innerException)
    {
        SourceType = sourceType;
    }
}
