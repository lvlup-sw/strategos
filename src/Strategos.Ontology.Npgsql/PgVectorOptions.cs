namespace Strategos.Ontology.Npgsql;

/// <summary>
/// Configuration options for the pgvector-backed object set provider.
/// </summary>
public sealed class PgVectorOptions
{
    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database schema to use for tables. Defaults to "public".
    /// </summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// When true, automatically creates tables and indexes on first use.
    /// </summary>
    public bool AutoCreateSchema { get; set; }

    /// <summary>
    /// The type of pgvector index to create. Defaults to IVFFlat.
    /// </summary>
    public PgVectorIndexType IndexType { get; set; } = PgVectorIndexType.IvfFlat;

    /// <summary>
    /// Optional pgvector ITERATIVE-SCAN knobs (DR-13/R8) applied per similarity
    /// query via transaction-scoped <c>SET LOCAL</c>. When set,
    /// <see cref="PgVectorObjectSetProvider.ExecuteSimilarityAsync{T}"/> shapes the
    /// search as an index-ordered ANN CTE and runs it inside a transaction so the
    /// knobs take effect without leaking across pooled connections. When <c>null</c>
    /// (the default) the pre-DR-13 single-statement similarity query is used and the
    /// server/session defaults stand. Requires the pgvector SERVER extension &gt;=
    /// 0.8.0.
    /// </summary>
    public IterativeScanOptions? IterativeScan { get; set; }
}

/// <summary>
/// Supported pgvector index types.
/// </summary>
public enum PgVectorIndexType
{
    /// <summary>IVFFlat index — good balance of build time and query performance.</summary>
    IvfFlat,

    /// <summary>HNSW index — faster queries, slower builds, more memory.</summary>
    Hnsw,
}
