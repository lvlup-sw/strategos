// =============================================================================
// <copyright file="AgentStructuredOutputException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when <c>ChatResponse&lt;TResult&gt;.TryGetResult</c> returns false (DR-3).
/// Carries a truncated copy of the raw payload (≤4 KB) for diagnostics.
/// </summary>
public sealed class AgentStructuredOutputException : AgentException
{
    private const int MaxPayloadBytes = 4096;

    public override string Diagnostic => AgentDiagnostics.AGAG002;

    /// <summary>Raw payload from the chat response, truncated to 4 KB.</summary>
    public string? RawPayload { get; }

    public AgentStructuredOutputException(string? rawPayload)
        : base($"Structured-output deserialization failed (ChatResponse<T>.TryGetResult returned false). Diagnostic: {AgentDiagnostics.AGAG002}.")
    {
        RawPayload = Truncate(rawPayload);
    }

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

        return payload.Length <= MaxPayloadBytes ? payload : payload[..MaxPayloadBytes];
    }
}
