// -----------------------------------------------------------------------
// <copyright file="IPhaseAwareSaga.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions;

/// <summary>
/// Marker interface emitted on every generated Strategos saga so middleware
/// can read its current phase as a stable string identifier without binding
/// to the generated enum type.
/// </summary>
/// <remarks>
/// <para>
/// The basileus <c>StrategosHeaderMiddleware</c> consumes this interface to
/// derive per-step agent identities: it calls
/// <c>provider.DeriveStepIdentity(workflowId, saga.CurrentPhaseName)</c>
/// before stamping the agent-identity header on outgoing messages.
/// </para>
/// <para>
/// The generator emits <c>CurrentPhaseName =&gt; Phase.ToString()</c>; phase
/// strings are stable across builds because they derive from the workflow's
/// declared phase enum.
/// </para>
/// </remarks>
public interface IPhaseAwareSaga
{
    /// <summary>
    /// Gets the current saga phase as a stable string identifier
    /// (the generator emits <c>Phase.ToString()</c>).
    /// </summary>
    string CurrentPhaseName { get; }
}
