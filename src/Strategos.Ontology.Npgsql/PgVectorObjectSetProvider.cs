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

    /// <summary>
    /// Initializes a new instance of the <see cref="PgVectorObjectSetProvider"/> class.
    /// </summary>
    public PgVectorObjectSetProvider(
        NpgsqlDataSource dataSource,
        IEmbeddingProvider embeddingProvider,
        IOptions<PgVectorOptions> options,
        ILogger<PgVectorObjectSetProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(embeddingProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _dataSource = dataSource;
        _embeddingProvider = embeddingProvider;
        _options = options.Value;
        _logger = logger;
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

        // 2. Get table name from TypeMapper
        var tableName = TypeMapper.GetTableName<T>();

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

        var tableName = TypeMapper.GetTableName<T>();
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

        var tableName = TypeMapper.GetTableName<T>();
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
    public async Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);

        var tableName = TypeMapper.GetTableName<T>();
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

    /// <inheritdoc />
    public async Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return;
        }

        var tableName = TypeMapper.GetTableName<T>();
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

    // The two explicit-descriptor-name overloads below are INTENTIONAL temporary placeholders
    // landed by Task F1 (Strategos 2.4.1 Ontology Descriptor-Name Dispatch, bug #31). They exist
    // solely so PgVectorObjectSetProvider continues to satisfy IObjectSetWriter while the interface
    // change lands ahead of the real implementation. Task F3 (graph-backed explicit-name write path)
    // will replace the NotImplementedException throws with the descriptor-partition-aware
    // SQL/COPY logic. Do not call these overloads yet.

    /// <inheritdoc />
    public Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class
        => throw new NotImplementedException("Implemented in Task F3");

    /// <inheritdoc />
    public Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class
        => throw new NotImplementedException("Implemented in Task F3");

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
