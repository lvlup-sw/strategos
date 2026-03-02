using System.Text;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Internal;

/// <summary>
/// Generates SQL statements for pgvector operations.
/// Extracted for testability — SQL generation logic is pure and deterministic.
/// </summary>
internal static class SqlGenerator
{
    /// <summary>
    /// Quotes a SQL identifier, escaping any embedded double quotes per the SQL standard.
    /// </summary>
    internal static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// Returns the pgvector distance operator for the given metric.
    /// </summary>
    internal static string GetDistanceOperator(DistanceMetric metric) => metric switch
    {
        DistanceMetric.Cosine => "<=>",
        DistanceMetric.L2 => "<->",
        DistanceMetric.InnerProduct => "<#>",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported distance metric."),
    };

    /// <summary>
    /// Returns the pgvector index operator class for the given metric.
    /// </summary>
    internal static string GetIndexOperatorClass(DistanceMetric metric) => metric switch
    {
        DistanceMetric.Cosine => "vector_cosine_ops",
        DistanceMetric.L2 => "vector_l2_ops",
        DistanceMetric.InnerProduct => "vector_ip_ops",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported distance metric."),
    };

    /// <summary>
    /// Builds a similarity search SQL query.
    /// </summary>
    internal static string BuildSimilarityQuery(
        string schema,
        string tableName,
        DistanceMetric metric,
        string? whereClause = null)
    {
        var op = GetDistanceOperator(metric);
        var sb = new StringBuilder();
        sb.Append($"SELECT id, data, (embedding {op} @query) AS distance FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}");

        if (!string.IsNullOrEmpty(whereClause))
        {
            sb.Append($" WHERE {whereClause}");
        }

        sb.Append(" ORDER BY distance LIMIT @topK");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a SELECT query for filter/stream operations.
    /// </summary>
    internal static string BuildSelectQuery(
        string schema,
        string tableName,
        string? whereClause = null)
    {
        var sb = new StringBuilder();
        sb.Append($"SELECT id, data FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}");

        if (!string.IsNullOrEmpty(whereClause))
        {
            sb.Append($" WHERE {whereClause}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds an INSERT statement for a single object.
    /// </summary>
    internal static string BuildInsertSql(string schema, string tableName, bool hasEmbedding)
    {
        if (hasEmbedding)
        {
            return $"INSERT INTO {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)} (id, data, embedding) VALUES (@id, @data::jsonb, @embedding)";
        }

        return $"INSERT INTO {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)} (id, data) VALUES (@id, @data::jsonb)";
    }

    /// <summary>
    /// Builds a DDL statement to create the pgvector extension, table, and index.
    /// </summary>
    internal static string BuildSchemaCreationDdl(
        string schema,
        string tableName,
        int vectorDimensions,
        PgVectorIndexType indexType,
        DistanceMetric metric = DistanceMetric.Cosine)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vectorDimensions, 1);

        var sb = new StringBuilder();

        sb.AppendLine("CREATE EXTENSION IF NOT EXISTS vector;");
        sb.AppendLine();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)} (");
        sb.AppendLine("    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),");
        sb.AppendLine("    data jsonb NOT NULL,");
        sb.AppendLine($"    embedding vector({vectorDimensions}),");
        sb.AppendLine("    created_at timestamptz DEFAULT now()");
        sb.AppendLine(");");
        sb.AppendLine();

        var opsClass = GetIndexOperatorClass(metric);
        var indexMethod = indexType switch
        {
            PgVectorIndexType.IvfFlat => "ivfflat",
            PgVectorIndexType.Hnsw => "hnsw",
            _ => throw new ArgumentOutOfRangeException(nameof(indexType), indexType, "Unsupported pgvector index type."),
        };

        var indexName = QuoteIdentifier($"idx_{tableName}_embedding");
        sb.Append($"CREATE INDEX IF NOT EXISTS {indexName} ON {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)} USING {indexMethod} (embedding {opsClass})");

        if (indexType == PgVectorIndexType.IvfFlat)
        {
            sb.Append(" WITH (lists = 100)");
        }

        sb.Append(';');

        return sb.ToString();
    }
}
