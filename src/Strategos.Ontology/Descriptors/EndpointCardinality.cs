// =============================================================================
// <copyright file="EndpointCardinality.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// The multiplicity of one endpoint of a reified association
/// (<see cref="ObjectKind.Association"/>) relative to the association object.
/// </summary>
/// <remarks>
/// DR-6 (#121). A reified association is a junction object: many association
/// rows fold INTO one endpoint object on each side, so the only cardinality
/// that forms a valid reified relation is <see cref="ManyToOne"/> on both
/// endpoints. The analyzer rule <c>AONT210</c> flags any endpoint declared
/// with a cardinality other than <see cref="ManyToOne"/>. The authoring
/// default when no <c>WithCardinality(...)</c> is declared is
/// <see cref="ManyToOne"/>.
/// </remarks>
public enum EndpointCardinality
{
    /// <summary>
    /// Many association rows reference one endpoint object. This is the only
    /// valid endpoint cardinality for a reified relation and the authoring
    /// default.
    /// </summary>
    ManyToOne,

    /// <summary>
    /// One association row references at most one endpoint object. Invalid for
    /// a reified relation — flagged by <c>AONT210</c>.
    /// </summary>
    OneToOne,

    /// <summary>
    /// One endpoint object fans out to many association rows on this side.
    /// Invalid for a reified relation — flagged by <c>AONT210</c>.
    /// </summary>
    OneToMany,

    /// <summary>
    /// The endpoint itself participates as a many-to-many relation. Invalid
    /// for a reified relation (a relation cannot be an endpoint) — flagged by
    /// <c>AONT210</c>.
    /// </summary>
    ManyToMany,
}
