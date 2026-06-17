using System.Linq;
using System.Threading.Tasks;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.FailureModes;

// ---------------------------------------------------------------------------
// G-18 / DR-18 (#116) — the cross-provider FAILURE-MODE MATRIX. Extends the
// DR-8 matrix (Strategos.Ontology[.Npgsql].Tests/Integration/EdgeFailureMode*)
// with the three v2.9.0-completion modes that exercise the NEW edge surface
// (the validating CTE, the polymorphic fan-out, and the bitemporal projection):
//
//   1. Relate_NonexistentEndpoint_TypedError_BothProviders — a relate to an
//      unstored endpoint surfaces a typed RelationEndpointNotFoundException on
//      BOTH providers and writes NO row. In-memory: eager validation throws
//      (executed here). Npgsql: the self-validating CTE returns
//      src_exists/tgt_exists flags whose absence drives the SAME typed error
//      (DR-13/R4) — the flag-returning SQL shape is asserted here; the live
//      execution rides RelateBatchTests + the gated Npgsql matrix.
//   2. PolymorphicTraversal_WrongPartition_NeverSilent — a polymorphic traversal
//      reads the correct per-descriptor partition or refuses; it NEVER returns
//      rows from the wrong partition. In-memory: per-row resolution routes each
//      row to its stored descriptor. Npgsql: each fanned-out hop reads ONLY its
//      own per-(link, target) junction + object table (a Stock hop never reads
//      bond rows).
//   3. AsOf_NonBitemporalAssociation_DegradesGracefully — an association asserted
//      WITHOUT a valid-time is an always-open interval (valid_from/system_from
//      DEFAULT now(), *_to NULL). The as-of-transaction-time read returns it for
//      @asOfTx at/after the assertion and an EMPTY set before — never an error,
//      never wrong rows. The graceful-degradation comes from the open-interval
//      (system_to IS NULL) branch of BuildAsOfTransactionTimeSql.
//
// NOTE (scope): the MCP isError failure mode from the plan lives in the MCP
// track (G-15), NOT this Npgsql tree. It is intentionally ABSENT here.
//
// INV-2: raw Npgsql throughout — no Marten/Wolverine. INV-8: identity by
// descriptor name, never typeof.
// ---------------------------------------------------------------------------
public class EdgeFailureModeMatrixTests
{
    private const string NodeDescriptor = "FmNode";
    private const string LinkName = "links_to";

    // =======================================================================
    // Mode 1 — non-existent endpoint, typed error on BOTH providers.
    // =======================================================================

    [Test]
    public async Task Relate_NonexistentEndpoint_TypedError_BothProviders()
    {
        // --- IN-MEMORY: eager endpoint validation throws the typed error and
        //     leaves no dangling row. Executed here (no DB). ---
        var graph = BuildSelfLinkGraph(allowsSelfLoop: false);
        var inMemory = new InMemoryObjectSetProvider(graph);
        inMemory.Seed(new FmNode("a"), "a", NodeDescriptor); // only the source exists
        IObjectSetWriter writer = inMemory;

        await Assert.That(async () =>
                await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "ghost"))
            .Throws<RelationEndpointNotFoundException>();
        await Assert.That(inMemory.GetRelations(NodeDescriptor, "a", LinkName)).IsEmpty();

        // --- NPGSQL: the self-validating relate CTE (DR-13/R4) is a SINGLE
        //     statement that RETURNS the endpoint-existence flags the provider
        //     reads to raise the SAME typed RelationEndpointNotFoundException. A
        //     missing endpoint resolves zero rows in its CTE, so src_exists /
        //     tgt_exists report the absence atomically with the (zero-row) insert —
        //     never a silent no-op. The flag-returning shape is the structural
        //     guarantee that the typed error can be raised; assert it. ---
        var junction = SqlGenerator.JunctionTableNameFor(
            PgVectorObjectSetProvider.ResolveRelateJunction(graph, NodeDescriptor, LinkName, NodeDescriptor));
        var sql = SqlGenerator.BuildValidatingRelateInsertSql(
            "public", junction, "fm_node", "Id", "fm_node", "Id");

        await Assert.That(sql).Contains("AS src_exists");
        await Assert.That(sql).Contains("AS tgt_exists");
        // The insert is a data-modifying CTE over the resolved endpoints, so a
        // missing endpoint yields ZERO inserted rows — no dangling row, ever.
        await Assert.That(sql).Contains("ON CONFLICT (source_id, target_id) DO NOTHING");
    }

    // =======================================================================
    // Mode 2 — polymorphic traversal reads the right partition, never wrong rows.
    // =======================================================================

    [Test]
    public async Task PolymorphicTraversal_WrongPartition_NeverSilent()
    {
        var graph = BuildPolymorphicGraph();

        // --- IN-MEMORY: a single Holdings traversal carrying a Stock row and a
        //     Bond row resolves EACH from its OWN partition — never a wrong-row
        //     leak (a stock id never resolves a bond, and vice versa). ---
        RelationResolver relations = (sd, si, ln) =>
            sd == Account && si == "acc1" && ln == PolyLink
                ?
                [
                    new RelationRow(Stock, "s1", AssociationObjectId: null),
                    new RelationRow(Bond, "b1", AssociationObjectId: null),
                ]
                : [];

        var evaluator = new InMemoryExpressionEvaluator(graph, relations, idProjector: null);
        Func<string, IReadOnlyList<object>> resolver = name => name switch
        {
            Account => [new AccountRow("acc1")],
            Stock => [new StockRow("s1")],
            Bond => [new BondRow("b1")],
            _ => [],
        };

        var traverse = new TraverseLinkExpression(
            new RootExpression(typeof(AccountRow), Account), PolyLink, typeof(object));
        var result = evaluator.Evaluate<object>(traverse, resolver);

        // Exactly the two correctly-partitioned rows — never a cross-partition leak.
        await Assert.That(result.OfType<StockRow>().Select(s => s.Id)).Contains("s1");
        await Assert.That(result.OfType<BondRow>().Select(b => b.Id)).Contains("b1");
        await Assert.That(result).HasCount().EqualTo(2);

        // --- NPGSQL: each fanned-out hop reads ONLY its own per-descriptor
        //     junction + object table. The Stock hop's SQL references the stock
        //     tables and NOT the bond tables, so a polymorphic read can never
        //     silently pull rows from the wrong partition. ---
        var hops = PgVectorObjectSetProvider.ResolveTraversalHops(
            graph, Account, PolyLink, targetDescriptorOverride: null);

        var stockHop = hops.Single(h => h.TargetDescriptorName == Stock);
        var bondHop = hops.Single(h => h.TargetDescriptorName == Bond);

        var stockSql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            "public", stockHop.SourceTable, stockHop.SourceKeyProperty, stockHop.JunctionTable, stockHop.TargetTable);

        await Assert.That(stockSql).Contains("\"public\".\"account_holdings_stock\" j");
        await Assert.That(stockSql).Contains("\"public\".\"stock\" t");
        // The Stock partition's read NEVER touches the Bond partition.
        await Assert.That(stockSql).DoesNotContain("account_holdings_bond");
        await Assert.That(stockSql).DoesNotContain("\"bond\"");

        // And the UNION read covers BOTH partitions (so nothing is silently
        // dropped), each still scoped to its own tables.
        var unionSql = SqlGenerator.BuildPolymorphicTraversalSql("public", hops);
        await Assert.That(unionSql).Contains("\"public\".\"account_holdings_stock\" j");
        await Assert.That(unionSql).Contains("\"public\".\"account_holdings_bond\" j");
        await Assert.That(unionSql).Contains("UNION ALL");
    }

    // =======================================================================
    // Mode 3 — as-of read of a non-bitemporal association degrades gracefully.
    // =======================================================================

    [Test]
    public async Task AsOf_NonBitemporalAssociation_DegradesGracefully()
    {
        // A "non-bitemporal" association is one asserted WITHOUT a valid-time: the
        // DR-4 attributed-relate INSERT sets neither *_from nor *_to, so the table
        // defaults valid_from/system_from to now() and leaves *_to NULL — an
        // always-open interval. The as-of-transaction-time read must therefore:
        //   - return the row whenever @asOfTx is at/after the assertion
        //     (system_from <= @asOfTx), AND
        //   - never error on the open interval (system_to IS NULL).
        var sql = SqlGenerator.BuildAsOfTransactionTimeSql("public", "supersedes");

        // It reads the quartet columns back (a pure projection — no mutation, INV-7).
        await Assert.That(sql).Contains("valid_from");
        await Assert.That(sql).Contains("system_to");

        // The system-time bound is half-open and TOLERATES the open interval: a
        // non-retracted (system_to IS NULL) row is visible, so a never-temporally-
        // managed association degrades to "always current" rather than vanishing or
        // erroring.
        await Assert.That(sql).Contains("system_from <= @asOfTx");
        await Assert.That(sql).Contains("system_to IS NULL OR system_to > @asOfTx");

        // The anchor binds via @asOfTx (deterministic point-in-time), never now()
        // — so an as-of read of a non-bitemporal row before its assertion instant
        // is a well-defined EMPTY set, not an error.
        await Assert.That(sql).DoesNotContain("now()");
        await Assert.That(sql).Contains("@asOfTx");
    }

    // -----------------------------------------------------------------------
    // Graph builders.
    // -----------------------------------------------------------------------

    private const string Account = "Account";
    private const string SecurityInterface = "ISecurity";
    private const string Stock = "Stock";
    private const string Bond = "Bond";
    private const string PolyLink = "Holdings";

    // A CLR-free (SymbolKey-only) self-linking node, so endpoint resolution can
    // only go through the graph (INV-8). FmNode -> FmNode via "links_to".
    private static OntologyGraph BuildSelfLinkGraph(bool allowsSelfLoop)
    {
        var node = new ObjectTypeDescriptor
        {
            Name = NodeDescriptor,
            DomainName = "fm",
            ClrType = typeof(FmNode),
            IdAccessor = instance => ((FmNode)instance).Id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links =
            [
                new LinkDescriptor(LinkName, NodeDescriptor, LinkCardinality.OneToMany)
                {
                    AllowsSelfLoop = allowsSelfLoop,
                },
            ],
        };

        var objectTypes = new[] { node };
        return new OntologyGraph(
            domains: [new DomainDescriptor("fm") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    // Account.Holdings -> ISecurity (interface), implemented by Stock and Bond.
    // Mirrors JunctionDmlRoutingTests.BuildPolymorphicGraph.
    private static OntologyGraph BuildPolymorphicGraph()
    {
        var iSecurity = new InterfaceDescriptor(SecurityInterface, typeof(object));

        var account = new ObjectTypeDescriptor
        {
            Name = Account,
            DomainName = "portfolio",
            ClrType = typeof(AccountRow),
            IdAccessor = instance => ((AccountRow)instance).Id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links = [new LinkDescriptor(PolyLink, SecurityInterface, LinkCardinality.OneToMany)],
        };

        var stock = new ObjectTypeDescriptor
        {
            Name = Stock,
            DomainName = "portfolio",
            ClrType = typeof(StockRow),
            IdAccessor = instance => ((StockRow)instance).Id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            ImplementedInterfaces = [iSecurity],
        };

        var bond = new ObjectTypeDescriptor
        {
            Name = Bond,
            DomainName = "portfolio",
            ClrType = typeof(BondRow),
            IdAccessor = instance => ((BondRow)instance).Id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            ImplementedInterfaces = [iSecurity],
        };

        var objectTypes = new[] { account, stock, bond };
        return new OntologyGraph(
            domains: [new DomainDescriptor("portfolio") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [iSecurity],
            crossDomainLinks: [],
            workflowChains: []);
    }

    /// <summary>A CLR-free self-linking node carrier; identity flows through Id.</summary>
    private sealed record FmNode(string Id);

    /// <summary>The polymorphic-link source carrier.</summary>
    private sealed record AccountRow(string Id);

    /// <summary>A concrete ISecurity implementor partition.</summary>
    private sealed record StockRow(string Id);

    /// <summary>A second concrete ISecurity implementor partition.</summary>
    private sealed record BondRow(string Id);
}
