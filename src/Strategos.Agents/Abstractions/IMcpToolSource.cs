// =============================================================================
// <copyright file="IMcpToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Hexagonal port for Model Context Protocol tool discovery. Implementations resolve
/// AIFunctions from an MCP server, allowing Strategos agents to consume external MCP
/// servers as skill providers. The concrete adapter (wrapping ModelContextProtocol.Client)
/// lives in the <c>LevelUp.Strategos.Agents.Mcp</c> sub-package so the
/// <c>LevelUp.Strategos.Agents</c> assembly stays free of MCP dependencies.
/// </summary>
public interface IMcpToolSource
{
    /// <summary>
    /// Resolves the AIFunctions exposed by the underlying MCP server.
    /// Lifecycle (handshake, connection) is the adapter's responsibility.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token honored by the adapter.</param>
    /// <returns>AIFunctions ready to be appended to <c>ChatOptions.Tools</c>.</returns>
    Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken);
}
