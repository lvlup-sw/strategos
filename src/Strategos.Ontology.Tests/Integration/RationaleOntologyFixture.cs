using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Integration;

/// <summary>
/// A reflection-free, polyglot instance carrier for the rationale ontology
/// (DR-9 / DR-10). It is deliberately NOT a descriptor type: the corpus names
/// every object type by <see cref="ObjectTypeDescriptor.SymbolKey"/> only
/// (ClrType == null), so this single envelope stands in for every Decision,
/// Constraint, and reified association OBJECT alike. Identity flows solely
/// through <see cref="Id"/> via the descriptor's
/// <see cref="ObjectTypeDescriptor.IdAccessor"/> (INV-8 — no per-call
/// reflection on the instance type), and edge/node attributes are read from a
/// property bag rather than reflected CLR members.
/// </summary>
public sealed class RationaleNode
{
    private readonly IReadOnlyDictionary<string, string> _properties;

    public RationaleNode(string id, IReadOnlyDictionary<string, string>? properties = null)
    {
        Id = id;
        _properties = properties ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>The stable id this instance is keyed by. The descriptor's
    /// IdAccessor reads exactly this — the single reflection-free id path.</summary>
    public string Id { get; }

    /// <summary>Reads a node/edge attribute from the property bag. Reified
    /// associations carry their OWN attributes here (rationale, weight,
    /// severity), so the traversal proof can assert edge properties without
    /// reflecting over a CLR edge type.</summary>
    public string? Get(string key) => _properties.TryGetValue(key, out var v) ? v : null;
}

/// <summary>
/// DR-9 / DR-10 provider-agnostic corpus + relate/traverse SCRIPT for the
/// CLR-free, edge-centric rationale ontology. The fixture exposes only graph
/// data, materialized relation rows, an item resolver, and traversal
/// expressions — it binds to NO executor, so the in-memory
/// <see cref="InMemoryExpressionEvaluator"/> (T12) and the Npgsql provider
/// (T13) can replay the identical ontology and relate rows.
/// </summary>
/// <remarks>
/// INV-8: every <see cref="ObjectTypeDescriptor"/> is SymbolKey-only
/// (<c>ClrType == null</c>); the reified associations are
/// <see cref="ObjectKind.Association"/> with two endpoints, each also named by
/// descriptor name (never a CLR type). Links name their targets by
/// <see cref="LinkDescriptor.TargetSymbolKey"/> so every hop's target partition
/// resolves through the graph's SymbolKey -> descriptor-name reverse index
/// (DR-10), never via typeof(TLinked).
/// <para>
/// Bitemporal validity is OUT of scope for v2.9.0 (tracked #126); associations
/// carry a plain rationale/weight/severity attribute, not a valid-time interval.
/// </para>
/// </remarks>
public sealed class RationaleOntologyFixture
{
    public const string Domain = "rationale";

    // SymbolKey monikers — the SCIP-shaped polyglot identity for each object
    // type. These are the ONLY identity each descriptor carries (ClrType null).
    public const string DecisionSymbol = "scip-typescript . ./rationale/decision.ts#Decision";
    public const string ConstraintSymbol = "scip-typescript . ./rationale/constraint.ts#Constraint";
    public const string SupersedesSymbol = "scip-typescript . ./rationale/edges.ts#Supersedes";
    public const string MotivatesSymbol = "scip-typescript . ./rationale/edges.ts#Motivates";
    public const string ConflictsWithSymbol = "scip-typescript . ./rationale/edges.ts#ConflictsWith";

    // Descriptor names (per-domain unique). Distinct from the carrier CLR name
    // on purpose: nothing in the corpus is keyed by a CLR Type.
    public const string Decision = "Decision";
    public const string Constraint = "Constraint";
    public const string Supersedes = "Supersedes";
    public const string Motivates = "Motivates";
    public const string ConflictsWith = "ConflictsWith";

    // Link names. Each association is reachable two CLR-free ways: an edge-view
    // link (target = the association, via its SymbolKey) surfaces the reified
    // edge object with its attributes; a far-node link (target = a node, via the
    // node's SymbolKey) resolves the related node. Both route through the reverse
    // index — no chained alias-loss hop is needed (that limitation is #128).
    public const string LinkSupersedesEdge = "supersedesEdge";
    public const string LinkSupersededDecision = "supersededDecision";
    public const string LinkMotivatesEdge = "motivatesEdge";
    public const string LinkConflictsWithEdge = "conflictsWithEdge";

    private RationaleOntologyFixture(
        OntologyGraph graph,
        IReadOnlyDictionary<string, IReadOnlyList<RationaleNode>> instancesByDescriptor,
        IReadOnlyList<RelateRow> relations)
    {
        Graph = graph;
        InstancesByDescriptor = instancesByDescriptor;
        Relations = relations;
    }

    /// <summary>The frozen, SymbolKey-only ontology graph.</summary>
    public OntologyGraph Graph { get; }

    /// <summary>The instance corpus, partitioned by descriptor name. T13 seeds
    /// these into the Npgsql store under the SAME descriptor partitions before
    /// replaying <see cref="Relations"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<RationaleNode>> InstancesByDescriptor { get; }

    /// <summary>The provider-agnostic relate SCRIPT: every (source, link, target)
    /// edge plus its optional attributed-relate association object id. T12 serves
    /// these from memory via <see cref="RelationResolver"/>; T13 replays them as
    /// RelateAsync calls against Npgsql, then reads them back through the same
    /// traversal expressions (<see cref="TraverseSupersedesEdges"/> et al.).</summary>
    public IReadOnlyList<RelateRow> Relations { get; }

    /// <summary>Resolves the materialized relation rows for a (source descriptor,
    /// source id, link) triple — the relate-store read shape. Provider-agnostic:
    /// the rows are the SAME ones a Npgsql replay would store and read back.</summary>
    public IReadOnlyList<RelationRow> RelationResolver(string srcDescriptor, string srcId, string linkName) =>
        Relations
            .Where(r => r.SourceDescriptor == srcDescriptor
                && r.SourceId == srcId
                && r.LinkName == linkName)
            .Select(r => new RelationRow(r.TargetDescriptor, r.TargetId, r.AssociationObjectId))
            .ToList();

    /// <summary>Resolves all instances in a descriptor's partition by descriptor
    /// name. Casting/filtering is the evaluator's job.</summary>
    public IReadOnlyList<object> ResolveItems(string descriptorName) =>
        InstancesByDescriptor.TryGetValue(descriptorName, out var items)
            ? items
            : [];

    // ----- Traversal SCRIPT (provider-agnostic expression builders) -----

    /// <summary>Edge-view hop: Decision --supersedesEdge--> Supersedes (the
    /// reified association object), resolved via the association's SymbolKey.</summary>
    public ObjectSetExpression TraverseSupersedesEdges(string decisionId) =>
        EdgeViewTraversal(Decision, decisionId, LinkSupersedesEdge);

    /// <summary>Far-node hop: Decision --supersededDecision--> Decision (the
    /// superseded node), resolved via the Decision node's SymbolKey.</summary>
    public ObjectSetExpression TraverseSupersededDecision(string decisionId) =>
        EdgeViewTraversal(Decision, decisionId, LinkSupersededDecision);

    /// <summary>Edge-view hop: Constraint --motivatesEdge--> Motivates.</summary>
    public ObjectSetExpression TraverseMotivatesEdges(string constraintId) =>
        EdgeViewTraversal(Constraint, constraintId, LinkMotivatesEdge);

    /// <summary>Edge-view hop: Decision --conflictsWithEdge--> ConflictsWith.</summary>
    public ObjectSetExpression TraverseConflictsWithEdges(string decisionId) =>
        EdgeViewTraversal(Decision, decisionId, LinkConflictsWithEdge);

    // Builds a single-hop traversal anchored to one source instance (by id) over
    // a named link. The carrier type (RationaleNode) is used ONLY as the result
    // element type; the hop's TARGET partition is resolved from the graph by the
    // link's TargetSymbolKey, never from this CLR type (DR-10 / INV-8).
    private static ObjectSetExpression EdgeViewTraversal(
        string sourceDescriptor, string sourceId, string linkName)
    {
        var root = new RootExpression(typeof(RationaleNode), sourceDescriptor);
        var filtered = new FilterExpression(
            root,
            (RationaleNode n) => n.Id == sourceId);
        return new TraverseLinkExpression(filtered, linkName, typeof(RationaleNode));
    }

    /// <summary>
    /// Builds the CLR-free rationale ontology + its relate rows. The corpus is
    /// authored entirely through SymbolKey-only descriptors and association
    /// endpoints named by descriptor name (INV-8).
    /// </summary>
    public static RationaleOntologyFixture Build()
    {
        // Reflection-free id accessor shared by every partition: identity is the
        // carrier's Id, read through this delegate only (DR-1 / INV-8).
        Func<object, object?> id = instance => ((RationaleNode)instance).Id;

        // --- Object types (nodes): SymbolKey-only, ClrType == null. ---
        var decision = new ObjectTypeDescriptor
        {
            Name = Decision,
            DomainName = Domain,
            ClrType = null,
            SymbolKey = DecisionSymbol,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "rationale-source",
            IdAccessor = id,
            Links =
            [
                // Edge-view link: target is the Supersedes ASSOCIATION, named by
                // its SymbolKey. The hop surfaces the reified edge object.
                new LinkDescriptor(LinkSupersedesEdge, string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = SupersedesSymbol,
                    Source = DescriptorSource.Ingested,
                },
                // Far-node link: target is the superseded Decision NODE, named by
                // the Decision SymbolKey. The hop resolves the related node.
                new LinkDescriptor(LinkSupersededDecision, string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = DecisionSymbol,
                    Source = DescriptorSource.Ingested,
                },
                new LinkDescriptor(LinkConflictsWithEdge, string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = ConflictsWithSymbol,
                    Source = DescriptorSource.Ingested,
                },
            ],
        };

        var constraint = new ObjectTypeDescriptor
        {
            Name = Constraint,
            DomainName = Domain,
            ClrType = null,
            SymbolKey = ConstraintSymbol,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "rationale-source",
            IdAccessor = id,
            Links =
            [
                new LinkDescriptor(LinkMotivatesEdge, string.Empty, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = MotivatesSymbol,
                    Source = DescriptorSource.Ingested,
                },
            ],
        };

        // --- Reified associations: SymbolKey-only, Kind == Association, two
        // endpoints each named by descriptor name (never a CLR type). ---
        var supersedes = Association(Supersedes, SupersedesSymbol, Decision, Decision, id);
        var motivates = Association(Motivates, MotivatesSymbol, Constraint, Decision, id);
        var conflictsWith = Association(ConflictsWith, ConflictsWithSymbol, Decision, Decision, id);

        var objectTypes = new[] { decision, constraint, supersedes, motivates, conflictsWith };
        var graph = new OntologyGraph(
            domains: [new DomainDescriptor(Domain) { ObjectTypes = objectTypes }],
            objectTypes: objectTypes,
            interfaces: [],
            crossDomainLinks: [],
            workflowChains: []);

        // --- Instances (partitions by descriptor name). Nodes carry their own
        // attributes; reified association objects carry the EDGE attributes. ---
        var instances = new Dictionary<string, IReadOnlyList<RationaleNode>>(StringComparer.Ordinal)
        {
            [Decision] =
            [
                Node("D0", ("title", "Use a monolith")),
                Node("D1", ("title", "Split into services")),
                Node("D2", ("title", "Adopt event sourcing")),
            ],
            [Constraint] =
            [
                Node("C1", ("title", "Sub-100ms p99 latency")),
            ],
            [Supersedes] =
            [
                Node("sup-1", ("rationale", "D1 obsoletes the earlier choice")),
            ],
            [Motivates] =
            [
                Node("mot-1", ("weight", "high")),
            ],
            [ConflictsWith] =
            [
                Node("cfl-1", ("severity", "blocking")),
            ],
        };

        // --- Relate rows (the relate-store materialization). Edge-view rows carry
        // an AssociationObjectId; far-node rows are plain DR-2 relates. ---
        var relations = new List<RelateRow>
        {
            // D1 supersedes D0 — attributed relate. The edge-view link surfaces the
            // "sup-1" association object; the far-node link resolves Decision "D0".
            new(Decision, "D1", LinkSupersedesEdge, Supersedes, "n/a", "sup-1"),
            new(Decision, "D1", LinkSupersededDecision, Decision, "D0"),
            // C1 motivates D1 — attributed relate to the Motivates edge "mot-1".
            new(Constraint, "C1", LinkMotivatesEdge, Motivates, "n/a", "mot-1"),
            // D1 conflicts with D2 — attributed relate to the ConflictsWith edge.
            new(Decision, "D1", LinkConflictsWithEdge, ConflictsWith, "n/a", "cfl-1"),
        };

        return new RationaleOntologyFixture(graph, instances, relations);
    }

    private static ObjectTypeDescriptor Association(
        string name,
        string symbolKey,
        string fromDescriptor,
        string toDescriptor,
        Func<object, object?> idAccessor) =>
        new()
        {
            Name = name,
            DomainName = Domain,
            ClrType = null,
            SymbolKey = symbolKey,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "rationale-source",
            IdAccessor = idAccessor,
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("From", fromDescriptor),
                new AssociationEndpoint("To", toDescriptor),
            ],
        };

    private static RationaleNode Node(string id, params (string Key, string Value)[] properties)
    {
        var bag = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            bag[key] = value;
        }

        return new RationaleNode(id, bag);
    }

    /// <summary>
    /// The provider-agnostic relate-row shape: a (source, link, target) edge plus
    /// an optional attributed-relate association object id. T13 stores exactly
    /// these against Npgsql; T12 serves them from memory. Public so a
    /// cross-provider replay can drive RelateAsync from the same script.
    /// </summary>
    public sealed record RelateRow(
        string SourceDescriptor,
        string SourceId,
        string LinkName,
        string TargetDescriptor,
        string TargetId,
        string? AssociationObjectId = null);
}
