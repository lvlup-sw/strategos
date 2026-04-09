using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Npgsql.Internal;
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
    /// <typeparamref name="T"/>. Falls back to <c>typeof(T).Name</c> when
    /// <paramref name="graph"/> is <c>null</c> or the type is absent from
    /// the graph.
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
        if (graph is not null && graph.ObjectTypeNamesByType.TryGetValue(typeof(T), out var names))
        {
            if (names.Count == 1)
            {
                return TypeMapper.ToSnakeCase(names[0]);
            }

            if (names.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Type '{typeof(T).FullName}' has multiple registrations " +
                    $"({string.Join(", ", names.Select(n => $"'{n}'"))}). " +
                    $"Use StoreAsync<T>(string descriptorName, T item, ct) or " +
                    $"StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, ct) " +
                    $"to specify the target descriptor.");
            }
        }

        // Graph absent, type unregistered, or empty name list — fall back to
        // typeof(T).Name → snake_case for back-compat with direct unit-test
        // instantiation and DI configurations that do not resolve an
        // OntologyGraph.
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

        // 4. Build SQL with optional WHERE clause
        var sql = SqlGenerator.BuildSimilarityQuery(_options.Schema, tableName, expression.Metric, whereClause);

        // 5. Execute query
        var items = new List<T>();
        var scores = new List<double>();

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("query", new Vector(queryVector));
        cmd.Parameters.AddWithValue("topK", expression.TopK);

        foreach (var p in filterParams)
        {
            cmd.Parameters.AddWithValue(p.Name.TrimStart('@'), p.Value);
        }

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

        return new ScoredObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties, scores);
    }

    /// <inheritdoc />
    public async Task<ObjectSetResult<T>> ExecuteAsync<T>(
        ObjectSetExpression expression, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Resolve table name from the expression's declared descriptor name
        // via the shared read-path dispatch helper (bug #31).
        var tableName = ResolveTableName(expression);
        var translation = ExpressionTranslator.Translate(expression);
        var sql = SqlGenerator.BuildSelectQuery(_options.Schema, tableName, translation.WhereClause);

        var items = new List<T>();

        await using var cmd = _dataSource.CreateCommand(sql);
        AddTranslatedParameters(cmd, translation.Parameters);

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

    /// <inheritdoc />
    public async IAsyncEnumerable<T> StreamAsync<T>(
        ObjectSetExpression expression, [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        // Resolve table name from the expression's declared descriptor name
        // via the shared read-path dispatch helper (bug #31).
        var tableName = ResolveTableName(expression);
        var translation = ExpressionTranslator.Translate(expression);
        var sql = SqlGenerator.BuildSelectQuery(_options.Schema, tableName, translation.WhereClause);

        await using var cmd = _dataSource.CreateCommand(sql);
        AddTranslatedParameters(cmd, translation.Parameters);

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
    public async Task EnsureSchemaAsync<T>(CancellationToken ct = default) where T : class
    {
        var tableName = TypeMapper.GetTableName<T>();
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            _options.Schema,
            tableName,
            _embeddingProvider.Dimensions,
            _options.IndexType);

        await using var cmd = _dataSource.CreateCommand(ddl);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
}
