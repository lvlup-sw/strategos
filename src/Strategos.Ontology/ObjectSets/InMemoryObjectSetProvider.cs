using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Identity;

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
    // Partitioned by ontology descriptor name (string), NOT by CLR type. A
    // single CLR type may be registered under multiple descriptors (e.g.
    // trading_documents vs. knowledge_documents, both backed by
    // SemanticDocument); each descriptor must own its own partition so
    // queries routed to one do not accidentally see items from another
    // (bug #31 / Strategos 2.4.1). The default partition key for a
    // Seed<T>(item, content) call with no explicit descriptor name is
    // typeof(T).Name, which matches the default root expression built by
    // ObjectSet<T> when no descriptor name is supplied.
    private readonly ConcurrentDictionary<string, List<object>> _items = new();
    private readonly ConcurrentDictionary<string, List<string>> _searchableContent = new();
    private readonly ConcurrentDictionary<string, List<float[]>> _embeddings = new();

    // Materialized relation rows (DR-2). Keyed by the relation triple's
    // (srcDescriptor, srcId, linkName); the value is the list of target
    // endpoints related to that source under that link. The raw store is
    // insertion-ordered, but the READ path (GetRelations) never exposes that
    // order — it sorts ordinal-by-TargetId so replay is deterministic (INV-7).
    private readonly ConcurrentDictionary<(string SrcDescriptor, string SrcId, string LinkName), List<RelationRow>> _relations = new();
    private readonly IEmbeddingProvider? _embeddingProvider;
    private readonly InMemoryExpressionEvaluator? _evaluator;

    // Descriptor index by name, populated for graph-aware instances (DR-2).
    // Used to resolve the IdAccessor for eager endpoint validation in
    // RelateAsync and the LinkDescriptor for the self-loop policy. Null for
    // graph-less instances, whose seeded items carry no key accessor.
    private readonly IReadOnlyDictionary<string, ObjectTypeDescriptor>? _descriptorIndex;
    private readonly ObjectIdentityProjector _idProjector = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectSetProvider"/> class.
    /// </summary>
    public InMemoryObjectSetProvider()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryObjectSetProvider"/> class.
    /// Uses an optional embedding provider for cosine similarity scoring.
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
    /// Initializes a new graph-aware instance of the <see cref="InMemoryObjectSetProvider"/> class.
    /// Delegates expression evaluation to <see cref="InMemoryExpressionEvaluator"/>, gaining
    /// TraverseLinkExpression and InterfaceNarrowExpression support.
    /// </summary>
    /// <param name="graph">
    /// The frozen ontology graph used for link traversal and interface narrowing.
    /// </param>
    public InMemoryObjectSetProvider(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        // DR-3: wire the evaluator to this provider's relate-store + id projector
        // so TraverseLink resolves by source instance, not target type.
        _evaluator = new InMemoryExpressionEvaluator(graph, GetRelations, _idProjector);
        _descriptorIndex = BuildDescriptorIndex(graph);
    }

    /// <summary>
    /// Initializes a new graph-aware instance with an optional embedding provider.
    /// </summary>
    /// <param name="graph">
    /// The frozen ontology graph used for link traversal and interface narrowing.
    /// </param>
    /// <param name="embeddingProvider">
    /// When provided, <see cref="ExecuteSimilarityAsync{T}"/> will use real cosine similarity
    /// against stored embeddings instead of keyword scoring.
    /// </param>
    public InMemoryObjectSetProvider(OntologyGraph graph, IEmbeddingProvider? embeddingProvider)
    {
        ArgumentNullException.ThrowIfNull(graph);
        // DR-3: wire the evaluator to this provider's relate-store + id projector
        // so TraverseLink resolves by source instance, not target type.
        _evaluator = new InMemoryExpressionEvaluator(graph, GetRelations, _idProjector);
        _descriptorIndex = BuildDescriptorIndex(graph);
        _embeddingProvider = embeddingProvider;
    }

    // Name-keyed descriptor index for relate-time validation (DR-2). The
    // evaluator constructed alongside this index already enforces globally
    // unique descriptor names and throws on a collision, so by the time this
    // runs the names are unique and last-write-wins is never reached.
    private static IReadOnlyDictionary<string, ObjectTypeDescriptor> BuildDescriptorIndex(OntologyGraph graph)
    {
        var index = new Dictionary<string, ObjectTypeDescriptor>(StringComparer.Ordinal);
        foreach (var objectType in graph.ObjectTypes)
        {
            index[objectType.Name] = objectType;
        }

        return index;
    }

    /// <summary>
    /// Seeds an item into the in-memory store with its associated searchable text content.
    /// </summary>
    /// <typeparam name="T">The domain object type.</typeparam>
    /// <param name="item">The item to seed.</param>
    /// <param name="searchableContent">The text content used for keyword-based similarity scoring.</param>
    /// <param name="descriptorName">
    /// Optional ontology descriptor name to partition the seeded item under.
    /// When omitted, defaults to <c>typeof(T).Name</c>, which matches the
    /// default root expression built by <c>ObjectSet&lt;T&gt;</c> with no
    /// explicit descriptor. Supply an explicit name when a single CLR type
    /// is registered under multiple descriptors (bug #31).
    /// </param>
    public void Seed<T>(T item, string searchableContent, string? descriptorName = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(searchableContent);

        var key = descriptorName ?? typeof(T).Name;
        _items.GetOrAdd(key, _ => new List<object>()).Add(item);
        _searchableContent.GetOrAdd(key, _ => new List<string>()).Add(searchableContent);

        // Store embedding (or empty placeholder) to maintain index alignment with _items
        var embedding = item is ISearchable searchable ? searchable.Embedding : [];
        _embeddings.GetOrAdd(key, _ => new List<float[]>()).Add(embedding);
    }

    // Both default overloads delegate into the explicit-name variants with
    // typeof(T).Name as the partition key. This keeps the two write paths in
    // sync (a single source of truth for partition layout, embedding alignment,
    // and searchable content extraction) and matches the dispatch convention
    // used on the read side: ObjectSet<T> built without an explicit descriptor
    // name produces a RootExpression whose RootObjectTypeName is typeof(T).Name.

    /// <inheritdoc />
    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class
        => StoreAsync(typeof(T).Name, item, ct);

    /// <inheritdoc />
    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class
        => StoreBatchAsync(typeof(T).Name, items, ct);

    /// <inheritdoc />
    public Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
        ArgumentNullException.ThrowIfNull(item);

        var searchableText = item.ToString() ?? string.Empty;

        _items.GetOrAdd(descriptorName, _ => new List<object>()).Add(item);
        _searchableContent.GetOrAdd(descriptorName, _ => new List<string>()).Add(searchableText);

        // Store embedding (or empty placeholder) to maintain index alignment with _items
        var embedding = item is ISearchable searchable ? searchable.Embedding : [];
        _embeddings.GetOrAdd(descriptorName, _ => new List<float[]>()).Add(embedding);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(descriptorName);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await StoreAsync(descriptorName, item, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task RelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);

        // Plain (unattributed) DR-2 relate: no association object backs the row.
        WriteRelationRow(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId, associationObjectId: null);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RelateAsync<TRel>(
        string srcDescriptor,
        string srcId,
        string linkName,
        string tgtDescriptor,
        string tgtId,
        string associationDescriptor,
        TRel association,
        CancellationToken ct = default)
        where TRel : class
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);
        ArgumentNullException.ThrowIfNull(associationDescriptor);
        ArgumentNullException.ThrowIfNull(association);

        // EAGER endpoint validation runs BEFORE the association object is stored,
        // so a failed attributed relate leaves neither a dangling association nor
        // a dangling row (mirrors the plain RelateAsync posture / DR-8).
        ValidateEndpointExists(srcDescriptor, srcId);
        ValidateEndpointExists(tgtDescriptor, tgtId);

        // Project the association's id via the DR-1 projector. When the provider
        // is graph-aware and the association descriptor is known, project through
        // its IdAccessor; this is the same single id-resolution path DR-1 defines
        // (no per-call reflection on the instance type, INV-8).
        var associationObjectId = ProjectAssociationId(associationDescriptor, association);

        // Store the association object under its descriptor partition.
        await StoreAsync(associationDescriptor, association, ct).ConfigureAwait(false);

        // Write the row referencing the stored association object.
        WriteRelationRow(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId, associationObjectId);
    }

    /// <summary>
    /// Shared relate-row write path for both the plain and attributed
    /// <c>RelateAsync</c> overloads. Performs eager endpoint validation, the
    /// self-loop policy check, and idempotent row insertion. The duplicate key
    /// is (TargetDescriptor, TargetId, AssociationObjectId): a plain relate
    /// (null association id) collapses to one row per endpoint pair, while an
    /// attributed relate can coexist with — or replace — a plain row for the
    /// same endpoints when it carries a distinct association object.
    /// </summary>
    private void WriteRelationRow(
        string srcDescriptor,
        string srcId,
        string linkName,
        string tgtDescriptor,
        string tgtId,
        string? associationObjectId)
    {
        // EAGER endpoint validation (DR-8): both endpoints must correspond to a
        // stored instance BEFORE any row is written, so a failed relate never
        // leaves a dangling row. This in-memory posture is the contract the
        // future Npgsql provider mirrors via foreign-key constraints.
        ValidateEndpointExists(srcDescriptor, srcId);
        ValidateEndpointExists(tgtDescriptor, tgtId);

        // Self-loop policy (DR-8): relating an instance to itself is rejected
        // unless the source descriptor's link declares AllowsSelfLoop. Runtime
        // check only; analyzer promotion is a later task.
        if (string.Equals(srcDescriptor, tgtDescriptor, StringComparison.Ordinal)
            && string.Equals(srcId, tgtId, StringComparison.Ordinal)
            && !SelfLoopAllowed(srcDescriptor, linkName))
        {
            throw new SelfLoopNotAllowedException(srcDescriptor, srcId, linkName);
        }

        var rows = _relations.GetOrAdd((srcDescriptor, srcId, linkName), _ => new List<RelationRow>());

        // Idempotent: relating the same (src, link, tgt, associationObjectId)
        // twice yields one row. The DR-4 AssociationObjectId participates in the
        // duplicate key so an attributed relate is distinguishable from a plain
        // one over the same endpoint pair.
        var alreadyRelated = rows.Any(r =>
            r.TargetDescriptor == tgtDescriptor
            && r.TargetId == tgtId
            && r.AssociationObjectId == associationObjectId);
        if (!alreadyRelated)
        {
            rows.Add(new RelationRow(tgtDescriptor, tgtId, associationObjectId));
        }
    }

    /// <summary>
    /// Projects an association instance to its id through the DR-1
    /// <see cref="ObjectIdentityProjector"/> using the named descriptor's
    /// <see cref="ObjectTypeDescriptor.IdAccessor"/>. Throws when the provider
    /// is graph-aware but the descriptor is unknown — an attributed relate
    /// against an undeclared association descriptor is a caller error, not a
    /// silent no-op.
    /// </summary>
    private string ProjectAssociationId(string associationDescriptor, object association)
    {
        if (_descriptorIndex is null)
        {
            // Graph-less legacy mode: no descriptors, so fall back to the
            // instance's own ToString() as the stable id (mirrors the seed-time
            // posture for graph-less providers). DR-1 projection is unavailable.
            return association.ToString() ?? string.Empty;
        }

        if (!_descriptorIndex.TryGetValue(associationDescriptor, out var descriptor))
        {
            throw new RelationEndpointNotFoundException(associationDescriptor, "<association>");
        }

        return _idProjector.ProjectId(descriptor, association);
    }

    /// <inheritdoc />
    public Task UnrelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(srcDescriptor);
        ArgumentNullException.ThrowIfNull(srcId);
        ArgumentNullException.ThrowIfNull(linkName);
        ArgumentNullException.ThrowIfNull(tgtDescriptor);
        ArgumentNullException.ThrowIfNull(tgtId);

        if (_relations.TryGetValue((srcDescriptor, srcId, linkName), out var rows))
        {
            rows.RemoveAll(r => r.TargetDescriptor == tgtDescriptor && r.TargetId == tgtId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the materialized relation rows for a given relation triple in a
    /// deterministic, ordinal-by-<see cref="RelationRow.TargetId"/> order.
    /// </summary>
    /// <remarks>
    /// INV-7: the returned list is a freshly-sorted snapshot — it never exposes
    /// the relate-store's insertion order or raw
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
    /// enumeration order. DR-3 traversal consumes this accessor from the same
    /// assembly.
    /// </remarks>
    /// <param name="srcDescriptor">Descriptor name of the source endpoint.</param>
    /// <param name="srcId">Projected id of the source instance.</param>
    /// <param name="linkName">Name of the link.</param>
    /// <returns>Relation rows ordered by <see cref="RelationRow.TargetId"/>.</returns>
    internal IReadOnlyList<RelationRow> GetRelations(string srcDescriptor, string srcId, string linkName)
    {
        if (!_relations.TryGetValue((srcDescriptor, srcId, linkName), out var rows))
        {
            return [];
        }

        return rows
            .OrderBy(r => r.TargetId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Eager endpoint check (DR-8): throws <see cref="RelationEndpointNotFoundException"/>
    /// unless some instance stored under <paramref name="descriptorName"/> projects
    /// (via the descriptor's reflection-free <see cref="ObjectTypeDescriptor.IdAccessor"/>)
    /// to <paramref name="id"/>.
    /// </summary>
    private void ValidateEndpointExists(string descriptorName, string id)
    {
        // A graph-less provider carries no descriptors (and thus no IdAccessor),
        // so there is nothing to project against; eager validation is a no-op
        // for that legacy seeding mode.
        if (_descriptorIndex is null)
        {
            return;
        }

        if (!_descriptorIndex.TryGetValue(descriptorName, out var descriptor))
        {
            throw new RelationEndpointNotFoundException(descriptorName, id);
        }

        var stored = _items.TryGetValue(descriptorName, out var items) ? items : null;
        if (stored is not null)
        {
            foreach (var instance in stored)
            {
                if (string.Equals(_idProjector.ProjectId(descriptor, instance), id, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        throw new RelationEndpointNotFoundException(descriptorName, id);
    }

    /// <summary>
    /// Resolves whether the named link on <paramref name="srcDescriptor"/>
    /// permits self-loops (DR-8). Defaults to <c>false</c> — the safe posture —
    /// when no descriptor index is present (graph-less mode) or the link is not
    /// declared on the source descriptor.
    /// </summary>
    private bool SelfLoopAllowed(string srcDescriptor, string linkName)
    {
        if (_descriptorIndex is null
            || !_descriptorIndex.TryGetValue(srcDescriptor, out var descriptor))
        {
            return false;
        }

        var link = descriptor.Links.FirstOrDefault(l => l.Name == linkName);
        return link?.AllowsSelfLoop ?? false;
    }

    /// <inheritdoc />
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        var items = _evaluator is not null
            ? _evaluator.Evaluate<T>(expression, GetSeededItems)
            : ApplyExpressionLegacy(GetSeededItems<T>(expression.RootObjectTypeName), expression);

        var result = new ObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties);
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> StreamAsync<T>(
        ObjectSetExpression expression,
        [EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        var items = _evaluator is not null
            ? _evaluator.Evaluate<T>(expression, GetSeededItems)
            : ApplyExpressionLegacy(GetSeededItems<T>(expression.RootObjectTypeName), expression);

        foreach (var item in items)
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

        // Partition lookups route through the expression's declared descriptor
        // name so queries honor the same dispatch as ExecuteAsync / StreamAsync
        // (bug #31). The walk-to-root helper on ObjectSetExpression surfaces
        // the name from the root even when wrapped by filters, includes, etc.
        var partitionKey = expression.RootObjectTypeName;
        var items = GetSeededItems<T>(partitionKey);

        if (items.Count == 0)
        {
            return new ScoredObjectSetResult<T>(
                Array.Empty<T>(), 0, ObjectSetInclusion.Properties, Array.Empty<double>());
        }

        // Apply Source expression (filters, includes, etc.)
        items = _evaluator is not null
            ? _evaluator.Evaluate<T>(expression.Source, GetSeededItems)
            : ApplyExpressionLegacy(items, expression.Source);

        // When an embedding provider is available and non-placeholder embeddings exist, use cosine similarity
        if (_embeddingProvider is not null
            && _embeddings.TryGetValue(partitionKey, out var storedEmbeddings)
            && storedEmbeddings.Any(static e => e.Length > 0))
        {
            return await ExecuteCosineSimilarityAsync(items, storedEmbeddings, partitionKey, expression, ct).ConfigureAwait(false);
        }

        // Fall back to keyword scoring
        return ExecuteKeywordSimilarity(items, partitionKey, expression);
    }

    private async Task<ScoredObjectSetResult<T>> ExecuteCosineSimilarityAsync<T>(
        List<T> items,
        List<float[]> storedEmbeddings,
        string partitionKey,
        SimilarityExpression expression,
        CancellationToken ct) where T : class
    {
        // Honor QueryVector if provided, otherwise embed QueryText
        var queryEmbedding = expression.QueryVector
            ?? await _embeddingProvider!.EmbedAsync(expression.QueryText, ct).ConfigureAwait(false);

        // Build identity-based index map to avoid O(n^2) IndexOf lookups and duplicate-item misalignment
        var allItems = GetSeededItems<T>(partitionKey);
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
        string partitionKey,
        SimilarityExpression expression) where T : class
    {
        // Build identity-based index map to avoid O(n^2) IndexOf lookups and duplicate-item misalignment
        var allItems = GetSeededItems<T>(partitionKey);
        var allContent = GetSearchableContent(partitionKey);
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

    /// <summary>
    /// Item resolver delegate for <see cref="InMemoryExpressionEvaluator"/>.
    /// Returns raw (untyped) items from the internal partition by descriptor name.
    /// </summary>
    private IReadOnlyList<object> GetSeededItems(string descriptorName)
    {
        return _items.TryGetValue(descriptorName, out var items) ? items : [];
    }

    private List<T> GetSeededItems<T>(string partitionKey) where T : class
    {
        if (!_items.TryGetValue(partitionKey, out var items))
        {
            return new List<T>();
        }

        return items.Cast<T>().ToList();
    }

    private List<string> GetSearchableContent(string partitionKey)
    {
        if (!_searchableContent.TryGetValue(partitionKey, out var content))
        {
            return new List<string>();
        }

        return content;
    }

    private static List<T> ApplyExpressionLegacy<T>(List<T> items, ObjectSetExpression expression) where T : class
    {
        if (expression is FilterExpression filter)
        {
            // Recursively apply the source expression first
            var filtered = ApplyExpressionLegacy(items, filter.Source);

            var compiled = filter.Predicate.Compile();
            if (compiled is not Func<T, bool> func)
            {
                throw new InvalidOperationException(
                    $"Filter predicate type '{compiled.GetType()}' is not compatible with Func<{typeof(T).Name}, bool>.");
            }

            return filtered.Where(func).ToList();
        }

        if (expression is IncludeExpression include)
        {
            return ApplyExpressionLegacy(items, include.Source);
        }

        // RootExpression or other — return all items (base case)
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
