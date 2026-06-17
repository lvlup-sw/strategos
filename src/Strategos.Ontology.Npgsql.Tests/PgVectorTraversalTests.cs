using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// DR-7/DR-10 (Ontology Edge Foundation, t10): unit tests for the Postgres
/// instance-anchored <c>TraverseLink</c> lowering — a
/// <c>vertex ⋈ junction ⋈ vertex</c> SQL join over the T8 junction tables.
///
/// These assert generated parameterized SQL SHAPES and the graph-driven hop
/// resolution only — NO live database. They mirror
/// <see cref="PgVectorEdgeSchemaTests"/> / <see cref="PgVectorRelateTests"/>:
/// raw Npgsql + pgvector (INV-2 — no Marten/Wolverine), and the dispatch/SQL
/// seams are exposed as internal statics so the full code path is pinned
/// without a live <see cref="global::Npgsql.NpgsqlDataSource"/>.
///
/// DR-10 keystone (INV-8): the hop's TARGET descriptor (hence target table) is
/// resolved from the ONTOLOGY GRAPH via the source link — explicit override →
/// link <c>TargetTypeName</c> → <c>TargetSymbolKey</c> reverse index — and NEVER
/// re-derived from <c>typeof(TLinked)</c>. The Npgsql resolver mirrors T2's
/// <c>InMemoryExpressionEvaluator.ResolveHopTargetDescriptor</c> posture: the
/// override is authoritative and an unresolved multi-registration is REFUSED
/// (typed error) rather than mis-routed to the first CLR match.
/// </summary>
public class PgVectorTraversalTests
{
    private const string Origin = "MultiOrigin";
    private const string EdgeWrong = "EdgeWrong"; // first in enumeration — the typeof() trap
    private const string EdgeRight = "EdgeRight"; // the link's DECLARED target
    private const string LinkName = "toEdge";

    // -----------------------------------------------------------------------
    // SQL SHAPE — vertex ⋈ junction ⋈ vertex, filtered by the source's
    // business id; target table is the LINK-DECLARED descriptor's table.
    // -----------------------------------------------------------------------

    [Test]
    public async Task Traverse_InstanceAnchored_LowersToVertexJunctionVertexJoin()
    {
        // A pure link "WrittenBy" from Document -> Author. Traversing FROM a
        // specific Document instance (addressed by its BUSINESS id) joins the
        // source endpoint table -> junction -> target endpoint table on the
        // surrogate uuids, projecting the target rows.
        var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            schema: "public",
            sourceTableName: "document",
            sourceKeyProperty: "Id",
            junctionTableName: "document_written_by",
            targetTableName: "author");

        // Projects the TARGET rows (id + data), aliased as the target vertex.
        await Assert.That(sql).Contains("SELECT t.id, t.data");

        // Anchored at the SOURCE endpoint table, aliased s.
        await Assert.That(sql).Contains("FROM \"public\".\"document\" s");

        // Joined to the T8 junction table on the source surrogate uuid...
        await Assert.That(sql).Contains("JOIN \"public\".\"document_written_by\" j ON j.source_id = s.id");

        // ...then to the TARGET endpoint table on the target surrogate uuid.
        await Assert.That(sql).Contains("JOIN \"public\".\"author\" t ON t.id = j.target_id");

        // Filtered by the SOURCE's BUSINESS id (the data jsonb key field),
        // parameter-bound — never interpolated.
        await Assert.That(sql).Contains("s.data->>'Id' = @srcId");
        await Assert.That(sql).Contains("@srcId");

        // INV-2: raw Npgsql/pgvector — no event-store machinery.
        await Assert.That(sql).DoesNotContain("Marten");
        await Assert.That(sql).DoesNotContain("Wolverine");
    }

    [Test]
    public async Task Traverse_HopTarget_ResolvedFromGraphNotTypeof()
    {
        // DR-10 keystone (INV-8): typeof(MultiEdge) backs TWO descriptors
        // (EdgeWrong enumerated FIRST, EdgeRight second). The Origin link
        // DECLARES its target as EdgeRight, so a graph-driven hop MUST route the
        // target table to EdgeRight; a typeof(MultiEdge)-driven hop would land on
        // EdgeWrong (the first CLR match). The resolved hop carries the
        // EdgeRight-derived target table — proving graph-not-typeof.
        var graph = BuildMultiRegistrationGraph();

        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph, sourceDescriptorName: Origin, linkName: LinkName, targetDescriptorOverride: null);

        await Assert.That(hop.TargetDescriptorName).IsEqualTo(EdgeRight);
        await Assert.That(hop.TargetTable).IsEqualTo("edge_right");
        await Assert.That(hop.TargetTable).IsNotEqualTo("multi_edge"); // typeof(MultiEdge) trap
        await Assert.That(hop.SourceTable).IsEqualTo("multi_origin");
        await Assert.That(hop.SourceKeyProperty).IsEqualTo("Key");
        // Junction agrees with the T8 BuildJunctionTableDdl lowering: {src}_{snake(link)}.
        await Assert.That(hop.JunctionTable).IsEqualTo("multi_origin_to_edge");
    }

    [Test]
    public async Task Traverse_ExplicitTargetDescriptorOverride_DisambiguatesMultiRegistration()
    {
        // DR-10: when the link declares a NODE target but the caller wants a
        // different (multi-registered) partition, the explicit override is
        // AUTHORITATIVE — it names the exact descriptor, so no CLR Type
        // participates in resolution. Here the link declares the NODE (Origin),
        // and the override selects EdgeRight (NOT EdgeWrong, the first CLR match).
        var graph = BuildNodeLinkAttributedGraph();

        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph, sourceDescriptorName: Origin, linkName: LinkName, targetDescriptorOverride: EdgeRight);

        await Assert.That(hop.TargetDescriptorName).IsEqualTo(EdgeRight);
        await Assert.That(hop.TargetTable).IsEqualTo("edge_right");
    }

    [Test]
    public async Task Traverse_LinkTargetBySymbolKeyOnly_ResolvesTargetTableFromGraph()
    {
        // DR-10 (INV-8): a link whose target is named ONLY by TargetSymbolKey
        // (empty TargetTypeName, ClrType == null target) must still resolve its
        // target table from the graph — via the SymbolKey -> descriptor-name
        // reverse index — never typeof(TLinked). The old CLR path could never
        // match a ClrType == null target.
        const string targetSymbol = "scip-typescript ./edge.ts#PolyEdge";
        var graph = BuildSymbolKeyTargetGraph(targetSymbol);

        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph, sourceDescriptorName: Origin, linkName: LinkName, targetDescriptorOverride: null);

        await Assert.That(hop.TargetDescriptorName).IsEqualTo("SymEdge");
        await Assert.That(hop.TargetTable).IsEqualTo("sym_edge");
    }

    [Test]
    public async Task Traverse_AmbiguousTarget_NoOverride_Refuses()
    {
        // DR-10 / T2 posture: if a hop's target cannot be resolved from the graph
        // (here the link's declared NODE target is not the requested edge and the
        // caller supplied NO override to disambiguate the multi-registered edge),
        // the resolver REFUSES with a typed error rather than mis-routing to the
        // first CLR match. The link declares the NODE target (Origin), so the hop
        // resolves to the Origin table — which is unambiguous and CORRECT; to
        // exercise refusal we ask for a link that does not exist on the source.
        var graph = BuildNodeLinkAttributedGraph();

        await Assert.That(() => PgVectorObjectSetProvider.ResolveTraversalHop(
                graph, sourceDescriptorName: Origin, linkName: "noSuchLink", targetDescriptorOverride: null))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task ResolveTraversalHop_NullGraph_Throws()
    {
        // Instance-anchored traversal is a graph-aware operation: it needs the
        // descriptors' tables and key properties. A graph-less provider cannot
        // serve it, so the resolver throws rather than emitting wrong SQL.
        await Assert.That(() => PgVectorObjectSetProvider.ResolveTraversalHop(
                graph: null, sourceDescriptorName: Origin, linkName: LinkName, targetDescriptorOverride: null))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));
    }

    [Test]
    public async Task ResolveTraversalHop_FeedsTraversalSql_EndToEnd()
    {
        // The resolver output feeds the SQL builder unchanged: pin the full
        // provider dispatch -> SQL path the production traversal walks. The target
        // table is the GRAPH-resolved descriptor's table (EdgeRight), never
        // typeof(MultiEdge).
        var graph = BuildMultiRegistrationGraph();

        var hop = PgVectorObjectSetProvider.ResolveTraversalHop(
            graph, sourceDescriptorName: Origin, linkName: LinkName, targetDescriptorOverride: null);

        var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            "public", hop.SourceTable, hop.SourceKeyProperty, hop.JunctionTable, hop.TargetTable);

        await Assert.That(sql).Contains("FROM \"public\".\"multi_origin\" s");
        await Assert.That(sql).Contains("JOIN \"public\".\"multi_origin_to_edge\" j ON j.source_id = s.id");
        await Assert.That(sql).Contains("JOIN \"public\".\"edge_right\" t ON t.id = j.target_id");
        await Assert.That(sql).Contains("s.data->>'Key' = @srcId");
    }

    // -----------------------------------------------------------------------
    // Zero relations -> empty result (#114 / DR-8 guard at the SQL level).
    // -----------------------------------------------------------------------

    [Test]
    public async Task Traverse_ZeroRelations_ReturnsEmpty()
    {
        // #114 guard: instance-anchored traversal NEVER falls back to "all
        // targets". The lowering is an INNER JOIN THROUGH the junction table, so
        // a source with zero junction rows inherently yields zero result rows —
        // there is no unconditional target scan and no LEFT/OUTER join that would
        // surface unrelated targets.
        var sql = SqlGenerator.BuildInstanceAnchoredTraversalSql(
            schema: "public",
            sourceTableName: "document",
            sourceKeyProperty: "Id",
            junctionTableName: "document_written_by",
            targetTableName: "author");

        // The junction is on the JOIN path (inner), so no junction row => no row.
        await Assert.That(sql).Contains("JOIN \"public\".\"document_written_by\" j");

        // No all-targets fallback: the only target rows reachable are those whose
        // surrogate id appears in the junction for this source.
        await Assert.That(sql).DoesNotContain("LEFT JOIN");
        await Assert.That(sql).DoesNotContain("LEFT OUTER JOIN");
        await Assert.That(sql).DoesNotContain("FULL JOIN");
        await Assert.That(sql).DoesNotContain("CROSS JOIN");

        // The target table is NEVER the FROM root (which would scan all targets):
        // it is reached only via the junction join.
        await Assert.That(sql).DoesNotContain("FROM \"public\".\"author\"");
    }

    // -----------------------------------------------------------------------
    // Adversarial graph fixtures — mirror the in-memory traversal-identity
    // tests so the Npgsql resolver is proven against the SAME graphs.
    // Constructed via the internal OntologyGraph constructor (the builder DSL
    // cannot express multi-registration or SymbolKey-only link targets).
    // -----------------------------------------------------------------------

    private static OntologyGraph BuildMultiRegistrationGraph()
    {
        var origin = new ObjectTypeDescriptor
        {
            Name = Origin,
            DomainName = "multi",
            ClrType = typeof(TraversalOriginNode),
            KeyProperty = new PropertyDescriptor("Key", typeof(string)),
            Links =
            [
                // DECLARED target is EdgeRight — the partition the hop must hit.
                new LinkDescriptor(LinkName, EdgeRight, LinkCardinality.OneToMany),
            ],
        };

        var edgeWrong = AssociationDescriptor(EdgeWrong);
        var edgeRight = AssociationDescriptor(EdgeRight);

        // EdgeWrong is enumerated FIRST, so a typeof()-keyed lookup lands there.
        var objectTypes = new[] { origin, edgeWrong, edgeRight };
        return new OntologyGraph(
            domains: [new DomainDescriptor("multi") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    private static OntologyGraph BuildNodeLinkAttributedGraph()
    {
        var origin = new ObjectTypeDescriptor
        {
            Name = Origin,
            DomainName = "multi",
            ClrType = typeof(TraversalOriginNode),
            KeyProperty = new PropertyDescriptor("Key", typeof(string)),
            Links =
            [
                // Declared target is the NODE (Origin), not an association.
                new LinkDescriptor(LinkName, Origin, LinkCardinality.OneToMany),
            ],
        };

        var edgeWrong = AssociationDescriptor(EdgeWrong);
        var edgeRight = AssociationDescriptor(EdgeRight);

        var objectTypes = new[] { origin, edgeWrong, edgeRight };
        return new OntologyGraph(
            domains: [new DomainDescriptor("multi") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    private static OntologyGraph BuildSymbolKeyTargetGraph(string targetSymbol)
    {
        var origin = new ObjectTypeDescriptor
        {
            Name = Origin,
            DomainName = "sym",
            ClrType = typeof(TraversalOriginNode),
            KeyProperty = new PropertyDescriptor("Key", typeof(string)),
            Links =
            [
                // TargetTypeName empty; the target is named ONLY by SymbolKey.
                new LinkDescriptor(LinkName, string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = targetSymbol,
                },
            ],
        };

        var symEdge = new ObjectTypeDescriptor
        {
            Name = "SymEdge",
            DomainName = "sym",
            ClrType = null, // SymbolKey-only: no loaded CLR identity.
            SymbolKey = targetSymbol,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "sym-source",
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("From", Origin),
                new AssociationEndpoint("To", Origin),
            ],
        };

        var objectTypes = new[] { origin, symEdge };
        return new OntologyGraph(
            domains: [new DomainDescriptor("sym") { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);
    }

    private static ObjectTypeDescriptor AssociationDescriptor(string name) => new()
    {
        Name = name,
        DomainName = "multi",
        ClrType = typeof(TraversalMultiEdge), // SAME CLR type across both edges.
        KeyProperty = new PropertyDescriptor("Id", typeof(string)),
        Kind = ObjectKind.Association,
        AssociationEndpoints =
        [
            new AssociationEndpoint("From", Origin),
            new AssociationEndpoint("To", Origin),
        ],
    };
}

// Distinct CLR shapes so the source never collides with the edge in the
// type->descriptor reverse index. A single edge CLR type backs TWO descriptors.
public sealed record TraversalOriginNode(string Key);

public sealed record TraversalMultiEdge(string Id);
