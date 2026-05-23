// =============================================================================
// <copyright file="AgentStructuredOutputException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text;
using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when <c>ChatResponse&lt;TResult&gt;.TryGetResult</c> returns false (DR-3).
/// Carries a truncated copy of the raw payload (≤4 KB) for diagnostics.
/// </summary>
public sealed class AgentStructuredOutputException : AgentException
{
    private const int MaxPayloadBytes = 4096;

    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG002"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG002;

    /// <summary>Raw payload from the chat response, truncated to 4 KB.</summary>
    public string? RawPayload { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStructuredOutputException"/> class.
    /// </summary>
    /// <param name="rawPayload">The raw chat-response payload; truncated to a 4 KB UTF-8 ceiling.</param>
    public AgentStructuredOutputException(string? rawPayload)
        : base($"Structured-output deserialization failed (ChatResponse<T>.TryGetResult returned false). Diagnostic: {AgentDiagnostics.AGAG002}.")
    {
        RawPayload = Truncate(rawPayload);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStructuredOutputException"/> class.
    /// </summary>
    /// <param name="rawPayload">The raw chat-response payload; truncated to a 4 KB UTF-8 ceiling.</param>
    /// <param name="innerException">The deserialization exception that caused this failure.</param>
    public AgentStructuredOutputException(string? rawPayload, Exception innerException)
        : base($"Structured-output deserialization failed (ChatResponse<T>.TryGetResult returned false). Diagnostic: {AgentDiagnostics.AGAG002}.", innerException)
    {
        RawPayload = Truncate(rawPayload);
    }

    private static string? Truncate(string? payload)
    {
        if (payload is null)
        {
            return null;
        }

        if (Encoding.UTF8.GetByteCount(payload) <= MaxPayloadBytes)
        {
            return payload;
        }

        // Truncate on a UTF-8 byte budget rather than a char count, so multi-byte
        // payloads cannot exceed the documented 4 KB ceiling. Back the cut point up
        // past any continuation byte (0b10xxxxxx) so a multi-byte sequence is never
        // split into a replacement character.
        var bytes = Encoding.UTF8.GetBytes(payload);
        var end = MaxPayloadBytes;
        while (end > 0 && (bytes[end] & 0xC0) == 0x80)
        {
            end--;
        }

        return Encoding.UTF8.GetString(bytes, 0, end);
    }
}
