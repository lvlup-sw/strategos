// =============================================================================
// <copyright file="StrategosFunctionsChatClientInjectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using NSubstitute;
using Strategos.Agents.Configuration;

namespace Strategos.Agents.Tests.Unit.Configuration;

/// <summary>
/// T-015 backfill (defect surfaced by T-019): <see cref="StrategosFunctionsChatClient"/>
/// must override <c>GetResponseAsync</c> (and <c>GetStreamingResponseAsync</c>) to merge
/// its registered <see cref="AIFunction"/> tools into the per-request
/// <see cref="ChatOptions.Tools"/> before forwarding to the inner client. Without this
/// merge, the downstream <c>FunctionInvokingChatClient</c> sees <c>options.Tools == null</c>,
/// looks up requested tool names against an empty list, reports
/// <c>FunctionStatus.NotFound</c>, and retries with a synthetic tool-error message —
/// yielding 2 inner calls and 0 actual <see cref="AIFunction"/> invocations.
/// </summary>
[Property("Category", "Unit")]
public sealed class StrategosFunctionsChatClientInjectionTests
{
    [Test]
    public async Task GetResponseAsync_InjectsRegisteredToolsIntoChatOptions_WhenOptionsIsNull()
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

        var toolA = AIFunctionFactory.Create(() => "A", name: "tool_a");
        var toolB = AIFunctionFactory.Create(() => "B", name: "tool_b");

        var client = new StrategosFunctionsChatClient(inner, new[] { toolA, toolB });

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: null,
            cancellationToken: CancellationToken.None);

        await Assert.That(capturedOptions).IsNotNull();
        await Assert.That(capturedOptions!.Tools).IsNotNull();
        await Assert.That(capturedOptions.Tools!.Count).IsEqualTo(2);
        await Assert.That(capturedOptions.Tools).Contains(toolA);
        await Assert.That(capturedOptions.Tools).Contains(toolB);
    }

    [Test]
    public async Task GetResponseAsync_MergesRegisteredToolsWithHostSuppliedTools_WithoutOverwriting()
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

        var client = new StrategosFunctionsChatClient(inner, new[] { strategosTool });

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
        await Assert.That(capturedOptions.Tools!.Count).IsEqualTo(2);
        await Assert.That(capturedOptions.Tools).Contains((AITool)hostTool);
        await Assert.That(capturedOptions.Tools).Contains((AITool)strategosTool);
    }

    [Test]
    public async Task GetResponseAsync_DoesNotMutateCallerSuppliedChatOptions()
    {
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        var hostTool = AIFunctionFactory.Create(() => "host", name: "host_tool");
        var strategosTool = AIFunctionFactory.Create(() => "strat", name: "strategos_tool");

        var client = new StrategosFunctionsChatClient(inner, new[] { strategosTool });

        var hostTools = new List<AITool> { hostTool };
        var hostSupplied = new ChatOptions { Tools = hostTools };

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") },
            options: hostSupplied,
            cancellationToken: CancellationToken.None);

        // Caller's ChatOptions and Tools list must be untouched (per-request clone).
        await Assert.That(hostSupplied.Tools).IsSameReferenceAs(hostTools);
        await Assert.That(hostTools.Count).IsEqualTo(1);
        await Assert.That(hostTools[0]).IsSameReferenceAs((AITool)hostTool);
    }
}
