// =============================================================================
// <copyright file="RankedCandidate.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// A single document identifier and its 1-indexed rank within a single ranked input list.
/// Used as input to <see cref="RankFusion.Reciprocal"/>.
/// </summary>
/// <param name="DocumentId">Opaque document identifier (provider-specific; Strategos treats as ordinal string).</param>
/// <param name="Rank">1-indexed rank within the source list (rank 1 = highest-quality match).</param>
/// <remarks>
/// Rank semantics follow BM25 / Lucene convention: <c>1</c> is the top result. Ranks within
/// a single list need not be contiguous; <see cref="RankFusion.Reciprocal"/> consumes them
/// as-is when computing <c>weight / (k + rank)</c>.
/// </remarks>
public sealed record RankedCandidate(string DocumentId, int Rank);
