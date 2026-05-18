// =============================================================================
// <copyright file="McpToolSourceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Exceptions;
using Strategos.Agents.Mcp;

namespace Strategos.Agents.Mcp.Tests;

[Property("Category", "Integration")]
public sealed class McpToolSourceTests
{
    [Test]
    public async Task GetToolsAsync_UnreachableEndpoint_ThrowsAgentMcpExceptionWithAGAG004AndRedactedEndpoint()
    {
        // Point at a closed local port. The MCP handshake (or transport open) must fail.
        // The exception must (a) be AgentMcpException, (b) carry AGAG004, (c) the
        // RedactedEndpoint property must NOT contain any embedded credentials (user:pass).
        var endpointWithCredentials = "http://nobody:secret-token@127.0.0.1:1/mcp";

        var source = McpToolSource.ForHttpEndpoint(new Uri(endpointWithCredentials), TimeSpan.FromSeconds(2));
        await using var _ = source;

        var ex = await Assert.ThrowsAsync<AgentMcpException>(async () =>
            await source.GetToolsAsync(CancellationToken.None));

        await Assert.That(ex!.Diagnostic).IsEqualTo(AgentDiagnostics.AGAG004);
        await Assert.That(ex.RedactedEndpoint).IsNotNull();
        await Assert.That(ex.RedactedEndpoint!.Contains("secret-token")).IsFalse();
        await Assert.That(ex.RedactedEndpoint.Contains("nobody")).IsFalse();
    }

    [Test]
    public async Task McpToolSource_ImplementsIAsyncDisposable()
    {
        var source = McpToolSource.ForHttpEndpoint(new Uri("http://127.0.0.1:1/mcp"), TimeSpan.FromSeconds(2));
        await Assert.That(source is IAsyncDisposable).IsTrue();
        await source.DisposeAsync();
    }
}
