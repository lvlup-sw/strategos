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

    /// <inheritdoc />
    /// <remarks>
    /// DR-7/DR-8 (Ontology Edge Foundation): materializes a pure-link relation
    /// into the T8 junction table. Endpoints are addressed by their projected
    /// BUSINESS id (the in-memory relate-store contract), so each endpoint's
    /// surrogate <c>id uuid</c> is resolved via a <c>data->>'key'</c> subquery
    /// against its object table. Validation is EAGER: both endpoints are probed
    /// via <c>SELECT EXISTS</c> BEFORE the insert, so a missing endpoint surfaces
    /// a typed <see cref="RelationEndpointNotFoundException"/> and no junction row
    /// is written (mirroring the in-memory provider / DR-8). Idempotency rides
    /// the T8 <c>UNIQUE(source_id, target_id)</c> via <c>ON CONFLICT DO NOTHING</c>.
    /// INV-2: raw Npgsql only.
    /// <para>
    /// TOCTOU (review L): the two eager <c>SELECT EXISTS</c> probes and the
    /// <c>INSERT … ON CONFLICT</c> are separate commands on no shared transaction,
    /// so a concurrent delete of an endpoint between the probe and the insert opens
    /// a check-to-use window. The typed
    /// <see cref="ObjectSets.RelationEndpointNotFoundException"/> guarantee is
    /// therefore BEST-EFFORT under concurrency: a racing delete can turn an
    /// expected typed error into the insert resolving zero endpoint rows (a silent
    /// no-op) instead. It is never CORRUPTING — the insert's endpoint-resolving
    /// subquery plus the junction's foreign keys reject any row whose endpoints no
    /// longer exist, so a dangling junction row can never be written, and
    /// <c>ON CONFLICT DO NOTHING</c> keeps re-relates idempotent. Wrapping the
    /// probe + insert in one transaction (or folding the existence check into the
    /// insert's <c>RETURNING</c>) would close the window; it is intentionally
    /// deferred — not restructured here — to keep the change minimal.
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

        // EAGER endpoint validation (DR-8): probe BOTH endpoints before writing
        // any row, so a failed relate never leaves a dangling junction row.
        await ValidateEndpointExistsAsync(srcDescriptor, source, srcId, "@srcId", ct).ConfigureAwait(false);
        await ValidateEndpointExistsAsync(tgtDescriptor, target, tgtId, "@tgtId", ct).ConfigureAwait(false);

        // SELF-LOOP policy (DR-8 parity, t14): relating an instance to itself along
        // a link whose AllowsSelfLoop is false is REFUSED with the SAME typed error
        // the in-memory provider raises — never silently written. Checked AFTER
        // eager endpoint validation so the ordering matches the in-memory
        // WriteRelationRow guard (a missing endpoint still surfaces first).
        ThrowIfDisallowedSelfLoop(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId);

        // DR-11b: route to the per-(link, target-descriptor) junction table. A
        // polymorphic link writes account_holdings_stock / _bond; a monomorphic
        // link keeps account_written_by (DR-7..DR-10 lockstep).
        var junctionTableName = SqlGenerator.JunctionTableNameFor(
            ResolveRelateJunction(_graph!, srcDescriptor, linkName, tgtDescriptor));
        var sql = SqlGenerator.BuildRelateInsertSql(
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
        if (string.Equals(srcDescriptor, tgtDescriptor, StringComparison.Ordinal)
            && string.Equals(srcId, tgtId, StringComparison.Ordinal)
            && !SelfLoopAllowed(srcDescriptor, linkName))
        {
            throw new SelfLoopNotAllowedException(srcDescriptor, srcId, linkName);
        }
    }

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
        var target = new RelateEndpoint(plan.TargetTable, plan.TargetKeyProperty);

        // EAGER endpoint validation (DR-8): probe BOTH endpoints before writing
        // the association row, so a failed attributed relate never leaves a
        // dangling association (mirrors the plain relate posture / the in-memory
        // provider).
        await ValidateEndpointExistsAsync(srcDescriptor, source, srcId, "@srcId", ct).ConfigureAwait(false);
        await ValidateEndpointExistsAsync(tgtDescriptor, target, tgtId, "@tgtId", ct).ConfigureAwait(false);

        // SELF-LOOP policy (DR-8 parity, t14): an attributed relate routes through
        // the SAME self-loop guard as the plain path (the in-memory provider
        // enforces both via WriteRelationRow), so a disallowed (x, link, x) is
        // refused with the same typed error before any association row is written.
        ThrowIfDisallowedSelfLoop(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId);

        var sql = SqlGenerator.BuildAssociationRelateInsertSql(
            _options.Schema,
            plan.AssociationTable,
            plan.SourceColumn,
            plan.SourceTable,
            plan.SourceKeyProperty,
            plan.TargetColumn,
            plan.TargetTable,
            plan.TargetKeyProperty);

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(association));
        cmd.Parameters.AddWithValue("srcId", srcId);
        cmd.Parameters.AddWithValue("tgtId", tgtId);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            _options.Schema,
            tableName,
            _embeddingProvider.Dimensions,
            _options.IndexType);

        await using var cmd = _dataSource.CreateCommand(ddl);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
    /// names a registered INTERFACE resolves POLYMORPHICALLY to that interface's
    /// implementor descriptors (one per implementor); any other link resolves
    /// MONOMORPHICALLY to the single descriptor named by
    /// <see cref="ResolveHopTargetDescriptorName"/> (TargetTypeName →
    /// TargetSymbolKey). The result drives both the relate write-routing and the
    /// traversal read fan-out: a count &gt; 1 means the link is polymorphic and its
    /// junction tables are disambiguated per target descriptor (Posture 2).
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
}
