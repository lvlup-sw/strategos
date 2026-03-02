using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Strategos.Ontology.Embeddings;

namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// In-memory implementation of <see cref="IObjectSetProvider"/> and <see cref="IObjectSetWriter"/>
/// for testing and development.
/// Supports seeding items with searchable content, in-memory filtering via compiled predicates,
/// and keyword-based or cosine similarity scoring.
/// </summary>
/// <remarks>
/// <see cref="Seed{T}"/> is not thread-safe. Seed all items before querying,
/// or synchronize access externally if seeding concurrently.
/// When an <see cref="IEmbeddingProvider"/> is supplied, <see cref="ExecuteSimilarityAsync{T}"/>
/// uses real cosine similarity against stored embeddings instead of keyword scoring.
/// </remarks>
public sealed class InMemoryObjectSetProvider : IObjectSetProvider, IObjectSetWriter
{
    private readonly ConcurrentDictionary<Type, List<object>> _items = new();
    private readonly ConcurrentDictionary<Type, List<string>> _searchableContent = new();
    private readonly ConcurrentDictionary<Type, List<float[]>> _embeddings = new();
    private readonly IEmbeddingProvider? _embeddingProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectSetProvider"/> class.
    /// </summary>
    public InMemoryObjectSetProvider()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectSetProvider"/> class
    /// with an optional embedding provider for cosine similarity scoring.
    /// </summary>
    /// <param name="embeddingProvider">
    /// When provided, <see cref="ExecuteSimilarityAsync{T}"/> will use real cosine similarity
    /// against stored embeddings instead of keyword scoring.
    /// </param>
    public InMemoryObjectSetProvider(IEmbeddingProvider? embeddingProvider)
    {
        _embeddingProvider = embeddingProvider;
    }

    /// <summary>
    /// Seeds an item into the in-memory store with its associated searchable text content.
    /// </summary>
    /// <typeparam name="T">The domain object type.</typeparam>
    /// <param name="item">The item to seed.</param>
    /// <param name="searchableContent">The text content used for keyword-based similarity scoring.</param>
    public void Seed<T>(T item, string searchableContent) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(searchableContent);

        var type = typeof(T);
        _items.GetOrAdd(type, _ => new List<object>()).Add(item);
        _searchableContent.GetOrAdd(type, _ => new List<string>()).Add(searchableContent);

        // Store embedding (or empty placeholder) to maintain index alignment with _items
        var embedding = item is ISearchable searchable ? searchable.Embedding : [];
        _embeddings.GetOrAdd(type, _ => new List<float[]>()).Add(embedding);
    }

    /// <inheritdoc />
    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);

        var type = typeof(T);
        var searchableText = item.ToString() ?? string.Empty;

        _items.GetOrAdd(type, _ => new List<object>()).Add(item);
        _searchableContent.GetOrAdd(type, _ => new List<string>()).Add(searchableText);

        // Store embedding (or empty placeholder) to maintain index alignment with _items
        var embedding = item is ISearchable searchable ? searchable.Embedding : [];
        _embeddings.GetOrAdd(type, _ => new List<float[]>()).Add(embedding);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await StoreAsync(item, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);
        var items = GetSeededItems<T>();
        var filtered = ApplyExpression(items, expression);
        var result = new ObjectSetResult<T>(filtered, filtered.Count, ObjectSetInclusion.Properties);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> StreamAsync<T>(
        ObjectSetExpression expression,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);
        var items = GetSeededItems<T>();
        var filtered = ApplyExpression(items, expression);

        foreach (var item in filtered)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        var items = GetSeededItems<T>();

        if (items.Count == 0)
        {
            return new ScoredObjectSetResult<T>(
                Array.Empty<T>(), 0, ObjectSetInclusion.Properties, Array.Empty<double>());
        }

        // Apply Source filter if present (e.g., pre-filtered source + similarity)
        if (expression.Source is FilterExpression filter)
        {
            items = ApplyExpression(items, filter);
        }

        // When an embedding provider is available and embeddings are stored, use cosine similarity
        if (_embeddingProvider is not null && _embeddings.TryGetValue(typeof(T), out var storedEmbeddings) && storedEmbeddings.Count > 0)
        {
            return await ExecuteCosineSimilarityAsync(items, storedEmbeddings, expression, ct).ConfigureAwait(false);
        }

        // Fall back to keyword scoring
        return ExecuteKeywordSimilarity(items, expression);
    }

    private async Task<ScoredObjectSetResult<T>> ExecuteCosineSimilarityAsync<T>(
        List<T> items,
        List<float[]> storedEmbeddings,
        SimilarityExpression expression,
        CancellationToken ct) where T : class
    {
        // Honor QueryVector if provided, otherwise embed QueryText
        var queryEmbedding = expression.QueryVector
            ?? await _embeddingProvider!.EmbedAsync(expression.QueryText, ct).ConfigureAwait(false);

        // Build identity-based index map to avoid O(n^2) IndexOf lookups and duplicate-item misalignment
        var allItems = GetSeededItems<T>();
        var indexMap = BuildIdentityIndexMap(allItems);

        var scored = items
            .Select(item =>
            {
                var embedding = indexMap.TryGetValue(item, out var idx) && idx < storedEmbeddings.Count
                    ? storedEmbeddings[idx]
                    : [];
                return (Item: item, Score: CosineSimilarity(queryEmbedding, embedding));
            })
            .Where(pair => pair.Score >= expression.MinRelevance)
            .OrderByDescending(pair => pair.Score)
            .ToList();

        var totalCount = scored.Count;

        var limited = scored
            .Take(expression.TopK)
            .ToList();

        var resultItems = limited.Select(p => p.Item).ToList();
        var resultScores = limited.Select(p => p.Score).ToList();

        return new ScoredObjectSetResult<T>(
            resultItems, totalCount, ObjectSetInclusion.Properties, resultScores);
    }

    private ScoredObjectSetResult<T> ExecuteKeywordSimilarity<T>(
        List<T> items,
        SimilarityExpression expression) where T : class
    {
        // Build identity-based index map to avoid O(n^2) IndexOf lookups and duplicate-item misalignment
        var allItems = GetSeededItems<T>();
        var allContent = GetSearchableContent<T>();
        var indexMap = BuildIdentityIndexMap(allItems);

        var scored = items
            .Select(item =>
            {
                var content = indexMap.TryGetValue(item, out var idx) && idx < allContent.Count
                    ? allContent[idx]
                    : string.Empty;
                return (Item: item, Score: CalculateKeywordScore(expression.QueryText, content));
            })
            .Where(pair => pair.Score >= expression.MinRelevance)
            .OrderByDescending(pair => pair.Score)
            .ToList();

        var totalCount = scored.Count;

        var limited = scored
            .Take(expression.TopK)
            .ToList();

        var resultItems = limited.Select(p => p.Item).ToList();
        var resultScores = limited.Select(p => p.Score).ToList();

        return new ScoredObjectSetResult<T>(
            resultItems, totalCount, ObjectSetInclusion.Properties, resultScores);
    }

    /// <summary>
    /// Builds an identity-based (ReferenceEquals) map from item to its index in the list.
    /// First occurrence wins, avoiding issues with duplicate-valued items.
    /// </summary>
    private static Dictionary<T, int> BuildIdentityIndexMap<T>(List<T> items) where T : class
    {
        var map = new Dictionary<T, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < items.Count; i++)
        {
            map.TryAdd(items[i], i);
        }

        return map;
    }

    private List<T> GetSeededItems<T>() where T : class
    {
        if (!_items.TryGetValue(typeof(T), out var items))
        {
            return new List<T>();
        }

        return items.Cast<T>().ToList();
    }

    private List<string> GetSearchableContent<T>() where T : class
    {
        if (!_searchableContent.TryGetValue(typeof(T), out var content))
        {
            return new List<string>();
        }

        return content;
    }

    private static List<T> ApplyExpression<T>(List<T> items, ObjectSetExpression expression) where T : class
    {
        if (expression is FilterExpression filter)
        {
            // Recursively apply the source expression first
            var filtered = ApplyExpression(items, filter.Source);

            var compiled = filter.Predicate.Compile();
            if (compiled is not Func<T, bool> func)
            {
                throw new InvalidOperationException(
                    $"Filter predicate type '{compiled.GetType()}' is not compatible with Func<{typeof(T).Name}, bool>.");
            }

            return filtered.Where(func).ToList();
        }

        // RootExpression — return all items (base case)
        return items;
    }

    private static double CalculateKeywordScore(string query, string content)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(content))
        {
            return 0.0;
        }

        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matchCount = queryTerms.Count(q => content.Contains(q, StringComparison.OrdinalIgnoreCase));
        return (double)matchCount / queryTerms.Length;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0.0;
        }

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        if (denominator == 0)
        {
            return 0.0;
        }

        return dotProduct / denominator;
    }
}
