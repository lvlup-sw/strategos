// -----------------------------------------------------------------------
// <copyright file="PersistenceMode.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Models;

/// <summary>
/// Internal mirror of <c>Strategos.Attributes.PersistenceMode</c> for use within the source generator.
/// </summary>
/// <remarks>
/// Must be kept in sync with the public enum values. The generator reads the
/// attribute's named argument as an integer and maps it to this enum.
/// </remarks>
internal enum PersistenceMode
{
    /// <summary>
    /// Default. Handlers mutate the saga document via reducers.
    /// </summary>
    SagaDocument = 0,

    /// <summary>
    /// Event-sourced. Handlers append to Marten event stream and call ApplyEvent.
    /// </summary>
    EventSourced = 1,
}
