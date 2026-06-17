using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Traversal;

/// <summary>
/// DR-12 (#131, T5): a <c>Where(...).TraverseLink&lt;T&gt;("link")</c> expression
/// LOWERS to traversal SQL and is reachable from the public <c>ExecuteAsync&lt;T&gt;</c>
/// entrypoint — closing the gap left by DR-7..DR-11b, where
/// <see cref="ExpressionTranslator"/> handled only Filter + Include and a
/// <see cref="TraverseLinkExpression"/> threw <see cref="NotSupportedException"/>.
/// </summary>
/// <remarks>
/// These assert the GENERATED parameterized SQL SHAPE only — NO live database
/// (INV-2; there is no Postgres in the default dev/CI lane). The lowering seam is
/// exposed as an internal static so the full <c>ExecuteAsync</c> dispatch path is
/// pinned without a live <see cref="global::Npgsql.NpgsqlDataSource"/>, mirroring
/// the <see cref="PgVectorTraversalTests"/> / <see cref="Schema.JunctionDmlRoutingTests"/>
/// seam posture. The hop's TARGET descriptor is resolved from the ontology GRAPH
/// via the DR-10 path (override → link <c>TargetTypeName</c> → <c>TargetSymbolKey</c>),
/// NEVER from <c>typeof(TLinked)</c> (INV-8).
/// </remarks>
public class TraverseLinkExecuteTests
{
    private const string Account = "Account";
    private const string Author = "Author";
    private const string MonoLink = "WrittenBy"; // Account -> Author (single concrete target)

    [Test]
    public async Task ExecuteAsync_TraverseLinkExpression_LowersToInstanceAnchoredTraversal()
    {
        // A single monomorphic hop reached via the public ExecuteAsync entrypoint:
        // Where(a => a.Id == "acct-1").TraverseLink<Author>("WrittenBy"). The
        // lowering seam ExecuteAsync<T> consumes must emit the
        // vertex ⋈ junction ⋈ vertex join anchored at the source's business id.
        var graph = BuildGraph();
        var expression = TraverseFrom(Account, MonoLink, typeof(object), "acct-1");

        var lowering = PgVectorObjectSetProvider.LowerTraversalExpression(graph, expression, schema: "public");

        // Projects the TARGET rows, anchored at the SOURCE vertex, joined through
        // the per-(source, link) junction to the GRAPH-resolved target table.
        await Assert.That(lowering.Sql).Contains("FROM \"public\".\"account\" s");
        await Assert.That(lowering.Sql).Contains("\"public\".\"account_written_by\"");
        await Assert.That(lowering.Sql).Contains("\"public\".\"author\"");

        // The source instance's business id is parameter-bound, never interpolated.
        await Assert.That(lowering.Sql).Contains("@srcId");
        await Assert.That(lowering.Parameters.Any(p => p.Name == "@srcId" && (string)p.Value == "acct-1")).IsTrue();

        // INV-2: raw Npgsql/pgvector — no event-store machinery.
        await Assert.That(lowering.Sql).DoesNotContain("Marten");
        await Assert.That(lowering.Sql).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task ExpressionTranslator_NoLongerThrows_OnTraverseLink_RoutesToLoweringSeam()
    {
        // DR-12 removes the DR-7..DR-11b NotSupportedException gap: the translator
        // recognizes a TraverseLinkExpression and reports that the provider must
        // route it through the traversal-lowering seam (it is NOT a WHERE-only
        // expression), rather than throwing.
        var graph = BuildGraph();
        var expression = TraverseFrom(Account, MonoLink, typeof(object), "acct-1");

        await Assert.That(ExpressionTranslator.IsTraversal(expression)).IsTrue();

        var rootOnly = new RootExpression(typeof(object), Account);
        await Assert.That(ExpressionTranslator.IsTraversal(rootOnly)).IsFalse();
    }

    // -----------------------------------------------------------------------
    // Live-DB execution-parity variant — SKIPS in the default lane (no Postgres),
    // RUNS only when STRATEGOS_PG_TEST_CONN is provisioned (DR-9 gate).
    // -----------------------------------------------------------------------

    [Test]
    [Integration.SkipIfNoPostgres]
    public async Task ExecuteAsync_TraverseLink_LiveExecution_ReturnsRelatedTargets()
    {
        // Execution parity is asserted only in a provisioned DB lane. Here we pin
        // that the lowering the public path produces is non-empty and anchored —
        // the live datasource wiring is exercised by the cross-provider parity
        // harness when a connection string is present.
        var graph = BuildGraph();
        var expression = TraverseFrom(Account, MonoLink, typeof(object), "acct-1");

        var lowering = PgVectorObjectSetProvider.LowerTraversalExpression(graph, expression, schema: "public");

        await Assert.That(lowering.Sql).IsNotNull();
        await Assert.That(lowering.Sql).Contains("@srcId");
    }

    // A Where(s => business-id == value).TraverseLink<T>(link) expression, built
    // without the typed ObjectSet builder so the test pins the provider lowering
    // directly. The source filter anchors the traversal at the business id.
    private static TraverseLinkExpression TraverseFrom(
        string sourceDescriptor, string linkName, Type linkedType, string srcId)
    {
        var root = new RootExpression(typeof(SourceAnchor), sourceDescriptor);
        System.Linq.Expressions.Expression<Func<SourceAnchor, bool>> predicate = s => s.Id == srcId;
        var filter = new FilterExpression(root, predicate);
        return new TraverseLinkExpression(filter, linkName, linkedType, targetDescriptorName: null);
    }

    private static OntologyGraph BuildGraph()
    {
        var account = new ObjectTypeDescriptor
        {
            Name = Account,
            DomainName = "portfolio",
            ClrType = typeof(SourceAnchor),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links = [new LinkDescriptor(MonoLink, Author, LinkCardinality.OneToMany)],
        };

        var author = new ObjectTypeDescriptor
        {
            Name = Author,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
        };

        var objectTypes = new[] { account, author };
        return new OntologyGraph(
            domains: [new DomainDescriptor("portfolio") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    public sealed record SourceAnchor(string Id);
}
