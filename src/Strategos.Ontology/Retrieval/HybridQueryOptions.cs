// =============================================================================
// <copyright file="HybridQueryOptions.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Ontology.Retrieval;

/// <summary>
/// Optional per-call configuration for the hybrid (dense + sparse) retrieval path of
/// <c>OntologyQueryTool.QueryAsync</c>. Supplying <c>null</c> preserves byte-identical
/// 2.5.0 dense-only behavior (hard backward-compat gate; design §6.2 / §6.3).
/// </summary>
/// <remarks>
/// Hybrid is engaged only on the semantic-query branch and only when a
/// <c>IKeywordSearchProvider</c> has been registered AND <see cref="EnableKeyword"/>
/// is <c>true</c>. All other combinations degrade silently to dense-only with
/// metadata surfaced via <c>_meta.hybrid</c>. See design §6.4 for the full decision
/// tree.
/// </remarks>
public sealed record HybridQueryOptions
{
    /// <summary>
    /// Master switch. <c>true</c> (default) enables sparse fan-out when a provider is
    /// registered; <c>false</c> forces dense-only even if a provider is registered,
    /// which is useful for A/B testing and ablation runs.
    /// </summary>
    public bool EnableKeyword { get; init; } = true;

    /// <summary>
    /// TopK requested from the sparse (BM25 / keyword) leg before fusion. Must be
    /// non-negative. Note this is independent of the caller's overall <c>topK</c> —
    /// it sets the candidate pool size that fusion draws from.
    /// </summary>
    public int SparseTopK { get; init; } = 50;

    /// <summary>
    /// TopK requested from the dense (vector similarity) leg before fusion. Must be
    /// non-negative.
    /// </summary>
    public int DenseTopK { get; init; } = 50;

    /// <summary>
    /// Selects the rank-fusion algorithm. Default <see cref="Retrieval.FusionMethod.Reciprocal"/>
    /// (production default; rank-based, scale-invariant).
    /// </summary>
    public FusionMethod FusionMethod { get; init; } = FusionMethod.Reciprocal;

    /// <summary>
    /// Reciprocal Rank Fusion smoothing constant. Used only when
    /// <see cref="FusionMethod"/> is <see cref="Retrieval.FusionMethod.Reciprocal"/>. Must be
    /// strictly positive. Cormack 2009 §3.3 used <c>60</c>.
    /// </summary>
    public int RrfK { get; init; } = 60;

    /// <summary>
    /// Optional per-source weights in the order <c>[denseWeight, sparseWeight]</c>.
    /// <c>null</c> (default) means both legs contribute equally with weight <c>1.0</c>.
    /// When non-null the list must have exactly two elements and every element must be
    /// <c>≥ 0</c>. Weight <c>0</c> drops that leg from fusion.
    /// </summary>
    public IReadOnlyList<double>? SourceWeights { get; init; }

    /// <summary>
    /// Informational BM25 saturation threshold surfaced via
    /// <c>_meta.hybrid.bmSaturationThreshold</c>. <b>Does not affect fusion math in
    /// 2.6.0</b> — included so operators can correlate observability with the
    /// configured threshold without an additional out-of-band knob.
    /// </summary>
    public double BmSaturationThreshold { get; init; } = 18.0;

    /// <summary>
    /// Validates the option-record state per the design §6.6 behavior table. Invoked
    /// once at call entry by <c>OntologyQueryTool</c>; failure surfaces synchronously
    /// to the caller before any retrieval work begins.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="SparseTopK"/> &lt; 0, <see cref="DenseTopK"/> &lt; 0, or
    /// <see cref="RrfK"/> &lt;= 0.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="SourceWeights"/> is non-null with length ≠ 2, or contains a negative
    /// element.
    /// </exception>
    public void Validate()
    {
        if (SparseTopK < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SparseTopK), SparseTopK, "SparseTopK must be non-negative.");
        }

        if (DenseTopK < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DenseTopK), DenseTopK, "DenseTopK must be non-negative.");
        }

        if (RrfK <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RrfK), RrfK, "RrfK must be strictly positive.");
        }

        if (SourceWeights is not null)
        {
            if (SourceWeights.Count != 2)
            {
                throw new ArgumentException(
                    $"SourceWeights length ({SourceWeights.Count}) must equal 2 ([denseWeight, sparseWeight]).",
                    nameof(SourceWeights));
            }

            for (int i = 0; i < SourceWeights.Count; i++)
            {
                if (SourceWeights[i] < 0.0)
                {
                    throw new ArgumentException(
                        $"SourceWeights[{i}] = {SourceWeights[i]} must be ≥ 0.",
                        nameof(SourceWeights));
                }
            }
        }
    }
}
