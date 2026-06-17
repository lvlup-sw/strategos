using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql;

/// <summary>
/// PostgreSQL pgvector-backed implementation of object set provider and writer.
/// Stores objects as JSONB with optional vector embeddings for similarity search.
/// </summary>
[RequiresDynamicCode("JSON serialization of generic types requires dynamic code.")]
[RequiresUnreferencedCode("JSON serialization of generic types requires unreferenced code.")]
public sealed class PgVectorObjectSetProvider : IObjectSetProvider, IObjectSetWriter
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<PgVectorObjectSetProvider> _logger;
    private readonly PgVectorOptions _options;
    private readonly OntologyGraph? _graph;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgVectorObjectSetProvider"/> class.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="embeddingProvider">The embedding provider used for vector generation.</param>
    /// <param name="options">The pgvector provider options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="graph">
    /// Optional ontology graph used by the default-named write overloads
    /// (<see cref="StoreAsync{T}(T, CancellationToken)"/>,
    /// <see cref="StoreBatchAsync{T}(IReadOnlyList{T}, CancellationToken)"/>,
    /// <see cref="EnsureSchemaAsync{T}(string?, CancellationToken)"/>) to look up
    /// the registered descriptor name for a CLR type. When <c>null</c> (e.g. in
    /// direct unit-test instantiation) the default-overload resolution falls back
    /// to <c>typeof(T).Name</c> via <see cref="TypeMapper.ToSnakeCase(string)"/>.
    /// </param>
    public PgVectorObjectSetProvider(
        NpgsqlDataSource dataSource,
        IEmbeddingProvider embeddingProvider,
        IOptions<PgVectorOptions> options,
        ILogger<PgVectorObjectSetProvider> logger,
        OntologyGraph? graph = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(embeddingProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _dataSource = dataSource;
        _embeddingProvider = embeddingProvider;
        _options = options.Value;
        _logger = logger;
        _graph = graph;
    }

    /// <summary>
    /// Resolves the snake_case PostgreSQL table name for a read-path expression
    /// from the descriptor name declared on the expression's root, normalising
    /// it via <see cref="TypeMapper.ToSnakeCase(string)"/>.
    ///
    /// Replaces the prior <c>TypeMapper.GetTableName&lt;T&gt;()</c> lookup,
    /// which silently collapsed to <c>typeof(T).Name</c> and routed queries to
    /// the wrong physical table when a CLR type was registered under multiple
    /// ontology descriptors (bug #31 / Strategos 2.4.1).
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal</c> so the three read-path methods
    /// (<see cref="ExecuteSimilarityAsync{T}"/>, <see cref="ExecuteAsync{T}"/>,
    /// <see cref="StreamAsync{T}"/>) share a single dispatch step and unit tests
    /// can pin its behavior without needing a live
    /// <see cref="NpgsqlDataSource"/>.
    /// </remarks>
    internal static string ResolveTableName(ObjectSetExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return TypeMapper.ToSnakeCase(expression.RootObjectTypeName);
    }

    /// <summary>
    /// Resolves the snake_case PostgreSQL table name for a write-path call
    /// from an explicit descriptor name supplied by the caller (typically
    /// via the <see cref="IObjectSetWriter"/> explicit-name overloads).
    /// </summary>
    /// <remarks>
    /// Symmetric write-path counterpart to <see cref="ResolveTableName(ObjectSetExpression)"/>.
    /// Exposed as <c>internal</c> so the explicit-name <c>StoreAsync</c> /
    /// <c>StoreBatchAsync</c> overloads share a single dispatch step and unit
    /// tests can pin its behavior without needing a live
    /// <see cref="NpgsqlDataSource"/>. The default-named write overloads
    /// dispatch via <see cref="ResolveTableNameForDefaultOverload{T}(OntologyGraph?)"/>.
    /// </remarks>
    internal static string ResolveTableNameForDescriptor(string descriptorName)
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
        return TypeMapper.ToSnakeCase(descriptorName);
    }

    /// <summary>
    /// Resolves the snake_case PostgreSQL table name for a write-path call
    /// made via the default (no-name) overloads, using the ontology graph's
    /// reverse index to look up the descriptor name registered for
    /// <typeparamref name="T"/>. Falls back to <c>typeof(T).Name</c> only
    /// when <paramref name="graph"/> is <c>null</c>; throws
    /// <see cref="InvalidOperationException"/> when the graph is present
    /// but the type is not registered.
    /// </summary>
    /// <remarks>
    /// Symmetric write-path counterpart to <see cref="ResolveTableName(ObjectSetExpression)"/>
    /// for callers that do not thread an <see cref="ObjectSetExpression"/>.
    /// Replaces the prior <c>TypeMapper.GetTableName&lt;T&gt;()</c> lookup
    /// which silently collapsed to <c>typeof(T).Name</c> and routed writes
    /// to the wrong physical table when a CLR type was registered under
    /// multiple ontology descriptors (bug #31 / Strategos 2.4.1). Throws
    /// <see cref="InvalidOperationException"/> when the graph contains more
    /// than one descriptor for the type — the default overload cannot
    /// safely pick one and callers must use the explicit-name overload.
    /// Exposed as <c>internal static</c> so unit tests can pin its behavior
    /// without a live <see cref="NpgsqlDataSource"/>.
    /// </remarks>
    internal static string ResolveTableNameForDefaultOverload<T>(OntologyGraph? graph)
    {
        if (graph is not null)
        {
            if (!graph.ObjectTypeNamesByType.TryGetValue(typeof(T), out var names))
            {
                throw new InvalidOperationException(
                    $"Type '{typeof(T).FullName}' is not registered in the ontology graph. " +
                    $"Register it via Object<T>(...) in a DomainOntology, or use the explicit-name " +
                    $"StoreAsync<T>(string descriptorName, T item, ct) overload.");
            }

            if (names.Count == 1)
            {
                return TypeMapper.ToSnakeCase(names[0]);
            }

            throw new InvalidOperationException(
                $"Type '{typeof(T).FullName}' has multiple registrations " +
                $"({string.Join(", ", names.Select(n => $"'{n}'"))}). " +
                $"Use StoreAsync<T>(string descriptorName, T item, ct) or " +
                $"StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, ct) " +
                $"to specify the target descriptor.");
        }

        // Graph absent — fall back to typeof(T).Name → snake_case for
        // back-compat with direct unit-test instantiation and DI
        // configurations that do not resolve an OntologyGraph.
        return TypeMapper.ToSnakeCase(typeof(T).Name);
    }

    /// <inheritdoc />
    public async Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        // 1. Get query vector: use expression.QueryVector if provided, else embed QueryText
        var queryVector = expression.QueryVector
            ?? await _embeddingProvider.EmbedAsync(expression.QueryText, ct).ConfigureAwait(false);

        ValidateEmbedding<T>(queryVector);

        // 2. Resolve table name from the expression's declared descriptor name
        //    (honors explicit names supplied via GetObjectSet<T>(descriptorName);
        //    see ResolveTableName docs / bug #31).
        var tableName = ResolveTableName(expression);

        // 3. Translate Source expression (handles Filter, Include, Root transparently)
        var sourceTranslation = ExpressionTranslator.Translate(expression.Source);
        var whereClause = sourceTranslation.WhereClause;
        var filterParams = sourceTranslation.Parameters;

        // 4. Execute. When iterative-scan knobs are configured (DR-13/R8) the search
        //    is an ANN CTE run inside a transaction so the SET LOCAL knobs take
        //    effect; otherwise the pre-DR-13 single-statement query is used.
        var items = new List<T>();
        var scores = new List<double>();

        if (_options.IterativeScan is { } iterativeScan)
        {
            var composedSql = SqlGenerator.BuildIterativeScanSimilarityQuery(
                _options.Schema, tableName, expression.Metric, iterativeScan, whereClause);

            // SET LOCAL is transaction-scoped: the knobs apply only inside this
            // transaction and never leak onto the pooled connection.
            await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var scopedCmd = new NpgsqlCommand(composedSql, connection, transaction);
            BindSimilarityParameters(scopedCmd, queryVector, expression.TopK, filterParams);

            await ReadSimilarityRowsAsync(scopedCmd, expression, items, scores, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            return new ScoredObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties, scores);
        }

        var sql = SqlGenerator.BuildSimilarityQuery(_options.Schema, tableName, expression.Metric, whereClause);

        await using var cmd = _dataSource.CreateCommand(sql);
        BindSimilarityParameters(cmd, queryVector, expression.TopK, filterParams);

        await ReadSimilarityRowsAsync(cmd, expression, items, scores, ct).ConfigureAwait(false);

        return new ScoredObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties, scores);
    }

    /// <summary>
    /// Binds the shared similarity parameters (<c>@query</c>, <c>@topK</c>, and any
    /// translated filter params) onto a command, so the plain and iterative-scan
    /// (DR-13/R8) similarity paths bind identically.
    /// </summary>
    private static void BindSimilarityParameters(
        NpgsqlCommand cmd,
        float[] queryVector,
        int topK,
        IReadOnlyList<ExpressionTranslator.SqlParameter> filterParams)
    {
        cmd.Parameters.AddWithValue("query", new Vector(queryVector));
        cmd.Parameters.AddWithValue("topK", topK);

        foreach (var p in filterParams)
        {
            cmd.Parameters.AddWithValue(p.Name.TrimStart('@'), p.Value);
        }
    }

    /// <summary>
    /// Reads similarity rows from an executed command into <paramref name="items"/>
    /// / <paramref name="scores"/>, converting pgvector distance to a similarity
    /// score and dropping rows below <see cref="SimilarityExpression.MinRelevance"/>.
    /// Shared by the plain and iterative-scan (DR-13/R8) similarity paths so both
    /// read rows identically.
    /// </summary>
    private async Task ReadSimilarityRowsAsync<T>(
        NpgsqlCommand cmd,
        SimilarityExpression expression,
        List<T> items,
        List<double> scores,
        CancellationToken ct)
        where T : class
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rowIndex = 0;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var dataJson = reader.GetString(1);
            var distance = reader.GetDouble(2);

            // Convert distance to similarity score (higher = more similar)
            var similarity = ConvertDistanceToSimilarity(distance, expression.Metric);

            rowIndex++;

            // Filter by minRelevance
            if (similarity < expression.MinRelevance)
            {
                continue;
            }

            var item = JsonSerializer.Deserialize<T>(dataJson);
            if (item is not null)
            {
                items.Add(item);
                scores.Add(similarity);
            }
            else
            {
                _logger.LogWarning("Failed to deserialize {TypeName} from JSON data at row {RowIndex}", typeof(T).Name, rowIndex);
            }
        }
    }

    /// <inheritdoc />
    public async Task<ObjectSetResult<T>> ExecuteAsync<T>(
        ObjectSetExpression expression, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        // DR-12: a link traversal lowers to a junction-aware, depth-tiered
        // vertex ⋈ junction ⋈ vertex statement (graph-driven), NOT a plain
        // WHERE-over-one-table select — route it through the traversal seam.
        var (sql, parameters) = ExpressionTranslator.IsTraversal(expression)
            ? LowerTraversal(expression)
            : LowerSelect(expression);

        var items = new List<T>();

        await using var cmd = _dataSource.CreateCommand(sql);
        AddTranslatedParameters(cmd, parameters);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rowIndex = 0;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rowIndex++;
            var dataJson = reader.GetString(1);
            var item = JsonSerializer.Deserialize<T>(dataJson);
            if (item is not null)
            {
                items.Add(item);
            }
            else
            {
                _logger.LogWarning("Failed to deserialize {TypeName} from JSON data at row {RowIndex}", typeof(T).Name, rowIndex);
            }
        }

        return new ObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties);
    }

    /// <summary>
    /// Lowers a plain (non-traversal) read expression to a single-table SELECT and
    /// its parameters (the pre-DR-12 path), resolving the table name from the
    /// expression's declared descriptor name (bug #31).
    /// </summary>
    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private (string Sql, IReadOnlyList<ExpressionTranslator.SqlParameter> Parameters) LowerSelect(
        ObjectSetExpression expression)
    {
        var tableName = ResolveTableName(expression);
        var translation = ExpressionTranslator.Translate(expression);
        var sql = SqlGenerator.BuildSelectQuery(_options.Schema, tableName, translation.WhereClause);
        return (sql, translation.Parameters);
    }

    /// <summary>
    /// Lowers a link-traversal expression to depth-tiered traversal SQL + bound
    /// parameters via <see cref="LowerTraversalExpression"/> (DR-12), using the
    /// provider's <see cref="OntologyGraph"/> for graph-driven hop resolution.
    /// </summary>
    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private (string Sql, IReadOnlyList<ExpressionTranslator.SqlParameter> Parameters) LowerTraversal(
        ObjectSetExpression expression)
    {
        var lowering = LowerTraversalExpression(_graph, expression, _options.Schema);
        return (lowering.Sql, lowering.Parameters);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> StreamAsync<T>(
        ObjectSetExpression expression, [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        // DR-12: route a link traversal through the depth-tiered traversal seam,
        // a plain read through the single-table select — symmetric with ExecuteAsync.
        var (sql, parameters) = ExpressionTranslator.IsTraversal(expression)
            ? LowerTraversal(expression)
            : LowerSelect(expression);

        await using var cmd = _dataSource.CreateCommand(sql);
        AddTranslatedParameters(cmd, parameters);

        await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct).ConfigureAwait(false);
        var rowIndex = 0;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rowIndex++;
            var dataJson = reader.GetString(1);
            var item = JsonSerializer.Deserialize<T>(dataJson);
            if (item is not null)
            {
                yield return item;
            }
            else
            {
                _logger.LogWarning("Failed to deserialize {TypeName} from JSON data at row {RowIndex} during streaming", typeof(T).Name, rowIndex);
            }
        }
    }

    /// <inheritdoc />
    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);

        // Default overload: resolve via the ontology graph's reverse index
        // (F4). Throws if the type is multi-registered — callers must use
        // the explicit-name overload to disambiguate.
        var tableName = ResolveTableNameForDefaultOverload<T>(_graph);
        return StoreAsyncCore<T>(tableName, item, ct);
    }

    /// <inheritdoc />
    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);

        // Default overload: resolve via the ontology graph's reverse index
        // (F4). Throws if the type is multi-registered — callers must use
        // the explicit-name overload to disambiguate.
        var tableName = ResolveTableNameForDefaultOverload<T>(_graph);
        return StoreBatchAsyncCore<T>(tableName, items, ct);
    }

    /// <inheritdoc />
    public Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
        ArgumentNullException.ThrowIfNull(item);

        // Explicit-name overload: dispatch via the shared write-path helper so
        // the descriptor selects which physical partition to write to (bug #31).
        var tableName = ResolveTableNameForDescriptor(descriptorName);
        return StoreAsyncCore<T>(tableName, item, ct);
    }

    /// <inheritdoc />
    public Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
        ArgumentNullException.ThrowIfNull(items);

        // Explicit-name overload: dispatch via the shared write-path helper so
        // the descriptor selects which physical partition to write to (bug #31).
        var tableName = ResolveTableNameForDescriptor(descriptorName);
        return StoreBatchAsyncCore<T>(tableName, items, ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// DR-7/DR-8 (Ontology Edge Foundation): materializes a pure-link relation
    /// into the T8 junction table. Endpoints are addressed by their projected
    /// BUSINESS id (the in-memory relate-store contract), so each endpoint's
    /// surrogate <c>id uuid</c> is resolved via a <c>data->>'key'</c> subquery
    /// against its object table. Idempotency rides the T8
    /// <c>UNIQUE(source_id, target_id)</c> via <c>ON CONFLICT DO NOTHING</c>.
    /// INV-2: raw Npgsql only.
    /// <para>
    /// DR-13/R4: validation is EAGER and ATOMIC. The endpoint resolution, the
    /// idempotent insert (a data-modifying CTE, executed exactly once), and the
    /// endpoint-existence flags are ONE self-validating statement
    /// (<see cref="SqlGenerator.BuildValidatingRelateInsertSql"/>) issued as a
    /// single <see cref="NpgsqlBatch"/>. Because Postgres runs all <c>WITH</c>
    /// sub-statements under a SINGLE snapshot, there is no check-to-use gap: a
    /// missing endpoint (including one deleted concurrently) surfaces the typed
    /// <see cref="RelationEndpointNotFoundException"/> rather than a silent no-op,
    /// and no junction row is written. This replaces the pre-DR-13 three-round-trip
    /// (two <c>SELECT EXISTS</c> probes + insert) shape and its documented TOCTOU
    /// caveat. The disallowed-self-loop case takes a probe-only path (no insert),
    /// so its endpoint-first ordering is preserved without writing a row.
    /// </para>
    /// </remarks>
    public async Task RelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);

        var source = ResolveRelateEndpoint(_graph, srcDescriptor);
        var target = ResolveRelateEndpoint(_graph, tgtDescriptor);

        // Refuse an undeclared link up front (DR-7): the junction target is derived
        // from linkName below, so a typo would otherwise fail late with an opaque
        // "relation does not exist" instead of a typed error.
        RequireLinkDeclared(_graph, srcDescriptor, linkName);

        // DR-11b: route to the per-(link, target-descriptor) junction table. A
        // polymorphic link writes account_holdings_stock / _bond; a monomorphic
        // link keeps account_written_by (DR-7..DR-10 lockstep).
        var junctionTableName = SqlGenerator.JunctionTableNameFor(
            ResolveRelateJunction(_graph!, srcDescriptor, linkName, tgtDescriptor));

        // SELF-LOOP policy (DR-8 parity, t14): a DISALLOWED self-loop must NOT write
        // a row, so it cannot take the auto-inserting validating statement. Probe
        // the single shared endpoint first (a missing endpoint still surfaces FIRST,
        // matching the in-memory WriteRelationRow order), then refuse the self-loop.
        if (IsDisallowedSelfLoop(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId))
        {
            await ValidateEndpointExistsAsync(srcDescriptor, source, srcId, "@srcId", ct).ConfigureAwait(false);
            throw new SelfLoopNotAllowedException(srcDescriptor, srcId, linkName);
        }

        // DR-13/R4: ONE self-validating statement — resolve endpoints, insert in a
        // data-modifying CTE, return existence flags — issued as a single batch.
        var sql = SqlGenerator.BuildValidatingRelateInsertSql(
            _options.Schema,
            junctionTableName,
            source.TableName,
            source.KeyProperty,
            target.TableName,
            target.KeyProperty);

        await ExecuteValidatingRelateAsync(
            sql,
            configureParameters: parameters =>
            {
                parameters.Add(new NpgsqlParameter("srcId", srcId));
                parameters.Add(new NpgsqlParameter("tgtId", tgtId));
            },
            srcDescriptor,
            srcId,
            tgtDescriptor,
            tgtId,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a DR-13/R4 self-validating relate statement
    /// (<see cref="SqlGenerator.BuildValidatingRelateInsertSql"/> /
    /// <see cref="SqlGenerator.BuildValidatingAssociationRelateInsertSql"/>) as a
    /// SINGLE <see cref="NpgsqlBatch"/>, reads the returned
    /// <c>src_exists</c> / <c>tgt_exists</c> flags, and throws a typed
    /// <see cref="RelationEndpointNotFoundException"/> for whichever endpoint is
    /// absent. Because the insert and the existence probe share the statement's
    /// single snapshot, a missing endpoint is detected atomically with the insert —
    /// no row is written and the typed error is never lost to a TOCTOU race.
    /// </summary>
    private async Task ExecuteValidatingRelateAsync(
        string sql,
        Action<NpgsqlParameterCollection> configureParameters,
        string srcDescriptor,
        string srcId,
        string tgtDescriptor,
        string tgtId,
        CancellationToken ct)
    {
        var batchCommand = new NpgsqlBatchCommand(sql);
        configureParameters(batchCommand.Parameters);

        await using var batch = _dataSource.CreateBatch();
        batch.BatchCommands.Add(batchCommand);

        bool srcExists;
        bool tgtExists;
        await using (var reader = await batch.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                // The statement always returns exactly one flags row; a missing row
                // means the contract was violated rather than an endpoint absent.
                throw new InvalidOperationException(
                    "Self-validating relate statement returned no endpoint-existence row.");
            }

            srcExists = reader.GetBoolean(reader.GetOrdinal("src_exists"));
            tgtExists = reader.GetBoolean(reader.GetOrdinal("tgt_exists"));
        }

        // Source-first ordering matches the in-memory WriteRelationRow guard
        // (it validates the source endpoint before the target).
        if (!srcExists)
        {
            throw new RelationEndpointNotFoundException(srcDescriptor, srcId);
        }

        if (!tgtExists)
        {
            throw new RelationEndpointNotFoundException(tgtDescriptor, tgtId);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// DR-13/R6: the Npgsql provider RESERVES the batch surface but DEFERS the
    /// set-based DML to bulk edge ingestion (#115). Until then it throws
    /// <see cref="NotSupportedException"/> rather than silently degrading to a
    /// per-edge round-trip loop — which would mask the missing batched lowering and
    /// give callers the round-trip cost the batch API exists to eliminate. The
    /// throw is unconditional and opens no connection.
    /// </remarks>
    public Task RelateBatchAsync(IReadOnlyList<RelateRequest> requests, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        throw new NotSupportedException(
            "RelateBatchAsync is reserved on the Npgsql provider but its set-based DML is "
            + "deferred to bulk edge ingestion (#115). Use the single-pair RelateAsync, or the "
            + "in-memory provider, until #115 lands the batched lowering.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// DR-7 (Ontology Edge Foundation): removes a pure-link relation from the T8
    /// junction table. Symmetric with the plain <see cref="RelateAsync"/> write
    /// key: deletes the single junction row for the endpoint pair resolved from
    /// business ids. Removing a relation that does not exist deletes zero rows —
    /// a no-op (no throw). INV-2: raw Npgsql only.
    /// </remarks>
    public async Task UnrelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);

        var source = ResolveRelateEndpoint(_graph, srcDescriptor);
        var target = ResolveRelateEndpoint(_graph, tgtDescriptor);

        // Refuse an undeclared link up front (DR-7), symmetric with RelateAsync:
        // the junction target is derived from linkName below.
        RequireLinkDeclared(_graph, srcDescriptor, linkName);

        // DR-11b: delete from the SAME per-(link, target-descriptor) junction table
        // RelateAsync writes — symmetric routing so a polymorphic unrelate targets
        // the resolved target's own table.
        var junctionTableName = SqlGenerator.JunctionTableNameFor(
            ResolveRelateJunction(_graph!, srcDescriptor, linkName, tgtDescriptor));
        var sql = SqlGenerator.BuildUnrelateDeleteSql(
            _options.Schema,
            junctionTableName,
            source.TableName,
            source.KeyProperty,
            target.TableName,
            target.KeyProperty);

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("srcId", srcId);
        cmd.Parameters.AddWithValue("tgtId", tgtId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a descriptor by NAME from the ontology graph (INV-8: identity by
    /// descriptor name, never <c>typeof</c>), throwing a typed
    /// <see cref="InvalidOperationException"/> when it is not registered. Shared
    /// by the relate-endpoint and traversal-hop resolvers so a descriptor name
    /// denotes the same descriptor in both, with a single lookup and message.
    /// </summary>
    private static Descriptors.ObjectTypeDescriptor RequireDescriptor(OntologyGraph graph, string descriptorName)
    {
        var descriptor = graph.ObjectTypes.FirstOrDefault(
            o => string.Equals(o.Name, descriptorName, StringComparison.Ordinal));
        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"Descriptor '{descriptorName}' is not registered in the ontology graph. "
                + $"Register it via Object<T>(...) in a DomainOntology.");
        }

        return descriptor;
    }

    /// <summary>
    /// Validates that <paramref name="linkName"/> is a link DECLARED on the source
    /// descriptor BEFORE the plain relate/unrelate path derives a physical junction
    /// target from it (DR-7). An undeclared link otherwise fails LATE — the
    /// junction name <see cref="SqlGenerator.JunctionTableName"/> derives was never
    /// provisioned, so Postgres raises an opaque "relation does not exist". Refusing
    /// here turns that into a deterministic typed <see cref="InvalidOperationException"/>,
    /// matching the graph-first "refuse, don't degrade" posture the traversal hop
    /// resolver already takes. INV-8: resolved by descriptor name, never <c>typeof</c>.
    /// </summary>
    /// <remarks>
    /// A null graph or an unknown source descriptor is left to
    /// <see cref="ResolveRelateEndpoint"/>, which raises the canonical typed errors
    /// for those cases (it runs first on every relate/unrelate); this guard only adds
    /// the "descriptor exists but the link is not declared on it" check. The
    /// attributed (association-object) overloads route through
    /// <see cref="ResolveAssociationRelate"/> instead — which validates the src/tgt
    /// pairing against the association's declared endpoints — so the link name there
    /// is not a physical-routing input and is not re-validated here.
    /// </remarks>
    internal static void RequireLinkDeclared(OntologyGraph? graph, string srcDescriptor, string linkName)
    {
        var descriptor = graph?.ObjectTypes.FirstOrDefault(
            o => string.Equals(o.Name, srcDescriptor, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return;
        }

        var declared = descriptor.Links.Any(
            l => string.Equals(l.Name, linkName, StringComparison.Ordinal));
        if (declared)
        {
            return;
        }

        var available = descriptor.Links.Count > 0
            ? string.Join(", ", descriptor.Links.Select(l => l.Name))
            : "(none)";
        throw new InvalidOperationException(
            $"Link '{linkName}' is not declared on source descriptor '{srcDescriptor}'. "
            + $"Relate/unrelate derives the physical junction target from the link name, so an "
            + $"undeclared link fails late at SQL time. Available links: {available}. "
            + $"Declare it via HasMany/HasOne/ManyToMany in the DomainOntology.");
    }

    /// <summary>
    /// Resolves a relate endpoint descriptor name to the physical
    /// <see cref="RelateEndpoint.TableName"/> (snake_cased) and the
    /// <see cref="RelateEndpoint.KeyProperty"/> name (the <c>data jsonb</c> field
    /// holding the endpoint's business id) from the ontology graph. Relate is a
    /// graph-aware operation — it needs the key property names to build the
    /// <c>data->>'key'</c> subqueries — so a null graph or an unknown/keyless
    /// descriptor throws <see cref="InvalidOperationException"/> rather than
    /// emitting wrong SQL. INV-8: identity by descriptor name, never <c>typeof</c>.
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal static</c> so unit tests can pin its behavior and
    /// the resulting SQL without a live <see cref="NpgsqlDataSource"/>, matching
    /// the seam used by the read/write table-name resolvers.
    /// </remarks>
    internal static RelateEndpoint ResolveRelateEndpoint(OntologyGraph? graph, string descriptorName)
    {
        ArgumentNullException.ThrowIfNull(descriptorName);

        if (graph is null)
        {
            throw new InvalidOperationException(
                $"RelateAsync/UnrelateAsync require an ontology graph to resolve endpoint "
                + $"descriptor '{descriptorName}' (table name and key property). Construct "
                + $"PgVectorObjectSetProvider with the registered OntologyGraph.");
        }

        var descriptor = RequireDescriptor(graph, descriptorName);

        if (descriptor.KeyProperty is null)
        {
            throw new InvalidOperationException(
                $"Relate endpoint descriptor '{descriptorName}' declares no key property; "
                + $"a key is required to resolve the endpoint's stored row by business id. "
                + $"Declare one via obj.Key(...) in the DomainOntology.");
        }

        return new RelateEndpoint(
            TypeMapper.ToSnakeCase(descriptorName),
            descriptor.KeyProperty.Name);
    }

    /// <summary>
    /// Eager endpoint-existence probe (DR-8). Runs the
    /// <see cref="SqlGenerator.BuildEndpointExistsSql"/> <c>SELECT EXISTS</c> and
    /// throws <see cref="RelationEndpointNotFoundException"/> when no stored row
    /// under <paramref name="descriptorName"/> carries the business id
    /// <paramref name="id"/>. Surfaces the SAME typed error the in-memory
    /// provider raises, so the caller error is identical across backends.
    /// </summary>
    private async Task ValidateEndpointExistsAsync(
        string descriptorName,
        RelateEndpoint endpoint,
        string id,
        string parameterName,
        CancellationToken ct)
    {
        var sql = SqlGenerator.BuildEndpointExistsSql(
            _options.Schema, endpoint.TableName, endpoint.KeyProperty, parameterName);

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(parameterName.TrimStart('@'), id);
        var exists = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as bool?;

        if (exists != true)
        {
            throw new RelationEndpointNotFoundException(descriptorName, id);
        }
    }

    /// <summary>
    /// Self-loop policy (DR-8 parity, t14): throws
    /// <see cref="SelfLoopNotAllowedException"/> when the relate connects an
    /// instance to ITSELF — same (descriptor, id) on both endpoints — along a link
    /// whose <see cref="Descriptors.LinkDescriptor.AllowsSelfLoop"/> is
    /// <c>false</c>. Mirrors the in-memory provider's
    /// <c>WriteRelationRow</c> self-loop guard so a disallowed self-loop surfaces
    /// the SAME typed error across backends rather than a silent junction row.
    /// </summary>
    private void ThrowIfDisallowedSelfLoop(
        string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId)
    {
        if (IsDisallowedSelfLoop(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId))
        {
            throw new SelfLoopNotAllowedException(srcDescriptor, srcId, linkName);
        }
    }

    /// <summary>
    /// Whether a relate connects an instance to ITSELF — same (descriptor, id) on
    /// both endpoints — along a link whose
    /// <see cref="Descriptors.LinkDescriptor.AllowsSelfLoop"/> is <c>false</c>. The
    /// boolean form of <see cref="ThrowIfDisallowedSelfLoop"/>, used by the DR-13/R4
    /// plain relate to route a disallowed self-loop down the probe-only (no-insert)
    /// path so it never writes a row before the policy is enforced.
    /// </summary>
    private bool IsDisallowedSelfLoop(
        string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId) =>
        string.Equals(srcDescriptor, tgtDescriptor, StringComparison.Ordinal)
        && string.Equals(srcId, tgtId, StringComparison.Ordinal)
        && !SelfLoopAllowed(srcDescriptor, linkName);

    /// <summary>
    /// Resolves whether the named link on <paramref name="srcDescriptor"/> permits
    /// self-loops (DR-8). Defaults to <c>false</c> — the safe posture — when no
    /// graph is present, the source descriptor is unknown, or the link is not
    /// declared on it. Mirrors the in-memory provider's <c>SelfLoopAllowed</c>.
    /// </summary>
    private bool SelfLoopAllowed(string srcDescriptor, string linkName)
    {
        if (_graph is null)
        {
            return false;
        }

        var descriptor = _graph.ObjectTypes.FirstOrDefault(
            o => string.Equals(o.Name, srcDescriptor, StringComparison.Ordinal));
        var link = descriptor?.Links.FirstOrDefault(
            l => string.Equals(l.Name, linkName, StringComparison.Ordinal));
        return link?.AllowsSelfLoop ?? false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// DR-4/DR-8 (Ontology Edge Foundation, t11): removes an ATTRIBUTED relation
    /// from its T8 association-OBJECT table. Symmetric with the attributed
    /// <see cref="RelateAsync{TRel}(string, string, string, string, string, string, TRel, CancellationToken)"/>:
    /// deletes the single association row keyed on the association's BUSINESS id
    /// (its <c>data->>'key'</c> field). Removing an association that does not
    /// exist deletes zero rows — a no-op (no throw), mirroring the in-memory
    /// store's attributed-unrelate posture. INV-2: raw Npgsql only.
    /// </remarks>
    public async Task UnrelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, string associationDescriptor, string associationId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);
        ArgumentNullException.ThrowIfNull(associationDescriptor);
        ArgumentNullException.ThrowIfNull(associationId);

        // The association is resolved by NAME (INV-8) to its table + key
        // property; the endpoint descriptors are resolved as well so a wrong
        // (src, tgt) pairing is refused symmetrically with the relate path.
        var plan = ResolveAssociationRelate(_graph, associationDescriptor, srcDescriptor, tgtDescriptor);

        var sql = SqlGenerator.BuildAssociationUnrelateDeleteSql(
            _options.Schema,
            plan.AssociationTable,
            plan.AssociationKeyProperty);

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("associationId", associationId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// DR-4/DR-8 (Ontology Edge Foundation, t11): materializes an ATTRIBUTED
    /// relation into its T8 association-OBJECT table. The association is a reified
    /// object — it gets its own <c>id uuid</c>, its serialized attributes as
    /// <c>data jsonb</c>, and one <c>{role}_id</c> endpoint FK per endpoint
    /// resolved from each endpoint's BUSINESS id via a <c>data->>'key'</c>
    /// subquery against its object table (mirroring the pure-link junction relate
    /// / DR-7). Validation is EAGER (DR-8): both endpoints are probed via
    /// <c>SELECT EXISTS</c> BEFORE the insert, so a missing endpoint surfaces a
    /// typed <see cref="RelationEndpointNotFoundException"/> and no association
    /// row is written. The endpoint→role-column mapping comes from the
    /// association descriptor's endpoints (INV-8: identity by descriptor name,
    /// never <c>typeof</c>). INV-2: raw Npgsql only.
    /// </remarks>
    public async Task RelateAsync<TRel>(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, string associationDescriptor, TRel association, CancellationToken ct = default)
        where TRel : class
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);
        ArgumentNullException.ThrowIfNull(associationDescriptor);
        ArgumentNullException.ThrowIfNull(association);

        // Resolve the association's endpoints to their role-named FK columns +
        // physical tables + key properties (INV-8: by descriptor name). A
        // graph-less provider, an unknown association, or a src/tgt pairing that
        // does not match the association's declared endpoints is refused here.
        var plan = ResolveAssociationRelate(_graph, associationDescriptor, srcDescriptor, tgtDescriptor);

        var source = new RelateEndpoint(plan.SourceTable, plan.SourceKeyProperty);

        // SELF-LOOP policy (DR-8 parity, t14): a DISALLOWED self-loop must NOT write
        // an association row, so — as with the plain path (DR-13/R4) — probe the
        // single shared endpoint first (a missing endpoint surfaces FIRST), then
        // refuse the self-loop, never the auto-inserting validating statement.
        if (IsDisallowedSelfLoop(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId))
        {
            await ValidateEndpointExistsAsync(srcDescriptor, source, srcId, "@srcId", ct).ConfigureAwait(false);
            throw new SelfLoopNotAllowedException(srcDescriptor, srcId, linkName);
        }

        // DR-13/R4: ONE self-validating statement — resolve endpoints, insert the
        // association-object row in a data-modifying CTE, return existence flags —
        // issued as a single batch, so a missing endpoint surfaces the typed error
        // atomically with the insert and never leaves a dangling association.
        var sql = SqlGenerator.BuildValidatingAssociationRelateInsertSql(
            _options.Schema,
            plan.AssociationTable,
            plan.SourceColumn,
            plan.SourceTable,
            plan.SourceKeyProperty,
            plan.TargetColumn,
            plan.TargetTable,
            plan.TargetKeyProperty);

        var serializedAssociation = JsonSerializer.Serialize(association);
        await ExecuteValidatingRelateAsync(
            sql,
            configureParameters: parameters =>
            {
                parameters.Add(new NpgsqlParameter("id", Guid.NewGuid()));
                parameters.Add(new NpgsqlParameter("data", serializedAssociation));
                parameters.Add(new NpgsqlParameter("srcId", srcId));
                parameters.Add(new NpgsqlParameter("tgtId", tgtId));
            },
            srcDescriptor,
            srcId,
            tgtDescriptor,
            tgtId,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves an ATTRIBUTED relate (DR-4, t11) from the ontology graph into
    /// the physical operands for the association-object INSERT/DELETE: the
    /// association table + key property, and — for each of the caller's
    /// <paramref name="srcDescriptor"/> / <paramref name="tgtDescriptor"/>
    /// endpoints — the role-named <c>{role}_id</c> FK column, the endpoint object
    /// table, and the endpoint key property. The output feeds
    /// <see cref="SqlGenerator.BuildAssociationRelateInsertSql"/> /
    /// <see cref="SqlGenerator.BuildAssociationUnrelateDeleteSql"/> unchanged.
    /// </summary>
    /// <param name="graph">
    /// The ontology graph. Attributed relate is a graph-aware operation — it
    /// needs the association's declared endpoints (role → descriptor) and the
    /// endpoints' key properties — so a null graph throws
    /// <see cref="InvalidOperationException"/> rather than emitting wrong SQL.
    /// </param>
    /// <param name="associationDescriptor">The reified association descriptor name.</param>
    /// <param name="srcDescriptor">The SOURCE endpoint descriptor name.</param>
    /// <param name="tgtDescriptor">The TARGET endpoint descriptor name.</param>
    /// <remarks>
    /// INV-8: the association and both endpoints are resolved by descriptor NAME,
    /// never <c>typeof</c>. The caller's <paramref name="srcDescriptor"/> /
    /// <paramref name="tgtDescriptor"/> are matched to the association's two
    /// declared endpoints by their <see cref="Descriptors.AssociationEndpoint.DescriptorName"/>,
    /// so the source surrogate id lands in the source endpoint's
    /// <c>{role}_id</c> column and likewise for the target. A src/tgt pairing
    /// that does not map onto the association's two DISTINCT endpoints — an
    /// unknown endpoint, or both binding to the same endpoint — is refused with a
    /// typed <see cref="InvalidOperationException"/> rather than mis-routed.
    /// Exposed as <c>internal static</c> so unit tests can pin its behavior and
    /// the resulting SQL without a live <see cref="NpgsqlDataSource"/>, matching
    /// the seam used by the relate/traversal resolvers.
    /// </remarks>
    internal static AssociationRelatePlan ResolveAssociationRelate(
        OntologyGraph? graph,
        string associationDescriptor,
        string srcDescriptor,
        string tgtDescriptor)
    {
        ArgumentNullException.ThrowIfNull(associationDescriptor);
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);

        if (graph is null)
        {
            throw new InvalidOperationException(
                $"Attributed RelateAsync/UnrelateAsync require an ontology graph to resolve "
                + $"association '{associationDescriptor}' (table, key property, and endpoint "
                + $"role columns). Construct PgVectorObjectSetProvider with the registered "
                + $"OntologyGraph.");
        }

        var association = RequireDescriptor(graph, associationDescriptor);

        if (association.Kind != Descriptors.ObjectKind.Association)
        {
            throw new InvalidOperationException(
                $"Descriptor '{associationDescriptor}' has Kind '{association.Kind}', not "
                + $"'{Descriptors.ObjectKind.Association}'; attributed relate requires a reified "
                + $"association descriptor (declare it via Association<T>(...) in a DomainOntology).");
        }

        if (association.KeyProperty is null)
        {
            throw new InvalidOperationException(
                $"Association descriptor '{associationDescriptor}' declares no key property; a key "
                + $"is required to resolve the association's stored row by business id on unrelate. "
                + $"Declare one via assoc.Key(...) in the DomainOntology.");
        }

        if (association.AssociationEndpoints.Count != 2)
        {
            throw new InvalidOperationException(
                $"Association '{associationDescriptor}' declares {association.AssociationEndpoints.Count} "
                + $"endpoint(s); attributed relate requires exactly two (DR-4).");
        }

        // Map the caller's src/tgt descriptor names onto the association's two
        // DECLARED endpoints by descriptor name (INV-8). They must bind to the
        // two DISTINCT endpoints — so the source surrogate id lands in the
        // source's {role}_id column and the target's in the other.
        var (sourceEndpoint, targetEndpoint) = MatchEndpointsToDescriptors(
            associationDescriptor, association.AssociationEndpoints, srcDescriptor, tgtDescriptor);

        var source = ResolveRelateEndpoint(graph, sourceEndpoint.DescriptorName);
        var target = ResolveRelateEndpoint(graph, targetEndpoint.DescriptorName);

        return new AssociationRelatePlan
        {
            AssociationTable = TypeMapper.ToSnakeCase(associationDescriptor),
            AssociationKeyProperty = association.KeyProperty.Name,
            SourceColumn = $"{TypeMapper.ToSnakeCase(sourceEndpoint.Role)}_id",
            SourceTable = source.TableName,
            SourceKeyProperty = source.KeyProperty,
            TargetColumn = $"{TypeMapper.ToSnakeCase(targetEndpoint.Role)}_id",
            TargetTable = target.TableName,
            TargetKeyProperty = target.KeyProperty,
        };
    }

    /// <summary>
    /// Pairs the caller's <paramref name="srcDescriptor"/> /
    /// <paramref name="tgtDescriptor"/> with the association's two declared
    /// endpoints by <see cref="Descriptors.AssociationEndpoint.DescriptorName"/>,
    /// requiring them to bind to the two DISTINCT endpoints. Refuses with a typed
    /// <see cref="InvalidOperationException"/> when a descriptor names no endpoint
    /// or when both descriptors bind to the same one — the role-column routing is
    /// only well-defined when source and target each occupy a distinct endpoint.
    /// </summary>
    private static (Descriptors.AssociationEndpoint Source, Descriptors.AssociationEndpoint Target)
        MatchEndpointsToDescriptors(
            string associationDescriptor,
            IReadOnlyList<Descriptors.AssociationEndpoint> endpoints,
            string srcDescriptor,
            string tgtDescriptor)
    {
        var first = endpoints[0];
        var second = endpoints[1];

        // Source binds to the endpoint whose descriptor matches srcDescriptor;
        // target binds to the OTHER endpoint, which must match tgtDescriptor.
        if (string.Equals(first.DescriptorName, srcDescriptor, StringComparison.Ordinal)
            && string.Equals(second.DescriptorName, tgtDescriptor, StringComparison.Ordinal))
        {
            return (first, second);
        }

        if (string.Equals(second.DescriptorName, srcDescriptor, StringComparison.Ordinal)
            && string.Equals(first.DescriptorName, tgtDescriptor, StringComparison.Ordinal))
        {
            return (second, first);
        }

        throw new InvalidOperationException(
            $"Attributed relate endpoints (source '{srcDescriptor}', target '{tgtDescriptor}') do not "
            + $"map onto the two distinct declared endpoints of association '{associationDescriptor}' "
            + $"('{first.DescriptorName}' as role '{first.Role}', '{second.DescriptorName}' as role "
            + $"'{second.Role}'). Source and target must each occupy a distinct endpoint so each "
            + $"surrogate id routes to its own role column (INV-8: identity by descriptor name).");
    }

    /// <summary>
    /// A resolved attributed-relate plan (DR-4, t11): the physical operands for
    /// the association-object INSERT/DELETE produced by
    /// <see cref="ResolveAssociationRelate(OntologyGraph?, string, string, string)"/>
    /// and fed unchanged into
    /// <see cref="SqlGenerator.BuildAssociationRelateInsertSql"/> /
    /// <see cref="SqlGenerator.BuildAssociationUnrelateDeleteSql"/>. New sealed,
    /// <c>init</c>-only record (INV-6/INV-7).
    /// </summary>
    internal sealed record AssociationRelatePlan
    {
        /// <summary>The association-object table (snake_cased).</summary>
        public required string AssociationTable { get; init; }

        /// <summary>
        /// The association descriptor's key property — the <c>data jsonb</c>
        /// field holding the association's business id (used by unrelate).
        /// </summary>
        public required string AssociationKeyProperty { get; init; }

        /// <summary>The SOURCE endpoint's role-named <c>{role}_id</c> FK column.</summary>
        public required string SourceColumn { get; init; }

        /// <summary>The SOURCE endpoint object table (snake_cased).</summary>
        public required string SourceTable { get; init; }

        /// <summary>The SOURCE endpoint descriptor's key property.</summary>
        public required string SourceKeyProperty { get; init; }

        /// <summary>The TARGET endpoint's role-named <c>{role}_id</c> FK column.</summary>
        public required string TargetColumn { get; init; }

        /// <summary>The TARGET endpoint object table (snake_cased).</summary>
        public required string TargetTable { get; init; }

        /// <summary>The TARGET endpoint descriptor's key property.</summary>
        public required string TargetKeyProperty { get; init; }
    }

    /// <summary>
    /// Core single-item write helper. Takes a pre-resolved <paramref name="tableName"/>
    /// so both the default <see cref="StoreAsync{T}(T, CancellationToken)"/> overload
    /// (resolving via <see cref="ResolveTableNameForDefaultOverload{T}(OntologyGraph?)"/>)
    /// and the explicit-name overload (resolving via
    /// <see cref="ResolveTableNameForDescriptor"/>) share a single SQL /
    /// parameter-binding code path.
    /// </summary>
    private async Task StoreAsyncCore<T>(string tableName, T item, CancellationToken ct) where T : class
    {
        var hasEmbedding = item is ISearchable;
        var sql = SqlGenerator.BuildInsertSql(_options.Schema, tableName, hasEmbedding);

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(item));

        if (item is ISearchable searchable)
        {
            ValidateEmbedding<T>(searchable.Embedding);
            cmd.Parameters.AddWithValue("embedding", new Vector(searchable.Embedding));
        }

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Core batch write helper. Takes a pre-resolved <paramref name="tableName"/>
    /// so both the default <see cref="StoreBatchAsync{T}(IReadOnlyList{T}, CancellationToken)"/>
    /// overload and the explicit-name overload share a single COPY pipeline.
    /// </summary>
    private async Task StoreBatchAsyncCore<T>(string tableName, IReadOnlyList<T> items, CancellationToken ct) where T : class
    {
        if (items.Count == 0)
        {
            return;
        }

        var hasEmbedding = items[0] is ISearchable;

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);

        var qualifiedTable = $"{SqlGenerator.QuoteIdentifier(_options.Schema)}.{SqlGenerator.QuoteIdentifier(tableName)}";
        var copyColumns = hasEmbedding
            ? $"{qualifiedTable} (id, data, embedding)"
            : $"{qualifiedTable} (id, data)";

        await using var writer = await connection.BeginBinaryImportAsync(
            $"COPY {copyColumns} FROM STDIN (FORMAT BINARY)", ct).ConfigureAwait(false);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            await writer.StartRowAsync(ct).ConfigureAwait(false);
            await writer.WriteAsync(Guid.NewGuid(), ct).ConfigureAwait(false);
            await writer.WriteAsync(JsonSerializer.Serialize(item), NpgsqlTypes.NpgsqlDbType.Jsonb, ct).ConfigureAwait(false);

            if (item is ISearchable searchable)
            {
                ValidateEmbedding<T>(searchable.Embedding);
                await writer.WriteAsync(new Vector(searchable.Embedding), ct).ConfigureAwait(false);
            }
        }

        await writer.CompleteAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the database schema (extension, table, index) exists for the given type.
    /// </summary>
    /// <typeparam name="T">The CLR type whose backing table should be created.</typeparam>
    /// <param name="descriptorName">
    /// Optional explicit descriptor name identifying the target table. When
    /// <c>null</c>, the target is resolved via
    /// <see cref="ResolveTableNameForDefaultOverload{T}(OntologyGraph?)"/>
    /// using the provider's optional <see cref="OntologyGraph"/>; for
    /// multi-registered types the resolution throws and callers must
    /// specify the name explicitly (one call per descriptor).
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    public async Task EnsureSchemaAsync<T>(string? descriptorName = null, CancellationToken ct = default) where T : class
    {
        var tableName = ResolveEnsureSchemaTableName<T>(descriptorName, _graph);
        var keyPropertyName = ResolveEnsureSchemaKeyProperty<T>(descriptorName, _graph);
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            _options.Schema,
            tableName,
            _embeddingProvider.Dimensions,
            _options.IndexType,
            keyPropertyName: keyPropertyName);

        await using var cmd = _dataSource.CreateCommand(ddl);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the BUSINESS-id key property name (DR-13/R2) for an
    /// <see cref="EnsureSchemaAsync{T}(string?, CancellationToken)"/> call, so the
    /// vertex DDL can emit a <c>UNIQUE ((data->>'key'))</c> expression index that
    /// makes the relate/traversal endpoint-resolution subqueries deterministic.
    /// Resolves the descriptor by name (explicit, else default-overload
    /// resolution) and returns its <see cref="Descriptors.PropertyDescriptor.Name"/>;
    /// returns <c>null</c> when no graph is in scope OR the resolved descriptor
    /// declares no key — both leave the DDL key-index-free, byte-identical to the
    /// pre-DR-13 pgvector-only lowering.
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal static</c> so unit tests can pin its behavior without
    /// a live <see cref="NpgsqlDataSource"/>, matching the seam used by
    /// <see cref="ResolveEnsureSchemaTableName{T}(string?, OntologyGraph?)"/>. INV-8:
    /// the key is read from the descriptor resolved by NAME, never <c>typeof</c>.
    /// A descriptor that is registered but absent from the graph (or any resolution
    /// failure) yields <c>null</c> rather than throwing — the unique index is a
    /// hardening, never a gate on schema creation.
    /// </remarks>
    internal static string? ResolveEnsureSchemaKeyProperty<T>(string? descriptorName, OntologyGraph? graph)
    {
        if (graph is null)
        {
            return null;
        }

        // Resolve the descriptor NAME the same way the table name is resolved:
        // an explicit name wins; otherwise the default-overload (single-
        // registration) resolution. A multi-registered or unregistered type makes
        // the default resolution throw — but EnsureSchemaAsync would already have
        // thrown on the table-name resolution, so we mirror that by letting an
        // explicit name pass straight through.
        var resolvedName = descriptorName;
        if (resolvedName is null)
        {
            if (!graph.ObjectTypeNamesByType.TryGetValue(typeof(T), out var names) || names.Count != 1)
            {
                return null;
            }

            resolvedName = names[0];
        }

        var descriptor = graph.ObjectTypes.FirstOrDefault(
            o => string.Equals(o.Name, resolvedName, StringComparison.Ordinal));
        return descriptor?.KeyProperty?.Name;
    }

    /// <summary>
    /// Resolves the snake_case PostgreSQL table name for an
    /// <see cref="EnsureSchemaAsync{T}(string?, CancellationToken)"/> call.
    /// Honours an explicit <paramref name="descriptorName"/> when supplied;
    /// otherwise delegates to the shared default-overload resolution
    /// (<see cref="ResolveTableNameForDefaultOverload{T}(OntologyGraph?)"/>)
    /// so schema creation and writes stay in lockstep.
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal static</c> so unit tests can pin its behavior
    /// without a live <see cref="NpgsqlDataSource"/>, matching the seam used
    /// by the write-path helpers.
    /// </remarks>
    internal static string ResolveEnsureSchemaTableName<T>(string? descriptorName, OntologyGraph? graph)
    {
        if (descriptorName is not null)
        {
            return TypeMapper.ToSnakeCase(descriptorName);
        }

        return ResolveTableNameForDefaultOverload<T>(graph);
    }

    private static double ConvertDistanceToSimilarity(double distance, DistanceMetric metric) => metric switch
    {
        // pgvector <=> returns cosine distance in [0, 2] (1 - cosine_similarity).
        // Convert back: similarity = 1.0 - distance, yielding [-1, 1] range.
        // For normalized vectors, distance is in [0, 2] so similarity is in [-1, 1].
        DistanceMetric.Cosine => 1.0 - distance,
        // pgvector <-> returns L2 (Euclidean) distance in [0, inf).
        // Convert to a bounded similarity score via reciprocal.
        DistanceMetric.L2 => 1.0 / (1.0 + distance),
        // pgvector <#> returns negative inner product. Negate to get actual inner product.
        DistanceMetric.InnerProduct => -distance,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported distance metric."),
    };

    private void ValidateEmbedding<T>(float[]? embedding)
    {
        if (embedding is null || embedding.Length == 0)
        {
            throw new InvalidOperationException(
                $"Embedding must not be null or empty for {typeof(T).Name}.");
        }

        if (embedding.Length != _embeddingProvider.Dimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch for {typeof(T).Name}: item has {embedding.Length} dimensions, provider expects {_embeddingProvider.Dimensions}.");
        }
    }

    private static void AddTranslatedParameters(NpgsqlCommand cmd, IReadOnlyList<ExpressionTranslator.SqlParameter> parameters)
    {
        foreach (var p in parameters)
        {
            cmd.Parameters.AddWithValue(p.Name.TrimStart('@'), p.Value);
        }
    }

    /// <summary>
    /// A resolved relate endpoint: the physical snake_cased
    /// <see cref="TableName"/> of the endpoint's object table and the
    /// <see cref="KeyProperty"/> name (the <c>data jsonb</c> field holding the
    /// endpoint's business id), produced by
    /// <see cref="ResolveRelateEndpoint(OntologyGraph?, string)"/> and fed
    /// unchanged into the relate/unrelate SQL builders.
    /// </summary>
    /// <param name="TableName">The endpoint object table name (snake_cased).</param>
    /// <param name="KeyProperty">The descriptor's key property name.</param>
    internal sealed record RelateEndpoint(string TableName, string KeyProperty);

    /// <summary>
    /// Resolves an instance-anchored <c>TraverseLink</c> hop (DR-7/DR-10) from
    /// the ontology graph into the physical join operands: the SOURCE endpoint
    /// table + its key property, the T8 JUNCTION table for <c>(source, link)</c>,
    /// and the TARGET endpoint table + its resolved descriptor name. The output
    /// feeds <see cref="SqlGenerator.BuildInstanceAnchoredTraversalSql"/> unchanged.
    /// </summary>
    /// <param name="graph">
    /// The ontology graph. Traversal is a graph-aware operation — it needs the
    /// link's declared target and the descriptors' key properties — so a null
    /// graph throws <see cref="InvalidOperationException"/> rather than emitting
    /// wrong SQL.
    /// </param>
    /// <param name="sourceDescriptorName">
    /// The IMMEDIATE source descriptor name the traversal is anchored at.
    /// </param>
    /// <param name="linkName">The traversed link's descriptor name.</param>
    /// <param name="targetDescriptorOverride">
    /// An OPTIONAL explicit target descriptor name (the caller's
    /// <c>.TraverseLink(link, descriptorName)</c> selection). When supplied it is
    /// AUTHORITATIVE — it names the exact target partition, so no CLR Type
    /// participates in resolution. Mirrors
    /// <see cref="ObjectSets.TraverseLinkExpression.TargetDescriptorName"/>.
    /// </param>
    /// <remarks>
    /// DR-10 keystone (INV-8): the hop's TARGET descriptor is resolved from the
    /// GRAPH via the source link, NEVER from <c>typeof(TLinked)</c>. Precedence
    /// mirrors T2's
    /// <c>InMemoryExpressionEvaluator.ResolveHopTargetDescriptor</c>:
    /// <list type="number">
    /// <item>an explicit <paramref name="targetDescriptorOverride"/>;</item>
    /// <item>otherwise the source link's <c>TargetTypeName</c> (the canonical
    /// hand-authored target name);</item>
    /// <item>falling back to the descriptor named by the link's
    /// <c>TargetSymbolKey</c> (the polyglot, SymbolKey-only target).</item>
    /// </list>
    /// Adopting T2's posture, an UNRESOLVED target — no override, no
    /// graph-resolvable link target — is REFUSED with a typed
    /// <see cref="InvalidOperationException"/> rather than mis-routed to a
    /// first-CLR-match partition. INV-8: identity by descriptor name, never
    /// <c>typeof</c>.
    /// </remarks>
    internal static TraversalHop ResolveTraversalHop(
        OntologyGraph? graph,
        string sourceDescriptorName,
        string linkName,
        string? targetDescriptorOverride)
    {
        ArgumentNullException.ThrowIfNull(sourceDescriptorName);
        ArgumentNullException.ThrowIfNull(linkName);

        if (graph is null)
        {
            throw new InvalidOperationException(
                $"Instance-anchored TraverseLink requires an ontology graph to resolve the "
                + $"hop from source '{sourceDescriptorName}' along link '{linkName}' (source/target "
                + $"tables and key properties). Construct PgVectorObjectSetProvider with the "
                + $"registered OntologyGraph.");
        }

        // Reuse the relate-path endpoint resolution for the SOURCE (table + key
        // property), then resolve the source descriptor once more to read its
        // links. Both share the same graph-by-name lookup, so they agree on which
        // descriptor a name denotes (INV-8).
        var source = ResolveRelateEndpoint(graph, sourceDescriptorName);
        var sourceDescriptor = RequireDescriptor(graph, sourceDescriptorName);

        var link = sourceDescriptor.Links.FirstOrDefault(
            l => string.Equals(l.Name, linkName, StringComparison.Ordinal));
        if (link is null)
        {
            var available = string.Join(", ", sourceDescriptor.Links.Select(l => l.Name));
            throw new InvalidOperationException(
                $"Link '{linkName}' is not declared on source descriptor '{sourceDescriptorName}'. "
                + $"Available links: {available}. Declare it via HasOne/HasMany/ManyToMany in the "
                + $"DomainOntology.");
        }

        var targetDescriptorName = ResolveHopTargetDescriptorName(
            graph, link, targetDescriptorOverride);
        var target = ResolveRelateEndpoint(graph, targetDescriptorName);

        var junction = SqlGenerator.JunctionTableName(source.TableName, linkName);

        return new TraversalHop(
            source.TableName,
            source.KeyProperty,
            junction,
            target.TableName,
            targetDescriptorName);
    }

    /// <summary>
    /// Resolves the hop's TARGET descriptor NAME from the ontology graph (DR-10),
    /// mirroring T2's
    /// <c>InMemoryExpressionEvaluator.ResolveHopTargetDescriptor</c> precedence:
    /// explicit override → link <c>TargetTypeName</c> → link
    /// <c>TargetSymbolKey</c> reverse index. Never consults
    /// <c>typeof(TLinked)</c> (INV-8). Refuses with a typed error when no
    /// graph-registered descriptor backs the resolved name — adopting T2's
    /// posture of failing rather than mis-routing under ambiguity.
    /// </summary>
    private static string ResolveHopTargetDescriptorName(
        OntologyGraph graph,
        Descriptors.LinkDescriptor link,
        string? targetDescriptorOverride)
    {
        // Seam 1: an explicit override is authoritative.
        if (targetDescriptorOverride is { } explicitName)
        {
            if (!graph.ObjectTypes.Any(o => string.Equals(o.Name, explicitName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Explicit traversal target descriptor '{explicitName}' is not registered in "
                    + $"the ontology graph. Register it via Object<T>(...) in a DomainOntology.");
            }

            return explicitName;
        }

        // Seam 2: the link's canonical hand-authored target name.
        if (!string.IsNullOrEmpty(link.TargetTypeName)
            && graph.ObjectTypes.Any(o => string.Equals(o.Name, link.TargetTypeName, StringComparison.Ordinal)))
        {
            return link.TargetTypeName;
        }

        // Seam 3: the polyglot SymbolKey-only target, resolved via the graph's
        // SymbolKey -> descriptor-name reverse mapping (no CLR Type, INV-8).
        if (link.TargetSymbolKey is { } symbolKey)
        {
            var bySymbol = graph.ObjectTypes.FirstOrDefault(
                o => string.Equals(o.SymbolKey, symbolKey, StringComparison.Ordinal));
            if (bySymbol is not null)
            {
                return bySymbol.Name;
            }
        }

        // T2 posture: refuse rather than mis-route to a first-CLR-match partition.
        throw new InvalidOperationException(
            $"Traversal link '{link.Name}' has no graph-resolvable target descriptor "
            + $"(TargetTypeName '{link.TargetTypeName}', TargetSymbolKey '{link.TargetSymbolKey}'). "
            + $"Supply an explicit target descriptor name to disambiguate, or register the link's "
            + $"declared target. The hop target is NEVER inferred from the CLR type (INV-8).");
    }

    /// <summary>
    /// Resolves the SET of concrete target descriptor names a link lowers to
    /// (DR-11b, #128). A link whose declared <see cref="Descriptors.LinkDescriptor.TargetTypeName"/>
    /// names a registered INTERFACE resolves to that interface's implementor
    /// descriptors (one per implementor — the polymorphic fan-out); any other link
    /// resolves to the single descriptor named by
    /// <see cref="ResolveHopTargetDescriptorName"/> (TargetTypeName →
    /// TargetSymbolKey). This drives the traversal read fan-out — one hop per
    /// returned descriptor, each routed to its own per-(link, target) junction
    /// table (Posture 2). Whether a link is polymorphic is decided up front by the
    /// interface-typed <c>IsPolymorphicLink</c> predicate, so this resolver is
    /// invoked only for links already known to fan out.
    /// </summary>
    /// <remarks>
    /// INV-8: identity by descriptor NAME, never <c>typeof</c>. The interface
    /// implementors come from <see cref="OntologyGraph.GetImplementors(string)"/>,
    /// which is keyed by the interface descriptor name — the same name a link's
    /// <c>TargetTypeName</c> carries when it targets an interface. Returned in a
    /// deterministic (ordinal-by-name) order so the fan-out SQL is stable.
    /// </remarks>
    internal static IReadOnlyList<string> ResolveLinkTargetDescriptors(
        OntologyGraph graph,
        Descriptors.LinkDescriptor link)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(link);

        // A link whose declared target names a registered interface fans out to
        // the interface's implementor descriptors (polymorphic).
        if (!string.IsNullOrEmpty(link.TargetTypeName)
            && graph.Interfaces.Any(i => string.Equals(i.Name, link.TargetTypeName, StringComparison.Ordinal)))
        {
            var implementors = graph.GetImplementors(link.TargetTypeName)
                .Select(o => o.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            // An interface with zero implementors has no provisionable junction
            // table (mirrors the compile-time AONT212 guard). Refuse rather than
            // emit a dead query.
            if (implementors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Link '{link.Name}' targets interface '{link.TargetTypeName}', which no object "
                    + $"type implements; under the per-(link, target-descriptor) junction posture a "
                    + $"polymorphic link has one junction table per implementor, so with zero "
                    + $"implementors there is no junction table to relate/traverse (see AONT212).");
            }

            return implementors;
        }

        // Otherwise the link is monomorphic — the single graph-resolved descriptor
        // (TargetTypeName → TargetSymbolKey), with no override.
        return [ResolveHopTargetDescriptorName(graph, link, targetDescriptorOverride: null)];
    }

    /// <summary>
    /// Resolves the live relate/unrelate write into a
    /// <see cref="JunctionTableDescriptor"/> for <c>(src, link, tgt)</c> (DR-11b).
    /// The caller's <paramref name="tgtDescriptor"/> is the row's resolved target
    /// descriptor; the junction is POLYMORPHIC (and thus named
    /// <c>{source}_{snake(link)}_{snake(target)}</c>) when the link resolves to
    /// MORE THAN ONE descriptor, and MONOMORPHIC (named <c>{source}_{snake(link)}</c>,
    /// unchanged from DR-7..DR-10) otherwise. Fed into
    /// <see cref="SqlGenerator.JunctionTableNameFor"/> so the relate INSERT targets
    /// the SAME physical table the T2 DDL creates.
    /// </summary>
    /// <remarks>
    /// INV-8: the source table comes from the resolved source endpoint and the
    /// target table from the resolved <paramref name="tgtDescriptor"/> endpoint —
    /// both by descriptor name, never <c>typeof</c>. The link must be declared on
    /// the source (caller already enforces this via <see cref="RequireLinkDeclared"/>);
    /// here it is read to decide polymorphism.
    /// </remarks>
    internal static JunctionTableDescriptor ResolveRelateJunction(
        OntologyGraph graph,
        string srcDescriptor,
        string linkName,
        string tgtDescriptor)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);

        var source = ResolveRelateEndpoint(graph, srcDescriptor);
        var target = ResolveRelateEndpoint(graph, tgtDescriptor);

        var sourceDescriptor = RequireDescriptor(graph, srcDescriptor);
        var link = sourceDescriptor.Links.FirstOrDefault(
            l => string.Equals(l.Name, linkName, StringComparison.Ordinal));

        return new JunctionTableDescriptor
        {
            SourceTable = source.TableName,
            LinkName = linkName,
            TargetDescriptorName = tgtDescriptor,
            TargetTable = target.TableName,
            IsPolymorphic = link is not null && IsPolymorphicLink(graph, link),
        };
    }

    /// <summary>
    /// Whether a link is POLYMORPHIC (DR-11b) — its declared target is a registered
    /// INTERFACE, so it lowers to a junction table PER implementor descriptor and
    /// each table is disambiguated by its target descriptor name (Posture 2). A
    /// link to a CONCRETE object target (or a non-interface name) is monomorphic
    /// and keeps the single <c>{source}_{snake(link)}</c> table. The predicate is
    /// the interface-typed check itself — NOT the implementor count — so the
    /// write-path routing and the read-path fan-out agree even for an interface
    /// with a single implementor (both use the per-descriptor table). NEVER throws:
    /// a relate to a link whose declared target is not graph-resolvable keeps its
    /// pre-DR-11 single-table semantics (the caller's tgtDescriptor stays
    /// authoritative for the row). INV-8: by descriptor name, never <c>typeof</c>.
    /// </summary>
    private static bool IsPolymorphicLink(OntologyGraph graph, Descriptors.LinkDescriptor link) =>
        !string.IsNullOrEmpty(link.TargetTypeName)
        && graph.Interfaces.Any(i => string.Equals(i.Name, link.TargetTypeName, StringComparison.Ordinal));

    /// <summary>
    /// Resolves an instance-anchored traversal hop into the SET of physical hops
    /// it reads (DR-11b). A MONOMORPHIC link (or any hop carrying an explicit
    /// <paramref name="targetDescriptorOverride"/>) yields a single hop, named
    /// exactly as <see cref="ResolveTraversalHop"/> does; a POLYMORPHIC link with
    /// NO override fans out into ONE hop per resolved target descriptor, each
    /// anchored at its own per-(link, target) junction table and its target
    /// descriptor's object table. The hops feed
    /// <see cref="SqlGenerator.BuildPolymorphicTraversalSql"/> (UNION ALL) or, for
    /// the single-hop case, <see cref="SqlGenerator.BuildInstanceAnchoredTraversalSql"/>.
    /// </summary>
    /// <remarks>
    /// Graph-driven (INV-8): the fan-out descriptor set comes from
    /// <see cref="ResolveLinkTargetDescriptors"/>, so no
    /// <see cref="ObjectSets.TraverseLinkExpression"/> change is needed — the
    /// expression's existing nullable <c>TargetDescriptorName</c> already
    /// distinguishes the disambiguated-single hop (override supplied) from the
    /// fan-out hop (override null). An explicit override always denotes a single
    /// target partition, so it is never fanned out.
    /// </remarks>
    internal static IReadOnlyList<TraversalHop> ResolveTraversalHops(
        OntologyGraph? graph,
        string sourceDescriptorName,
        string linkName,
        string? targetDescriptorOverride)
    {
        ArgumentNullException.ThrowIfNull(sourceDescriptorName);
        ArgumentNullException.ThrowIfNull(linkName);

        if (graph is null)
        {
            // Reuse the single-hop resolver's typed null-graph error.
            return [ResolveTraversalHop(graph, sourceDescriptorName, linkName, targetDescriptorOverride)];
        }

        // An explicit override names a single partition — never a fan-out.
        if (targetDescriptorOverride is not null)
        {
            return [ResolveTraversalHop(graph, sourceDescriptorName, linkName, targetDescriptorOverride)];
        }

        var sourceDescriptor = RequireDescriptor(graph, sourceDescriptorName);
        var link = sourceDescriptor.Links.FirstOrDefault(
            l => string.Equals(l.Name, linkName, StringComparison.Ordinal));
        if (link is null)
        {
            // Defer to the single-hop resolver for the canonical typed error.
            return [ResolveTraversalHop(graph, sourceDescriptorName, linkName, targetDescriptorOverride: null)];
        }

        // Monomorphic — the single-hop lowering, unchanged from DR-7..DR-10. Uses
        // the SAME interface-typed predicate as the relate write-path so the two
        // never disagree on which links fan out.
        if (!IsPolymorphicLink(graph, link))
        {
            return [ResolveTraversalHop(graph, sourceDescriptorName, linkName, targetDescriptorOverride: null)];
        }

        // Polymorphic — one hop per resolved target descriptor, each routed to its
        // own per-(link, target) junction table and target object table.
        var targets = ResolveLinkTargetDescriptors(graph, link);
        var source = ResolveRelateEndpoint(graph, sourceDescriptorName);
        var hops = new List<TraversalHop>(targets.Count);
        foreach (var targetName in targets)
        {
            var target = ResolveRelateEndpoint(graph, targetName);
            var junctionName = SqlGenerator.JunctionTableNameFor(new JunctionTableDescriptor
            {
                SourceTable = source.TableName,
                LinkName = linkName,
                TargetDescriptorName = targetName,
                TargetTable = target.TableName,
                IsPolymorphic = true,
            });

            hops.Add(new TraversalHop(
                source.TableName,
                source.KeyProperty,
                junctionName,
                target.TableName,
                targetName));
        }

        return hops;
    }

    /// <summary>
    /// A resolved instance-anchored traversal hop (DR-7/DR-10): the physical join
    /// operands produced by
    /// <see cref="ResolveTraversalHop(OntologyGraph?, string, string, string?)"/>
    /// and fed unchanged into
    /// <see cref="SqlGenerator.BuildInstanceAnchoredTraversalSql"/>. New sealed,
    /// <c>init</c>-only positional record (INV-6/INV-7).
    /// </summary>
    /// <param name="SourceTable">The source endpoint object table (snake_cased).</param>
    /// <param name="SourceKeyProperty">
    /// The source descriptor's key property — the <c>data jsonb</c> field holding
    /// the anchor instance's business id.
    /// </param>
    /// <param name="JunctionTable">
    /// The T8 junction table for <c>(source, link)</c>.
    /// </param>
    /// <param name="TargetTable">
    /// The TARGET endpoint object table (snake_cased) — derived from the
    /// GRAPH-resolved <see cref="TargetDescriptorName"/>, never
    /// <c>typeof(TLinked)</c> (INV-8).
    /// </param>
    /// <param name="TargetDescriptorName">
    /// The graph-resolved hop-target descriptor name (DR-10).
    /// </param>
    internal sealed record TraversalHop(
        string SourceTable,
        string SourceKeyProperty,
        string JunctionTable,
        string TargetTable,
        string TargetDescriptorName);

    /// <summary>
    /// A single DEPTH STEP of a (possibly chained) instance-anchored traversal
    /// (DR-12). One step lowers ONE link hop. A MONOMORPHIC step carries exactly
    /// one <see cref="Hops"/> entry (the single resolved target); a POLYMORPHIC
    /// step carries one entry PER resolved target descriptor — the DR-11b fan-out —
    /// so it lowers to a UNION over its per-descriptor junction+vertex tables. A
    /// polymorphic step counts as fan-out against the depth budget (DR-12 tiering).
    /// New sealed, <c>init</c>-only record (INV-6/INV-7).
    /// </summary>
    /// <param name="LinkName">The link traversed at this depth step.</param>
    /// <param name="Hops">
    /// The resolved hops for this step: one (monomorphic) or several
    /// (polymorphic fan-out), as produced by <see cref="ResolveTraversalHops"/>.
    /// </param>
    internal sealed record TraversalStep(string LinkName, IReadOnlyList<TraversalHop> Hops)
    {
        /// <summary>
        /// Whether this step is a POLYMORPHIC fan-out — more than one resolved hop,
        /// so it lowers to a UNION over per-descriptor tables (DR-11b) and counts as
        /// fan-out against the DR-12 depth budget.
        /// </summary>
        public bool IsPolymorphic => Hops.Count > 1;
    }

    /// <summary>
    /// A resolved instance-anchored traversal PLAN (DR-12): the ordered depth
    /// <see cref="Steps"/> a (possibly chained) <see cref="ObjectSets.TraverseLinkExpression"/>
    /// lowers to, anchored at the source instance's business id
    /// (<see cref="SourceId"/>) under the source table's
    /// <see cref="SourceKeyProperty"/>. Fed into the depth-tiered SQL builders
    /// (join-chain ≤ the budget, recursive CTE beyond it; a polymorphic step
    /// fans out via UNION). New sealed, <c>init</c>-only record (INV-6/INV-7).
    /// </summary>
    internal sealed record TraversalPlan
    {
        /// <summary>The source endpoint object table (snake_cased), aliased the anchor vertex.</summary>
        public required string SourceTable { get; init; }

        /// <summary>
        /// The source descriptor's key property — the <c>data jsonb</c> field
        /// holding the anchor instance's business id (INV-8: identity by descriptor).
        /// </summary>
        public required string SourceKeyProperty { get; init; }

        /// <summary>
        /// The anchor instance's business id, bound via <c>@srcId</c> (never
        /// interpolated), or <c>null</c> when the source chain carries no
        /// single-id anchoring filter (the lowering then anchors structurally
        /// through the junction join only).
        /// </summary>
        public string? SourceId { get; init; }

        /// <summary>The ordered depth steps, anchor-outward.</summary>
        public required IReadOnlyList<TraversalStep> Steps { get; init; }

        /// <summary>
        /// The DR-12 depth-budget count: number of depth steps. Each step is one
        /// link hop regardless of its fan-out width (a polymorphic step is a single
        /// depth level that fans out via UNION).
        /// </summary>
        public int Depth => Steps.Count;

        /// <summary>
        /// Whether ANY depth step is a polymorphic fan-out (DR-11b). A polymorphic
        /// step counts as fan-out against the depth budget, so a plan containing one
        /// is lowered past the join-chain tier into the recursive-CTE tier even when
        /// its <see cref="Depth"/> is within budget (DR-12).
        /// </summary>
        public bool HasPolymorphicStep => Steps.Any(s => s.IsPolymorphic);
    }

    /// <summary>
    /// The lowered SQL for an instance-anchored traversal plan (DR-12): the
    /// generated parameterized statement (<see cref="Sql"/>) and its bound
    /// <see cref="Parameters"/>. Produced by
    /// <see cref="LowerTraversalExpression"/> and consumed by
    /// <see cref="ExecuteAsync{T}"/> / <see cref="StreamAsync{T}"/>. New sealed,
    /// <c>init</c>-only record (INV-6/INV-7).
    /// </summary>
    internal sealed record TraversalLowering(
        string Sql,
        IReadOnlyList<ExpressionTranslator.SqlParameter> Parameters);

    /// <summary>
    /// Lowers an instance-anchored <see cref="ObjectSets.TraverseLinkExpression"/>
    /// (possibly chained) into depth-tiered traversal SQL (DR-12). This is the seam
    /// the public read path (<see cref="ExecuteAsync{T}"/> / <see cref="StreamAsync{T}"/>)
    /// routes a traversal through, closing the DR-7..DR-11b gap where
    /// <see cref="ExpressionTranslator.Translate"/> threw on a traversal node.
    /// </summary>
    /// <param name="graph">
    /// The ontology graph. Traversal is graph-aware (link targets, key properties,
    /// polymorphic fan-out), so a null graph throws via the hop resolver rather than
    /// emitting wrong SQL.
    /// </param>
    /// <param name="expression">The traversal expression (outermost node a hop).</param>
    /// <param name="schema">The Postgres schema (e.g. <c>"public"</c>).</param>
    /// <remarks>
    /// DR-12 tiering: a plan within the join-collapse depth budget
    /// (<see cref="SqlGenerator.JoinChainDepthBudget"/>) with no polymorphic step
    /// lowers to a JOIN CHAIN; a deeper plan — or any plan whose step fans out
    /// polymorphically (a polymorphic hop counts as fan-out against the budget) —
    /// lowers to a RECURSIVE CTE. The hop targets are resolved from the GRAPH via
    /// the DR-10 path (<see cref="ResolveTraversalHops"/>), never <c>typeof</c> (INV-8).
    /// </remarks>
    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    internal static TraversalLowering LowerTraversalExpression(
        OntologyGraph? graph,
        ObjectSets.ObjectSetExpression expression,
        string schema)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        var plan = BuildTraversalPlan(graph, expression);
        var parameters = new List<ExpressionTranslator.SqlParameter>();
        if (plan.SourceId is { } srcId)
        {
            parameters.Add(new ExpressionTranslator.SqlParameter("@srcId", srcId));
        }

        var sql = SqlGenerator.BuildDepthTieredTraversalSql(schema, plan);
        return new TraversalLowering(sql, parameters);
    }

    /// <summary>
    /// Resolves a (possibly chained) <see cref="ObjectSets.TraverseLinkExpression"/>
    /// into an anchor-outward <see cref="TraversalPlan"/> (DR-12): one
    /// <see cref="TraversalStep"/> per link hop, the source table + key property of
    /// the anchor, and the anchor instance's business id when the source chain
    /// carries a single-id filter.
    /// </summary>
    /// <remarks>
    /// The chain is walked from the OUTERMOST hop back to the anchor: each hop's
    /// SOURCE descriptor is the prior hop's resolved target (or the root for the
    /// first hop). A polymorphic hop is resolved via <see cref="ResolveTraversalHops"/>
    /// (one hop per implementor descriptor); a chained hop after a polymorphic step
    /// would be ambiguous (which fanned-out descriptor is the source?), so a chained
    /// step's source descriptor is taken from the prior step's SINGLE resolved hop —
    /// a fan-out followed by a further hop is refused upstream by the resolver when
    /// the prior step is polymorphic. INV-8: source/target by descriptor name.
    /// </remarks>
    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    internal static TraversalPlan BuildTraversalPlan(
        OntologyGraph? graph,
        ObjectSets.ObjectSetExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Collect the traversal nodes anchor-outward (the expression nests
        // outermost-first, so reverse the walk).
        var hopsOuterFirst = new List<ObjectSets.TraverseLinkExpression>();
        var cursor = expression;
        while (true)
        {
            switch (cursor)
            {
                case ObjectSets.TraverseLinkExpression traverse:
                    hopsOuterFirst.Add(traverse);
                    cursor = traverse.Source;
                    continue;
                case ObjectSets.FilterExpression filter:
                    cursor = filter.Source;
                    continue;
                case ObjectSets.IncludeExpression include:
                    cursor = include.Source;
                    continue;
                case ObjectSets.InterfaceNarrowExpression narrow:
                    cursor = narrow.Source;
                    continue;
            }

            break;
        }

        if (hopsOuterFirst.Count == 0)
        {
            throw new InvalidOperationException(
                "LowerTraversalExpression requires a TraverseLinkExpression in the chain; "
                + "the supplied expression has no link hop to lower.");
        }

        hopsOuterFirst.Reverse();
        var hopsAnchorFirst = hopsOuterFirst;

        // The anchor's source descriptor name + its single-id filter come from the
        // FIRST hop's own source chain (walk to the root, translating any filter).
        var firstHop = hopsAnchorFirst[0];
        var anchorSourceDescriptor = ResolveImmediateSourceDescriptorName(firstHop.Source);
        var (sourceTable, sourceKeyProperty, sourceId) = ResolveAnchor(graph, anchorSourceDescriptor, firstHop.Source);

        var steps = new List<TraversalStep>(hopsAnchorFirst.Count);
        var currentSourceDescriptor = anchorSourceDescriptor;
        for (var i = 0; i < hopsAnchorFirst.Count; i++)
        {
            var hop = hopsAnchorFirst[i];
            var resolvedHops = ResolveTraversalHops(
                graph, currentSourceDescriptor, hop.LinkName, hop.TargetDescriptorName);
            steps.Add(new TraversalStep(hop.LinkName, resolvedHops));

            // The next step's source is THIS step's resolved target. A polymorphic
            // step has several targets, so a further chained hop is ambiguous —
            // refuse it rather than silently picking one descriptor.
            if (i + 1 < hopsAnchorFirst.Count)
            {
                if (resolvedHops.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Chained traversal after a POLYMORPHIC hop on link '{hop.LinkName}' "
                        + $"(which fans out to {resolvedHops.Count} target descriptors) is ambiguous: "
                        + $"the next hop's source descriptor is not uniquely defined. Disambiguate the "
                        + $"polymorphic hop with an explicit target descriptor name before chaining.");
                }

                currentSourceDescriptor = resolvedHops[0].TargetDescriptorName;
            }
        }

        return new TraversalPlan
        {
            SourceTable = sourceTable,
            SourceKeyProperty = sourceKeyProperty,
            SourceId = sourceId,
            Steps = steps,
        };
    }

    /// <summary>
    /// Resolves the anchor source's physical table + key property and the anchor
    /// instance's business id (DR-12). The id is extracted from the source chain's
    /// single-parameter filter (a <c>Where(s =&gt; s.Key == value)</c> anchoring on
    /// the business id); a source chain with no such filter yields a <c>null</c>
    /// id and the lowering anchors structurally through the junction join only.
    /// </summary>
    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static (string SourceTable, string SourceKeyProperty, string? SourceId) ResolveAnchor(
        OntologyGraph? graph,
        string anchorSourceDescriptor,
        ObjectSets.ObjectSetExpression anchorSource)
    {
        var endpoint = ResolveRelateEndpoint(graph, anchorSourceDescriptor);

        // Translate the source chain's filter to recover the anchor business id.
        // The anchored-traversal contract addresses the source by a single business
        // id, so a single-parameter filter binds @srcId; anything else leaves the
        // id null (structural anchoring only).
        var translation = ExpressionTranslator.Translate(anchorSource);
        var sourceId = translation.Parameters.Count == 1
            ? translation.Parameters[0].Value as string
            : null;

        return (endpoint.TableName, endpoint.KeyProperty, sourceId);
    }

    /// <summary>
    /// Resolves the descriptor name of the IMMEDIATE upstream element type of a
    /// traversal source chain (DR-12), mirroring
    /// <c>InMemoryExpressionEvaluator.ResolveImmediateSourceDescriptorName</c>: a
    /// root produces its declared descriptor name; a prior traversal produces its
    /// (graph-resolved) target descriptor; filters/includes/narrows are transparent.
    /// </summary>
    private static string ResolveImmediateSourceDescriptorName(ObjectSets.ObjectSetExpression expression) =>
        expression switch
        {
            ObjectSets.RootExpression root => root.ObjectTypeName,
            ObjectSets.TraverseLinkExpression traverse => traverse.RootObjectTypeName,
            ObjectSets.FilterExpression filter => ResolveImmediateSourceDescriptorName(filter.Source),
            ObjectSets.IncludeExpression include => ResolveImmediateSourceDescriptorName(include.Source),
            ObjectSets.InterfaceNarrowExpression narrow => ResolveImmediateSourceDescriptorName(narrow.Source),
            _ => throw new NotSupportedException(
                $"Cannot resolve immediate source descriptor from {expression.GetType().Name}."),
        };
}
