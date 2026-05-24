// =============================================================================
// <copyright file="StreamingTokenReceived.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Abstractions;
using Strategos.Agents.Models;

namespace Strategos.Agents.Events;

/// <summary>
/// Event raised when a token is received during streaming LLM response generation.
/// Legacy specialist-agent surface — NOT emitted by the <c>AgentStep</c>
/// <c>WithStreaming</c>/<c>IStreamingHandler</c> path (that path is a non-durable
/// side-channel and writes no progress events; see INV-1).
/// </summary>
/// <remarks>
/// <para>
/// This event captures individual tokens as they are streamed from the LLM,
/// enabling real-time progress tracking and complete audit trails of streaming
/// operations.
/// </para>
/// <para>
/// Events are published to <see cref="IProgressEventStore"/> for persistence
/// and can be replayed to reconstruct the streaming sequence for debugging.
/// </para>
/// <para>
/// Token events are only generated when <see cref="StreamingExecutionMode.Streaming"/>
/// is enabled on the specialist agent.
/// </para>
/// </remarks>
/// <param name="WorkflowId">The unique identifier for the workflow this token belongs to.</param>
/// <param name="TaskId">The task identifier from the TaskLedger this token is associated with.</param>
/// <param name="SpecialistType">The type of specialist agent generating the response.</param>
/// <param name="Token">The token text content.</param>
/// <param name="TokenIndex">The zero-based index of this token in the streaming sequence.</param>
/// <param name="Timestamp">The timestamp when this token was received.</param>
public sealed record StreamingTokenReceived(
    Guid WorkflowId,
    string TaskId,
    SpecialistType SpecialistType,
    string Token,
    int TokenIndex,
    DateTimeOffset Timestamp) : IProgressEvent;
