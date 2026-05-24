// =============================================================================
// <copyright file="AgentMcpException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown by IMcpToolSource adapters on handshake or tool-discovery failure (DR-5).
/// </summary>
public sealed class AgentMcpException : AgentException
{
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG004"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG004;

    /// <summary>MCP server endpoint, with credentials redacted by the adapter.</summary>
    public string? RedactedEndpoint { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentMcpException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the MCP failure.</param>
    public AgentMcpException(string message)
        : base($"{message} Diagnostic: {AgentDiagnostics.AGAG004}.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentMcpException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the MCP failure.</param>
    /// <param name="redactedEndpoint">The MCP server endpoint with embedded credentials removed.</param>
    public AgentMcpException(string message, string? redactedEndpoint)
        : base($"{message} Endpoint: {redactedEndpoint ?? "<unknown>"}. Diagnostic: {AgentDiagnostics.AGAG004}.")
    {
        RedactedEndpoint = redactedEndpoint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentMcpException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the MCP failure.</param>
    /// <param name="redactedEndpoint">The MCP server endpoint with embedded credentials removed.</param>
    /// <param name="innerException">The transport or handshake exception that caused this failure.</param>
    public AgentMcpException(string message, string? redactedEndpoint, Exception innerException)
        : base($"{message} Endpoint: {redactedEndpoint ?? "<unknown>"}. Diagnostic: {AgentDiagnostics.AGAG004}.", innerException)
    {
        RedactedEndpoint = redactedEndpoint;
    }
}
