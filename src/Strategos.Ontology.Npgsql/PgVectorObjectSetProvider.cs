using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
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
    private readonly PgVectorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PgVectorObjectSetProvider"/> class.
    /// </summary>
    public PgVectorObjectSetProvider(
        NpgsqlDataSource dataSource,
        IEmbeddingProvider embeddingProvider,
        IOptions<PgVectorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(embeddingProvider);
        ArgumentNullException.ThrowIfNull(options);

        _dataSource = dataSource;
        _embeddingProvider = embeddingProvider;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        // 1. Get query vector: use expression.QueryVector if provided, else embed QueryText
        var queryVector = expression.QueryVector
            ?? await _embeddingProvider.EmbedAsync(expression.QueryText, ct).ConfigureAwait(false);

        // 2. Get table name from TypeMapper
        var tableName = TypeMapper.GetTableName<T>();

        // 3. Build SQL
        var sql = SqlGenerator.BuildSimilarityQuery(_options.Schema, tableName, expression.Metric);

        // 4. Execute query
        var items = new List<T>();
        var scores = new List<double>();

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("query", new Vector(queryVector));
        cmd.Parameters.AddWithValue("topK", expression.TopK);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var dataJson = reader.GetString(1);
            var distance = reader.GetDouble(2);

            // Convert distance to similarity score (0..1 where 1 = most similar)
            var similarity = ConvertDistanceToSimilarity(distance, expression.Metric);

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
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var dataJson = reader.GetString(1);
            var item = JsonSerializer.Deserialize<T>(dataJson);
            if (item is not null)
            {
                items.Add(item);
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

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var dataJson = reader.GetString(1);
            var item = JsonSerializer.Deserialize<T>(dataJson);
            if (item is not null)
            {
                yield return item;
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
        var sql = SqlGenerator.BuildInsertSql(_options.Schema, tableName, hasEmbedding);

        await using var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var batch = new NpgsqlBatch(connection);

        foreach (var item in items)
        {
            var batchCmd = new NpgsqlBatchCommand(sql);
            batchCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            batchCmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(item));

            if (item is ISearchable searchable)
            {
                batchCmd.Parameters.AddWithValue("embedding", new Vector(searchable.Embedding));
            }

            batch.BatchCommands.Add(batchCmd);
        }

        await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
        // Cosine distance is in [0, 2], similarity = 1 - distance
        DistanceMetric.Cosine => 1.0 - distance,
        // L2 distance is in [0, inf), similarity = 1 / (1 + distance)
        DistanceMetric.L2 => 1.0 / (1.0 + distance),
        // Inner product: pgvector returns negative inner product, similarity = -distance
        DistanceMetric.InnerProduct => -distance,
        _ => 1.0 - distance,
    };

    private static void AddTranslatedParameters(NpgsqlCommand cmd, IReadOnlyList<ExpressionTranslator.SqlParameter> parameters)
    {
        foreach (var p in parameters)
        {
            cmd.Parameters.AddWithValue(p.Name.TrimStart('@'), p.Value);
        }
    }
}
