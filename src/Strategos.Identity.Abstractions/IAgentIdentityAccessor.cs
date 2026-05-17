// -----------------------------------------------------------------------
// <copyright file="IAgentIdentityAccessor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions;

/// <summary>
/// Read-only port that exposes the current workflow and agent identity
/// to in-handler code (sagas, application services, telemetry enrichers).
/// </summary>
/// <remarks>
/// <para>
/// Implementations read from the active Wolverine
/// <c>IMessageContext.Envelope.Headers</c>. The contract mirrors
/// <c>IHttpContextAccessor</c>: both properties return <c>null</c> when no
/// envelope is active (saga inspected via a Marten projection, a debugger
/// session, a background worker that isn't in a handler, etc.).
/// </para>
/// <para>
/// Implementations are NOT required to cache — the envelope's lifetime IS
/// the cache.
/// </para>
/// </remarks>
public interface IAgentIdentityAccessor
{
    /// <summary>
    /// Gets the workflow identity carried on the active envelope, or <c>null</c>
    /// when there is no active envelope or the header is missing or invalid.
    /// </summary>
    WorkflowIdentity? CurrentWorkflow { get; }

    /// <summary>
    /// Gets the agent identity carried on the active envelope, or <c>null</c>
    /// when there is no active envelope or the header is missing or invalid.
    /// </summary>
    AgentIdentity? CurrentAgent { get; }
}
