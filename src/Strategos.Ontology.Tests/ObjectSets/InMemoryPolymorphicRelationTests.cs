using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

// ---------------------------------------------------------------------------
// DR-11 (junction posture, #128) — in-memory side of the polymorphic-target
// resolution. Where the Npgsql provider fans a polymorphic link out into one
// junction table PER resolved descriptor (T2), the in-memory provider resolves
// each relation row by its STORED TargetDescriptor. A single link from one
// source can carry rows pointing at DIFFERENT descriptor partitions; the
// evaluator must route EACH row to its own partition (graceful per-row
// resolution) rather than refusing the heterogeneous set or collapsing it onto
// one partition. INV-8: routing is by the stored descriptor NAME, never typeof.
// ---------------------------------------------------------------------------

public class InMemoryPolymorphicRelationTests
{
    private const string Account = "Account";
    private const string SecurityInterface = "ISecurity"; // polymorphic link target (a node, not an association)
    private const string StockPartition = "Stock";
    private const string BondPartition = "Bond";
    private const string LinkName = "Holdings";

    [Test]
    public async Task InMemoryRelate_PolymorphicTarget_RoutesToCorrectDescriptorPartition()
    {
        var graph = BuildPolymorphicTargetGraph();

        // One source ("acc1") relates along "Holdings" to TWO targets in DIFFERENT
        // descriptor partitions: a Stock row and a Bond row. Each row carries its
        // own TargetDescriptor — the evaluator must resolve each from its own
        // partition (no AssociationObjectId, so this is the node-endpoint hop).
        RelationResolver relations = (sd, si, ln) =>
            sd == Account && si == "acc1" && ln == LinkName
                ?
                [
                    new RelationRow(StockPartition, "s1", AssociationObjectId: null),
                    new RelationRow(BondPartition, "b1", AssociationObjectId: null),
                ]
                : [];

        var evaluator = new InMemoryExpressionEvaluator(graph, relations, idProjector: null);

        Func<string, IReadOnlyList<object>> resolver = name => name switch
        {
            Account => new object[] { new AccountNode("acc1") },
            StockPartition => new object[] { new Stock("s1", StockPartition) },
            BondPartition => new object[] { new Bond("b1", BondPartition) },
            _ => [],
        };

        var root = new RootExpression(typeof(AccountNode), Account);
        var traverse = new TraverseLinkExpression(root, LinkName, typeof(object));

        // The traversal returns BOTH targets, each resolved from its own partition
        // — the heterogeneous set is served, not refused.
        var result = evaluator.Evaluate<object>(traverse, resolver);

        await Assert.That(result).HasCount().EqualTo(2);

        var stock = result.OfType<Stock>().SingleOrDefault();
        var bond = result.OfType<Bond>().SingleOrDefault();

        await Assert.That(stock).IsNotNull();
        await Assert.That(stock!.Id).IsEqualTo("s1");
        await Assert.That(stock.Partition).IsEqualTo(StockPartition);

        await Assert.That(bond).IsNotNull();
        await Assert.That(bond!.Id).IsEqualTo("b1");
        await Assert.That(bond.Partition).IsEqualTo(BondPartition);
    }

    // A graph whose Account.Holdings link targets the polymorphic ISecurity node
    // (not an association, no AssociationObjectId on the rows), with two concrete
    // implementor partitions Stock and Bond. The evaluator routes each relation
    // row to its stored partition.
    private static OntologyGraph BuildPolymorphicTargetGraph()
    {
        Func<object, object?> accountId = instance => ((AccountNode)instance).Key;
        Func<object, object?> stockId = instance => ((Stock)instance).Id;
        Func<object, object?> bondId = instance => ((Bond)instance).Id;

        var account = new ObjectTypeDescriptor
        {
            Name = Account,
            DomainName = "portfolio",
            ClrType = typeof(AccountNode),
            IdAccessor = accountId,
            Links =
            [
                // Declared target is the polymorphic ISecurity node — resolution
                // does not collapse the heterogeneous rows onto one partition.
                new LinkDescriptor(LinkName, SecurityInterface, LinkCardinality.OneToMany),
            ],
        };

        var stock = new ObjectTypeDescriptor
        {
            Name = StockPartition,
            DomainName = "portfolio",
            ClrType = typeof(Stock),
            IdAccessor = stockId,
        };

        var bond = new ObjectTypeDescriptor
        {
            Name = BondPartition,
            DomainName = "portfolio",
            ClrType = typeof(Bond),
            IdAccessor = bondId,
        };

        var objectTypes = new ObjectTypeDescriptor[] { account, stock, bond };
        return new OntologyGraph(
            domains: [new DomainDescriptor("portfolio") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    // Two concrete security shapes sharing the polymorphic Holdings link target.
    // Storing the partition name lets the test assert WHICH partition each row
    // resolved to, without the evaluator reflecting over the shape for identity.
    private sealed record Stock(string Id, string Partition);

    private sealed record Bond(string Id, string Partition);

    // A distinct CLR type for the source so it never collides with Stock/Bond in
    // the type->descriptor reverse index.
    private sealed record AccountNode(string Key);
}
