// =============================================================================
// <copyright file="IEventSourcedState.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Contract for workflow state types that support event-sourced persistence.
/// </summary>
/// <typeparam name="TState">The concrete state type (for covariant return).</typeparam>
/// <remarks>
/// <para>
/// When a workflow uses <see cref="Strategos.Attributes.PersistenceMode.EventSourced"/>,
/// the state type must implement this interface. Generated saga handlers will call
/// <see cref="ApplyEvent"/> instead of using the reducer pattern.
/// </para>
/// <para>
/// The <see cref="ApplyEvent"/> method should be a pure fold that returns a new state
/// instance with the event applied. The method is called both for local saga routing
/// (after appending the event to the Marten stream) and during event replay for
/// state reconstruction.
/// </para>
/// </remarks>
public interface IEventSourcedState<out TState> : IWorkflowState
    where TState : IWorkflowState
{
    /// <summary>
    /// Applies a progress event to produce a new state instance.
    /// </summary>
    /// <param name="evt">The event to apply.</param>
    /// <returns>A new state instance with the event applied.</returns>
    /// <remarks>
    /// <para>
    /// This method must be a pure function: given the same state and event,
    /// it must always produce the same result. It should handle all event types
    /// that the workflow can emit (typically via pattern matching).
    /// </para>
    /// <para>
    /// Unrecognized event types should return the current state unchanged
    /// (pass-through for informational events).
    /// </para>
    /// </remarks>
    TState ApplyEvent(IProgressEvent evt);
}
