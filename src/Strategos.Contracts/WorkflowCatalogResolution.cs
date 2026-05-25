// =============================================================================
// <copyright file="WorkflowCatalogResolution.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts.Generated;

namespace Strategos.Contracts;

/// <summary>
/// The typed outcome of resolving a catalog <see cref="CatalogWorkflowRef"/> against a
/// <see cref="WorkflowCatalog"/>. A typed result (rather than a bare <see cref="bool"/>)
/// hands the caller the resolved <see cref="WorkflowCatalogEntry"/> in the same step.
/// </summary>
public sealed record WorkflowCatalogResolution
{
    /// <summary>The shared unresolved result (no entry matched).</summary>
    public static readonly WorkflowCatalogResolution Miss = new();

    private WorkflowCatalogResolution()
    {
        this.Entry = null;
    }

    /// <summary>Initializes a resolved result wrapping the matched entry.</summary>
    /// <param name="entry">The catalog entry the reference resolved to.</param>
    public WorkflowCatalogResolution(WorkflowCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        this.Entry = entry;
    }

    /// <summary>Gets a value indicating whether the reference resolved to an entry.</summary>
    public bool Resolved => this.Entry is not null;

    /// <summary>Gets the matched catalog entry, or <see langword="null"/> if unresolved.</summary>
    public WorkflowCatalogEntry? Entry { get; init; }
}
