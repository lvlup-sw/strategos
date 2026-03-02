// =============================================================================
// <copyright file="ILinkBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Builder;

public interface ILinkBuilder
{
    ILinkBuilder Inverse(string inverseLinkName);
}
