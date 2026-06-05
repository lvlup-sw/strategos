using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Integration;

// ---------------------------------------------------------------------------
// DR-8 (t14): cross-provider FAILURE-MODE MATRIX — the IN-MEMORY half.
//
// The matrix pins four edge failure modes. Modes 1-3 fail safely AND
// IDENTICALLY across backends (same typed errors, same empty-set posture). This
// file pins the in-memory provider/evaluator side; the Npgsql side (gated on
// STRATEGOS_PG_TEST_CONN) is asserted in
// Strategos.Ontology.Npgsql.Tests/Integration/EdgeFailureModeMatrixNpgsqlTests.
//
//   1. Non-existent endpoint — relating to an UNSTORED endpoint id surfaces a
//      typed RelationEndpointNotFoundException and leaves NO dangling row.
//   2. Self-loop (x, link, x) — rejected with a typed SelfLoopNotAllowedException
//      unless the link declares AllowsSelfLoop; NEVER a silent drop.
//   3. Zero relations — traversal from an instance with no relation rows returns
//      an EMPTY set, never all target-type items (the #114 regression).
//   4. Ambiguous / unresolvable hop target without override — both backends are
//      SAFE (neither mis-routes to a wrong/arbitrary partition), but they
//      DIVERGE on HOW (review M2, see the per-test remarks below): the in-memory
//      evaluator degrades to the relation row's OWN stored far-node target
//      (no throw), whereas the Npgsql provider's ResolveHopTargetDescriptorName
//      REFUSES with a typed InvalidOperationException. This is a KNOWN backend
//      divergence (not a parity claim) rooted in the storage model — the
//      in-memory store records each row's TargetDescriptor, the SQL junction
//      table does not (it derives the target from the graph link) — and is
//      tracked for the #128 follow-up. (The compile-time half is AONT211, T5.)
//
// INV-8: identity by descriptor name, never typeof. INV-2 is an Npgsql concern.
// ---------------------------------------------------------------------------
public class EdgeFailureModeMatrixTests
{
    // A minimal CLR-typed edge ontology: a Node with a self-targeting "links_to"
    // link (so the self-loop mode is exercisable) plus a reified Edge association.
    public sealed record MatrixNode(string Id);

    public sealed record MatrixEdge(string Id, MatrixNode From, MatrixNode To, string Label);

    public sealed class MatrixOntology : DomainOntology
    {
        public override string DomainName => "matrix";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<MatrixNode>(obj =>
            {
                obj.Key(n => n.Id);
                obj.HasMany<MatrixNode>("links_to");
            });

            builder.Association<MatrixEdge>("MatrixEdge", a =>
            {
                a.Key(e => e.Id);
                a.Between(e => e.From).And(e => e.To);
                a.Property(e => e.Label).Required();
            });
        }
    }

    private const string NodeDescriptor = nameof(MatrixNode);
    private const string LinkName = "links_to";

    private static OntologyGraph BuildGraph()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<MatrixOntology>();
        return graphBuilder.Build();
    }

    private static InMemoryObjectSetProvider SeededProvider(params string[] ids)
    {
        var provider = new InMemoryObjectSetProvider(BuildGraph());
        foreach (var id in ids)
        {
            provider.Seed(new MatrixNode(id), id, NodeDescriptor);
        }

        return provider;
    }

    // -----------------------------------------------------------------------
    // Mode 1 — non-existent endpoint
    // -----------------------------------------------------------------------

    [Test]
    public async Task Relate_NonExistentEndpoint_ThrowsTypedError()
    {
        // Only the source is stored; the target id resolves to no stored instance.
        var provider = SeededProvider("a");
        IObjectSetWriter writer = provider;

        await Assert.That(async () =>
                await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "ghost"))
            .Throws<RelationEndpointNotFoundException>();

        // Eager validation: no dangling row survives the failed relate.
        var rows = provider.GetRelations(NodeDescriptor, "a", LinkName);
        await Assert.That(rows).IsEmpty();
    }

    // -----------------------------------------------------------------------
    // Mode 2 — self-loop
    // -----------------------------------------------------------------------

    [Test]
    public async Task SelfLoop_WhenLinkDisallows_ThrowsTypedError_NeverSilentDrop()
    {
        // "links_to" defaults to AllowsSelfLoop = false.
        var provider = SeededProvider("a");
        IObjectSetWriter writer = provider;

        // The relate is REFUSED with a typed error — not silently dropped.
        await Assert.That(async () =>
                await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "a"))
            .Throws<SelfLoopNotAllowedException>();

        // No row exists: the self-loop neither succeeded nor silently vanished.
        var rows = provider.GetRelations(NodeDescriptor, "a", LinkName);
        await Assert.That(rows).IsEmpty();
    }

    [Test]
    public async Task SelfLoop_WhenLinkAllows_CreatesRow()
    {
        // The companion to the disallowed case: when the link permits self-loops,
        // (x, link, x) is a legitimate row — proving mode 2 is a POLICY, not a ban.
        var provider = SelfLoopAllowedProvider("a");
        IObjectSetWriter writer = provider;

        await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "a");

        var rows = provider.GetRelations(NodeDescriptor, "a", LinkName);
        await Assert.That(rows).HasCount().EqualTo(1);
        await Assert.That(rows[0].TargetId).IsEqualTo("a");
    }

    // -----------------------------------------------------------------------
    // Mode 3 — zero relations
    // -----------------------------------------------------------------------

    [Test]
    public async Task Traverse_ZeroRelations_ReturnsEmpty_NotAllTargets()
    {
        // Three nodes are stored and "a" -> "b" is related, but we traverse from
        // "c", which has NO relation rows. The result must be EMPTY — never the
        // whole MatrixNode partition (the #114 type-level-fetch regression).
        var provider = SeededProvider("a", "b", "c");
        IObjectSetWriter writer = provider;
        await writer.RelateAsync(NodeDescriptor, "a", LinkName, NodeDescriptor, "b");

        var traverse = TraverseFrom("c");
        var result = await provider.ExecuteAsync<MatrixNode>(traverse);

        await Assert.That(result.Items).IsEmpty();
    }

    // -----------------------------------------------------------------------
    // Mode 4 — ambiguous / unresolvable hop target without override
    // -----------------------------------------------------------------------

    [Test]
    public async Task Traverse_AmbiguousMultiRegistrationWithoutOverride_DegradesToStoredFarNode_NoThrow()
    {
        // M2 (honest parity): this asserts the IN-MEMORY provider's ACTUAL
        // behavior, which DIVERGES from the Npgsql provider's for this mode — the
        // two are NOT identical here, and the matrix no longer claims they are.
        //
        // An edge-view link whose target is NOT graph-resolvable (empty
        // TargetTypeName, no TargetSymbolKey, no override) and whose source carries
        // a relation row. The evaluator's graph-first hop resolution
        // (TryResolveAssociationHopDescriptor) REFUSES to bind the hop to an
        // association partition it cannot name from the graph — it never mis-routes
        // the edge view to all MatrixEdge items. Crucially, the in-memory path then
        // DEGRADES to the relation row's OWN stored far-node target
        // (ResolveTargetEndpoints reads row.TargetDescriptor / row.TargetId) and
        // does NOT throw: here the row's far endpoint is node "b", so the safe
        // resolution yields exactly that far node.
        //
        // DIVERGENCE (review M2 / #128 follow-up): the Npgsql provider has no
        // per-row stored target to degrade to (the SQL junction table records only
        // surrogate target_id, not a TargetDescriptor name), so its
        // ResolveHopTargetDescriptorName REFUSES the unresolvable hop with a typed
        // InvalidOperationException instead of degrading. Both are SAFE (neither
        // mis-routes), but they are not the SAME — see
        // EdgeFailureModeMatrixNpgsqlTests.Traverse_AmbiguousMultiRegistrationWithoutOverride_ThrowsAtRuntime.
        var (graph, resolver, items) = AmbiguousEdgeViewGraph();
        var evaluator = new InMemoryExpressionEvaluator(graph, resolver, idProjector: null);

        var root = new RootExpression(typeof(MatrixNode), NodeDescriptor);
        var filtered = new FilterExpression(root, (MatrixNode n) => n.Id == "a");
        // Request the EDGE VIEW via the legacy CLR-typed hop, with NO descriptor
        // override — the ambiguous/unresolvable case.
        var traverse = new TraverseLinkExpression(filtered, LinkName, typeof(MatrixEdge));

        var result = evaluator.Evaluate<object>(traverse, items);

        // The edge-view hop was REFUSED (no association partition resolvable from
        // the graph), so NOT a single MatrixEdge association object came back; the
        // result is the safe far-node degradation, never the mis-routed edge
        // partition.
        await Assert.That(result.Any(o => o is MatrixEdge)).IsFalse();
        await Assert.That(result.OfType<MatrixNode>().Select(n => n.Id)).Contains("b");
    }

    private static (OntologyGraph Graph, RelationResolver Resolver, Func<string, IReadOnlyList<object>> Items)
        AmbiguousEdgeViewGraph()
    {
        Func<object, object?> id = instance => instance switch
        {
            MatrixNode n => n.Id,
            MatrixEdge e => e.Id,
            _ => null,
        };

        // Node descriptor with an edge-view link whose target names NOTHING the
        // graph can resolve: empty TargetTypeName, no TargetSymbolKey, no override.
        var node = new ObjectTypeDescriptor
        {
            Name = NodeDescriptor,
            DomainName = "matrix",
            ClrType = typeof(MatrixNode),
            IdAccessor = id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Links =
            [
                new LinkDescriptor(LinkName, string.Empty, LinkCardinality.OneToMany),
            ],
        };

        // Two association descriptors backed by the SAME CLR type (MatrixEdge):
        // the legacy single-registration CLR edge-view fallback is therefore
        // AMBIGUOUS (matches != 1), so it must NOT pick either partition.
        var edgeA = AmbiguousAssociation("MatrixEdgeA", id);
        var edgeB = AmbiguousAssociation("MatrixEdgeB", id);

        var objectTypes = new[] { node, edgeA, edgeB };
        var graph = new OntologyGraph(
            domains: [new DomainDescriptor("matrix") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);

        // The relate row names a far NODE endpoint ("b") with no association object
        // id, so the safe (non-mis-routed) resolution is the far node.
        var rows = new Dictionary<(string, string, string), IReadOnlyList<RelationRow>>(
            new TripleComparer())
        {
            [(NodeDescriptor, "a", LinkName)] = [new RelationRow(NodeDescriptor, "b", null)],
        };

        RelationResolver resolver = (src, srcId, link) =>
            rows.TryGetValue((src, srcId, link), out var r) ? r : [];

        var partitions = new Dictionary<string, IReadOnlyList<object>>(StringComparer.Ordinal)
        {
            [NodeDescriptor] = [new MatrixNode("a"), new MatrixNode("b")],
            ["MatrixEdgeA"] = [new MatrixEdge("e-1", new MatrixNode("a"), new MatrixNode("b"), "x")],
            ["MatrixEdgeB"] = [new MatrixEdge("e-2", new MatrixNode("a"), new MatrixNode("b"), "y")],
        };

        Func<string, IReadOnlyList<object>> items = name =>
            partitions.TryGetValue(name, out var p) ? p : [];

        return (graph, resolver, items);
    }

    private static ObjectTypeDescriptor AmbiguousAssociation(string name, Func<object, object?> id) =>
        new()
        {
            Name = name,
            DomainName = "matrix",
            ClrType = typeof(MatrixEdge),
            IdAccessor = id,
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("From", NodeDescriptor),
                new AssociationEndpoint("To", NodeDescriptor),
            ],
        };

    private sealed class TripleComparer : IEqualityComparer<(string, string, string)>
    {
        public bool Equals((string, string, string) x, (string, string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.Ordinal)
            && string.Equals(x.Item2, y.Item2, StringComparison.Ordinal)
            && string.Equals(x.Item3, y.Item3, StringComparison.Ordinal);

        public int GetHashCode((string, string, string) obj) =>
            HashCode.Combine(obj.Item1, obj.Item2, obj.Item3);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ObjectSetExpression TraverseFrom(string nodeId)
    {
        var root = new RootExpression(typeof(MatrixNode), NodeDescriptor);
        var filtered = new FilterExpression(root, (MatrixNode n) => n.Id == nodeId);
        return new TraverseLinkExpression(filtered, LinkName, typeof(MatrixNode));
    }

    private static InMemoryObjectSetProvider SelfLoopAllowedProvider(params string[] ids)
    {
        var baseGraph = BuildGraph();
        var rewritten = baseGraph.ObjectTypes
            .Select(ot => ot with
            {
                Links = ot.Links
                    .Select(l => l with { AllowsSelfLoop = true })
                    .ToList(),
            })
            .ToList();

        var graph = new OntologyGraph(
            domains: baseGraph.Domains,
            objectTypes: rewritten,
            interfaces: baseGraph.Interfaces,
            crossDomainLinks: baseGraph.CrossDomainLinks,
            workflowChains: baseGraph.WorkflowChains);

        var provider = new InMemoryObjectSetProvider(graph);
        foreach (var id in ids)
        {
            provider.Seed(new MatrixNode(id), id, NodeDescriptor);
        }

        return provider;
    }
}
