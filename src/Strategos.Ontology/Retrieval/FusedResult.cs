// =============================================================================
// <copyright file="FusedResult.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Output of a single rank-fusion call. Documents are returned sorted by <see cref="FusedScore"/>
/// descending; ties are broken by <see cref="DocumentId"/> ordinal ascending; <see cref="Rank"/>
/// reflects the post-fusion 1-indexed position.
/// </summary>
/// <param name="DocumentId">Opaque document identifier carried through from the inputs.</param>
/// <param name="FusedScore">The aggregated fusion score (formula depends on the method: RRF sum or weighted DBSF sum).</param>
/// <param name="Rank">1-indexed post-fusion rank (rank 1 = top result).</param>
public sealed record FusedResult(string DocumentId, double FusedScore, int Rank);
