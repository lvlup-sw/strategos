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
        sb.Append(");");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the idempotent INSERT that materializes a pure-link relation
    /// (DR-7) into the T8 junction table. The in-memory relate-store addresses
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
    /// t11) into a T8 association-OBJECT table. Unlike the pure-link junction
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
