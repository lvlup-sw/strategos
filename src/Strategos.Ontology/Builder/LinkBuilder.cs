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
    private string? _description;

    public ILinkBuilder Inverse(string inverseLinkName)
    {
        _inverseLinkName = inverseLinkName;
        return this;
    }

    public ILinkBuilder Description(string description)
    {
        _description = description;
        return this;
    }

    public LinkDescriptor Build()
    {
        var descriptor = baseDescriptor;
        if (_inverseLinkName is not null)
            descriptor = descriptor with { InverseLinkName = _inverseLinkName };
        if (_description is not null)
            descriptor = descriptor with { Description = _description };
        return descriptor;
    }
}
