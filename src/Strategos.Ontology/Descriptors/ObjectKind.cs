// =============================================================================
// <copyright file="ObjectKind.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Descriptors;

public enum ObjectKind
{
    Entity,
    Process,

    /// <summary>
    /// A reified association: a standalone object type that links two
    /// endpoints (a left and a right object type) and may carry its own
    /// properties on the edge. DR-4 (Ontology Edge Foundation): declared at
    /// the ontology level via <c>IOntologyBuilder.Association&lt;TRel&gt;</c>,
    /// not off a per-source link builder.
    /// </summary>
    Association,
}
