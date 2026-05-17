// -----------------------------------------------------------------------
// <copyright file="StrategosHeaders.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions;

/// <summary>
/// Wire-protocol header keys for Strategos identity propagation across the
/// Wolverine outbox and supported transports.
/// </summary>
/// <remarks>
/// <para>
/// Consumers MUST register
/// <c>opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)</c>
/// in their <c>UseWolverine</c> block so workflow identity survives the
/// outbox + transport hop between handlers. The agent identity header is
/// per-message-derived and is NOT propagated to outgoing messages — each
/// handler stamps its own.
/// </para>
/// <para>
/// Header keys are versioned by this package. Renaming a constant value is a
/// breaking wire-protocol change.
/// </para>
/// </remarks>
public static class StrategosHeaders
{
    /// <summary>
    /// Header key for the workflow identity. Stamped on every outgoing message
    /// emitted from inside a Strategos saga handler; propagated across handler
    /// hops via Wolverine's <c>PropagateIncomingHeaderToOutgoing</c> policy.
    /// </summary>
    public const string WorkflowIdentity = "x-strategos-workflow-identity";

    /// <summary>
    /// Header key for the per-step agent identity. Stamped by the middleware
    /// after deriving from <c>(WorkflowIdentity, saga.CurrentPhaseName)</c>.
    /// Not propagated — each handler emits its own.
    /// </summary>
    public const string AgentIdentity = "x-strategos-agent-identity";
}
