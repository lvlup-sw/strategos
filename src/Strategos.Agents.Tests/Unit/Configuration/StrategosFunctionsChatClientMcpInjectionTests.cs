// =============================================================================
// <copyright file="StrategosFunctionsChatClientMcpInjectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents.Tests.Unit.Configuration;

/// <summary>
/// Backfill (defect surfaced during T-020 ideation): <see cref="StrategosFunctionsChatClient"/>
/// must resolve <see cref="IMcpToolSource"/>-supplied tools lazily on first request and
/// merge them into the per-request <see cref="ChatOptions.Tools"/> alongside the
/// Strategos-registered tools and any host-supplied tools. Without this, the MCP
/// adapter wired by <c>AgentStepBuilder.WithMcpToolSource(...)</c> is unreachable
/// from <c>Build()</c> and MCP-discovered <c>AIFunction</c>s never surface to the
/// downstream <see cref="FunctionInvokingChatClient"/>.
/// </summary>
[Property("Category", "Unit")]
public sealed class StrategosFunctionsChatClientMcpInjectionTests
{
    [Test]
    public async Task GetResponseAsync_ResolvesMcpToolsLazilyOnFirstCall()
    {
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        var mcpTool = AIFunctionFactory.Create(() => "mcp", name: "mcp_tool");
        var mcpSource = Substitute.For<IMcpToolSource>();
        mcpSource
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AIFunction>>(new[] { mcpTool }));

        var client = new StrategosFunctionsChatClient(inner, Array.Empty<AIFunction>(), mcpSource);

        // BEFORE first call: resolver must not have been invoked.
        await mcpSource.Received(0).GetToolsAsync(Arg.Any<CancellationToken>());

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: null,
            cancellationToken: CancellationToken.None);

        // AFTER first call: resolver invoked exactly once.
        await mcpSource.Received(1).GetToolsAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetResponseAsync_CachesMcpToolsAcrossSubsequentCalls()
    {
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        var mcpTool = AIFunctionFactory.Create(() => "mcp", name: "mcp_tool");
        var mcpSource = Substitute.For<IMcpToolSource>();
        mcpSource
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AIFunction>>(new[] { mcpTool }));

        var client = new StrategosFunctionsChatClient(inner, Array.Empty<AIFunction>(), mcpSource);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "1") },
            options: null,
            cancellationToken: CancellationToken.None);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "2") },
            options: null,
            cancellationToken: CancellationToken.None);

        // Resolver invoked exactly once even though there were two requests.
        await mcpSource.Received(1).GetToolsAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetResponseAsync_MergesMcpToolsWithStrategosToolsAndHostTools()
    {
        ChatOptions? capturedOptions = null;
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedOptions = call.ArgAt<ChatOptions?>(1);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });

        var hostTool = AIFunctionFactory.Create(() => "host", name: "host_tool");
        var strategosTool = AIFunctionFactory.Create(() => "strat", name: "strategos_tool");
        var mcpTool = AIFunctionFactory.Create(() => "mcp", name: "mcp_tool");

        var mcpSource = Substitute.For<IMcpToolSource>();
        mcpSource
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AIFunction>>(new[] { mcpTool }));

        var client = new StrategosFunctionsChatClient(inner, new[] { strategosTool }, mcpSource);

        var hostSupplied = new ChatOptions
        {
            Tools = new List<AITool> { hostTool },
        };

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: hostSupplied,
            cancellationToken: CancellationToken.None);

        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.Tools).IsNotNull();
        await Assert.That(capturedOptions.Tools!.Count).IsEqualTo(3);
        await Assert.That(capturedOptions.Tools).Contains((AITool)hostTool);
        await Assert.That(capturedOptions.Tools).Contains((AITool)strategosTool);
        await Assert.That(capturedOptions.Tools).Contains((AITool)mcpTool);
    }

    [Test]
    public async Task GetResponseAsync_NameCollisionPrecedence_HostBeatsStrategosBeatsMcp()
    {
        // Precedence rule (host > Strategos > MCP):
        //   - Host wins because the caller's per-request ChatOptions express
        //     the most specific intent.
        //   - Strategos wins over MCP because in-process tools are the agent's
        //     own contract; MCP tools are externally-discovered and treated as
        //     a fallback skill source.
        // Implementation merges in order [host, Strategos, MCP] and skips any
        // later tool whose Name collides with an already-present tool.
        ChatOptions? capturedOptions = null;
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedOptions = call.ArgAt<ChatOptions?>(1);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });

        // Case A: host vs Strategos collision — host wins.
        var hostShared = AIFunctionFactory.Create(() => "from-host", name: "shared_name");
        var stratShared = AIFunctionFactory.Create(() => "from-strategos", name: "shared_name");

        var mcpSourceA = Substitute.For<IMcpToolSource>();
        mcpSourceA
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AIFunction>>(Array.Empty<AIFunction>()));

        var clientA = new StrategosFunctionsChatClient(inner, new[] { stratShared }, mcpSourceA);
        await clientA.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: new ChatOptions { Tools = new List<AITool> { hostShared } },
            cancellationToken: CancellationToken.None);

        await Assert.That(capturedOptions!.Tools!.Count).IsEqualTo(1);
        await Assert.That(capturedOptions.Tools![0]).IsSameReferenceAs((AITool)hostShared);

        // Case B: Strategos vs MCP collision — Strategos wins.
        capturedOptions = null;
        var stratOnly = AIFunctionFactory.Create(() => "from-strategos", name: "shared_name2");
        var mcpOnly = AIFunctionFactory.Create(() => "from-mcp", name: "shared_name2");

        var mcpSourceB = Substitute.For<IMcpToolSource>();
        mcpSourceB
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AIFunction>>(new[] { mcpOnly }));

        var clientB = new StrategosFunctionsChatClient(inner, new[] { stratOnly }, mcpSourceB);
        await clientB.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: null,
            cancellationToken: CancellationToken.None);

        await Assert.That(capturedOptions!.Tools!.Count).IsEqualTo(1);
        await Assert.That(capturedOptions.Tools![0]).IsSameReferenceAs((AITool)stratOnly);
    }

    [Test]
    public async Task GetResponseAsync_McpResolutionFailure_RethrowsAsAgentMcpException()
    {
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        var inner_ex = new InvalidOperationException("boom");
        var mcpSource = Substitute.For<IMcpToolSource>();
        mcpSource
            .GetToolsAsync(Arg.Any<CancellationToken>())
            .Throws(inner_ex);

        var client = new StrategosFunctionsChatClient(inner, Array.Empty<AIFunction>(), mcpSource);

        AgentMcpException? caught = null;
        try
        {
            await client.GetResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "hi") },
                options: null,
                cancellationToken: CancellationToken.None);
        }
        catch (AgentMcpException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.InnerException).IsSameReferenceAs(inner_ex);
    }
}
