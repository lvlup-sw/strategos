namespace Strategos.Ontology.Npgsql;

/// <summary>
/// Per-query pgvector ITERATIVE-SCAN knobs (DR-13/R8, a pgvector 0.8.0 feature).
/// A filtered similarity query can otherwise under-return rows: the HNSW index
/// returns a fixed candidate set and the planner post-filters, so fewer than
/// <c>topK</c> rows survive. Iterative scans keep scanning the index until enough
/// post-filter rows are found. These options map onto the
/// <c>hnsw.iterative_scan</c> / <c>hnsw.max_scan_tuples</c> / <c>hnsw.ef_search</c>
/// GUCs, applied per query via <c>SET LOCAL</c> (transaction-scoped — they never
/// leak across pooled connections).
/// </summary>
/// <remarks>
/// INV-6/INV-7: a sealed, <c>init</c>-only immutable record — query options are
/// inert data. <see cref="MaxScanTuples"/> and <see cref="EfSearch"/> are nullable
/// so an unset knob emits no <c>SET LOCAL</c> and the session/index default
/// stands. This surface targets the HNSW index (the default for filtered
/// similarity search); the IVFFlat <c>probes</c> / <c>max_probes</c> knobs are out
/// of scope for R8.
/// </remarks>
public sealed record IterativeScanOptions
{
    /// <summary>
    /// The iterative-scan mode (<c>hnsw.iterative_scan</c>): <see cref="IterativeScanMode.Off"/>
    /// (the pgvector default), <see cref="IterativeScanMode.StrictOrder"/>, or
    /// <see cref="IterativeScanMode.RelaxedOrder"/>.
    /// </summary>
    public required IterativeScanMode Mode { get; init; }

    /// <summary>
    /// The cap on tuples visited during an iterative scan
    /// (<c>hnsw.max_scan_tuples</c>, pgvector default 20000). <c>null</c> leaves the
    /// session default unset.
    /// </summary>
    public int? MaxScanTuples { get; init; }

    /// <summary>
    /// The HNSW search breadth (<c>hnsw.ef_search</c>, pgvector default 40) — higher
    /// improves recall at query-time cost. <c>null</c> leaves the session default
    /// unset.
    /// </summary>
    public int? EfSearch { get; init; }
}

/// <summary>
/// The pgvector HNSW iterative-scan mode (<c>hnsw.iterative_scan</c>), a 0.8.0
/// feature (DR-13/R8).
/// </summary>
public enum IterativeScanMode
{
    /// <summary>Iterative scans disabled (<c>'off'</c>) — the pgvector default.</summary>
    Off,

    /// <summary>
    /// Iterative scan preserving strict distance ordering (<c>'strict_order'</c>):
    /// results are returned in exact distance order.
    /// </summary>
    StrictOrder,

    /// <summary>
    /// Iterative scan allowing relaxed ordering (<c>'relaxed_order'</c>): trades
    /// strict distance ordering for fewer scans / lower latency.
    /// </summary>
    RelaxedOrder,
}
