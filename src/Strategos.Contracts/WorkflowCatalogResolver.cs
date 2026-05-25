// =============================================================================
// <copyright file="WorkflowCatalogResolver.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts.Generated;

namespace Strategos.Contracts;

/// <summary>
/// Consumer-time validation of a catalog <see cref="CatalogWorkflowRef"/> against a
/// published <see cref="WorkflowCatalog"/> manifest (#65/#66).
/// </summary>
/// <remarks>
/// A catalog <see cref="WorkflowRef"/> only names a workflow by its
/// <c>(workflowId, catalogVersion)</c> pair — the wire contract carries no payload.
/// Resolution is the consumer-side step that turns that pair into the concrete
/// <see cref="WorkflowCatalogEntry"/> (and its <see cref="WorkflowDefinitionV1"/> IR).
/// A reference whose pair is absent from the manifest does not resolve: the consumer
/// (Basileus AgentHost) MUST reject it rather than execute an unpinned journey. This
/// is a pure, side-effect-free helper — it allocates nothing beyond the result and
/// performs no I/O.
/// </remarks>
public static class WorkflowCatalogResolver
{
    /// <summary>
    /// Resolves a catalog workflow reference against a manifest.
    /// </summary>
    /// <param name="catalog">The published catalog manifest to resolve against.</param>
    /// <param name="reference">The catalog reference to validate.</param>
    /// <returns>
    /// A <see cref="WorkflowCatalogResolution"/> whose <see cref="WorkflowCatalogResolution.Resolved"/>
    /// is <see langword="true"/> and <see cref="WorkflowCatalogResolution.Entry"/> is non-null when the
    /// reference's <c>(WorkflowId, CatalogVersion)</c> pair matches an entry; otherwise an unresolved result.
    /// </returns>
    public static WorkflowCatalogResolution Resolve(WorkflowCatalog catalog, CatalogWorkflowRef reference)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reference);

        var entries = catalog.Entries;
        if (entries is null)
        {
            return WorkflowCatalogResolution.Miss;
        }

        foreach (var entry in entries)
        {
            if (string.Equals(entry.WorkflowId, reference.WorkflowId, StringComparison.Ordinal)
                && string.Equals(entry.CatalogVersion, reference.CatalogVersion, StringComparison.Ordinal))
            {
                return new WorkflowCatalogResolution(entry);
            }
        }

        return WorkflowCatalogResolution.Miss;
    }
}
