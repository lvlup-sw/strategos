// =============================================================================
// <copyright file="RecordingChatClient.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// Hand-rolled <see cref="IChatClient"/> test double that records whether the
/// buffered (<see cref="GetResponseAsync"/>) or streaming
/// (<see cref="GetStreamingResponseAsync"/>) entry point was invoked, and replays
/// a scripted sequence of <see cref="ChatResponseUpdate"/>s on the streaming path.
/// </summary>
/// <remarks>
/// The streaming path is the only one the orchestrator drives when a handler is
/// configured; the buffered method is wired to throw so a test can prove the
/// streaming branch was taken (it must NOT fall back to buffering).
/// </remarks>
internal sealed class RecordingChatClient : IChatClient
{
    private readonly IReadOnlyList<ChatResponseUpdate> _streamingUpdates;
    private readonly Func<ChatResponse>? _bufferedResponse;
    private readonly bool _throwOnBuffered;

    public RecordingChatClient(
        IReadOnlyList<ChatResponseUpdate> streamingUpdates,
        Func<ChatResponse>? bufferedResponse = null,
        bool throwOnBuffered = true)
    {
        _streamingUpdates = streamingUpdates;
        _bufferedResponse = bufferedResponse;
        _throwOnBuffered = throwOnBuffered;
    }

    /// <summary>Gets a value indicating whether the buffered path was invoked.</summary>
    public bool BufferedInvoked { get; private set; }

    /// <summary>Gets a value indicating whether the streaming path was invoked.</summary>
    public bool StreamingInvoked { get; private set; }

    /// <summary>Gets the number of updates yielded so far on the streaming path.</summary>
    public int UpdatesYielded { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        BufferedInvoked = true;
        if (_throwOnBuffered)
        {
            throw new InvalidOperationException(
                "Buffered GetResponseAsync must not be called when a streaming handler is configured.");
        }

        return Task.FromResult(_bufferedResponse?.Invoke() ?? new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamingInvoked = true;
        foreach (var update in _streamingUpdates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpdatesYielded++;
            yield return update;
            await Task.Yield();
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
