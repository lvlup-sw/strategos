using System.Text.RegularExpressions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public class OntologyGraphVersionTests
{
    private static readonly Regex LowercaseHex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    // ------------------------------------------------------------------
    // Helpers — construct OntologyGraph instances directly via the
    // internal constructor so tests can isolate single structural deltas
    // without going through the full DSL pipeline.
    // ------------------------------------------------------------------

    private static OntologyGraph Graph(
        IReadOnlyList<DomainDescriptor>? domains = null,
        IReadOnlyList<ObjectTypeDescriptor>? objectTypes = null,
        IReadOnlyList<InterfaceDescriptor>? interfaces = null,
        IReadOnlyList<ResolvedCrossDomainLink>? crossDomainLinks = null,
        IReadOnlyList<WorkflowChain>? workflowChains = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new OntologyGraph(
            domains ?? [],
            objectTypes ?? [],
            interfaces ?? [],
            crossDomainLinks ?? [],
            workflowChains ?? [],
            objectTypeNamesByType: null,
            warnings: warnings);
    }

    private static DomainDescriptor Domain(string name, params ObjectTypeDescriptor[] objectTypes) =>
        new(name) { ObjectTypes = objectTypes };

    private static ObjectTypeDescriptor ObjectType(
        string name,
        string domain,
        Type? clrType = null,
        PropertyDescriptor[]? properties = null,
        ActionDescriptor[]? actions = null,
        LinkDescriptor[]? links = null,
        EventDescriptor[]? events = null,
        InterfaceDescriptor[]? implementedInterfaces = null,
        LifecycleDescriptor? lifecycle = null,
        string? parentTypeName = null)
    {
        return new ObjectTypeDescriptor(name, clrType ?? typeof(object), domain)
        {
            Properties = properties ?? [],
            Actions = actions ?? [],
            Links = links ?? [],
            Events = events ?? [],
            ImplementedInterfaces = implementedInterfaces ?? [],
            Lifecycle = lifecycle,
            ParentTypeName = parentTypeName,
        };
    }

    [Test]
    public async Task Version_OnEmptyGraph_ReturnsLowercaseSha256Hex()
    {
        var graph = new OntologyGraphBuilder().Build();

        await Assert.That(graph.Version).IsNotNull();
        await Assert.That(graph.Version.Length).IsEqualTo(64);
        await Assert.That(LowercaseHex64.IsMatch(graph.Version)).IsTrue();
    }

    [Test]
    public async Task Version_OnEmptyGraph_DoesNotIncludeSha256Prefix()
    {
        // Wire-format note: the "sha256:" prefix is added at the MCP _meta-emission
        // boundary (Track B's ResponseMeta factory), NOT on the property itself.
        // OntologyGraph.Version is bare hex.
        var graph = new OntologyGraphBuilder().Build();

        await Assert.That(graph.Version.StartsWith("sha256:")).IsFalse();
    }

    [Test]
    public async Task Version_BuiltTwice_ReturnsSameHash()
    {
        var graphA = new OntologyGraphBuilder().Build();
        var graphB = new OntologyGraphBuilder().Build();

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    // ------------------------------------------------------------------
    // A2: hasher must be sensitive to ObjectType structural changes.
    // ------------------------------------------------------------------

    [Test]
    public async Task Version_AddingObjectType_ChangesHash()
    {
        var graphA = Graph(domains: [Domain("d")]);

        var t = ObjectType("T", "d");
        var graphB = Graph(domains: [Domain("d", t)], objectTypes: [t]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingProperty_ChangesHash()
    {
        var tA = ObjectType("T", "d");
        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);

        var tB = ObjectType("T", "d", properties:
        [
            new PropertyDescriptor("X", typeof(string)),
        ]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_RenamingAction_ChangesHash()
    {
        var tA = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "desc"),
        ]);
        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);

        var tB = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoItRenamed", "desc"),
        ]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingLink_ChangesHash()
    {
        var tA = ObjectType("T", "d");
        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);

        var tB = ObjectType("T", "d", links:
        [
            new LinkDescriptor("ToOther", "Other", LinkCardinality.OneToMany),
        ]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingEvent_ChangesHash()
    {
        var tA = ObjectType("T", "d");
        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);

        var tB = ObjectType("T", "d", events:
        [
            new EventDescriptor(typeof(int), "evt"),
        ]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_LifecycleStateAddition_ChangesHash()
    {
        var lcA = new LifecycleDescriptor
        {
            PropertyName = "Status",
            StateEnumTypeName = "S",
            States =
            [
                new LifecycleStateDescriptor { Name = "Open", IsInitial = true },
            ],
            Transitions = [],
        };
        var lcB = new LifecycleDescriptor
        {
            PropertyName = "Status",
            StateEnumTypeName = "S",
            States =
            [
                new LifecycleStateDescriptor { Name = "Open", IsInitial = true },
                new LifecycleStateDescriptor { Name = "Closed", IsTerminal = true },
            ],
            Transitions = [],
        };

        var tA = ObjectType("T", "d", lifecycle: lcA);
        var tB = ObjectType("T", "d", lifecycle: lcB);

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ImplementedInterface_ChangesHash()
    {
        var iface = new InterfaceDescriptor("ISearchable", typeof(IDisposable));

        var tA = ObjectType("T", "d");
        var tB = ObjectType("T", "d", implementedInterfaces: [iface]);

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB], interfaces: [iface]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    // ------------------------------------------------------------------
    // A3: hasher must be sensitive to graph-level Interfaces,
    // CrossDomainLinks, and WorkflowChains.
    // ------------------------------------------------------------------

    [Test]
    public async Task Version_AddingInterface_ChangesHash()
    {
        var graphA = Graph(domains: [Domain("d")]);

        var iface = new InterfaceDescriptor("ISearchable", typeof(IDisposable));
        var graphB = Graph(domains: [Domain("d")], interfaces: [iface]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingCrossDomainLink_ChangesHash()
    {
        var src = ObjectType("Src", "a");
        var tgt = ObjectType("Tgt", "b");
        var graphA = Graph(
            domains: [Domain("a", src), Domain("b", tgt)],
            objectTypes: [src, tgt]);

        var xdl = new ResolvedCrossDomainLink(
            "Link",
            "a",
            src,
            "b",
            tgt,
            LinkCardinality.OneToMany,
            []);
        var graphB = Graph(
            domains: [Domain("a", src), Domain("b", tgt)],
            objectTypes: [src, tgt],
            crossDomainLinks: [xdl]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingWorkflowChain_ChangesHash()
    {
        var consumed = ObjectType("In", "d", clrType: typeof(int));
        var produced = ObjectType("Out", "d", clrType: typeof(string));
        var graphA = Graph(
            domains: [Domain("d", consumed, produced)],
            objectTypes: [consumed, produced]);

        var chain = new WorkflowChain("WF", consumed, produced);
        var graphB = Graph(
            domains: [Domain("d", consumed, produced)],
            objectTypes: [consumed, produced],
            workflowChains: [chain]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    // ------------------------------------------------------------------
    // A4: hasher must be INSENSITIVE to free-form Description text and
    // to OntologyGraph.Warnings — design §4.1. Documentation churn must
    // not bust caches that exist for structural invalidation.
    // ------------------------------------------------------------------

    [Test]
    public async Task Version_ChangingActionDescription_DoesNotChangeHash()
    {
        var tA = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "Original description"),
        ]);
        var tB = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "Completely different prose"),
        ]);

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ChangingLinkDescription_DoesNotChangeHash()
    {
        var tA = ObjectType("T", "d", links:
        [
            new LinkDescriptor("ToOther", "Other", LinkCardinality.OneToMany)
            {
                Description = "Original",
            },
        ]);
        var tB = ObjectType("T", "d", links:
        [
            new LinkDescriptor("ToOther", "Other", LinkCardinality.OneToMany)
            {
                Description = "Different",
            },
        ]);

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_DifferingWarnings_DoesNotChangeHash()
    {
        // Graphs with identical structural shape but different Warnings lists.
        // Warnings are advisory diagnostic output; they must not influence the
        // structural cache key.
        var graphA = Graph(domains: [Domain("d")], warnings: []);
        var graphB = Graph(domains: [Domain("d")], warnings: ["orphan interface"]);

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }
}
