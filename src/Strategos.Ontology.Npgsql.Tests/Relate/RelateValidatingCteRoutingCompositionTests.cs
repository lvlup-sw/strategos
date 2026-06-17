using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;

namespace Strategos.Ontology.Npgsql.Tests.Relate;

/// <summary>
/// Integration regression for the load-bearing DR-11b ↔ DR-13 composition: the
/// per-<c>(link, target-descriptor)</c> junction routing (DR-11b) and the
/// self-validating relate CTE (DR-13/R4) MUST compose — the validating
/// <c>INSERT</c> has to target the SAME per-descriptor table the routing resolves,
/// not a single <c>{source}_{snake(link)}</c> table.
/// </summary>
/// <remarks>
/// Both behaviors were authored on the same keystone, so the v2.9.0 integration
/// merge folded them additively rather than re-introducing one over the other.
/// This test pins that they STAY composed: it walks the exact two-step
/// <see cref="PgVectorObjectSetProvider.RelateAsync"/> performs —
/// <see cref="SqlGenerator.JunctionTableNameFor"/> ∘
/// <see cref="PgVectorObjectSetProvider.ResolveRelateJunction"/> (DR-11b) to pick
/// the table, then <see cref="SqlGenerator.BuildValidatingRelateInsertSql"/>
/// (DR-13) to build the statement — and asserts the resolved per-descriptor table
/// appears inside the validating CTE's <c>INSERT INTO</c>. Graph-driven + generated
/// SQL only, no live database (INV-2); polymorphism comes from the GRAPH (an
/// interface with several implementors), never a CLR type (INV-8).
/// </remarks>
public class RelateValidatingCteRoutingCompositionTests
{
    private const string Account = "Account";
    private const string SecurityInterface = "ISecurity";
    private const string Stock = "Stock";
    private const string Bond = "Bond";
    private const string Author = "Author";
    private const string MonoLink = "WrittenBy"; // Account -> Author (single concrete target)
    private const string PolyLink = "Holdings";   // Account -> ISecurity (Stock | Bond)

    [Test]
    public async Task Relate_PolymorphicLink_UsesValidatingCte_AgainstResolvedPerDescriptorTable()
    {
        // The exact two-step RelateAsync composes: route to the per-descriptor
        // junction (DR-11b), then build the self-validating CTE against THAT table
        // (DR-13). A relate to Stock must insert into account_holdings_stock and a
        // relate to Bond into account_holdings_bond — each inside the validating CTE,
        // never collapsed onto a single account_holdings table.
        var graph = BuildPolymorphicGraph();

        var stockTable = SqlGenerator.JunctionTableNameFor(
            PgVectorObjectSetProvider.ResolveRelateJunction(graph, Account, PolyLink, Stock));
        var bondTable = SqlGenerator.JunctionTableNameFor(
            PgVectorObjectSetProvider.ResolveRelateJunction(graph, Account, PolyLink, Bond));

        await Assert.That(stockTable).IsEqualTo("account_holdings_stock");
        await Assert.That(bondTable).IsEqualTo("account_holdings_bond");

        var stockSql = SqlGenerator.BuildValidatingRelateInsertSql(
            "public", stockTable, "account", "Id", "stock", "Id");
        var bondSql = SqlGenerator.BuildValidatingRelateInsertSql(
            "public", bondTable, "account", "Id", "bond", "Id");

        // DR-13: the statement is the validating CTE (snapshot-atomic existence
        // probe + data-modifying insert), proving routing did not drop the CTE.
        await Assert.That(stockSql).Contains("AS src_exists");
        await Assert.That(stockSql).Contains("AS tgt_exists");
        await Assert.That(stockSql).Contains("ON CONFLICT (source_id, target_id) DO NOTHING");

        // DR-11b: the CTE's INSERT targets the resolved per-descriptor table — the
        // composition point. Stock's CTE writes account_holdings_stock with an honest
        // FK to the stock object table; Bond's writes account_holdings_bond.
        await Assert.That(stockSql)
            .Contains("INSERT INTO \"public\".\"account_holdings_stock\" (source_id, target_id)");
        await Assert.That(stockSql).Contains("\"public\".\"stock\" WHERE data->>'Id' = @tgtId");

        await Assert.That(bondSql)
            .Contains("INSERT INTO \"public\".\"account_holdings_bond\" (source_id, target_id)");
        await Assert.That(bondSql).Contains("\"public\".\"bond\" WHERE data->>'Id' = @tgtId");

        // The two per-descriptor partitions are distinct — a polymorphic relate is
        // never routed to a shared table by the validating CTE.
        await Assert.That(stockTable).IsNotEqualTo(bondTable);
    }

    [Test]
    public async Task Relate_MonomorphicLink_UsesValidatingCte_AgainstSingleSourceLinkTable()
    {
        // A monomorphic link keeps its single {source}_{snake(link)} table inside
        // the validating CTE — DR-11b routing leaves the DR-7..DR-10 lockstep name
        // unchanged, and the CTE composes against it (no per-descriptor suffix).
        var graph = BuildPolymorphicGraph();

        var table = SqlGenerator.JunctionTableNameFor(
            PgVectorObjectSetProvider.ResolveRelateJunction(graph, Account, MonoLink, Author));

        await Assert.That(table).IsEqualTo("account_written_by");

        var sql = SqlGenerator.BuildValidatingRelateInsertSql(
            "public", table, "account", "Id", "author", "Id");

        await Assert.That(sql)
            .Contains("INSERT INTO \"public\".\"account_written_by\" (source_id, target_id)");
        await Assert.That(sql).Contains("AS src_exists");
        await Assert.That(sql).Contains("AS tgt_exists");
    }

    // A graph where Account has BOTH a monomorphic link (WrittenBy -> Author) and a
    // polymorphic link (Holdings -> ISecurity, implemented by Stock and Bond).
    // Mirrors JunctionDmlRoutingTests.BuildPolymorphicGraph so the composition test
    // pins the SAME routing seam the DR-11b unit tests assert.
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
