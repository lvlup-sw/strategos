// -----------------------------------------------------------------------
// <copyright file="HarnessProbeCommand.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// A start command that routes and is handled, but starts no saga and runs no workflow
/// step. It exists solely to reproduce, deterministically, the state the harness used to
/// misreport as success: the polled saga document is absent for the whole wait because the
/// saga was never created, and nothing at all executed.
/// </summary>
/// <remarks>
/// It is deliberately a HANDLED message rather than an unroutable one, so the probe
/// exercises the harness's completion oracle rather than the message bus's routing
/// behaviour for messages nobody handles.
/// </remarks>
/// <param name="WorkflowId">The identity the harness is asked to poll for, which no saga will ever claim.</param>
public sealed record HarnessProbeCommand(Guid WorkflowId)
{
    /// <summary>
    /// The message handler. It does nothing on purpose: no saga, no step, no invocation
    /// recorded.
    /// </summary>
    public static class Handler
    {
        /// <summary>
        /// Accepts the probe command and performs no work.
        /// </summary>
        /// <param name="command">The probe command.</param>
        public static void Handle(HarnessProbeCommand command)
        {
            _ = command;
        }
    }
}
