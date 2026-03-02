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
