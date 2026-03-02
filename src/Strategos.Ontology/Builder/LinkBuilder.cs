// =============================================================================
// <copyright file="LinkBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class LinkBuilder(LinkDescriptor baseDescriptor) : ILinkBuilder
{
    private string? _inverseLinkName;

    public ILinkBuilder Inverse(string inverseLinkName)
    {
        _inverseLinkName = inverseLinkName;
        return this;
    }

    public LinkDescriptor Build() =>
        _inverseLinkName is not null
            ? baseDescriptor with { InverseLinkName = _inverseLinkName }
            : baseDescriptor;
}
