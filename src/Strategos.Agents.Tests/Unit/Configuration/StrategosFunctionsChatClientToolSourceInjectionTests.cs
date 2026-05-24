// =============================================================================
// <copyright file="StrategosFunctionsChatClientToolSourceInjectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using NSubstitute;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Agents.Exceptions;
using Strategos.Agents.Tests.Fixtures;

namespace Strategos.Agents.Tests.Unit.Configuration;

/// <summary>
/// <see cref="StrategosFunctionsChatClient"/> must resolve <see cref="IToolSource"/>-supplied
/// tools lazily on first request and merge them into the per-request
/// <see cref="ChatOptions.Tools"/> alongside the Strategos-registered tools and any
/// host-supplied tools (DR-9). Multiple sources accumulate; each source is resolved at
/// most once and cached. Merge precedence is host &gt; Strategos &gt; tool-sources
/// (in registration order).
/// </summary>
[Property("Category", "Unit")]
public sealed class StrategosFunctionsChatClientToolSourceInjectionTests
{
    [Test]
    public async Task GetResponseAsync_ResolvesToolSourceLazilyOnFirstCall()
    {
        var inner = OkClient();

        var tool = AIFunctionFactory.Create(() => "mcp", name: "mcp_tool");
        var source = new InProcessTestToolSource(new[] { tool });

        var client = new StrategosFunctionsChatClient(inner, Array.Empty<AIFunction>(), new IToolSource[] { source });

        await Assert.That(source.GetToolsAsyncCount).IsEqualTo(0);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: null,
            cancellationToken: CancellationToken.None);

        await Assert.That(source.GetToolsAsyncCount).IsEqualTo(1);
    }

    [Test]
    public async Task ToolSource_ResolvedAtMostOnce_Cached()
    {
        var inner = OkClient();

        var tool = AIFunctionFactory.Create(() => "mcp", name: "mcp_tool");
        var source = new InProcessTestToolSource(new[] { tool });

        var client = new StrategosFunctionsChatClient(inner, Array.Empty<AIFunction>(), new IToolSource[] { source });

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "1") },
            options: null,
            cancellationToken: CancellationToken.None);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "2") },
            options: null,
            cancellationToken: CancellationToken.None);

        // Resolved exactly once even though there were two requests.
        await Assert.That(source.GetToolsAsyncCount).IsEqualTo(1);
    }

    [Test]
    public async Task TwoToolSources_BothResolved_MergedByPrecedence()
    {
        ChatOptions? captured = null;
        var inner = CapturingClient(o => captured = o);

        var hostTool = AIFunctionFactory.Create(() => "host", name: "host_tool");
        var strategosTool = AIFunctionFactory.Create(() => "strat", name: "strategos_tool");
        var sourceOneTool = AIFunctionFactory.Create(() => "s1", name: "source_one_tool");
        var sourceTwoTool = AIFunctionFactory.Create(() => "s2", name: "source_two_tool");

        var sourceOne = new InProcessTestToolSource(new[] { sourceOneTool });
        var sourceTwo = new InProcessTestToolSource(new[] { sourceTwoTool });

        var client = new StrategosFunctionsChatClient(
            inner,
            new[] { strategosTool },
            new IToolSource[] { sourceOne, sourceTwo });

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: new ChatOptions { Tools = new List<AITool> { hostTool } },
            cancellationToken: CancellationToken.None);

        await Assert.That(sourceOne.GetToolsAsyncCount).IsEqualTo(1);
        await Assert.That(sourceTwo.GetToolsAsyncCount).IsEqualTo(1);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Tools).IsNotNull();
        await Assert.That(captured.Tools!.Count).IsEqualTo(4);

        // Merge order: host, Strategos, then sources in registration order.
        await Assert.That(captured.Tools![0]).IsSameReferenceAs((AITool)hostTool);
        await Assert.That(captured.Tools[1]).IsSameReferenceAs((AITool)strategosTool);
        await Assert.That(captured.Tools[2]).IsSameReferenceAs((AITool)sourceOneTool);
        await Assert.That(captured.Tools[3]).IsSameReferenceAs((AITool)sourceTwoTool);
    }

    [Test]
    public async Task HostTool_WinsOverSourceTool_OnNameCollision()
    {
        ChatOptions? captured = null;
        var inner = CapturingClient(o => captured = o);

        // Host vs source collision — host wins.
        var hostShared = AIFunctionFactory.Create(() => "from-host", name: "shared_name");
        var sourceShared = AIFunctionFactory.Create(() => "from-source", name: "shared_name");

        var source = new InProcessTestToolSource(new[] { sourceShared });

        var client = new StrategosFunctionsChatClient(
            inner,
            Array.Empty<AIFunction>(),
            new IToolSource[] { source });

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: new ChatOptions { Tools = new List<AITool> { hostShared } },
            cancellationToken: CancellationToken.None);

        await Assert.That(captured!.Tools!.Count).IsEqualTo(1);
        await Assert.That(captured.Tools![0]).IsSameReferenceAs((AITool)hostShared);

        // Strategos vs source collision — Strategos wins.
        captured = null;
        var stratShared = AIFunctionFactory.Create(() => "from-strategos", name: "shared_name2");
        var sourceShared2 = AIFunctionFactory.Create(() => "from-source", name: "shared_name2");

        var source2 = new InProcessTestToolSource(new[] { sourceShared2 });

        var client2 = new StrategosFunctionsChatClient(
            inner,
            new[] { stratShared },
            new IToolSource[] { source2 });

        await client2.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: null,
            cancellationToken: CancellationToken.None);

        await Assert.That(captured!.Tools!.Count).IsEqualTo(1);
        await Assert.That(captured.Tools![0]).IsSameReferenceAs((AITool)stratShared);
    }

    [Test]
    public async Task GetResponseAsync_McpSourceFailure_PropagatesAgentMcpExceptionUnchanged()
    {
        // DR-9: each source's own exception propagates unchanged — the MCP adapter
        // already surfaces AgentMcpException (AGAG004); the middleware does NOT reclassify.
        var inner = OkClient();

        var innerEx = new AgentMcpException("handshake failed", redactedEndpoint: "http://host/mcp");
        var source = new InProcessTestToolSource(innerEx);

        var client = new StrategosFunctionsChatClient(inner, Array.Empty<AIFunction>(), new IToolSource[] { source });

        var caught = await Assert.ThrowsAsync<AgentMcpException>(async () =>
            await client.GetResponseAsync(
                new[] { new ChatMessage(ChatRole.User, "hi") },
                options: null,
                cancellationToken: CancellationToken.None));

        await Assert.That(caught).IsSameReferenceAs(innerEx);
    }

    private static IChatClient OkClient()
    {
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));
        return inner;
    }

    private static IChatClient CapturingClient(Action<ChatOptions?> capture)
    {
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capture(call.ArgAt<ChatOptions?>(1));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });
        return inner;
    }
}
