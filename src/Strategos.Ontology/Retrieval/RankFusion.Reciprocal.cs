// =============================================================================
// <copyright file="RankFusion.Reciprocal.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Weighted Reciprocal Rank Fusion (Cormack 2009 generalized).
/// </summary>
public static partial class RankFusion
{
    /// <summary>
    /// Fuses multiple ranked lists into a single result list using weighted Reciprocal Rank
    /// Fusion (Cormack, Clarke, Buettcher SIGIR 2009; weighted form per the Elasticsearch /
    /// Bruch 2024 convention <c>weight_L / (k + rank_L(d))</c>).
    /// </summary>
    /// <param name="rankedLists">Input ranked lists. Each inner list is sorted by quality
    /// (rank 1 = top); ranks need not be contiguous; lists must not contain duplicate
    /// <see cref="RankedCandidate.DocumentId"/> values (enforced via <see cref="System.Diagnostics.Debug.Assert(bool)"/>).</param>
    /// <param name="weights">Optional per-list weights (must equal <c>rankedLists.Count</c>
    /// when non-null; every element must be ≥ 0). Null = all <c>1.0</c>, which produces
    /// bit-identical output to Cormack 2009 RRF.</param>
    /// <param name="k">RRF smoothing constant (default <c>60</c> per the original paper).
    /// Must be strictly positive.</param>
    /// <param name="topK">Maximum number of fused results to return (default <c>10</c>).
    /// Must be non-negative. <c>0</c> returns an empty list without computation.</param>
    /// <returns>The fused results sorted by <see cref="FusedResult.FusedScore"/> descending,
    /// with ties broken by <see cref="FusedResult.DocumentId"/> ordinal ascending. Each
    /// element's <see cref="FusedResult.Rank"/> is 1-indexed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rankedLists"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="weights"/> is non-null
    /// and its length differs from <paramref name="rankedLists"/>, or when any weight is negative.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="k"/> &lt;= 0
    /// or when <paramref name="topK"/> &lt; 0.</exception>
    /// <remarks>
    /// Formula: <c>fused_score(d) = Σ_{L ∈ rankedLists} weight_L / (k + rank_L(d))</c>.
    /// Documents absent from a list contribute <c>0</c> from that list. A list with
    /// <c>weight_L = 0</c> contributes nothing. Sum is computed left-to-right in input order
    /// for float reproducibility (tolerance <c>1e-12</c>).
    /// </remarks>
    public static IReadOnlyList<FusedResult> Reciprocal(
        IReadOnlyList<IReadOnlyList<RankedCandidate>> rankedLists,
        IReadOnlyList<double>? weights = null,
        int k = 60,
        int topK = 10)
    {
        ArgumentNullException.ThrowIfNull(rankedLists);

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "k must be strictly positive.");
        }

        if (topK < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be non-negative.");
        }

        if (weights is not null)
        {
            if (weights.Count != rankedLists.Count)
            {
                throw new ArgumentException(
                    $"weights length ({weights.Count}) must equal rankedLists count ({rankedLists.Count}).",
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

        if (topK == 0 || rankedLists.Count == 0)
        {
            return Array.Empty<FusedResult>();
        }

        // Aggregate fused scores by DocumentId.
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);

        for (int li = 0; li < rankedLists.Count; li++)
        {
            var list = rankedLists[li];
            if (list is null || list.Count == 0)
            {
                continue;
            }

            double weight = weights is null ? 1.0 : weights[li];
            if (weight == 0.0)
            {
                // Zero-weight list contributes nothing per the design behavior table.
                continue;
            }

#if DEBUG
            // Caller contract: no duplicate DocumentId within a single list.
            var seen = new HashSet<string>(StringComparer.Ordinal);
#endif
            for (int ci = 0; ci < list.Count; ci++)
            {
                var cand = list[ci];
#if DEBUG
                System.Diagnostics.Debug.Assert(
                    seen.Add(cand.DocumentId),
                    $"Duplicate DocumentId '{cand.DocumentId}' in rankedLists[{li}].");
#endif
                double contribution = weight / (k + cand.Rank);
                if (scores.TryGetValue(cand.DocumentId, out var prev))
                {
                    scores[cand.DocumentId] = prev + contribution;
                }
                else
                {
                    scores[cand.DocumentId] = contribution;
                }
            }
        }

        if (scores.Count == 0)
        {
            return Array.Empty<FusedResult>();
        }

        // Sort by score desc, then DocumentId ordinal asc for stable tie-break.
        var ordered = scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(topK)
            .ToList();

        var fused = new FusedResult[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            fused[i] = new FusedResult(ordered[i].Key, ordered[i].Value, i + 1);
        }

        return fused;
    }
}
