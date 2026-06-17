using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.Npgsql.Schema;
using Strategos.Ontology.Npgsql.Tests.Integration;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Tests.Integration;

namespace Strategos.Ontology.Npgsql.Tests.Parity;

// ---------------------------------------------------------------------------
// G-18 / DR-18 (#116) — the cross-provider PARITY PROOF for the v2.9.0 edge
// layer. The corpus is reconciled per a known expressibility limit: a
// SymbolKey-ONLY interface fan-out is NOT expressible — an interface carries a
// CLR type, so a CLR-free (ClrType == null) descriptor cannot also be a
// polymorphic interface target. The proof therefore SPLITS into two dimensions
// that together cover the edge surface:
//
//   Dimension A (POLYGLOT, SymbolKey-only + MONOMORPHIC) — a rationale ontology
//     (decisions/constraints + Supersedes/Motivates/ConflictsWith reified
//     associations) declared CLR-free and monomorphic, run relate -> traverse ->
//     validate through BOTH the in-memory evaluator and the Npgsql provider,
//     asserting identical observable results. Proves the CLR-free polyglot edge
//     path (INV-8: identity by descriptor name / SymbolKey, never typeof).
//
//   Dimension B (POLYMORPHIC) — a CLR INTERFACE-typed link (Account.Holdings ->
//     ISecurity, implemented by Stock and Bond), asserting the relate fan-out
//     routes to per-descriptor tables and traversal UNION-reads them — both
//     providers, identical observable results.
//
// Live-EXECUTION variants gate [SkipIfNoPostgres] (they need a reachable
// Postgres; the default lane has none, so they SKIP). The in-memory replay and
// the generated-SQL-shape assertions run here, unconditionally.
//
// INV-2: raw Npgsql/pgvector throughout — no Marten/Wolverine.
// ---------------------------------------------------------------------------
public class RationaleCorpusParityTests
{
    // =======================================================================
    // Dimension A — POLYGLOT (SymbolKey-only + MONOMORPHIC), both providers.
    // =======================================================================

    [Test]
    public async Task RationaleCorpus_SymbolKeyOnlyMonomorphic_IdenticalAcrossProviders()
    {
        var fixture = RationaleOntologyFixture.Build();
        var graph = fixture.Graph;

        // The corpus is CLR-free AND monomorphic: every descriptor names itself
        // only by SymbolKey (ClrType == null), and every link resolves to exactly
        // ONE target descriptor (no interface fan-out). This is the precondition
        // that distinguishes Dimension A from Dimension B and the reason the two
        // dimensions are SPLIT (a SymbolKey-only interface fan-out is inexpressible).
        foreach (var descriptor in graph.ObjectTypes)
        {
            await Assert.That(descriptor.ClrType).IsNull();
            await Assert.That(descriptor.SymbolKey).IsNotNull();
        }

        // Each corpus link resolves monomorphically — a single graph-resolved
        // target descriptor, never a polymorphic interface fan-out.
        await AssertMonomorphic(graph, RationaleOntologyFixture.Decision, RationaleOntologyFixture.LinkSupersedesEdge);
        await AssertMonomorphic(graph, RationaleOntologyFixture.Decision, RationaleOntologyFixture.LinkSupersededDecision);
        await AssertMonomorphic(graph, RationaleOntologyFixture.Constraint, RationaleOntologyFixture.LinkMotivatesEdge);
        await AssertMonomorphic(graph, RationaleOntologyFixture.Decision, RationaleOntologyFixture.LinkConflictsWithEdge);

        // The corpus traversals, keyed for cross-backend comparison.
        var traversals = BuildTraversals(fixture);

        // OBSERVABLE-RESULT parity (in-memory oracle): the SAME corpus relate rows,
        // replayed through the in-memory evaluator, produce a deterministic set per
        // traversal. This is the cross-provider reference the Npgsql live variant
        // reproduces; here it pins the oracle is non-trivial (the corpus relates a
        // decision to its superseding edges + far node, etc.).
        var oracle = Project(new InMemoryRationaleEvaluator(fixture), traversals);
        await Assert.That(oracle.Values.Any(rows => rows.Count > 0)).IsTrue();

        // SQL-SHAPE parity (DB-free): the Npgsql provider's graph-driven resolvers
        // must route each monomorphic corpus traversal to the SAME junction the
        // SymbolKey reverse index names — proving the CLR-free path produces the
        // observable shape the in-memory oracle implies, without a live DB.
        foreach (var (key, traversal) in traversals)
        {
            var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
                graph, traversal.SourceDescriptor, traversal.LinkName, targetDescriptorOverride: null);

            var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
                "public", hop.SourceTable, hop.SourceKeyProperty, hop.JunctionTable, hop.TargetTable);

            await Assert.That(sql).Contains($"\"public\".\"{hop.JunctionTable}\" j ON j.source_id = s.id");
            await Assert.That(sql).Contains($"\"public\".\"{hop.TargetTable}\" t ON t.id = j.target_id");
            await Assert.That(sql).Contains("s.data->>'Id' = @srcId");
            // INV-2: raw Npgsql — no event-store machinery leaks into the SQL.
            await Assert.That(sql).DoesNotContain("Marten");
            await Assert.That(sql).DoesNotContain("Wolverine");
            // A monomorphic hop is a SINGLE join chain, never a polymorphic UNION.
            await Assert.That(sql).DoesNotContain("UNION ALL");
        }
    }

    [Test]
    [SkipIfNoPostgres]
    public async Task RationaleCorpus_SymbolKeyOnlyMonomorphic_LiveExecutionIdenticalAcrossProviders()
    {
        var connectionString = Environment.GetEnvironmentVariable(SkipIfNoPostgresAttribute.ConnectionEnvVar);
        await Assert.That(connectionString).IsNotNull();

        var fixture = RationaleOntologyFixture.Build();
        var traversals = BuildTraversals(fixture);

        // In-memory ORACLE replay.
        var oracle = Project(new InMemoryRationaleEvaluator(fixture), traversals);

        // Npgsql replay of the SAME corpus + relate rows, read back through the
        // SAME traversals.
        await using var harness = await NpgsqlRationaleHarness.CreateAsync(connectionString!, fixture.Graph);
        await harness.SeedAsync(fixture.InstancesByDescriptor);
        await harness.ReplayRelationsAsync(fixture.Relations);

        var actual = Project(harness.Evaluator, traversals);

        // Identical observable results across providers.
        foreach (var (key, expected) in oracle)
        {
            await Assert.That(actual[key]).IsEquivalentTo(expected);
        }
    }

    // =======================================================================
    // Dimension B — POLYMORPHIC (CLR interface-typed link), both providers.
    // =======================================================================

    [Test]
    public async Task RationaleCorpus_ClrInterfacePolymorphic_FanOutIdenticalAcrossProviders()
    {
        var graph = BuildPolymorphicGraph();

        // --- IN-MEMORY observable result: a single Account.Holdings traversal
        //     serves the HETEROGENEOUS set (the Stock row and the Bond row), each
        //     resolved from its own descriptor partition (per-row resolution). ---
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
        var inMemoryTargets = evaluator.Evaluate<object>(traverse, resolver);

        var inMemoryStock = inMemoryTargets.OfType<StockRow>().SingleOrDefault();
        var inMemoryBond = inMemoryTargets.OfType<BondRow>().SingleOrDefault();
        await Assert.That(inMemoryTargets).HasCount().EqualTo(2);
        await Assert.That(inMemoryStock?.Id).IsEqualTo("s1");
        await Assert.That(inMemoryBond?.Id).IsEqualTo("b1");

        // --- NPGSQL observable result (SQL-shape, DB-free): the SAME polymorphic
        //     link fans the RELATE out to per-descriptor junction tables and the
        //     TRAVERSE UNION-reads them — the schema-level counterpart of the
        //     in-memory per-row set, so both backends observe {Stock s1, Bond b1}. ---

        // Relate fan-out: each target routes to its OWN per-descriptor table.
        var toStock = SqlGenerator.JunctionTableNameFor(
            PgVectorObjectSetProvider.ResolveRelateJunction(graph, Account, PolyLink, Stock));
        var toBond = SqlGenerator.JunctionTableNameFor(
            PgVectorObjectSetProvider.ResolveRelateJunction(graph, Account, PolyLink, Bond));
        await Assert.That(toStock).IsEqualTo("account_holdings_stock");
        await Assert.That(toBond).IsEqualTo("account_holdings_bond");
        await Assert.That(toStock).IsNotEqualTo(toBond);

        // Traverse fan-out: a single anchored hop resolves ONE physical hop per
        // resolved descriptor, each reading its own per-descriptor table, and the
        // hops UNION-read so one traversal observes BOTH partitions.
        var hops = PgVectorObjectSetProvider.ResolveTraversalHops(
            graph, Account, PolyLink, targetDescriptorOverride: null);
        await Assert.That(hops.Count).IsEqualTo(2);

        var hopTables = hops.Select(h => h.JunctionTable).OrderBy(n => n, StringComparer.Ordinal).ToList();
        await Assert.That(hopTables).Contains("account_holdings_bond");
        await Assert.That(hopTables).Contains("account_holdings_stock");

        var unionSql = SqlGenerator.BuildPolymorphicTraversalSql("public", hops);
        await Assert.That(unionSql).Contains("UNION ALL");
        await Assert.That(unionSql).Contains("\"public\".\"account_holdings_stock\" j");
        await Assert.That(unionSql).Contains("\"public\".\"account_holdings_bond\" j");
        await Assert.That(unionSql).Contains("\"public\".\"stock\" t");
        await Assert.That(unionSql).Contains("\"public\".\"bond\" t");

        // PARITY: the in-memory per-row resolution and the Npgsql fan-out resolve
        // the SAME two descriptor partitions for the single polymorphic link — the
        // observable target descriptor set is identical across providers.
        var inMemoryPartitions = new[]
        {
            inMemoryStock is null ? null : Stock,
            inMemoryBond is null ? null : Bond,
        }.Where(n => n is not null).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var npgsqlPartitions = hops.Select(h => h.TargetDescriptorName)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        await Assert.That(npgsqlPartitions).IsEquivalentTo(inMemoryPartitions);
    }

    // NOTE: a live-execution polymorphic fan-out variant
    // (RationaleCorpus_ClrInterfacePolymorphic_LiveFanOutIdenticalAcrossProviders)
    // is intentionally NOT added here. NpgsqlRationaleHarness provisions the
    // monomorphic corpus's single-table junctions; a polymorphic live proof needs
    // a per-(link, target-descriptor) junction-provisioning + UNION-read harness
    // that does not yet exist. Rather than ship a hollow [SkipIfNoPostgres] stub
    // that asserts nothing, the polymorphic fan-out is proven DB-free above
    // (relate routing + UNION traversal shape, both providers). The live harness
    // is deferred — see the integration report.

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------

    private const string Account = "Account";
    private const string SecurityInterface = "ISecurity";
    private const string Stock = "Stock";
    private const string Bond = "Bond";
    private const string PolyLink = "Holdings";

    private static async Task AssertMonomorphic(OntologyGraph graph, string sourceDescriptor, string linkName)
    {
        var link = graph.ObjectTypes
            .Single(o => o.Name == sourceDescriptor)
            .Links.Single(l => l.Name == linkName);
        var targets = PgVectorObjectSetProvider.ResolveLinkTargetDescriptors(graph, link);
        await Assert.That(targets.Count).IsEqualTo(1);
    }

    private static Dictionary<string, RationaleTraversal> BuildTraversals(RationaleOntologyFixture fixture) =>
        new(StringComparer.Ordinal)
        {
            ["supersedesEdge"] = new(
                fixture.TraverseSupersedesEdges("D1"),
                RationaleOntologyFixture.Decision, "D1", RationaleOntologyFixture.LinkSupersedesEdge),
            ["supersededDecision"] = new(
                fixture.TraverseSupersededDecision("D1"),
                RationaleOntologyFixture.Decision, "D1", RationaleOntologyFixture.LinkSupersededDecision),
            ["motivatesEdge"] = new(
                fixture.TraverseMotivatesEdges("C1"),
                RationaleOntologyFixture.Constraint, "C1", RationaleOntologyFixture.LinkMotivatesEdge),
            ["conflictsWithEdge"] = new(
                fixture.TraverseConflictsWithEdges("D1"),
                RationaleOntologyFixture.Decision, "D1", RationaleOntologyFixture.LinkConflictsWithEdge),
        };

    private static Dictionary<string, IReadOnlyList<(string Id, string? Title, string? Rationale, string? Weight, string? Severity)>>
        Project(IRationaleEvaluator evaluator, IReadOnlyDictionary<string, RationaleTraversal> traversals)
    {
        var projected = new Dictionary<string, IReadOnlyList<(string, string?, string?, string?, string?)>>(StringComparer.Ordinal);
        foreach (var (key, traversal) in traversals)
        {
            projected[key] = evaluator.Evaluate(traversal)
                .Select(n => (n.Id, n.Get("title"), n.Get("rationale"), n.Get("weight"), n.Get("severity")))
                .ToList();
        }

        return projected;
    }

    // A graph where Account.Holdings targets the ISecurity INTERFACE, implemented
    // by Stock and Bond — the polymorphic (CLR interface-typed) dimension. Mirrors
    // JunctionDmlRoutingTests.BuildPolymorphicGraph so the parity proof pins the
    // SAME routing seam the DR-11b unit tests assert. CLR-typed (the interface
    // carries a CLR type), which is exactly why this cannot be SymbolKey-only.
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

    /// <summary>The polymorphic-link source carrier; identity flows through Id.</summary>
    private sealed record AccountRow(string Id);

    /// <summary>A concrete ISecurity implementor partition.</summary>
    private sealed record StockRow(string Id);

    /// <summary>A second concrete ISecurity implementor partition.</summary>
    private sealed record BondRow(string Id);
}
