using Strategos.Ontology.Builder;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// Unit tests for <see cref="PgVectorObjectSetProvider"/>'s read-path dispatch.
///
/// The provider's dispatch step — resolving a table name from an incoming
/// <see cref="ObjectSetExpression"/> — is exposed as the internal static helper
/// <c>PgVectorObjectSetProvider.ResolveTableName</c> so it can be verified
/// without needing a live <see cref="Npgsql.NpgsqlDataSource"/>. The production
/// <c>ExecuteSimilarityAsync</c>, <c>ExecuteAsync</c>, and <c>StreamAsync</c>
/// methods all delegate to this helper so the assertions below pin the
/// behavior of the full code path.
/// </summary>
public class PgVectorObjectSetProviderTests
{
    [Test]
    public async Task ExecuteSimilarityAsync_UsesDescriptorNameFromExpression_NotTypeofTName()
    {
        // Arrange — build a SimilarityExpression whose root declares an explicit
        // descriptor name ("trading_documents") that differs from typeof(T).Name
        // ("SemanticDocument" → "semantic_document"). Track A requires the root
        // expression to carry the descriptor name; the provider must honor it.
        var root = new RootExpression(typeof(SemanticDocument), "trading_documents");
        var similarity = new SimilarityExpression(
            root,
            queryText: "equity volatility",
            topK: 5,
            minRelevance: 0.0,
            metric: DistanceMetric.Cosine,
            queryVector: new float[] { 0.1f, 0.2f, 0.3f });

        // Act — invoke the provider's resolution step directly. This is the
        // single table-name lookup the production ExecuteSimilarityAsync uses
        // to build the FROM clause via SqlGenerator.BuildSimilarityQuery.
        var tableName = PgVectorObjectSetProvider.ResolveTableName(similarity);
        var sql = SqlGenerator.BuildSimilarityQuery("public", tableName, similarity.Metric);

        // Assert — FROM clause must reference "trading_documents" (the declared
        // descriptor), NOT "semantic_document" (typeof(T).Name).
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(sql).Contains("\"public\".\"trading_documents\"");
        await Assert.That(sql).DoesNotContain("\"semantic_document\"");
    }

    [Test]
    public async Task ExecuteAsync_UsesDescriptorNameFromExpression_NotTypeofTName()
    {
        // Arrange — a plain RootExpression with an explicit descriptor name.
        // The non-similarity read path (ExecuteAsync) must honour the
        // declared name the same way ExecuteSimilarityAsync does.
        var root = new RootExpression(typeof(SemanticDocument), "trading_documents");

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableName(root);
        var sql = SqlGenerator.BuildSelectQuery("public", tableName);

        // Assert
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(sql).Contains("\"public\".\"trading_documents\"");
        await Assert.That(sql).DoesNotContain("\"semantic_document\"");
    }

    [Test]
    public async Task ExecuteAsync_FilterExpressionOverRoot_WalksToRootDescriptorName()
    {
        // Arrange — a FilterExpression wrapping a RootExpression with a
        // non-default descriptor name. The walk-to-root helper on
        // ObjectSetExpression (Track A2) must surface the root's declared
        // name, and the provider's dispatch must reach it.
        var root = new RootExpression(typeof(SemanticDocument), "knowledge_documents");
        System.Linq.Expressions.Expression<Func<SemanticDocument, bool>> pred = d => d.Id != Guid.Empty;
        var filter = new FilterExpression(root, pred);

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableName(filter);
        var sql = SqlGenerator.BuildSelectQuery("public", tableName);

        // Assert
        await Assert.That(tableName).IsEqualTo("knowledge_documents");
        await Assert.That(sql).Contains("\"public\".\"knowledge_documents\"");
    }

    [Test]
    public async Task StreamAsync_UsesDescriptorNameFromExpression_NotTypeofTName()
    {
        // Arrange — StreamAsync shares the non-similarity read-path dispatch
        // with ExecuteAsync. Pin the behavior explicitly so a future refactor
        // that diverges the two code paths cannot silently regress streaming.
        var root = new RootExpression(typeof(SemanticDocument), "streaming_documents");

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableName(root);
        var sql = SqlGenerator.BuildSelectQuery("public", tableName);

        // Assert
        await Assert.That(tableName).IsEqualTo("streaming_documents");
        await Assert.That(sql).Contains("\"public\".\"streaming_documents\"");
        await Assert.That(sql).DoesNotContain("\"semantic_document\"");
    }

    // ---------------------------------------------------------------------
    // Track F3 — explicit-descriptor-name write-path dispatch tests.
    //
    // The write-path equivalent of ResolveTableName is exposed as the
    // internal static helper PgVectorObjectSetProvider.ResolveTableNameForDescriptor,
    // which the explicit-name StoreAsync/StoreBatchAsync overloads delegate to.
    // Asserting against the helper plus the SQL/COPY strings produced by
    // SqlGenerator pins the full code path without needing a live
    // NpgsqlDataSource.
    // ---------------------------------------------------------------------

    [Test]
    public async Task StoreAsync_ExplicitName_WritesToNamedTable()
    {
        // Arrange — descriptor name "trading_documents" deliberately differs
        // from typeof(SemanticDocument).Name so we can detect a regression to
        // the typeof(T).Name path.
        const string descriptorName = "trading_documents";

        // Act — invoke the dispatch helper the explicit-name StoreAsync<T>
        // overload uses. Then build the INSERT SQL the production code path
        // emits via SqlGenerator.BuildInsertSql.
        var tableName = PgVectorObjectSetProvider.ResolveTableNameForDescriptor(descriptorName);
        var insertSql = SqlGenerator.BuildInsertSql("public", tableName, hasEmbedding: false);
        var insertSqlWithEmbedding = SqlGenerator.BuildInsertSql("public", tableName, hasEmbedding: true);

        // Assert — table name resolves verbatim (already snake_case) and the
        // generated INSERT statements target "trading_documents", NOT
        // "semantic_document".
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(TypeMapper.ToSnakeCase("trading_documents")).IsEqualTo("trading_documents");
        await Assert.That(insertSql).Contains("\"public\".\"trading_documents\"");
        await Assert.That(insertSql).DoesNotContain("\"semantic_document\"");
        await Assert.That(insertSqlWithEmbedding).Contains("\"public\".\"trading_documents\"");
        await Assert.That(insertSqlWithEmbedding).Contains("embedding");
    }

    [Test]
    public async Task StoreBatchAsync_ExplicitName_UsesCopyToNamedTable()
    {
        // Arrange — same dispatch step. The batch path uses the resolved
        // table name to build a fully-qualified COPY target identifier.
        const string descriptorName = "trading_documents";

        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableNameForDescriptor(descriptorName);
        var qualifiedTable =
            $"{SqlGenerator.QuoteIdentifier("public")}.{SqlGenerator.QuoteIdentifier(tableName)}";
        var copyColumnsNoEmbedding = $"{qualifiedTable} (id, data)";
        var copyColumnsWithEmbedding = $"{qualifiedTable} (id, data, embedding)";
        var copyCommandNoEmbedding = $"COPY {copyColumnsNoEmbedding} FROM STDIN (FORMAT BINARY)";
        var copyCommandWithEmbedding = $"COPY {copyColumnsWithEmbedding} FROM STDIN (FORMAT BINARY)";

        // Assert — COPY target table matches the explicit descriptor, not
        // typeof(T).Name. The qualified identifier is the exact string
        // PgVectorObjectSetProvider.StoreBatchAsync passes to
        // NpgsqlConnection.BeginBinaryImportAsync.
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(qualifiedTable).IsEqualTo("\"public\".\"trading_documents\"");
        await Assert.That(copyCommandNoEmbedding)
            .IsEqualTo("COPY \"public\".\"trading_documents\" (id, data) FROM STDIN (FORMAT BINARY)");
        await Assert.That(copyCommandWithEmbedding)
            .IsEqualTo("COPY \"public\".\"trading_documents\" (id, data, embedding) FROM STDIN (FORMAT BINARY)");
        await Assert.That(copyCommandNoEmbedding).DoesNotContain("semantic_document");
    }

    [Test]
    public async Task WriteOverloads_DefaultResolver_FallsBackToTypeofTName_WhenGraphAbsent()
    {
        // Post-F4/E5: the default StoreAsync<T>(T) / StoreBatchAsync<T>
        // overloads dispatch via ResolveTableNameForDefaultOverload<T>,
        // which falls back to typeof(T).Name → snake_case when no graph
        // is in scope. Pins the back-compat fallback so direct
        // instantiation without DI wiring keeps working.
        var defaultTableName = PgVectorObjectSetProvider
            .ResolveTableNameForDefaultOverload<SemanticDocument>(graph: null);

        await Assert.That(defaultTableName).IsEqualTo("semantic_document");
        var defaultInsertSql = SqlGenerator.BuildInsertSql("public", defaultTableName, hasEmbedding: false);
        await Assert.That(defaultInsertSql).Contains("\"public\".\"semantic_document\"");

        // Sanity — the explicit-name dispatch helper produces a different
        // table name from the same CLR type when given a non-matching
        // descriptor, proving the two paths are independently resolvable.
        var explicitTableName = PgVectorObjectSetProvider.ResolveTableNameForDescriptor("trading_documents");
        await Assert.That(explicitTableName).IsNotEqualTo(defaultTableName);
    }

    // ---------------------------------------------------------------------
    // Track F4 — graph-backed default-overload write-path dispatch tests.
    //
    // The default StoreAsync<T>(T) / StoreBatchAsync<T>(IReadOnlyList<T>)
    // overloads must resolve the target table via the ontology graph when
    // one is available, honouring single-registration names, throwing with
    // a diagnostic for multi-registered types, and falling back to
    // typeof(T).Name only when no graph is in scope (e.g. direct unit-test
    // instantiation without DI wiring).
    //
    // The dispatch step is exposed as the internal static helper
    // PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<T>(graph)
    // so the assertions below pin the full code path without needing a
    // live NpgsqlDataSource.
    // ---------------------------------------------------------------------

    [Test]
    public async Task StoreAsync_DefaultOverload_ResolvesViaGraph_SingleRegistration()
    {
        // Arrange — one Object<F4Foo>("foo_table", ...) registration. The
        // graph's reverse index should map typeof(F4Foo) → ["foo_table"].
        var graph = new OntologyGraphBuilder()
            .AddDomain<F4SingleRegistrationOntology>()
            .Build();

        // Act — default-overload dispatch helper with the graph in scope.
        var tableName = PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph);

        // Assert — the descriptor name "foo_table" is honoured, NOT the
        // typeof(F4Foo).Name → "f4_foo" fallback path.
        await Assert.That(tableName).IsEqualTo("foo_table");
        await Assert.That(tableName).IsNotEqualTo("f4_foo");
    }

    [Test]
    public async Task StoreAsync_DefaultOverload_WithMultiRegistration_ThrowsWithDiagnostic()
    {
        // Arrange — two registrations of the same CLR type under different
        // descriptor names. The default overload CANNOT safely pick one
        // automatically and must throw with a diagnostic pointing the
        // caller at the explicit-name overload.
        var graph = new OntologyGraphBuilder()
            .AddDomain<F4MultiRegistrationOntology>()
            .Build();

        // Act + Assert — the dispatch helper throws InvalidOperationException
        // naming the type, both descriptor names, and the explicit-name
        // overload as the remediation.
        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph))
            .ThrowsException()
            .WithMessageContaining(typeof(F4Foo).FullName!);

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph))
            .ThrowsException()
            .WithMessageContaining("'a'");

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph))
            .ThrowsException()
            .WithMessageContaining("'b'");

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph))
            .ThrowsException()
            .WithMessageContaining("StoreAsync");
    }

    [Test]
    public async Task StoreAsync_DefaultOverload_FallsBackToTypeofTName_WhenGraphAbsent()
    {
        // Arrange — no graph available (direct unit-test call). The helper
        // must fall back to typeof(T).Name → snake_case so existing
        // provider constructions without a graph continue to work.
        // Act
        var tableName = PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F4Foo>(graph: null);

        // Assert — snake_case form of typeof(F4Foo).Name ("F4Foo" → "f4_foo").
        await Assert.That(tableName).IsEqualTo(TypeMapper.ToSnakeCase(nameof(F4Foo)));
    }

    // ---------------------------------------------------------------------
    // Task F2 (#33 Finding 2) — strict graph check: unregistered type must
    // throw when a graph is present rather than silently falling back to
    // typeof(T).Name. The null-graph (test-mode) fallback is preserved.
    // ---------------------------------------------------------------------

    [Test]
    public async Task ResolveTableNameForDefaultOverload_GraphPresentTypeUnregistered_Throws()
    {
        // Arrange — a graph that contains no registration for F2Unregistered.
        // The graph IS present, so the silent typeof(T).Name fallback is
        // a misconfiguration, not a valid test-mode scenario.
        var graph = new OntologyGraphBuilder()
            .AddDomain<F2RegisteredOnlyOntology>()
            .Build();

        // Act + Assert — must throw InvalidOperationException naming the
        // unregistered type's full name and pointing at Object<T>(...).
        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F2Unregistered>(graph))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F2Unregistered>(graph))
            .ThrowsException()
            .WithMessageContaining(typeof(F2Unregistered).FullName!);

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<F2Unregistered>(graph))
            .ThrowsException()
            .WithMessageContaining("Object<T>");
    }

    [Test]
    public async Task ResolveTableNameForDefaultOverload_GraphAbsent_FallsBackToCamelCase()
    {
        // Arrange — no graph (graph is null). Test-mode instantiation path
        // must continue to fall back to typeof(T).Name → snake_case so
        // direct unit-test construction without DI wiring keeps working.
        var tableName = PgVectorObjectSetProvider
            .ResolveTableNameForDefaultOverload<F2Unregistered>(graph: null);

        // Assert — snake_case of "F2Unregistered".
        await Assert.That(tableName).IsEqualTo(TypeMapper.ToSnakeCase(nameof(F2Unregistered)));
    }

    [Test]
    public async Task ResolveTableNameForDefaultOverload_GraphPresentTypeRegistered_ReturnsRegisteredName()
    {
        // Arrange — a graph with a single Object<F2Registered>("f2_registered_table")
        // entry. The positive-control: registered type must resolve to the
        // declared descriptor name, not typeof(T).Name.
        var graph = new OntologyGraphBuilder()
            .AddDomain<F2RegisteredOnlyOntology>()
            .Build();

        var tableName = PgVectorObjectSetProvider
            .ResolveTableNameForDefaultOverload<F2Registered>(graph);

        await Assert.That(tableName).IsEqualTo("f2_registered_table");
        await Assert.That(tableName).IsNotEqualTo(TypeMapper.ToSnakeCase(nameof(F2Registered)));
    }

    // ---------------------------------------------------------------------
    // Track F5 — EnsureSchemaAsync descriptor-name overload tests.
    //
    // EnsureSchemaAsync<T>(string? descriptorName = null, ct) must resolve
    // its target table from the explicit descriptor name when supplied and
    // otherwise delegate to the same graph-backed default-overload
    // resolution used by the write path. Pinned via the internal static
    // seam PgVectorObjectSetProvider.ResolveEnsureSchemaTableName so the
    // assertions reach the DDL-building step without needing a live
    // NpgsqlDataSource.
    // ---------------------------------------------------------------------

    [Test]
    public async Task EnsureSchemaAsync_WithExplicitName_CreatesNamedTable()
    {
        // Arrange — an explicit descriptor name. Even without a graph in
        // scope the caller's choice wins.
        // Act
        var tableName = PgVectorObjectSetProvider
            .ResolveEnsureSchemaTableName<SemanticDocument>("trading_documents", graph: null);
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            tableName,
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat);

        // Assert — DDL creates "trading_documents", NOT "semantic_document".
        await Assert.That(tableName).IsEqualTo("trading_documents");
        await Assert.That(ddl)
            .Contains("CREATE TABLE IF NOT EXISTS \"public\".\"trading_documents\"");
        await Assert.That(ddl).DoesNotContain("\"semantic_document\"");
    }

    [Test]
    public async Task EnsureSchemaAsync_WithoutName_FallsBackToDefaultResolution()
    {
        // Arrange — a graph containing a single Object<F4Foo>("foo_table")
        // registration. Calling EnsureSchemaAsync<F4Foo>() with no
        // descriptor name must route through the default-overload
        // resolution (F4) and honour the descriptor name "foo_table".
        var graph = new OntologyGraphBuilder()
            .AddDomain<F4SingleRegistrationOntology>()
            .Build();

        // Act
        var tableName = PgVectorObjectSetProvider
            .ResolveEnsureSchemaTableName<F4Foo>(descriptorName: null, graph);
        var ddl = SqlGenerator.BuildSchemaCreationDdl(
            "public",
            tableName,
            vectorDimensions: 1536,
            indexType: PgVectorIndexType.IvfFlat);

        // Assert — DDL creates "foo_table", NOT "f4_foo".
        await Assert.That(tableName).IsEqualTo("foo_table");
        await Assert.That(ddl)
            .Contains("CREATE TABLE IF NOT EXISTS \"public\".\"foo_table\"");
        await Assert.That(ddl).DoesNotContain("\"f4_foo\"");
    }

    /// <summary>
    /// Test CLR type whose <c>Name</c> deliberately differs from any expected
    /// descriptor name, so we can detect if the provider is still reaching for
    /// <c>typeof(T).Name</c> instead of <c>expression.RootObjectTypeName</c>.
    /// </summary>
    private sealed class SemanticDocument
    {
        public Guid Id { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}

// ---------------------------------------------------------------------------
// Track F4 test fixtures — top-level so OntologyGraphBuilder.AddDomain<T>()
// can instantiate them (requires a public parameterless constructor).
// ---------------------------------------------------------------------------

public sealed class F4Foo
{
    public string Id { get; set; } = string.Empty;
}

public class F4SingleRegistrationOntology : DomainOntology
{
    public override string DomainName => "f4-single";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<F4Foo>("foo_table", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}

public class F4MultiRegistrationOntology : DomainOntology
{
    public override string DomainName => "f4-multi";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<F4Foo>("a", obj =>
        {
            obj.Key(f => f.Id);
        });

        builder.Object<F4Foo>("b", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}

// ---------------------------------------------------------------------------
// Task F2 test fixtures — top-level so OntologyGraphBuilder.AddDomain<T>()
// can instantiate them (requires a public parameterless constructor).
// ---------------------------------------------------------------------------

public sealed class F2Registered
{
    public string Id { get; set; } = string.Empty;
}

public sealed class F2Unregistered
{
    public string Id { get; set; } = string.Empty;
}

public class F2RegisteredOnlyOntology : DomainOntology
{
    public override string DomainName => "f2-registered-only";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<F2Registered>("f2_registered_table", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}
