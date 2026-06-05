using System.Text;
using Strategos.Ontology.Descriptors;
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

    /// <summary>
    /// Builds the DDL for a <b>pure link</b> lowered to a <b>junction table</b>
    /// (DR-7). A pure link <c>(srcId, linkName, tgtId)</c> from one object type
    /// to another becomes a row in a junction table whose two endpoint columns
    /// (<c>source_id</c>, <c>target_id</c>) are foreign keys to the source and
    /// target object tables, plus an <c>edge_id</c> identity column.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="sourceTableName">
    /// The source endpoint's object table name (already snake_cased, as resolved
    /// by <see cref="TypeMapper.ToSnakeCase(string)"/> at the call site for a
    /// descriptor name).
    /// </param>
    /// <param name="linkName">
    /// The link's descriptor name (e.g. <c>"WrittenBy"</c>); snake_cased here to
    /// form the junction table name <c>{source}_{link}</c>.
    /// </param>
    /// <param name="targetTableName">The target endpoint's object table name.</param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector DDL only — no Marten/Wolverine. The unique
    /// constraint on <c>(source_id, target_id)</c> mirrors the in-memory
    /// relate-store's idempotency on the <c>(src, link, tgt)</c> triple: the
    /// table is scoped to a single link, so the endpoint pair is the triple's
    /// natural key.
    /// </remarks>
    internal static string BuildJunctionTableDdl(
        string schema,
        string sourceTableName,
        string linkName,
        string targetTableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(linkName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);

        var junctionTableName = $"{sourceTableName}_{TypeMapper.ToSnakeCase(linkName)}";
        var qSchema = QuoteIdentifier(schema);

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {qSchema}.{QuoteIdentifier(junctionTableName)} (");
        sb.AppendLine("    edge_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),");
        sb.AppendLine($"    source_id uuid NOT NULL REFERENCES {qSchema}.{QuoteIdentifier(sourceTableName)} (id),");
        sb.AppendLine($"    target_id uuid NOT NULL REFERENCES {qSchema}.{QuoteIdentifier(targetTableName)} (id),");
        sb.AppendLine("    UNIQUE (source_id, target_id)");
        sb.Append(");");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the DDL for a reified <b>association</b>
    /// (<see cref="ObjectKind.Association"/>) lowered to an <b>object table</b>
    /// (DR-7). An association is a standalone object that links two endpoints and
    /// may carry its own edge attributes, so it gets an object table
    /// (<c>id</c> + <c>data jsonb</c>) plus one foreign-key column per endpoint,
    /// each named for the endpoint's role and referencing the endpoint's object
    /// table.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="association">
    /// The association descriptor. Must have <see cref="ObjectTypeDescriptor.Kind"/>
    /// of <see cref="ObjectKind.Association"/> and exactly two
    /// <see cref="ObjectTypeDescriptor.AssociationEndpoints"/> (DR-4).
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector DDL only. Each endpoint column is named
    /// <c>{role}_id</c> (snake_cased) so a self-association — both endpoints the
    /// same object type — still yields two distinct, role-disambiguated columns.
    /// The endpoint table name is derived from the endpoint's
    /// <see cref="AssociationEndpoint.DescriptorName"/> (INV-8: identity by
    /// descriptor name, never <c>typeof</c>).
    /// </remarks>
    internal static string BuildAssociationObjectTableDdl(
        string schema,
        ObjectTypeDescriptor association)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(association);

        if (association.Kind != ObjectKind.Association)
        {
            throw new ArgumentException(
                $"Descriptor '{association.Name}' has Kind '{association.Kind}', not "
                + $"'{ObjectKind.Association}'; only association descriptors lower to an "
                + "association-object table.",
                nameof(association));
        }

        if (association.AssociationEndpoints.Count != 2)
        {
            throw new ArgumentException(
                $"Association '{association.Name}' declares {association.AssociationEndpoints.Count} "
                + "endpoint(s); a reified association object table requires exactly two (DR-4).",
                nameof(association));
        }

        var qSchema = QuoteIdentifier(schema);
        var tableName = TypeMapper.ToSnakeCase(association.Name);

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {qSchema}.{QuoteIdentifier(tableName)} (");
        sb.AppendLine("    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),");
        sb.AppendLine("    data jsonb NOT NULL,");

        foreach (var endpoint in association.AssociationEndpoints)
        {
            var columnName = $"{TypeMapper.ToSnakeCase(endpoint.Role)}_id";
            var endpointTable = TypeMapper.ToSnakeCase(endpoint.DescriptorName);
            sb.AppendLine(
                $"    {columnName} uuid NOT NULL REFERENCES {qSchema}.{QuoteIdentifier(endpointTable)} (id),");
        }

        sb.AppendLine("    created_at timestamptz DEFAULT now()");
        sb.Append(");");

        return sb.ToString();
    }
}
