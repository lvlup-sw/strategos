// =============================================================================
// <copyright file="RankFusion.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Pure rank-fusion utilities. Two static methods:
/// <list type="bullet">
///   <item><description><see cref="Reciprocal(System.Collections.Generic.IReadOnlyList{System.Collections.Generic.IReadOnlyList{RankedCandidate}}, System.Collections.Generic.IReadOnlyList{double}?, int, int)"/> — weighted Reciprocal Rank Fusion (Cormack 2009 generalized).</description></item>
///   <item><description><see cref="DistributionBased(System.Collections.Generic.IReadOnlyList{System.Collections.Generic.IReadOnlyList{ScoredCandidate}}, System.Collections.Generic.IReadOnlyList{double}?, int)"/> — Distribution-Based Score Fusion (Qdrant 2024).</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// <b>Default to <see cref="Reciprocal(System.Collections.Generic.IReadOnlyList{System.Collections.Generic.IReadOnlyList{RankedCandidate}}, System.Collections.Generic.IReadOnlyList{double}?, int, int)"/> with all weights = 1.0.</b>
/// Production default across Elasticsearch, OpenSearch, Azure AI Search, Qdrant, Vespa, Pinecone.
/// </para>
/// <para>
/// <b>Add per-source weights</b> for known quality asymmetries — production data
/// shows per-source weighting moves NDCG more than <c>k</c> tuning.
/// </para>
/// <para>
/// <b>Switch to <see cref="DistributionBased(System.Collections.Generic.IReadOnlyList{System.Collections.Generic.IReadOnlyList{ScoredCandidate}}, System.Collections.Generic.IReadOnlyList{double}?, int)"/></b>
/// when score variance differs significantly across paths.
/// </para>
/// </remarks>
public static partial class RankFusion
{
}
