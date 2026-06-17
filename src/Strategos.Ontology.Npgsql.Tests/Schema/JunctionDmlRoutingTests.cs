using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;

namespace Strategos.Ontology.Npgsql.Tests.Schema;

/// <summary>
/// DR-11b (live DML threading, #128): the resolved target descriptor is threaded
/// through the LIVE relate/traverse DML so a POLYMORPHIC link actually writes to
/// and reads from the per-<c>(link, target-descriptor)</c> junction tables T2
/// generates. A MONOMORPHIC link is unchanged — it keeps the single
/// <c>{source}_{snake(link)}</c> table (DR-7..DR-10 lockstep).
/// </summary>
/// <remarks>
/// These assert the graph-driven routing + generated SQL only — no live database
/// (INV-2). Polymorphism is derived from the GRAPH (a link whose declared target
/// resolves to MORE THAN ONE descriptor — e.g. an interface with several
/// implementors), never from a CLR type (INV-8). The read fan-out is a graph-
/// driven UNION over the per-descriptor tables and needs no
/// <c>TraverseLinkExpression</c> change.
/// </remarks>
public class JunctionDmlRoutingTests
{
    private const string Account = "Account";
    private const string SecurityInterface = "ISecurity";
    private const string Stock = "Stock";
    private const string Bond = "Bond";
    private const string Author = "Author";
    private const string MonoLink = "WrittenBy"; // Account -> Author (single concrete target)
    private const string PolyLink = "Holdings";   // Account -> ISecurity (Stock | Bond)

    // -----------------------------------------------------------------------
    // WRITE path — relate routes to the per-(link, target-descriptor) table.
    // -----------------------------------------------------------------------

    [Test]
    public async Task ResolveRelateJunction_MonomorphicLink_UsesSingleSourceLinkTable()
    {
        // A monomorphic link (Author is the link's lone concrete target) keeps the
        // pre-DR-11 junction name — relate writes the SAME physical table the
        // DR-7..DR-10 lockstep tests assert.
        var graph = BuildPolymorphicGraph();

        var junction = PgVectorObjectSetProvider.ResolveRelateJunction(
            graph, srcDescriptor: Account, linkName: MonoLink, tgtDescriptor: Author);

        await Assert.That(junction.IsPolymorphic).IsFalse();
        await Assert.That(SqlGenerator.JunctionTableNameFor(junction)).IsEqualTo("account_written_by");
    }

    [Test]
    public async Task ResolveRelateJunction_PolymorphicLink_RoutesEachTargetToOwnTable()
    {
        // A polymorphic link (Holdings -> ISecurity, implemented by Stock and Bond)
        // routes a relate to Stock into account_holdings_stock and a relate to Bond
        // into account_holdings_bond — distinct per-descriptor tables, each named
        // by the resolved target descriptor.
        var graph = BuildPolymorphicGraph();

        var toStock = PgVectorObjectSetProvider.ResolveRelateJunction(
            graph, srcDescriptor: Account, linkName: PolyLink, tgtDescriptor: Stock);
        var toBond = PgVectorObjectSetProvider.ResolveRelateJunction(
            graph, srcDescriptor: Account, linkName: PolyLink, tgtDescriptor: Bond);

        await Assert.That(toStock.IsPolymorphic).IsTrue();
        await Assert.That(toBond.IsPolymorphic).IsTrue();

        var stockName = SqlGenerator.JunctionTableNameFor(toStock);
        var bondName = SqlGenerator.JunctionTableNameFor(toBond);

        await Assert.That(stockName).IsEqualTo("account_holdings_stock");
        await Assert.That(bondName).IsEqualTo("account_holdings_bond");
        await Assert.That(stockName).IsNotEqualTo(bondName);

        // The relate INSERT targets that per-descriptor table with an honest FK to
        // the resolved target's own object table.
        var sql = SqlGenerator.BuildRelateInsertSql(
            "public", stockName, "account", "Id", "stock", "Id");
        await Assert.That(sql).Contains("INSERT INTO \"public\".\"account_holdings_stock\" (source_id, target_id)");
        await Assert.That(sql).Contains("\"public\".\"stock\" t");
    }

    // -----------------------------------------------------------------------
    // READ path — traversal resolves per-descriptor table(s).
    // -----------------------------------------------------------------------

    [Test]
    public async Task ResolveTraversalHops_PolymorphicLink_NoOverride_FansOutAcrossPerDescriptorTables()
    {
        // A polymorphic hop with NO override resolves a hop PER target descriptor —
        // Stock and Bond — each anchored at its own per-descriptor junction +
        // target table. The graph-driven fan-out needs no expression change.
        var graph = BuildPolymorphicGraph();

        var hops = PgVectorObjectSetProvider.ResolveTraversalHops(
            graph, sourceDescriptorName: Account, linkName: PolyLink, targetDescriptorOverride: null);

        await Assert.That(hops.Count).IsEqualTo(2);

        var tables = hops.Select(h => h.JunctionTable).OrderBy(n => n, StringComparer.Ordinal).ToList();
        await Assert.That(tables).Contains("account_holdings_bond");
        await Assert.That(tables).Contains("account_holdings_stock");

        // Each hop's target table is its OWN resolved descriptor's table.
        var stockHop = hops.Single(h => h.TargetDescriptorName == Stock);
        await Assert.That(stockHop.TargetTable).IsEqualTo("stock");
        await Assert.That(stockHop.JunctionTable).IsEqualTo("account_holdings_stock");
    }

    [Test]
    public async Task BuildPolymorphicTraversalSql_FansOutAcrossPerDescriptorTables_AsUnion()
    {
        // The fanned-out hops lower to a UNION ALL over each per-descriptor
        // vertex-junction-vertex join, so a single instance-anchored traversal
        // reads back the related targets from EVERY per-descriptor table.
        var graph = BuildPolymorphicGraph();

        var hops = PgVectorObjectSetProvider.ResolveTraversalHops(
            graph, sourceDescriptorName: Account, linkName: PolyLink, targetDescriptorOverride: null);

        var sql = SqlGenerator.BuildPolymorphicTraversalSql("public", hops);

        await Assert.That(sql).Contains("UNION ALL");
        await Assert.That(sql).Contains("\"public\".\"account_holdings_stock\" j");
        await Assert.That(sql).Contains("\"public\".\"account_holdings_bond\" j");
        await Assert.That(sql).Contains("\"public\".\"stock\" t");
        await Assert.That(sql).Contains("\"public\".\"bond\" t");
        await Assert.That(sql).Contains("s.data->>'Id' = @srcId");
    }

    [Test]
    public async Task ResolveTraversalHops_MonomorphicLink_SingleHopUnchangedName()
    {
        // A monomorphic hop fans out to exactly ONE hop, named by (source, link) —
        // identical to the pre-DR-11 single-hop lowering (DR-7..DR-10 lockstep).
        var graph = BuildPolymorphicGraph();

        var hops = PgVectorObjectSetProvider.ResolveTraversalHops(
            graph, sourceDescriptorName: Account, linkName: MonoLink, targetDescriptorOverride: null);

        await Assert.That(hops.Count).IsEqualTo(1);
        await Assert.That(hops[0].JunctionTable).IsEqualTo("account_written_by");
        await Assert.That(hops[0].TargetTable).IsEqualTo("author");
    }

    // -----------------------------------------------------------------------
    // ResolveLinkTargetDescriptors — the shared graph-driven fan-out resolver.
    // -----------------------------------------------------------------------

    [Test]
    public async Task ResolveLinkTargetDescriptors_InterfaceLink_ReturnsImplementorsOrdinal()
    {
        // The Holdings link targets ISecurity, implemented by Stock and Bond — the
        // resolver returns both implementor descriptor names, ordinal-ordered for a
        // stable fan-out.
        var graph = BuildPolymorphicGraph();
        var link = graph.ObjectTypes.Single(o => o.Name == Account).Links.Single(l => l.Name == PolyLink);

        var targets = PgVectorObjectSetProvider.ResolveLinkTargetDescriptors(graph, link);

        await Assert.That(targets).IsEquivalentTo(new[] { Bond, Stock });
    }

    [Test]
    public async Task ResolveLinkTargetDescriptors_ConcreteLink_ReturnsSingleResolvedDescriptor()
    {
        // A monomorphic link (WrittenBy -> Author, a concrete object target)
        // resolves to exactly its one graph-resolved target descriptor.
        var graph = BuildPolymorphicGraph();
        var link = graph.ObjectTypes.Single(o => o.Name == Account).Links.Single(l => l.Name == MonoLink);

        var targets = PgVectorObjectSetProvider.ResolveLinkTargetDescriptors(graph, link);

        await Assert.That(targets).IsEquivalentTo(new[] { Author });
    }

    [Test]
    public async Task ResolveLinkTargetDescriptors_InterfaceWithNoImplementor_Throws()
    {
        // An interface link with zero implementors has no provisionable junction
        // table — the resolver refuses (mirrors the compile-time AONT212 guard)
        // rather than emitting a dead query.
        var graph = BuildEmptyInterfaceGraph();
        var link = graph.ObjectTypes.Single(o => o.Name == Account).Links.Single(l => l.Name == PolyLink);

        await Assert.That(() => PgVectorObjectSetProvider.ResolveLinkTargetDescriptors(graph, link))
            .Throws<InvalidOperationException>();
    }

    // A graph whose Account.Holdings link targets ISecurity, which NO object type
    // implements — the polymorphic fan-out resolves to the empty set.
    private static OntologyGraph BuildEmptyInterfaceGraph()
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

        var objectTypes = new[] { account };
        return new OntologyGraph(
            domains: [new DomainDescriptor("portfolio") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [iSecurity],
            crossDomainLinks: [],
            workflowChains: []);
    }

    // A graph where Account has BOTH a monomorphic link (WrittenBy -> Author) and a
    // polymorphic link (Holdings -> ISecurity, implemented by Stock and Bond).
    private static OntologyGraph BuildPolymorphicGraph()
    {
        var iSecurity = new InterfaceDescriptor(SecurityInterface, typeof(object));

        var account = new ObjectTypeDescriptor
        {
            Name = Account,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links =
            [
                new LinkDescriptor(MonoLink, Author, LinkCardinality.OneToMany),
                new LinkDescriptor(PolyLink, SecurityInterface, LinkCardinality.OneToMany),
            ],
        };

        var author = new ObjectTypeDescriptor
        {
            Name = Author,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
        };

        var stock = new ObjectTypeDescriptor
        {
            Name = Stock,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            ImplementedInterfaces = [iSecurity],
        };

        var bond = new ObjectTypeDescriptor
        {
            Name = Bond,
            DomainName = "portfolio",
            ClrType = typeof(object),
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            ImplementedInterfaces = [iSecurity],
        };

        var objectTypes = new[] { account, author, stock, bond };
        return new OntologyGraph(
            domains: [new DomainDescriptor("portfolio") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [iSecurity],
            crossDomainLinks: [],
            workflowChains: []);
    }
}
