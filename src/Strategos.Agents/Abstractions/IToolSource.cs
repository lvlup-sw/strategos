// =============================================================================
// <copyright file="IToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Hexagonal port for tool discovery (DR-6). A tool source resolves the
/// <see cref="AIFunction"/>s an agent step may call during a turn, decoupling the
/// agent from where those tools come from. The protocol-specific adapters live
/// outside this assembly — the MCP adapter (wrapping ModelContextProtocol.Client)
/// in the <c>LevelUp.Strategos.Agents.Mcp</c> sub-package, while the in-process
/// reflection adapter (<c>AgentToolSource</c>) ships here — so the
/// <c>LevelUp.Strategos.Agents</c> assembly stays free of any MCP dependency.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Failure contract.</strong> Conforming adapters throw an
/// <c>AgentException</c> subtype (e.g. <c>AgentToolSourceException</c> / AGAG007,
/// or <c>AgentMcpException</c> / AGAG004) and apply credential redaction before
/// constructing the exception message. The <c>ResolveToolsAsync</c> boundary in
/// <c>StrategosFunctionsChatClient</c> propagates those exceptions unchanged.
/// Any other exception thrown by an adapter is treated as foreign: the boundary
/// wraps it in <c>AgentToolSourceException</c> (AGAG007) and applies URI
/// user-info redaction to the message (<c>scheme://user:pass@host</c> →
/// <c>scheme://host</c>) before propagating. That redaction covers only URI
/// user-info — it does not claim to scrub arbitrary secrets from foreign message
/// text. Cancellation (<see cref="OperationCanceledException"/>) is always
/// propagated unwrapped regardless of adapter type.
/// </para>
/// </remarks>
public interface IToolSource
{
    /// <summary>
    /// Resolves the <see cref="AIFunction"/>s this source exposes. Any lifecycle
    /// (handshake, connection, reflection) is the adapter's own responsibility.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token honored by the adapter.</param>
    /// <returns>AIFunctions ready to be appended to <c>ChatOptions.Tools</c>.</returns>
    Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken);
}
