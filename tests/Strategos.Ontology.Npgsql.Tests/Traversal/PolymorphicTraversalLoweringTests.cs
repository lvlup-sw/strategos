using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Traversal;

/// <summary>
/// DR-12 (#131, T8): a POLYMORPHIC hop routes through the DR-11b UNION fan-out
/// seam and counts as fan-out against the depth budget (so it lowers via the
/// recursive-CTE tier regardless of hop count); a chained hop after an
/// interface-narrowed (disambiguated) polymorphic target resolves the correct
/// FAR endpoint descriptor (#128 regression); and a zero-relation source yields
/// an empty result in BOTH tiers.
/// </summary>
/// <remarks>
/// GENERATED-SQL SHAPE assertions only — no live database (INV-2). Polymorphism
/// is a GRAPH property (a link whose declared target is an interface), resolved
/// via the DR-11b <c>ResolveTraversalHops</c> + <c>BuildPolymorphicTraversalSql</c>
/// seam, never a CLR type (INV-8). This task WIRES that seam into the depth-tiered
/// traversal lowering rather than rebuilding it.
/// </remarks>
public class PolymorphicTraversalLoweringTests
{
    private const string Account = "Account";
    private const string SecurityInterface = "ISecurity";
    private const string Stock = "Stock";
    private const string Bond = "Bond";
    private const string Issuer = "Issuer";
    private const string PolyLink = "Holdings"; // Account -> ISecurity (Stock | Bond)
    private const string IssuedBy = "issuedBy";  // Stock -> Issuer (chained far endpoint)

    [Test]
    public async Task Lower_PolymorphicHop_UnionsPerDescriptorTables()
    {
        // A single polymorphic hop (Holdings -> ISecurity, implemented by Stock and
        // Bond) fans out to one vertex ⋈ junction ⋈ vertex leg per per-descriptor
        // junction table, UNION'd — the DR-11b posture, now reachable from the
        // depth-tiered lowering. A polymorphic hop is fan-out, so it lowers via the
        // recursive-CTE tier even at depth 1.
        var graph = BuildPolymorphicGraph();
        var expression = TraversalChainGraphs.ChainFrom(Account, "acct-1", (PolyLink, typeof(object)));

        var plan = PgVectorObjectSetProvider.BuildTraversalPlan(graph, expression);

        // The hop is recognized as a polymorphic fan-out step.
        await Assert.That(plan.HasPolymorphicStep).IsTrue();
        await Assert.That(plan.Steps[0].IsPolymorphic).IsTrue();
        await Assert.That(plan.Steps[0].Hops.Count).IsEqualTo(2);

        var sql = SqlGenerator.BuildDepthTieredTraversalSql("public", plan);

        // Fan-out routes through the recursive-CTE tier (a polymorphic hop counts
        // as fan-out against the budget) and UNIONs the per-descriptor tables.
        await Assert.That(sql).Contains("WITH RECURSIVE");
        await Assert.That(sql).Contains("UNION ALL");
        await Assert.That(sql).Contains("\"public\".\"account_holdings_stock\"");
        await Assert.That(sql).Contains("\"public\".\"account_holdings_bond\"");

        // Each leg reads its OWN resolved descriptor's target table — honest FKs,
        // never a polymorphic/union endpoint.
        await Assert.That(sql).Contains("\"public\".\"stock\"");
        await Assert.That(sql).Contains("\"public\".\"bond\"");

        // Anchored at the source business id, parameter-bound.
        await Assert.That(sql).Contains("s.data->>'Id' = @srcId");
    }

    [Test]
    public async Task Lower_PolymorphicHop_RoutesThroughDr11bUnionSeam()
    {
        // The polymorphic fan-out is the SAME shape the DR-11b
        // BuildPolymorphicTraversalSql seam emits — proving the lowering WIRES that
        // seam rather than rebuilding the union legs. Each per-descriptor leg is a
        // vertex ⋈ junction ⋈ vertex join over its own pair of tables.
        var graph = BuildPolymorphicGraph();

        var hops = PgVectorObjectSetProvider.ResolveTraversalHops(
            graph, sourceDescriptorName: Account, linkName: PolyLink, targetDescriptorOverride: null);
        var seamSql = SqlGenerator.BuildPolymorphicTraversalSql("public", hops);

        // The seam emits the per-descriptor legs the depth-tiered CTE also walks.
        await Assert.That(seamSql).Contains("\"public\".\"account_holdings_stock\" j");
        await Assert.That(seamSql).Contains("\"public\".\"account_holdings_bond\" j");
        await Assert.That(seamSql).Contains("UNION ALL");
    }

    [Test]
    public async Task Traverse_ChainedHopAfterInterfaceNarrow_ResolvesCorrectFarEndpoint()
    {
        // #128 regression: a polymorphic link DISAMBIGUATED to a single concrete
        // partition (Holdings -> Stock via the explicit override) may be chained
        // onward — the far hop (Stock -> Issuer via issuedBy) resolves its source
        // descriptor as STOCK (the disambiguated target), so the chained junction
        // is stock_issued_by and the far endpoint is the Issuer table. The far
        // endpoint is resolved from the GRAPH (Stock's own link), never a
        // CLR-first match across the multi-registered interface implementors.
        var graph = BuildPolymorphicGraph();
        var expression = ChainWithOverride(
            Account, "acct-1",
            firstLink: PolyLink, firstOverride: Stock,
            secondLink: IssuedBy);

        var plan = PgVectorObjectSetProvider.BuildTraversalPlan(graph, expression);

        // The disambiguated first hop is monomorphic (override names one partition).
        await Assert.That(plan.Steps).HasCount().EqualTo(2);
        await Assert.That(plan.Steps[0].IsPolymorphic).IsFalse();
        await Assert.That(plan.Steps[0].Hops[0].TargetDescriptorName).IsEqualTo(Stock);

        // The chained hop's source is STOCK, so its junction is stock_issued_by and
        // its far endpoint is the Issuer table — resolved from Stock's graph link.
        await Assert.That(plan.Steps[1].Hops[0].SourceTable).IsEqualTo("stock");
        await Assert.That(plan.Steps[1].Hops[0].JunctionTable).IsEqualTo("stock_issued_by");
        await Assert.That(plan.Steps[1].Hops[0].TargetDescriptorName).IsEqualTo(Issuer);
        await Assert.That(plan.Steps[1].Hops[0].TargetTable).IsEqualTo("issuer");
    }

    [Test]
    public async Task Traverse_ChainedHopAfterUnnarrowedPolymorphic_Refuses()
    {
        // A chained hop after an UN-disambiguated polymorphic hop is ambiguous: the
        // next hop's source descriptor is not uniquely defined (Stock? Bond?), so
        // the planner refuses rather than silently picking one fanned-out partition.
        var graph = BuildPolymorphicGraph();
        var expression = TraversalChainGraphs.ChainFrom(
            Account, "acct-1", (PolyLink, typeof(object)), (IssuedBy, typeof(object)));

        await Assert.That(() => PgVectorObjectSetProvider.BuildTraversalPlan(graph, expression))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Traverse_ZeroRelations_ReturnsEmpty_BothTiers()
    {
        // The zero-relation guard (#114 / DR-8) holds in BOTH tiers: every join is
        // inner and the target table is never the FROM root, so a source with no
        // junction rows yields no result rows — never an all-targets scan.

        // Join-chain tier (monomorphic, within budget).
        var monoGraph = TraversalChainGraphs.AccountAuthorFirm();
        var monoSql = SqlGenerator.BuildDepthTieredTraversalSql(
            "public",
            PgVectorObjectSetProvider.BuildTraversalPlan(
                monoGraph,
                TraversalChainGraphs.ChainFrom("Account", "acct-1", ("writtenBy", typeof(object)))));

        await Assert.That(monoSql).DoesNotContain("WITH RECURSIVE");
        await Assert.That(monoSql).DoesNotContain("LEFT JOIN");
        await Assert.That(monoSql).DoesNotContain("FROM \"public\".\"author\"");
        // The target is reached only THROUGH the junction (inner join path).
        await Assert.That(monoSql).Contains("ON j0.source_id = s.id");

        // Recursive-CTE tier (polymorphic fan-out).
        var polyGraph = BuildPolymorphicGraph();
        var polySql = SqlGenerator.BuildDepthTieredTraversalSql(
            "public",
            PgVectorObjectSetProvider.BuildTraversalPlan(
                polyGraph,
                TraversalChainGraphs.ChainFrom(Account, "acct-1", (PolyLink, typeof(object)))));

        await Assert.That(polySql).Contains("WITH RECURSIVE");
        await Assert.That(polySql).DoesNotContain("LEFT JOIN");
        // No unconditional target scan — the projection joins through the
        // recursion's reached node ids only.
        await Assert.That(polySql).DoesNotContain("FROM \"public\".\"stock\" t,");
        await Assert.That(polySql).Contains("JOIN traversal tr");
    }

    // A chained Where(...).TraverseLink<object>(firstLink, firstOverride)
    //                     .TraverseLink<object>(secondLink) expression.
    private static TraverseLinkExpression ChainWithOverride(
        string sourceDescriptor, string srcId,
        string firstLink, string firstOverride, string secondLink)
    {
        var root = new RootExpression(typeof(TraversalChainGraphs.Anchor), sourceDescriptor);
        System.Linq.Expressions.Expression<Func<TraversalChainGraphs.Anchor, bool>> predicate = s => s.Id == srcId;
        ObjectSetExpression source = new FilterExpression(root, predicate);
        var first = new TraverseLinkExpression(source, firstLink, typeof(object), firstOverride);
        return new TraverseLinkExpression(first, secondLink, typeof(object), targetDescriptorName: null);
    }

    // Account.Holdings -> ISecurity (Stock | Bond); Stock.issuedBy -> Issuer (a
    // concrete chained far endpoint reachable only after disambiguating Holdings).
    private static OntologyGraph BuildPolymorphicGraph()
    {
        var iSecurity = new InterfaceDescriptor(SecurityInterface, typeof(object));

        var account = new ObjectTypeDescriptor
        {
            Name = Account,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links = [new LinkDescriptor(PolyLink, SecurityInterface, LinkCardinality.OneToMany)],
        };

        var stock = new ObjectTypeDescriptor
        {
            Name = Stock,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            ImplementedInterfaces = [iSecurity],
            Links = [new LinkDescriptor(IssuedBy, Issuer, LinkCardinality.OneToMany)],
        };

        var bond = new ObjectTypeDescriptor
        {
            Name = Bond,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            ImplementedInterfaces = [iSecurity],
        };

        var issuer = new ObjectTypeDescriptor
        {
            Name = Issuer,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
        };

        var objectTypes = new[] { account, stock, bond, issuer };
        return new OntologyGraph(
            domains: [new DomainDescriptor("portfolio") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [iSecurity],
            crossDomainLinks: [],
            workflowChains: []);
    }
}
