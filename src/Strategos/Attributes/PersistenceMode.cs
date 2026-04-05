// -----------------------------------------------------------------------
// <copyright file="PersistenceMode.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Attributes;

/// <summary>
/// Specifies the persistence strategy for generated workflow saga handlers.
/// </summary>
/// <remarks>
/// <para>
/// This enum controls how the source generator produces state mutation code
/// in the generated Wolverine saga handlers:
/// <list type="bullet">
///   <item><description>
///     <see cref="SagaDocument"/>: Default. Handlers call <c>Reducer.Reduce(State, evt.UpdatedState)</c>
///     to apply state changes directly to the saga document.
///   </description></item>
///   <item><description>
///     <see cref="EventSourced"/>: Handlers append events to the Marten event stream via
///     <c>session.Events.Append()</c> and apply locally via <c>State.ApplyEvent(evt)</c>.
///     The state type must implement <c>IEventSourcedState&lt;TState&gt;</c>.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public enum PersistenceMode
{
    /// <summary>
    /// Default persistence mode. Generated handlers mutate the saga document
    /// directly using the reducer pattern.
    /// </summary>
    SagaDocument = 0,

    /// <summary>
    /// Event-sourced persistence mode. Generated handlers append events to
    /// the Marten event stream and apply them locally for saga routing.
    /// Requires the state type to implement <see cref="Strategos.Abstractions.IEventSourcedState{TState}"/>.
    /// </summary>
    EventSourced = 1,
}
