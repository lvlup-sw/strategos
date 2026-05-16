// =============================================================================
// <copyright file="ScoredCandidate.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// A single document identifier and its raw provider-scale score within a single scored input
/// list. Used as input to <see cref="RankFusion.DistributionBased"/>.
/// </summary>
/// <param name="DocumentId">Opaque document identifier (provider-specific; Strategos treats as ordinal string).</param>
/// <param name="Score">Provider-specific score. Any finite real value is accepted; DBSF normalizes each list to <c>[0,1]</c> via μ±3σ.</param>
public sealed record ScoredCandidate(string DocumentId, double Score);
