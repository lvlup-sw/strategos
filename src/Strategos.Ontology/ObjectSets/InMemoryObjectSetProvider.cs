using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// In-memory implementation of <see cref="IObjectSetProvider"/> for testing and development.
/// Supports seeding items with searchable content, in-memory filtering via compiled predicates,
/// and keyword-based similarity scoring.
/// </summary>
public sealed class InMemoryObjectSetProvider : IObjectSetProvider
{
    private readonly ConcurrentDictionary<Type, List<object>> _items = new();
    private readonly ConcurrentDictionary<Type, List<string>> _searchableContent = new();

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
    }

    /// <inheritdoc />
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class
    {
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
    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        var items = GetSeededItems<T>();
        var content = GetSearchableContent<T>();

        if (items.Count == 0)
        {
            return Task.FromResult(new ScoredObjectSetResult<T>(
                Array.Empty<T>(), 0, ObjectSetInclusion.Properties, Array.Empty<double>()));
        }

        var scored = items
            .Select((item, index) => (Item: item, Score: CalculateMockScore(expression.QueryText, content[index])))
            .Where(pair => pair.Score >= expression.MinRelevance)
            .OrderByDescending(pair => pair.Score)
            .ToList();

        var totalCount = scored.Count;

        var limited = scored
            .Take(expression.TopK)
            .ToList();

        var resultItems = limited.Select(p => p.Item).ToList();
        var resultScores = limited.Select(p => p.Score).ToList();

        var result = new ScoredObjectSetResult<T>(
            resultItems, totalCount, ObjectSetInclusion.Properties, resultScores);

        return Task.FromResult(result);
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
            var compiled = filter.Predicate.Compile();
            var func = (Func<T, bool>)compiled;
            return items.Where(func).ToList();
        }

        // For RootExpression and other unsupported expression types, return all items
        // (RootExpression) or empty (unknown types handled by returning all seeded)
        return items;
    }

    private static double CalculateMockScore(string query, string content)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(content))
        {
            return 0.0;
        }

        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matchCount = queryTerms.Count(q => content.Contains(q, StringComparison.OrdinalIgnoreCase));
        return (double)matchCount / queryTerms.Length;
    }
}
