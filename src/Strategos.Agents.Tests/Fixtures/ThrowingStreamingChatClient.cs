// =============================================================================
// <copyright file="ThrowingStreamingChatClient.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// <see cref="IChatClient"/> test double whose streaming enumeration throws a
/// supplied exception on first move-next. Used to assert how the orchestrator
/// classifies mid-stream failures (e.g. cancellation must propagate unwrapped).
/// </summary>
internal sealed class ThrowingStreamingChatClient : IChatClient
{
    private readonly Func<Exception> _throwFactory;

    public ThrowingStreamingChatClient(Func<Exception> throwFactory)
        => _throwFactory = throwFactory;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Buffered path must not be invoked.");

#pragma warning disable CS1998 // async method lacks awaits — the throw is the point.
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        throw _throwFactory();
#pragma warning disable CS0162 // unreachable — required to type the iterator.
        yield break;
#pragma warning restore CS0162
    }
#pragma warning restore CS1998

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
