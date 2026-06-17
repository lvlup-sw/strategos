using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Traversal;

/// <summary>
/// DR-12 (#131, T6/T7): the DEPTH-TIERED traversal lowering. A chain within the
/// join-collapse depth budget (<see cref="SqlGenerator.JoinChainDepthBudget"/>,
/// all monomorphic) lowers to a flat <c>vertex ⋈ junction ⋈ vertex ⋈ …</c> JOIN
/// CHAIN the Postgres planner reorders freely; a deeper chain — or any plan whose
/// step fans out polymorphically (a polymorphic hop counts as fan-out against the
/// budget) — lowers to a RECURSIVE CTE.
/// </summary>
/// <remarks>
/// GENERATED-SQL SHAPE assertions only — no live database (INV-2). The chain is
/// resolved through <c>PgVectorObjectSetProvider.BuildTraversalPlan</c> and
/// lowered through <c>SqlGenerator.BuildDepthTieredTraversalSql</c>, exactly the
/// path the public <c>ExecuteAsync&lt;T&gt;</c> walks (DR-12 T5). Hop targets are
/// resolved from the GRAPH (DR-10), never <c>typeof</c> (INV-8).
/// </remarks>
public class DepthTierSqlShapeTests
{
    [Test]
    public async Task Lower_TwoHopMonomorphic_EmitsVertexJunctionVertexJoinChain()
    {
        // Account --writtenBy--> Author --employedBy--> Firm: a two-hop monomorphic
        // chain (within the budget of 3) lowers to a FLAT join chain anchored at
        // the account's business id, projecting the FINAL (Firm) target rows.
        var graph = TraversalChainGraphs.AccountAuthorFirm();
        var expression = TraversalChainGraphs.ChainFrom(
            "Account", "acct-1", ("writtenBy", typeof(object)), ("employedBy", typeof(object)));

        var sql = SqlGenerator.BuildDepthTieredTraversalSql(
            "public", PgVectorObjectSetProvider.BuildTraversalPlan(graph, expression));

        // Flat join chain — NOT a recursive CTE — within the budget.
        await Assert.That(sql).DoesNotContain("WITH RECURSIVE");

        // Anchored at the source vertex by its business id (parameter-bound).
        await Assert.That(sql).Contains("FROM \"public\".\"account\" s");
        await Assert.That(sql).Contains("s.data->>'Id' = @srcId");

        // Hop 1: account ⋈ account_written_by ⋈ author.
        await Assert.That(sql).Contains("JOIN \"public\".\"account_written_by\" j0 ON j0.source_id = s.id");
        await Assert.That(sql).Contains("JOIN \"public\".\"author\" t0 ON t0.id = j0.target_id");

        // Hop 2: author ⋈ author_employed_by ⋈ firm, chained off hop 1's vertex.
        await Assert.That(sql).Contains("JOIN \"public\".\"author_employed_by\" j1 ON j1.source_id = t0.id");
        await Assert.That(sql).Contains("JOIN \"public\".\"firm\" t1 ON t1.id = j1.target_id");

        // Projects the FINAL hop's target rows (the far endpoint).
        await Assert.That(sql).Contains("SELECT t1.id, t1.data");

        // No all-targets fallback — every join is inner, the target is never the
        // FROM root (#114 / DR-8 guard preserved at depth).
        await Assert.That(sql).DoesNotContain("LEFT JOIN");
        await Assert.That(sql).DoesNotContain("FROM \"public\".\"firm\"");
    }

    [Test]
    public async Task Lower_ThreeHopMonomorphic_StaysWithinJoinChainBudget()
    {
        // Exactly at the budget (3 hops, all monomorphic) — still a flat join
        // chain, the deepest depth that stays inside join_collapse_limit (1 + 3×2
        // = 7 relations < 8).
        var graph = TraversalChainGraphs.FourLevelChain();
        var expression = TraversalChainGraphs.ChainFrom(
            "L0", "n0",
            ("toL1", typeof(object)), ("toL2", typeof(object)), ("toL3", typeof(object)));

        var sql = SqlGenerator.BuildDepthTieredTraversalSql(
            "public", PgVectorObjectSetProvider.BuildTraversalPlan(graph, expression));

        await Assert.That(sql).DoesNotContain("WITH RECURSIVE");
        await Assert.That(sql).Contains("SELECT t2.id, t2.data");
        await Assert.That(sql).Contains("JOIN \"public\".\"l3\" t2 ON t2.id = j2.target_id");
    }

    [Test]
    public async Task Lower_FourHopOrVariableDepth_EmitsRecursiveCte()
    {
        // FOUR monomorphic hops (L0 -> L1 -> L2 -> L3 -> L4) is PAST the budget of
        // 3 — a flat join would join 1 + 4×2 = 9 relations, over join_collapse_limit
        // (8), so the planner would degrade. The lowering switches to a RECURSIVE
        // CTE that walks the junction edges one depth level at a time.
        var graph = TraversalChainGraphs.FiveLevelChain();
        var expression = TraversalChainGraphs.ChainFrom(
            "L0", "n0",
            ("toL1", typeof(object)), ("toL2", typeof(object)),
            ("toL3", typeof(object)), ("toL4", typeof(object)));

        var sql = SqlGenerator.BuildDepthTieredTraversalSql(
            "public", PgVectorObjectSetProvider.BuildTraversalPlan(graph, expression));

        // Recursive CTE — NOT a flat join chain — past the budget.
        await Assert.That(sql).Contains("WITH RECURSIVE traversal");

        // Base case: the anchor's surrogate id, addressed by its business id at depth 0.
        await Assert.That(sql).Contains("FROM \"public\".\"l0\" s WHERE s.data->>'Id' = @srcId");

        // The recursion advances one depth level per chain step, following the
        // step's own junction table (tr.depth = i) — the chain's link sequence,
        // not any junction.
        await Assert.That(sql).Contains("\"public\".\"l0_to_l1\"");
        await Assert.That(sql).Contains("\"public\".\"l3_to_l4\"");
        await Assert.That(sql).Contains("tr.depth = 0");
        await Assert.That(sql).Contains("tr.depth = 3");

        // Projects the FINAL-depth target rows (L4 at depth 4).
        await Assert.That(sql).Contains("FROM \"public\".\"l4\" t ");
        await Assert.That(sql).Contains("tr.depth = 4");

        // No all-targets fallback: the projection joins through the recursion's
        // reached node ids, never an unconditional target scan.
        await Assert.That(sql).DoesNotContain("LEFT JOIN");
    }

    [Test]
    public async Task Lower_TierBoundary_ThreeHopsJoinChain_FourHopsCte()
    {
        // The boundary is the join-collapse depth budget: 3 hops is the deepest
        // flat join chain, 4 hops crosses into the recursive-CTE tier. Pinning the
        // boundary keeps the budget honest if JoinChainDepthBudget is ever retuned.
        await Assert.That(SqlGenerator.JoinChainDepthBudget).IsEqualTo(3);

        var threeHop = SqlGenerator.BuildDepthTieredTraversalSql(
            "public",
            PgVectorObjectSetProvider.BuildTraversalPlan(
                TraversalChainGraphs.FourLevelChain(),
                TraversalChainGraphs.ChainFrom(
                    "L0", "n0",
                    ("toL1", typeof(object)), ("toL2", typeof(object)), ("toL3", typeof(object)))));

        var fourHop = SqlGenerator.BuildDepthTieredTraversalSql(
            "public",
            PgVectorObjectSetProvider.BuildTraversalPlan(
                TraversalChainGraphs.FiveLevelChain(),
                TraversalChainGraphs.ChainFrom(
                    "L0", "n0",
                    ("toL1", typeof(object)), ("toL2", typeof(object)),
                    ("toL3", typeof(object)), ("toL4", typeof(object)))));

        await Assert.That(threeHop).DoesNotContain("WITH RECURSIVE");
        await Assert.That(fourHop).Contains("WITH RECURSIVE");
    }
}

/// <summary>
/// Shared adversarial chain graphs + a chain-expression builder for the DR-12
/// depth-tier tests. Constructed via the internal <see cref="OntologyGraph"/>
/// constructor so multi-hop monomorphic chains can be expressed directly.
/// </summary>
internal static class TraversalChainGraphs
{
    /// <summary>
    /// Builds a chained <c>Where(s =&gt; s.Id == srcId).TraverseLink(l1).TraverseLink(l2)…</c>
    /// expression directly (no typed ObjectSet builder), anchoring the traversal at
    /// the source's business id.
    /// </summary>
    internal static TraverseLinkExpression ChainFrom(
        string sourceDescriptor,
        string srcId,
        params (string LinkName, Type LinkedType)[] hops)
    {
        var root = new RootExpression(typeof(Anchor), sourceDescriptor);
        System.Linq.Expressions.Expression<Func<Anchor, bool>> predicate = s => s.Id == srcId;
        ObjectSetExpression source = new FilterExpression(root, predicate);

        TraverseLinkExpression? built = null;
        foreach (var (linkName, linkedType) in hops)
        {
            built = new TraverseLinkExpression(source, linkName, linkedType, targetDescriptorName: null);
            source = built;
        }

        return built!;
    }

    // Account --writtenBy--> Author --employedBy--> Firm (all monomorphic).
    internal static OntologyGraph AccountAuthorFirm()
    {
        var account = Node("Account", links: [Link("writtenBy", "Author")]);
        var author = Node("Author", links: [Link("employedBy", "Firm")]);
        var firm = Node("Firm");
        return Graph(account, author, firm);
    }

    // L0 -> L1 -> L2 -> L3 (a four-level, three-hop monomorphic chain).
    internal static OntologyGraph FourLevelChain()
    {
        var l0 = Node("L0", links: [Link("toL1", "L1")]);
        var l1 = Node("L1", links: [Link("toL2", "L2")]);
        var l2 = Node("L2", links: [Link("toL3", "L3")]);
        var l3 = Node("L3");
        return Graph(l0, l1, l2, l3);
    }

    // L0 -> L1 -> L2 -> L3 -> L4 (a five-level, four-hop monomorphic chain —
    // PAST the budget of 3).
    internal static OntologyGraph FiveLevelChain()
    {
        var l0 = Node("L0", links: [Link("toL1", "L1")]);
        var l1 = Node("L1", links: [Link("toL2", "L2")]);
        var l2 = Node("L2", links: [Link("toL3", "L3")]);
        var l3 = Node("L3", links: [Link("toL4", "L4")]);
        var l4 = Node("L4");
        return Graph(l0, l1, l2, l3, l4);
    }

    private static LinkDescriptor Link(string name, string target) =>
        new(name, target, LinkCardinality.OneToMany);

    private static ObjectTypeDescriptor Node(string name, LinkDescriptor[]? links = null) => new()
    {
        Name = name,
        DomainName = "chain",
        ClrType = typeof(object),
        KeyProperty = new PropertyDescriptor("Id", typeof(string)),
        Links = links ?? [],
    };

    private static OntologyGraph Graph(params ObjectTypeDescriptor[] objectTypes) =>
        new(
            domains: [new DomainDescriptor("chain") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);

    internal sealed record Anchor(string Id);
}
