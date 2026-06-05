using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

// ---------------------------------------------------------------------------
// DR-10 (#128): a hop's TARGET descriptor must be resolved from the ONTOLOGY
// GRAPH — the source descriptor's LinkDescriptor for the traversed link
// (TargetTypeName, falling back to TargetSymbolKey), honoring an explicit
// TraverseLinkExpression.TargetDescriptorName — NEVER re-derived from
// typeof(TLinked).
//
// typeof(TLinked) mis-routes the moment one CLR type backs MORE THAN ONE
// descriptor (multi-registration): the type→descriptor map is ambiguous, so a
// CLR-type match picks the FIRST descriptor in enumeration order rather than
// the partition the LINK actually declares. It is also flatly impossible for a
// SymbolKey-only (ClrType == null) target. INV-8: no reflection on the hop's
// identity path.
// ---------------------------------------------------------------------------

// A single CLR type that backs TWO association descriptors. Storing the
// descriptor name on the instance lets the test assert WHICH partition was
// resolved, without the evaluator ever reflecting over this shape for identity.
public sealed record MultiEdge(string Id, string Partition);

public class InMemoryTraversalIdentityTests
{
    private const string Origin = "MultiOrigin";
    private const string EdgeWrong = "EdgeWrong"; // first in enumeration — the typeof() trap
    private const string EdgeRight = "EdgeRight"; // the link's DECLARED target
    private const string LinkName = "toEdge";

    // Builds a graph where typeof(MultiEdge) is ambiguous: it backs both
    // EdgeWrong (enumerated first) and EdgeRight. The Origin link declares its
    // target as EdgeRight, so a graph-driven hop MUST route there; a
    // typeof(MultiEdge)-driven hop routes to EdgeWrong (the first CLR match).
    private static OntologyGraph BuildMultiRegistrationGraph()
    {
        Func<object, object?> originId = instance => ((OriginNode)instance).Key;
        Func<object, object?> edgeId = instance => ((MultiEdge)instance).Id;

        var origin = new ObjectTypeDescriptor
        {
            Name = Origin,
            DomainName = "multi",
            ClrType = typeof(OriginNode),
            IdAccessor = originId,
            Links =
            [
                // DECLARED target is EdgeRight — the partition the hop must hit.
                new LinkDescriptor(LinkName, EdgeRight, LinkCardinality.OneToMany),
            ],
        };

        var edgeWrong = new ObjectTypeDescriptor
        {
            Name = EdgeWrong,
            DomainName = "multi",
            ClrType = typeof(MultiEdge), // SAME CLR type as EdgeRight.
            IdAccessor = edgeId,
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("From", Origin),
                new AssociationEndpoint("To", Origin),
            ],
        };

        var edgeRight = new ObjectTypeDescriptor
        {
            Name = EdgeRight,
            DomainName = "multi",
            ClrType = typeof(MultiEdge), // SAME CLR type as EdgeWrong.
            IdAccessor = edgeId,
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("From", Origin),
                new AssociationEndpoint("To", Origin),
            ],
        };

        // EdgeWrong is enumerated FIRST, so a typeof()-keyed lookup lands there.
        var objectTypes = new ObjectTypeDescriptor[] { origin, edgeWrong, edgeRight };

        return new OntologyGraph(
            domains: [new DomainDescriptor("multi") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    [Test]
    public async Task Traverse_TargetClrTypeRegisteredUnderTwoDescriptors_RoutesToLinkDeclaredTarget()
    {
        var graph = BuildMultiRegistrationGraph();

        // The relate row points at the EdgeRight partition (TargetDescriptor)
        // and its association object id is "edge-right" — an id that exists ONLY
        // in EdgeRight's partition. EdgeWrong's partition holds a DECOY with a
        // different id, so a mis-routed hop yields the decoy or empty, never the
        // correct edge.
        RelationResolver relations = (sd, si, ln) =>
            sd == Origin && si == "o1" && ln == LinkName
                ? [new RelationRow(EdgeRight, "n/a", AssociationObjectId: "edge-right")]
                : [];

        var evaluator = new InMemoryExpressionEvaluator(graph, relations, idProjector: null);

        Func<string, IReadOnlyList<object>> resolver = name => name switch
        {
            Origin => new object[] { new OriginNode("o1") },
            EdgeRight => new object[] { new MultiEdge("edge-right", EdgeRight) },
            EdgeWrong => new object[] { new MultiEdge("edge-wrong", EdgeWrong) },
            _ => [],
        };

        var root = new RootExpression(typeof(OriginNode), Origin);
        // TraverseLink<MultiEdge>("toEdge"): requests the edge view. The hop
        // target descriptor must be resolved from the link (EdgeRight), NOT from
        // typeof(MultiEdge) (which would pick EdgeWrong, the first CLR match).
        var traverse = new TraverseLinkExpression(root, LinkName, typeof(MultiEdge));

        var result = evaluator.Evaluate<MultiEdge>(traverse, resolver);

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo("edge-right");
        await Assert.That(result[0].Partition).IsEqualTo(EdgeRight);
    }
}

// A distinct CLR type for the source so it never collides with MultiEdge in
// the type→descriptor reverse index.
public sealed record OriginNode(string Key);
