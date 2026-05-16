// =============================================================================
// <copyright file="RankFusion.DistributionBased.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Distribution-Based Score Fusion (DBSF, Qdrant 2024).
/// </summary>
public static partial class RankFusion
{
    private const double ZeroVarianceEpsilon = 1e-9;

    /// <summary>
    /// Fuses multiple scored lists using Distribution-Based Score Fusion.
    /// Per-list compute μ, σ; clamp scores to <c>[μ-3σ, μ+3σ]</c>; normalize to
    /// <c>[0,1]</c>; multiply by per-list weight; sum across lists.
    /// </summary>
    /// <param name="scoredLists">Input scored lists. Each inner list's documents
    /// must have unique <see cref="ScoredCandidate.DocumentId"/> (enforced via
    /// <see cref="System.Diagnostics.Debug.Assert(bool)"/>).</param>
    /// <param name="weights">Optional per-list weights (must equal
    /// <c>scoredLists.Count</c> when non-null; every element must be ≥ 0). Null =
    /// all <c>1.0</c>, matching Qdrant's stock unweighted DBSF bit-for-bit
    /// (within <c>1e-9</c>).</param>
    /// <param name="topK">Maximum number of fused results to return (default <c>10</c>).
    /// Must be non-negative. <c>0</c> returns an empty list without computation.</param>
    /// <returns>The fused results sorted by <see cref="FusedResult.FusedScore"/>
    /// descending, with ties broken by <see cref="FusedResult.DocumentId"/> ordinal
    /// ascending. Each element's <see cref="FusedResult.Rank"/> is 1-indexed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scoredLists"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="weights"/> is non-null
    /// and its length differs from <paramref name="scoredLists"/>, or when any weight is negative.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="topK"/> &lt; 0.</exception>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><b>Single-element list:</b> that element normalizes to <c>0.5</c> (Qdrant convention).</description></item>
    ///   <item><description><b>Zero-variance list</b> (σ &lt; <c>1e-9</c>): every element normalizes to <c>0.5</c>.</description></item>
    ///   <item><description><b>σ definition:</b> population stdev (<c>Σ(x-μ)²/N</c>, matching <c>numpy.std(ddof=0)</c> as used by qdrant-client).</description></item>
    ///   <item><description><b>Outlier handling:</b> scores outside <c>[μ-3σ, μ+3σ]</c> are clamped to the boundary (DBSF's stated advantage over min-max).</description></item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<FusedResult> DistributionBased(
        IReadOnlyList<IReadOnlyList<ScoredCandidate>> scoredLists,
        IReadOnlyList<double>? weights = null,
        int topK = 10)
    {
        ArgumentNullException.ThrowIfNull(scoredLists);

        if (topK < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be non-negative.");
        }

        if (weights is not null)
        {
            if (weights.Count != scoredLists.Count)
            {
                throw new ArgumentException(
                    $"weights length ({weights.Count}) must equal scoredLists count ({scoredLists.Count}).",
                    nameof(weights));
            }

            for (int wi = 0; wi < weights.Count; wi++)
            {
                if (weights[wi] < 0.0)
                {
                    throw new ArgumentException(
                        $"weights[{wi}] = {weights[wi]} must be ≥ 0.",
                        nameof(weights));
                }
            }
        }

        if (topK == 0 || scoredLists.Count == 0)
        {
            return Array.Empty<FusedResult>();
        }

        var fused = new Dictionary<string, double>(StringComparer.Ordinal);

        for (int li = 0; li < scoredLists.Count; li++)
        {
            var list = scoredLists[li];
            if (list is null || list.Count == 0)
            {
                continue;
            }

            double weight = weights is null ? 1.0 : weights[li];
            if (weight == 0.0)
            {
                continue;
            }

            // Single-element list: normalize to 0.5 (Qdrant convention).
            if (list.Count == 1)
            {
                var only = list[0];
                fused[only.DocumentId] = fused.GetValueOrDefault(only.DocumentId, 0.0) + (weight * 0.5);
                continue;
            }

            // Compute μ and population σ.
            double sum = 0.0;
            for (int ci = 0; ci < list.Count; ci++)
            {
                sum += list[ci].Score;
            }

            double mu = sum / list.Count;
            double varSum = 0.0;
            for (int ci = 0; ci < list.Count; ci++)
            {
                double d = list[ci].Score - mu;
                varSum += d * d;
            }

            double sigma = Math.Sqrt(varSum / list.Count);

            // Zero-variance list: every element normalizes to 0.5 (Qdrant convention).
            if (sigma < ZeroVarianceEpsilon)
            {
#if DEBUG
                var seenZ = new HashSet<string>(StringComparer.Ordinal);
#endif
                for (int ci = 0; ci < list.Count; ci++)
                {
                    var c = list[ci];
#if DEBUG
                    System.Diagnostics.Debug.Assert(
                        seenZ.Add(c.DocumentId),
                        $"Duplicate DocumentId '{c.DocumentId}' in scoredLists[{li}].");
#endif
                    fused[c.DocumentId] = fused.GetValueOrDefault(c.DocumentId, 0.0) + (weight * 0.5);
                }

                continue;
            }

            double low = mu - (3.0 * sigma);
            double high = mu + (3.0 * sigma);
            double span = high - low;

#if DEBUG
            var seen = new HashSet<string>(StringComparer.Ordinal);
#endif
            for (int ci = 0; ci < list.Count; ci++)
            {
                var c = list[ci];
#if DEBUG
                System.Diagnostics.Debug.Assert(
                    seen.Add(c.DocumentId),
                    $"Duplicate DocumentId '{c.DocumentId}' in scoredLists[{li}].");
#endif
                double clamped = Math.Min(high, Math.Max(low, c.Score));
                double normalized = (clamped - low) / span;
                fused[c.DocumentId] = fused.GetValueOrDefault(c.DocumentId, 0.0) + (weight * normalized);
            }
        }

        if (fused.Count == 0)
        {
            return Array.Empty<FusedResult>();
        }

        var ordered = fused
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        var results = new FusedResult[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            results[i] = new FusedResult(ordered[i].Key, ordered[i].Value, i + 1);
        }

        return results;
    }
}
