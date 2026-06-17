using System.Text;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Schema;
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
    /// Escapes a value destined for a single-quoted SQL string LITERAL by doubling
    /// any embedded single quotes per the SQL standard. Used for descriptor-derived
    /// key-property names that are interpolated into <c>data->>'...'</c> JSON-path
    /// literals (those keys are NOT parameter-bindable, so they must be escaped at
    /// generation time). Key names flow from C# member names today, but this layer
    /// does not provably constrain them — escaping makes the generated SQL safe
    /// regardless of the caller (review M1).
    /// </summary>
    internal static string EscapeStringLiteral(string literal) =>
        literal.Replace("'", "''");

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
    /// Builds a similarity query with pgvector ITERATIVE-SCAN knobs (DR-13/R8): the
    /// supplied <see cref="IterativeScanOptions"/> are emitted as transaction-scoped
    /// <c>SET LOCAL</c> statements, and the vector search is shaped as an
    /// index-ordered top-K CTE (<c>WITH ann AS (...)</c>) the outer query then
    /// projects from. A filter is applied in the OUTER query (post-CTE), so the CTE
    /// stays a pure index scan and the iterative scan supplies enough candidate rows
    /// for the outer filter to still satisfy <c>topK</c>.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="tableName">The vertex object table (snake_cased).</param>
    /// <param name="metric">The pgvector distance metric.</param>
    /// <param name="options">The iterative-scan knobs to apply via <c>SET LOCAL</c>.</param>
    /// <param name="whereClause">
    /// An OPTIONAL filter applied in the outer query (the same translated predicate
    /// <see cref="BuildSimilarityQuery"/> takes), or <c>null</c> for none.
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. The knobs target the HNSW index
    /// (<c>hnsw.*</c> GUCs). <c>SET LOCAL</c> is transaction-scoped, so the caller
    /// MUST run this composed statement inside an explicit transaction for the knobs
    /// to take effect and to guarantee they never leak onto a pooled connection. The
    /// GUC VALUES here are provider-controlled enum/int literals (never caller text),
    /// so they are safe to interpolate; the query vector and topK still bind via
    /// <c>@query</c> / <c>@topK</c>.
    /// </remarks>
    internal static string BuildIterativeScanSimilarityQuery(
        string schema,
        string tableName,
        DistanceMetric metric,
        IterativeScanOptions options,
        string? whereClause = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentNullException.ThrowIfNull(options);

        var op = GetDistanceOperator(metric);
        var qSchema = QuoteIdentifier(schema);
        var qTable = QuoteIdentifier(tableName);

        var sb = new StringBuilder();

        // 1. Transaction-scoped GUCs. The mode is always emitted; the numeric knobs
        //    only when supplied, so an unset knob leaves the session default.
        sb.Append($"SET LOCAL hnsw.iterative_scan = '{ToGucLiteral(options.Mode)}'; ");
        if (options.MaxScanTuples is { } maxScanTuples)
        {
            sb.Append($"SET LOCAL hnsw.max_scan_tuples = {maxScanTuples}; ");
        }

        if (options.EfSearch is { } efSearch)
        {
            sb.Append($"SET LOCAL hnsw.ef_search = {efSearch}; ");
        }

        // 2. The ANN CTE: an index-ordered top-K scan over the vector column. Kept a
        //    pure scan (no filter) so the planner uses the HNSW index for the
        //    ordered limit; the iterative scan over-fetches as needed.
        sb.Append("WITH ann AS (");
        sb.Append($"SELECT id, data, (embedding {op} @query) AS distance FROM {qSchema}.{qTable} ");
        sb.Append("ORDER BY distance LIMIT @topK) ");

        // 3. The outer query: project from the CTE, applying the filter post-scan.
        sb.Append("SELECT id, data, distance FROM ann");
        if (!string.IsNullOrEmpty(whereClause))
        {
            sb.Append($" WHERE {whereClause}");
        }

        sb.Append(" ORDER BY distance");

        return sb.ToString();
    }

    /// <summary>
    /// Maps an <see cref="IterativeScanMode"/> to its pgvector
    /// <c>hnsw.iterative_scan</c> GUC literal.
    /// </summary>
    private static string ToGucLiteral(IterativeScanMode mode) => mode switch
    {
        IterativeScanMode.Off => "off",
        IterativeScanMode.StrictOrder => "strict_order",
        IterativeScanMode.RelaxedOrder => "relaxed_order",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported iterative-scan mode."),
    };

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
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="tableName">The vertex object table name (snake_cased).</param>
    /// <param name="vectorDimensions">The embedding vector dimensionality.</param>
    /// <param name="indexType">The pgvector index method.</param>
    /// <param name="metric">The distance metric the vector index is built for.</param>
    /// <param name="keyPropertyName">
    /// The descriptor's key property name — the <c>data jsonb</c> field holding the
    /// vertex's BUSINESS id (DR-13/R2). When supplied, a <c>CREATE UNIQUE INDEX
    /// ... ((data->>'key'))</c> expression index is emitted so the relate/unrelate/
    /// traversal endpoint-resolution subqueries (<c>data->>'key' = @id</c>) resolve
    /// a SINGLE deterministic row per business id. When <c>null</c> (a
    /// pgvector-only table with no declared key, e.g. direct instantiation without
    /// a graph) no key index is emitted and the DDL is byte-identical to the
    /// pre-DR-13 lowering.
    /// </param>
    internal static string BuildSchemaCreationDdl(
        string schema,
        string tableName,
        int vectorDimensions,
        PgVectorIndexType indexType,
        DistanceMetric metric = DistanceMetric.Cosine,
        string? keyPropertyName = null)
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

        // DR-13/R2: the business-id KEY-PROPERTY unique expression index. The
        // relate/unrelate/traversal endpoint-resolution subqueries all key on
        // data->>'key' = @id and assume that resolves a SINGLE row; this index
        // makes that uniqueness a storage-layer guarantee. Omitted entirely for a
        // keyless (pgvector-only) table so the back-compat DDL is unchanged.
        if (!string.IsNullOrEmpty(keyPropertyName))
        {
            // The key name is interpolated into a single-quoted JSON-path literal
            // (not parameter-bindable), so escape any embedded apostrophe — the
            // same posture the relate SQL builders take (review M1).
            var key = EscapeStringLiteral(keyPropertyName);
            var keyIndexName = JunctionIdentifier.Derive($"ux_{tableName}_{TypeMapper.ToSnakeCase(keyPropertyName)}");
            sb.AppendLine();
            sb.Append(
                $"CREATE UNIQUE INDEX IF NOT EXISTS {QuoteIdentifier(keyIndexName)} "
                + $"ON {QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)} ((data->>'{key}'));");
        }

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
        sb.AppendLine(");");
        AppendReverseJunctionIndex(sb, schema, junctionTableName);

        return sb.ToString();
    }

    /// <summary>
    /// Appends the REVERSE-traversal index DDL (DR-13/R1) for a junction table.
    /// The <c>UNIQUE (source_id, target_id)</c> constraint already backs FORWARD
    /// traversal (source → target), but a composite index is prefix-ordered, so it
    /// does NOT serve a lookup keyed on <c>target_id</c> alone — reverse traversal
    /// (target → source) and a target-endpoint FK-cascade probe would otherwise
    /// fall back to a sequential scan. This emits an explicit
    /// <c>(target_id, source_id)</c> index so both directions are index-backed.
    /// </summary>
    /// <remarks>
    /// The index name is <c>ix_{junction}_target_source</c>, run through
    /// <see cref="JunctionIdentifier.Derive(string)"/> so it can never exceed
    /// PostgreSQL's silent 63-byte truncation limit (the junction name itself may
    /// already be near the cap). <c>IF NOT EXISTS</c> keeps the DDL idempotent,
    /// matching the <c>CREATE TABLE IF NOT EXISTS</c> posture. INV-2: raw DDL only.
    /// </remarks>
    private static void AppendReverseJunctionIndex(
        StringBuilder sb, string schema, string junctionTableName)
    {
        var indexName = JunctionIdentifier.Derive($"ix_{junctionTableName}_target_source");
        sb.Append(
            $"CREATE INDEX IF NOT EXISTS {QuoteIdentifier(indexName)} "
            + $"ON {QuoteIdentifier(schema)}.{QuoteIdentifier(junctionTableName)} (target_id, source_id);");
    }

    /// <summary>
    /// Resolves the snake_cased junction table name for a pure link
    /// <c>(source, link)</c> — the SAME identifier
    /// <see cref="BuildJunctionTableDdl"/> creates, so relate/unrelate writes
    /// and the schema DDL can never drift (DR-7).
    /// </summary>
    /// <param name="sourceTableName">
    /// The source endpoint's object table name (already snake_cased).
    /// </param>
    /// <param name="linkName">The link's descriptor name; snake_cased here.</param>
    internal static string JunctionTableName(string sourceTableName, string linkName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(linkName);
        return $"{sourceTableName}_{TypeMapper.ToSnakeCase(linkName)}";
    }

    /// <summary>
    /// Resolves the snake_cased junction table name for a resolved per-<c>(link,
    /// target-descriptor)</c> junction (DR-11, Posture 2). A MONOMORPHIC link
    /// (<see cref="JunctionTableDescriptor.IsPolymorphic"/> == <c>false</c>) names
    /// the table <c>{source}_{snake(link)}</c> — IDENTICAL to the pre-DR-11
    /// <see cref="JunctionTableName(string, string)"/> lowering, so the
    /// relate/traverse DML and the schema DDL stay in lockstep and the table count
    /// is unchanged. A POLYMORPHIC link additionally disambiguates each fanned-out
    /// table by its resolved target descriptor name
    /// (<c>{source}_{snake(link)}_{snake(targetDescriptor)}</c>) so the
    /// per-descriptor tables never collide. The result is passed through
    /// <see cref="JunctionIdentifier.Derive(string)"/> so it can never exceed
    /// PostgreSQL's silent 63-byte truncation limit.
    /// </summary>
    /// <param name="junction">The resolved per-(link, target-descriptor) identity.</param>
    /// <remarks>
    /// INV-8: identity is derived from the resolved descriptor NAME, never a CLR
    /// type — so a <c>SymbolKey</c>-only descriptor resolves a junction name too.
    /// </remarks>
    internal static string JunctionTableNameFor(JunctionTableDescriptor junction)
    {
        ArgumentNullException.ThrowIfNull(junction);

        var baseName = $"{junction.SourceTable}_{TypeMapper.ToSnakeCase(junction.LinkName)}";
        if (junction.IsPolymorphic)
        {
            baseName = $"{baseName}_{TypeMapper.ToSnakeCase(junction.TargetDescriptorName)}";
        }

        return JunctionIdentifier.Derive(baseName);
    }

    /// <summary>
    /// Builds the per-<c>(link, target-descriptor)</c> junction-table DDL (DR-11,
    /// Posture 2) for EACH resolved target descriptor a link lowers to. A
    /// monomorphic link yields a single-element list — ONE junction table, same
    /// count as the pre-DR-11 lowering. A link resolving (interface narrow /
    /// multi-registration) to several descriptors yields one table PER descriptor,
    /// each with a single HONEST foreign key to that descriptor's own object table
    /// (never a polymorphic/union endpoint).
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="resolvedTargets">
    /// The DR-10 graph-resolved target descriptors for one link. Each names its
    /// own source table, target table, and resolved target descriptor name.
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector DDL only — no Marten/Wolverine. Each table's
    /// <c>UNIQUE (source_id, target_id)</c> mirrors the in-memory relate-store's
    /// idempotency on the <c>(src, link, tgt)</c> triple within that descriptor
    /// partition. The derived junction names are checked PAIRWISE through
    /// <see cref="JunctionIdentifier.Derive(string, string)"/>, so two distinct
    /// resolved targets that would (after 63-byte truncation) collide onto one
    /// physical table surface a typed
    /// <see cref="OntologySchemaIdentifierException"/> rather than silently merging.
    /// </remarks>
    internal static IReadOnlyList<string> BuildJunctionTableDdlForResolvedTargets(
        string schema,
        IReadOnlyList<JunctionTableDescriptor> resolvedTargets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(resolvedTargets);

        var ddls = new List<string>(resolvedTargets.Count);
        var derivedNames = new List<string>(resolvedTargets.Count);

        foreach (var target in resolvedTargets)
        {
            var junctionName = JunctionTableNameFor(target);

            // Pairwise collision guard (DR-11/T1): a fan-out whose two resolved
            // descriptors derive the SAME junction identifier (after the 63-byte
            // truncation) would silently route both to one table. Surface it as a
            // typed error instead.
            foreach (var existing in derivedNames)
            {
                JunctionIdentifier.Derive(existing, junctionName);
            }

            derivedNames.Add(junctionName);
            ddls.Add(BuildJunctionTableDdlByName(schema, junctionName, target.SourceTable, target.TargetTable));
        }

        return ddls;
    }

    /// <summary>
    /// Builds one junction table's DDL from an already-derived junction name and
    /// the source/target object table names. Shared by
    /// <see cref="BuildJunctionTableDdlForResolvedTargets"/>; emits the same edge
    /// shape as <see cref="BuildJunctionTableDdl"/> (edge identity + two endpoint
    /// FKs + endpoint-pair uniqueness) but with the DR-11-resolved identifier.
    /// </summary>
    private static string BuildJunctionTableDdlByName(
        string schema,
        string junctionTableName,
        string sourceTableName,
        string targetTableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);

        var qSchema = QuoteIdentifier(schema);

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {qSchema}.{QuoteIdentifier(junctionTableName)} (");
        sb.AppendLine("    edge_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),");
        sb.AppendLine($"    source_id uuid NOT NULL REFERENCES {qSchema}.{QuoteIdentifier(sourceTableName)} (id),");
        sb.AppendLine($"    target_id uuid NOT NULL REFERENCES {qSchema}.{QuoteIdentifier(targetTableName)} (id),");
        sb.AppendLine("    UNIQUE (source_id, target_id)");
        sb.AppendLine(");");
        AppendReverseJunctionIndex(sb, schema, junctionTableName);

        return sb.ToString();
    }

    /// <summary>
    /// Builds the idempotent INSERT that materializes a pure-link relation
    /// (DR-7) into the T8 junction table. SUPERSEDED in the live relate path by the
    /// self-validating, single-snapshot
    /// <see cref="BuildValidatingRelateInsertSql"/> (DR-13/R4); retained as the
    /// minimal insert shape (the pre-R4 lowering) and pinned by the relate SQL-shape
    /// tests. The in-memory relate-store addresses
    /// endpoints by their projected BUSINESS id (a string), so this resolves
    /// each endpoint row's surrogate <c>id uuid</c> via a subquery against the
    /// endpoint object table's <c>data jsonb</c> key field, then inserts the
    /// resolved <c>(source_id, target_id)</c> pair.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="junctionTableName">
    /// The junction table name, as produced by
    /// <see cref="JunctionTableName(string, string)"/>.
    /// </param>
    /// <param name="sourceTableName">The source endpoint object table name.</param>
    /// <param name="sourceKeyProperty">
    /// The source descriptor's key property name — the <c>data jsonb</c> field
    /// holding the source's business id (INV-8: identity by descriptor).
    /// </param>
    /// <param name="targetTableName">The target endpoint object table name.</param>
    /// <param name="targetKeyProperty">The target descriptor's key property name.</param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. Idempotency rides the T8
    /// <c>UNIQUE(source_id, target_id)</c> via <c>ON CONFLICT DO NOTHING</c>,
    /// mirroring the in-memory store's idempotency on the <c>(src, link, tgt)</c>
    /// triple (the junction table is scoped to one link). Endpoint business ids
    /// are bound via the <c>@srcId</c> / <c>@tgtId</c> parameters, never
    /// interpolated, so the statement is injection-safe.
    /// </remarks>
    internal static string BuildRelateInsertSql(
        string schema,
        string junctionTableName,
        string sourceTableName,
        string sourceKeyProperty,
        string targetTableName,
        string targetKeyProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKeyProperty);

        var qSchema = QuoteIdentifier(schema);
        var qJunction = QuoteIdentifier(junctionTableName);
        var qSource = QuoteIdentifier(sourceTableName);
        var qTarget = QuoteIdentifier(targetTableName);
        var srcKey = EscapeStringLiteral(sourceKeyProperty);
        var tgtKey = EscapeStringLiteral(targetKeyProperty);

        return
            $"INSERT INTO {qSchema}.{qJunction} (source_id, target_id) "
            + $"SELECT s.id, t.id "
            + $"FROM {qSchema}.{qSource} s, {qSchema}.{qTarget} t "
            + $"WHERE s.data->>'{srcKey}' = @srcId AND t.data->>'{tgtKey}' = @tgtId "
            + "ON CONFLICT (source_id, target_id) DO NOTHING";
    }

    /// <summary>
    /// Builds the SELF-VALIDATING pure-link relate statement (DR-13/R4): a SINGLE
    /// statement that resolves both endpoints, performs the idempotent junction
    /// insert in a data-modifying CTE, and RETURNS the two endpoint-existence flags.
    /// This collapses the pre-DR-13 three commands (two eager <c>SELECT EXISTS</c>
    /// probes + one <c>INSERT</c>, each a separate round-trip on no shared snapshot)
    /// into one round-trip, and — because Postgres runs all <c>WITH</c>
    /// sub-statements under a SINGLE snapshot — closes the probe→insert TOCTOU
    /// window: a concurrent endpoint delete can no longer turn the expected typed
    /// error into a silent no-op.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="junctionTableName">
    /// The junction table name, as produced by
    /// <see cref="JunctionTableNameFor(JunctionTableDescriptor)"/> /
    /// <see cref="JunctionTableName(string, string)"/>.
    /// </param>
    /// <param name="sourceTableName">The source endpoint object table name.</param>
    /// <param name="sourceKeyProperty">The source descriptor's key property name.</param>
    /// <param name="targetTableName">The target endpoint object table name.</param>
    /// <param name="targetKeyProperty">The target descriptor's key property name.</param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. The data-modifying <c>ins</c> CTE is
    /// executed EXACTLY ONCE (a documented Postgres guarantee) even though the final
    /// <c>SELECT</c> does not read its output, and its cross join over the resolved
    /// endpoint CTEs writes ZERO rows when either endpoint is absent — so a missing
    /// endpoint leaves no dangling row. The final <c>SELECT</c> returns
    /// <c>src_exists</c> / <c>tgt_exists</c>; the provider reads them and raises the
    /// typed <see cref="ObjectSets.RelationEndpointNotFoundException"/> for whichever
    /// endpoint is missing. Idempotency still rides the junction
    /// <c>UNIQUE(source_id, target_id)</c> via <c>ON CONFLICT DO NOTHING</c>.
    /// Endpoint business ids bind via <c>@srcId</c> / <c>@tgtId</c>, never
    /// interpolated.
    /// </remarks>
    internal static string BuildValidatingRelateInsertSql(
        string schema,
        string junctionTableName,
        string sourceTableName,
        string sourceKeyProperty,
        string targetTableName,
        string targetKeyProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKeyProperty);

        var qSchema = QuoteIdentifier(schema);
        var qJunction = QuoteIdentifier(junctionTableName);
        var qSource = QuoteIdentifier(sourceTableName);
        var qTarget = QuoteIdentifier(targetTableName);
        var srcKey = EscapeStringLiteral(sourceKeyProperty);
        var tgtKey = EscapeStringLiteral(targetKeyProperty);

        return
            $"WITH s AS (SELECT id FROM {qSchema}.{qSource} WHERE data->>'{srcKey}' = @srcId), "
            + $"t AS (SELECT id FROM {qSchema}.{qTarget} WHERE data->>'{tgtKey}' = @tgtId), "
            + $"ins AS ("
            + $"INSERT INTO {qSchema}.{qJunction} (source_id, target_id) "
            + $"SELECT s.id, t.id FROM s, t "
            + $"ON CONFLICT (source_id, target_id) DO NOTHING"
            + $") "
            + $"SELECT EXISTS(SELECT 1 FROM s) AS src_exists, EXISTS(SELECT 1 FROM t) AS tgt_exists";
    }

    /// <summary>
    /// Builds the DELETE that removes a pure-link relation (DR-7) from the T8
    /// junction table, keyed on the endpoint pair resolved from business ids via
    /// the same <c>data->>'key'</c> subqueries as
    /// <see cref="BuildRelateInsertSql"/>. Removing a relation that does not
    /// exist deletes zero rows — a no-op (no throw), mirroring the in-memory
    /// store's unrelate posture.
    /// </summary>
    /// <param name="schema">The Postgres schema.</param>
    /// <param name="junctionTableName">The junction table name.</param>
    /// <param name="sourceTableName">The source endpoint object table name.</param>
    /// <param name="sourceKeyProperty">The source descriptor's key property name.</param>
    /// <param name="targetTableName">The target endpoint object table name.</param>
    /// <param name="targetKeyProperty">The target descriptor's key property name.</param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. Endpoint business ids bind via
    /// <c>@srcId</c> / <c>@tgtId</c>, never interpolated.
    /// </remarks>
    internal static string BuildUnrelateDeleteSql(
        string schema,
        string junctionTableName,
        string sourceTableName,
        string sourceKeyProperty,
        string targetTableName,
        string targetKeyProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKeyProperty);

        var qSchema = QuoteIdentifier(schema);
        var qJunction = QuoteIdentifier(junctionTableName);
        var qSource = QuoteIdentifier(sourceTableName);
        var qTarget = QuoteIdentifier(targetTableName);
        var srcKey = EscapeStringLiteral(sourceKeyProperty);
        var tgtKey = EscapeStringLiteral(targetKeyProperty);

        return
            $"DELETE FROM {qSchema}.{qJunction} "
            + $"WHERE source_id = (SELECT id FROM {qSchema}.{qSource} WHERE data->>'{srcKey}' = @srcId) "
            + $"AND target_id = (SELECT id FROM {qSchema}.{qTarget} WHERE data->>'{tgtKey}' = @tgtId)";
    }

    /// <summary>
    /// Builds the eager endpoint-existence probe (DR-8): a <c>SELECT EXISTS</c>
    /// over the endpoint object table testing whether some stored row's
    /// <c>data jsonb</c> key field equals the parameter-bound business id. The
    /// provider runs this for BOTH endpoints BEFORE the relate insert so a
    /// missing endpoint surfaces a typed
    /// <see cref="ObjectSets.RelationEndpointNotFoundException"/> and no junction
    /// row is written — the same eager posture the in-memory provider enforces.
    /// </summary>
    /// <param name="schema">The Postgres schema.</param>
    /// <param name="tableName">The endpoint object table name.</param>
    /// <param name="keyProperty">
    /// The endpoint descriptor's key property name (the <c>data jsonb</c> field
    /// holding the business id).
    /// </param>
    /// <param name="parameterName">
    /// The bound parameter name carrying the business id (e.g. <c>"@srcId"</c> /
    /// <c>"@tgtId"</c>) so the source and target probes are distinguishable.
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. The id is bound via
    /// <paramref name="parameterName"/>, never interpolated.
    /// </remarks>
    internal static string BuildEndpointExistsSql(
        string schema,
        string tableName,
        string keyProperty,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        var qSchema = QuoteIdentifier(schema);
        var qTable = QuoteIdentifier(tableName);
        var key = EscapeStringLiteral(keyProperty);

        return
            $"SELECT EXISTS(SELECT 1 FROM {qSchema}.{qTable} "
            + $"WHERE data->>'{key}' = {parameterName})";
    }

    /// <summary>
    /// Builds the instance-anchored <c>TraverseLink</c> lowering (DR-7/DR-10): a
    /// <c>vertex ⋈ junction ⋈ vertex</c> SQL join that, given a source instance
    /// addressed by its BUSINESS id, joins its endpoint object table → the T8
    /// junction table → the target endpoint object table on the surrogate
    /// <c>id uuid</c> columns and projects the target rows.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="sourceTableName">
    /// The source endpoint's object table name (snake_cased), aliased <c>s</c>.
    /// </param>
    /// <param name="sourceKeyProperty">
    /// The source descriptor's key property name — the <c>data jsonb</c> field
    /// holding the source's business id (INV-8: identity by descriptor). The
    /// traversal is ANCHORED to the single source whose stored row carries the
    /// <c>@srcId</c> business id.
    /// </param>
    /// <param name="junctionTableName">
    /// The junction table name, as produced by
    /// <see cref="JunctionTableName(string, string)"/> — the SAME physical table
    /// the relate/unrelate writes and the T8 DDL target, aliased <c>j</c>.
    /// </param>
    /// <param name="targetTableName">
    /// The TARGET endpoint's object table name (snake_cased), aliased <c>t</c>.
    /// DR-10 (INV-8): the target table is the GRAPH-resolved hop-target
    /// descriptor's table (see
    /// <c>PgVectorObjectSetProvider.ResolveTraversalHop</c>), NEVER
    /// <c>typeof(TLinked)</c>.
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only — no Marten/Wolverine. The source business
    /// id binds via the <c>@srcId</c> parameter, never interpolated, so the
    /// statement is injection-safe.
    /// <para>
    /// #114 / DR-8 guard at the SQL level: the junction is on the INNER join path,
    /// so a source with ZERO junction rows yields ZERO result rows — instance-
    /// anchored traversal never falls back to an all-targets scan (there is no
    /// LEFT/OUTER join and the target table is never the FROM root). This mirrors
    /// the in-memory evaluator's posture: an instance with no relation rows yields
    /// an empty set, never every object of the link's target type.
    /// </para>
    /// </remarks>
    internal static string BuildInstanceAnchoredTraversalSql(
        string schema,
        string sourceTableName,
        string sourceKeyProperty,
        string junctionTableName,
        string targetTableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(junctionTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);

        var qSchema = QuoteIdentifier(schema);
        var qSource = QuoteIdentifier(sourceTableName);
        var qJunction = QuoteIdentifier(junctionTableName);
        var qTarget = QuoteIdentifier(targetTableName);
        var srcKey = EscapeStringLiteral(sourceKeyProperty);

        return
            $"SELECT t.id, t.data "
            + $"FROM {qSchema}.{qSource} s "
            + $"JOIN {qSchema}.{qJunction} j ON j.source_id = s.id "
            + $"JOIN {qSchema}.{qTarget} t ON t.id = j.target_id "
            + $"WHERE s.data->>'{srcKey}' = @srcId";
    }

    /// <summary>
    /// Builds the instance-anchored traversal SQL for a POLYMORPHIC link (DR-11b):
    /// a <c>UNION ALL</c> over one <c>vertex ⋈ junction ⋈ vertex</c> join per
    /// resolved target descriptor, so a single anchored traversal reads back the
    /// related targets from EVERY per-(link, target-descriptor) junction table the
    /// link fanned out into (Posture 2). Each leg is the SAME shape
    /// <see cref="BuildInstanceAnchoredTraversalSql"/> emits and is anchored at the
    /// shared source business id via the same <c>@srcId</c> parameter.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="hops">
    /// The fanned-out hops, as produced by
    /// <c>PgVectorObjectSetProvider.ResolveTraversalHops</c>. Each names its own
    /// per-descriptor junction + target table; all share the source table and key.
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. <c>UNION ALL</c> (not <c>UNION</c>) — the
    /// per-descriptor target partitions are disjoint, so there are no cross-leg
    /// duplicates to dedupe, and <c>ALL</c> avoids a needless sort. A single hop is
    /// emitted as a plain join (no UNION), identical to
    /// <see cref="BuildInstanceAnchoredTraversalSql"/>, so the monomorphic lowering
    /// is unchanged. The source id binds via <c>@srcId</c>, never interpolated.
    /// </remarks>
    internal static string BuildPolymorphicTraversalSql(
        string schema,
        IReadOnlyList<PgVectorObjectSetProvider.TraversalHop> hops)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(hops);
        if (hops.Count == 0)
        {
            throw new ArgumentException("At least one traversal hop is required.", nameof(hops));
        }

        var legs = hops.Select(hop => BuildInstanceAnchoredTraversalSql(
            schema, hop.SourceTable, hop.SourceKeyProperty, hop.JunctionTable, hop.TargetTable));

        return string.Join(" UNION ALL ", legs);
    }

    /// <summary>
    /// The DR-12 JOIN-CHAIN depth budget: a traversal of this many or fewer
    /// monomorphic hops lowers to a single flat <c>vertex ⋈ junction ⋈ vertex ⋈ …</c>
    /// join chain, which the Postgres planner can reorder freely. Past the budget,
    /// the lowering switches to a recursive CTE.
    /// </summary>
    /// <remarks>
    /// Sized to the planner's join-reordering window. PostgreSQL exhaustively
    /// reorders a join tree only while the number of joined relations stays under
    /// <c>join_collapse_limit</c> (default 8) and below <c>geqo_threshold</c>
    /// (default 12); past that the genetic query optimizer takes over and plans
    /// degrade. Each traversal hop contributes a junction relation AND a vertex
    /// relation (two relations per hop), plus the anchor vertex, so a 3-hop chain
    /// joins 1 + 3×2 = 7 relations — the last depth that stays safely inside the
    /// default <c>join_collapse_limit</c> of 8. A POLYMORPHIC hop is NOT counted
    /// here: it is fan-out (a UNION over per-descriptor legs), so any plan with a
    /// polymorphic step is lowered via the CTE tier regardless of depth (see
    /// <see cref="BuildDepthTieredTraversalSql"/>).
    /// </remarks>
    internal const int JoinChainDepthBudget = 3;

    /// <summary>
    /// Lowers a resolved instance-anchored <see cref="PgVectorObjectSetProvider.TraversalPlan"/>
    /// into depth-tiered traversal SQL (DR-12). A plan within
    /// <see cref="JoinChainDepthBudget"/> hops with NO polymorphic step lowers to a
    /// flat JOIN CHAIN; a deeper plan — or any plan whose step fans out
    /// polymorphically (a polymorphic hop counts as fan-out against the budget) —
    /// lowers to a RECURSIVE CTE.
    /// </summary>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. The anchor business id binds via
    /// <c>@srcId</c>, never interpolated. Each per-descriptor leg of a polymorphic
    /// hop reuses the SAME <c>vertex ⋈ junction ⋈ vertex</c> shape
    /// <see cref="BuildInstanceAnchoredTraversalSql"/> emits (DR-11b seam).
    /// </remarks>
    internal static string BuildDepthTieredTraversalSql(
        string schema,
        PgVectorObjectSetProvider.TraversalPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Steps.Count == 0)
        {
            throw new ArgumentException("A traversal plan requires at least one depth step.", nameof(plan));
        }

        // Tier selection: a polymorphic step is fan-out, so it pushes past the
        // join-chain budget into the recursive-CTE tier even when depth is within
        // budget; likewise a chain deeper than the budget.
        var useRecursiveCte = plan.HasPolymorphicStep || plan.Depth > JoinChainDepthBudget;
        return useRecursiveCte
            ? BuildRecursiveCteTraversalSql(schema, plan)
            : BuildJoinChainTraversalSql(schema, plan);
    }

    /// <summary>
    /// Lowers a within-budget, all-monomorphic plan to a flat join chain (DR-12,
    /// T6): <c>vertex_s ⋈ junction_1 ⋈ vertex_1 ⋈ junction_2 ⋈ vertex_2 ⋈ …</c>,
    /// anchored at the source's business id and projecting the FINAL hop's target
    /// rows. The Postgres planner reorders this freely within
    /// <see cref="JoinChainDepthBudget"/>.
    /// </summary>
    private static string BuildJoinChainTraversalSql(
        string schema,
        PgVectorObjectSetProvider.TraversalPlan plan)
    {
        var qSchema = QuoteIdentifier(schema);
        var qSource = QuoteIdentifier(plan.SourceTable);
        var srcKey = EscapeStringLiteral(plan.SourceKeyProperty);

        var sb = new StringBuilder();

        // The FINAL step's target vertex is projected; alias each step's junction
        // and target vertex so a multi-hop chain stays unambiguous.
        var lastStepIndex = plan.Steps.Count - 1;
        var targetAlias = $"t{lastStepIndex}";

        sb.Append($"SELECT {targetAlias}.id, {targetAlias}.data ");
        sb.Append($"FROM {qSchema}.{qSource} s");

        var previousVertexAlias = "s";
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            // A join-chain step is monomorphic (the tier selector routed any
            // polymorphic step to the CTE tier), so it has exactly one hop.
            var hop = plan.Steps[i].Hops[0];
            var junctionAlias = $"j{i}";
            var vertexAlias = $"t{i}";
            var qJunction = QuoteIdentifier(hop.JunctionTable);
            var qTarget = QuoteIdentifier(hop.TargetTable);

            sb.Append($" JOIN {qSchema}.{qJunction} {junctionAlias} ON {junctionAlias}.source_id = {previousVertexAlias}.id");
            sb.Append($" JOIN {qSchema}.{qTarget} {vertexAlias} ON {vertexAlias}.id = {junctionAlias}.target_id");
            previousVertexAlias = vertexAlias;
        }

        sb.Append($" WHERE s.data->>'{srcKey}' = @srcId");
        return sb.ToString();
    }

    /// <summary>
    /// Lowers a beyond-budget OR polymorphic plan to a RECURSIVE CTE (DR-12, T7).
    /// The CTE walks the junction edges level by level from the anchor, so a deep
    /// or variable-depth chain — and a polymorphic step's UNION fan-out — is
    /// expressed without an unbounded flat join the planner would mishandle past
    /// <see cref="JoinChainDepthBudget"/>.
    /// </summary>
    private static string BuildRecursiveCteTraversalSql(
        string schema,
        PgVectorObjectSetProvider.TraversalPlan plan)
    {
        var qSchema = QuoteIdentifier(schema);
        var qSource = QuoteIdentifier(plan.SourceTable);
        var srcKey = EscapeStringLiteral(plan.SourceKeyProperty);

        // Base case: the anchor's surrogate id, addressed by its business id.
        // Recursive case: for each frontier id, the next step's target ids reached
        // through that step's junction table(s). A polymorphic step contributes one
        // UNION ALL leg per per-descriptor junction table (DR-11b fan-out); the
        // recursion advances by one depth level per step.
        var sb = new StringBuilder();
        sb.Append("WITH RECURSIVE traversal(node_id, depth) AS (");
        sb.Append($"SELECT s.id, 0 FROM {qSchema}.{qSource} s WHERE s.data->>'{srcKey}' = @srcId");
        sb.Append(" UNION ALL ");

        // Each step's edges, selected by the recursion's current depth so the walk
        // follows the chain's link sequence rather than any junction.
        var stepLegs = new List<string>();
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            foreach (var hop in step.Hops)
            {
                var qJunction = QuoteIdentifier(hop.JunctionTable);
                stepLegs.Add(
                    $"SELECT {qJunction}.target_id, tr.depth + 1 "
                    + $"FROM traversal tr JOIN {qSchema}.{qJunction} {qJunction} "
                    + $"ON {qJunction}.source_id = tr.node_id AND tr.depth = {i}");
            }
        }

        sb.Append(string.Join(" UNION ALL ", stepLegs));
        sb.Append(") ");

        // Project the FINAL-depth target rows from each terminal step's target
        // table(s). The terminal step is the last; its hops' target tables hold the
        // far endpoints reached at the maximum depth.
        var terminalStep = plan.Steps[^1];
        var projections = new List<string>();
        foreach (var hop in terminalStep.Hops)
        {
            var qTarget = QuoteIdentifier(hop.TargetTable);
            projections.Add(
                $"SELECT t.id, t.data FROM {qSchema}.{qTarget} t "
                + $"JOIN traversal tr ON tr.node_id = t.id AND tr.depth = {plan.Steps.Count}");
        }

        sb.Append(string.Join(" UNION ALL ", projections));
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

        // Two endpoint roles that normalize to the SAME {role}_id column would emit
        // a duplicate column and fail at runtime with an opaque Postgres "column
        // specified more than once". ToSnakeCase only lowercases/underscores, so
        // roles that differ only by case or separators (e.g. "Owner" / "owner")
        // collide after normalization. Surface a typed, actionable error here.
        var roleColumns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var endpoint in association.AssociationEndpoints)
        {
            var roleColumn = $"{TypeMapper.ToSnakeCase(endpoint.Role)}_id";
            if (!roleColumns.Add(roleColumn))
            {
                throw new ArgumentException(
                    $"Association '{association.Name}' has endpoint roles that normalize to the same "
                    + $"FK column '{roleColumn}'. Endpoint roles must be distinct after snake_case "
                    + "normalization so each endpoint maps to its own column; rename one of the roles.",
                    nameof(association));
            }
        }

        var qSchema = QuoteIdentifier(schema);
        var tableName = TypeMapper.ToSnakeCase(association.Name);

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {qSchema}.{QuoteIdentifier(tableName)} (");
        sb.AppendLine("    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),");
        sb.AppendLine("    data jsonb NOT NULL,");

        foreach (var endpoint in association.AssociationEndpoints)
        {
            // QuoteIdentifier the {role}_id column so it stays identifier-identical
            // with the DML in BuildAssociationRelateInsertSql (review M1):
            // ToSnakeCase only lowercases/underscores — it does not neutralize a
            // quote or space in a role name. Quoting in BOTH positions keeps the
            // DDL column and the INSERT column the SAME physical identifier.
            var columnName = QuoteIdentifier($"{TypeMapper.ToSnakeCase(endpoint.Role)}_id");
            var endpointTable = TypeMapper.ToSnakeCase(endpoint.DescriptorName);
            sb.AppendLine(
                $"    {columnName} uuid NOT NULL REFERENCES {qSchema}.{QuoteIdentifier(endpointTable)} (id),");
        }

        sb.AppendLine("    created_at timestamptz DEFAULT now()");
        sb.Append(");");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the INSERT that materializes an ATTRIBUTED relation (DR-4/DR-8,
    /// t11) into a T8 association-OBJECT table. SUPERSEDED in the live attributed
    /// relate path by the self-validating, single-snapshot
    /// <see cref="BuildValidatingAssociationRelateInsertSql"/> (DR-13/R4); retained
    /// as the minimal insert shape and pinned by the association SQL-shape tests.
    /// Unlike the pure-link junction
    /// insert (<see cref="BuildRelateInsertSql"/>), an association row carries
    /// its OWN identity and edge attributes, so this writes a fresh
    /// <c>id uuid</c>, the serialized association payload as <c>data jsonb</c>,
    /// and one <c>{role}_id</c> endpoint FK per endpoint — each resolved from
    /// the endpoint's BUSINESS id via the same <c>data->>'key'</c> subquery
    /// against its object table that the junction relate uses (the in-memory
    /// store addresses endpoints by their projected business id).
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="associationTableName">
    /// The association-object table name, as produced for the association
    /// descriptor by <see cref="BuildAssociationObjectTableDdl"/> /
    /// <see cref="TypeMapper.ToSnakeCase(string)"/>.
    /// </param>
    /// <param name="sourceColumn">
    /// The <c>{role}_id</c> FK column for the SOURCE endpoint (the
    /// role-disambiguated column from the T8 association-object DDL).
    /// </param>
    /// <param name="sourceTableName">The source endpoint object table name.</param>
    /// <param name="sourceKeyProperty">
    /// The source descriptor's key property name — the <c>data jsonb</c> field
    /// holding the source's business id (INV-8: identity by descriptor).
    /// </param>
    /// <param name="targetColumn">The <c>{role}_id</c> FK column for the TARGET endpoint.</param>
    /// <param name="targetTableName">The target endpoint object table name.</param>
    /// <param name="targetKeyProperty">The target descriptor's key property name.</param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. The association id and attribute payload
    /// bind via <c>@id</c> / <c>@data</c>; the endpoint business ids bind via
    /// <c>@srcId</c> / <c>@tgtId</c> — never interpolated, so the statement is
    /// injection-safe. The endpoint FK columns are role-disambiguated so a
    /// self-association (both endpoints the same object table) still routes each
    /// surrogate id to its own column.
    /// </remarks>
    internal static string BuildAssociationRelateInsertSql(
        string schema,
        string associationTableName,
        string sourceColumn,
        string sourceTableName,
        string sourceKeyProperty,
        string targetColumn,
        string targetTableName,
        string targetKeyProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(associationTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKeyProperty);

        var qSchema = QuoteIdentifier(schema);
        var qAssoc = QuoteIdentifier(associationTableName);
        var qSource = QuoteIdentifier(sourceTableName);
        var qTarget = QuoteIdentifier(targetTableName);
        var srcKey = EscapeStringLiteral(sourceKeyProperty);
        var tgtKey = EscapeStringLiteral(targetKeyProperty);

        // The {role}_id endpoint FK columns are routed through QuoteIdentifier so
        // they stay identifier-identical with the T8 association-object DDL
        // (BuildAssociationObjectTableDdl), which now also quotes them (review
        // M1). ToSnakeCase only lowercases/underscores — it does NOT neutralize a
        // quote or space in a role name — so the column identifier must be quoted
        // in BOTH the DDL and the DML to stay the SAME physical column.
        var qSourceColumn = QuoteIdentifier(sourceColumn);
        var qTargetColumn = QuoteIdentifier(targetColumn);

        return
            $"INSERT INTO {qSchema}.{qAssoc} (id, data, {qSourceColumn}, {qTargetColumn}) "
            + $"SELECT @id, @data::jsonb, s.id, t.id "
            + $"FROM {qSchema}.{qSource} s, {qSchema}.{qTarget} t "
            + $"WHERE s.data->>'{srcKey}' = @srcId AND t.data->>'{tgtKey}' = @tgtId";
    }

    /// <summary>
    /// Builds the SELF-VALIDATING ATTRIBUTED relate statement (DR-13/R4): the
    /// attributed-relate counterpart to
    /// <see cref="BuildValidatingRelateInsertSql"/>. A SINGLE statement resolves
    /// both endpoints, inserts the association-object row (its own <c>@id</c> +
    /// <c>@data</c> + the two role-named endpoint FK columns) in a data-modifying
    /// CTE, and RETURNS the endpoint-existence flags. Collapses the three-round-trip
    /// (two probes + insert) attributed relate into one and closes the same TOCTOU
    /// window via the single-snapshot <c>WITH</c> semantics.
    /// </summary>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <param name="associationTableName">The association-object table name.</param>
    /// <param name="sourceColumn">The <c>{role}_id</c> FK column for the SOURCE endpoint.</param>
    /// <param name="sourceTableName">The source endpoint object table name.</param>
    /// <param name="sourceKeyProperty">The source descriptor's key property name.</param>
    /// <param name="targetColumn">The <c>{role}_id</c> FK column for the TARGET endpoint.</param>
    /// <param name="targetTableName">The target endpoint object table name.</param>
    /// <param name="targetKeyProperty">The target descriptor's key property name.</param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. As with the plain path, the data-modifying
    /// <c>ins</c> CTE runs exactly once under the same snapshot as the existence
    /// probe; its cross join writes ZERO rows when either endpoint is absent, so a
    /// missing endpoint leaves no dangling association row and the returned
    /// <c>src_exists</c> / <c>tgt_exists</c> flags drive the typed
    /// <see cref="ObjectSets.RelationEndpointNotFoundException"/>. The association id
    /// and payload bind via <c>@id</c> / <c>@data</c>; endpoint ids via
    /// <c>@srcId</c> / <c>@tgtId</c> — never interpolated. The role FK columns are
    /// quoted to stay identifier-identical with the association-object DDL.
    /// </remarks>
    internal static string BuildValidatingAssociationRelateInsertSql(
        string schema,
        string associationTableName,
        string sourceColumn,
        string sourceTableName,
        string sourceKeyProperty,
        string targetColumn,
        string targetTableName,
        string targetKeyProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(associationTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetColumn);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKeyProperty);

        var qSchema = QuoteIdentifier(schema);
        var qAssoc = QuoteIdentifier(associationTableName);
        var qSource = QuoteIdentifier(sourceTableName);
        var qTarget = QuoteIdentifier(targetTableName);
        var srcKey = EscapeStringLiteral(sourceKeyProperty);
        var tgtKey = EscapeStringLiteral(targetKeyProperty);
        var qSourceColumn = QuoteIdentifier(sourceColumn);
        var qTargetColumn = QuoteIdentifier(targetColumn);

        return
            $"WITH s AS (SELECT id FROM {qSchema}.{qSource} WHERE data->>'{srcKey}' = @srcId), "
            + $"t AS (SELECT id FROM {qSchema}.{qTarget} WHERE data->>'{tgtKey}' = @tgtId), "
            + $"ins AS ("
            + $"INSERT INTO {qSchema}.{qAssoc} (id, data, {qSourceColumn}, {qTargetColumn}) "
            + $"SELECT @id, @data::jsonb, s.id, t.id FROM s, t"
            + $") "
            + $"SELECT EXISTS(SELECT 1 FROM s) AS src_exists, EXISTS(SELECT 1 FROM t) AS tgt_exists";
    }

    /// <summary>
    /// Builds the DELETE that removes an ATTRIBUTED relation (DR-4, t11) from a
    /// T8 association-OBJECT table, keyed on the association's BUSINESS id (its
    /// <c>data jsonb</c> key field) — the symmetric counterpart to
    /// <see cref="BuildAssociationRelateInsertSql"/>. Removing an association
    /// that does not exist deletes zero rows — a no-op (no throw), mirroring the
    /// in-memory store's attributed-unrelate posture.
    /// </summary>
    /// <param name="schema">The Postgres schema.</param>
    /// <param name="associationTableName">The association-object table name.</param>
    /// <param name="associationKeyProperty">
    /// The association descriptor's key property name — the <c>data jsonb</c>
    /// field holding the association's business id (INV-8).
    /// </param>
    /// <remarks>
    /// INV-2: raw Npgsql/pgvector only. The association business id binds via
    /// <c>@associationId</c>, never interpolated.
    /// </remarks>
    internal static string BuildAssociationUnrelateDeleteSql(
        string schema,
        string associationTableName,
        string associationKeyProperty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(associationTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(associationKeyProperty);

        var qSchema = QuoteIdentifier(schema);
        var qAssoc = QuoteIdentifier(associationTableName);
        var assocKey = EscapeStringLiteral(associationKeyProperty);

        return
            $"DELETE FROM {qSchema}.{qAssoc} "
            + $"WHERE data->>'{assocKey}' = @associationId";
    }
}
