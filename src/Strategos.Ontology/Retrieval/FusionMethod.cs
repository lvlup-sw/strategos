// =============================================================================
// <copyright file="FusionMethod.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Selects the rank-fusion algorithm used by <c>OntologyQueryTool</c> when a hybrid
/// (dense + sparse) retrieval path is engaged.
/// </summary>
/// <remarks>
/// Numeric values are pinned by design §6.1 so they remain stable across persistence
/// (configuration, logs, <c>_meta.hybrid.fusionMethod</c> projections). Treat this enum
/// as an additive-only contract.
/// </remarks>
public enum FusionMethod
{
    /// <summary>
    /// Reciprocal Rank Fusion (Cormack 2009). Generalizes to weighted RRF when
    /// <c>HybridQueryOptions.SourceWeights</c> is set. This is the production
    /// default — it is rank-based and therefore insensitive to scale skew
    /// between sparse (BM25) and dense (cosine) score distributions.
    /// </summary>
    Reciprocal = 0,

    /// <summary>
    /// Distribution-Based Score Fusion (Qdrant 2024). Score-aware via μ±3σ
    /// normalization. Prefer this when score variance differs significantly
    /// across retrieval paths and rank-only fusion under-weights one source.
    /// </summary>
    DistributionBased = 1,
}
