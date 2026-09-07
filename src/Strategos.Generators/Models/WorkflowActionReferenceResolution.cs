// -----------------------------------------------------------------------
// <copyright file="WorkflowActionReferenceResolution.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Models;

/// <summary>
/// Describes whether a workflow step occurrence's action identity was authored and
/// could be reduced to the generator's closed name-only model.
/// </summary>
internal enum WorkflowActionReferenceResolution
{
    /// <summary>No <c>Performs</c> declaration was authored for the occurrence.</summary>
    Missing,

    /// <summary>The authored reference was reduced to three constant names.</summary>
    Resolved,

    /// <summary>A <c>Performs</c> declaration exists but is dynamic or otherwise unreadable.</summary>
    DynamicOrInvalid,
}
