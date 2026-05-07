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
    // MEDIUM-4: hasher must be sensitive to additional structural fields
    // (KeyProperty, ObjectKind, action dispatch routing, interface action
    // declarations, interface mappings) — design §4.1.
    // ------------------------------------------------------------------

    [Test]
    public async Task Version_RenamingKeyProperty_ChangesHash()
    {
        // Renaming the key property is a primary-key / dispatch-routing change
        // that must bust schema caches, even though property shape is unchanged.
        var tA = new ObjectTypeDescriptor("T", typeof(object), "d")
        {
            KeyProperty = new PropertyDescriptor("Id", typeof(string)),
            Properties = [new PropertyDescriptor("Id", typeof(string))],
        };
        var tB = new ObjectTypeDescriptor("T", typeof(object), "d")
        {
            KeyProperty = new PropertyDescriptor("Key", typeof(string)),
            Properties = [new PropertyDescriptor("Key", typeof(string))],
        };

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ChangingObjectKind_ChangesHash()
    {
        // ObjectKind (Entity vs Process) is a semantic shape designation that
        // changes how consumers reason about the type.
        var tA = new ObjectTypeDescriptor("T", typeof(object), "d") { Kind = ObjectKind.Entity };
        var tB = new ObjectTypeDescriptor("T", typeof(object), "d") { Kind = ObjectKind.Process };

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_RebindingActionWorkflow_ChangesHash()
    {
        // Rebinding an action's BoundWorkflowName changes dispatch routing
        // even though the action's surface (Name/Accepts/Returns) is identical.
        var tA = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "desc")
            {
                BindingType = ActionBindingType.Workflow,
                BoundWorkflowName = "WorkflowA",
            },
        ]);
        var tB = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "desc")
            {
                BindingType = ActionBindingType.Workflow,
                BoundWorkflowName = "WorkflowB",
            },
        ]);

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_RebindingActionTool_ChangesHash()
    {
        // Rebinding BoundToolName / BoundToolMethod is also dispatch routing.
        var tA = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "desc")
            {
                BindingType = ActionBindingType.Tool,
                BoundToolName = "ToolA",
                BoundToolMethod = "Method1",
            },
        ]);
        var tB = ObjectType("T", "d", actions:
        [
            new ActionDescriptor("DoIt", "desc")
            {
                BindingType = ActionBindingType.Tool,
                BoundToolName = "ToolB",
                BoundToolMethod = "Method1",
            },
        ]);

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingInterfaceAction_ChangesHash()
    {
        // Interface actions are part of the schema agents reason about; adding
        // one must bust caches that exist for structural invalidation.
        var ifaceA = new InterfaceDescriptor("ISearchable", typeof(IDisposable));
        var ifaceB = new InterfaceDescriptor("ISearchable", typeof(IDisposable))
        {
            Actions =
            [
                new InterfaceActionDescriptor { Name = "Search" },
            ],
        };

        var graphA = Graph(domains: [Domain("d")], interfaces: [ifaceA]);
        var graphB = Graph(domains: [Domain("d")], interfaces: [ifaceB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ChangingInterfaceActionAcceptsType_ChangesHash()
    {
        // InterfaceActionDescriptor.AcceptsTypeName is part of the action's
        // structural shape — changing it changes the contract.
        var ifaceA = new InterfaceDescriptor("ISearchable", typeof(IDisposable))
        {
            Actions =
            [
                new InterfaceActionDescriptor { Name = "Search", AcceptsTypeName = "QueryA" },
            ],
        };
        var ifaceB = new InterfaceDescriptor("ISearchable", typeof(IDisposable))
        {
            Actions =
            [
                new InterfaceActionDescriptor { Name = "Search", AcceptsTypeName = "QueryB" },
            ],
        };

        var graphA = Graph(domains: [Domain("d")], interfaces: [ifaceA]);
        var graphB = Graph(domains: [Domain("d")], interfaces: [ifaceB]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ChangingInterfacePropertyMapping_ChangesHash()
    {
        // InterfacePropertyMappings are user-authored Via() bindings — rebinding
        // an interface property to a different local property is a dispatch
        // change, even though the type's local properties are unchanged.
        var iface = new InterfaceDescriptor("ISearchable", typeof(IDisposable));
        var tA = new ObjectTypeDescriptor("T", typeof(object), "d")
        {
            ImplementedInterfaces = [iface],
            InterfacePropertyMappings =
            [
                new InterfacePropertyMapping("LocalA", "InterfaceProp", "ISearchable"),
            ],
        };
        var tB = new ObjectTypeDescriptor("T", typeof(object), "d")
        {
            ImplementedInterfaces = [iface],
            InterfacePropertyMappings =
            [
                new InterfacePropertyMapping("LocalB", "InterfaceProp", "ISearchable"),
            ],
        };

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA], interfaces: [iface]);
        var graphB = Graph(domains: [Domain("d", tB)], objectTypes: [tB], interfaces: [iface]);

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ChangingInterfaceActionMapping_ChangesHash()
    {
        // InterfaceActionMappings record which concrete action implements each
        // interface action. Source-of-truth user input; rebinding changes
        // dispatch.
        var iface = new InterfaceDescriptor("ISearchable", typeof(IDisposable));
        var tA = new ObjectTypeDescriptor("T", typeof(object), "d")
        {
            ImplementedInterfaces = [iface],
            InterfaceActionMappings =
            [
                new InterfaceActionMapping
                {
                    InterfaceActionName = "Search",
                    ConcreteActionName = "SearchAlpha",
                },
            ],
        };
        var tB = new ObjectTypeDescriptor("T", typeof(object), "d")
        {
            ImplementedInterfaces = [iface],
            InterfaceActionMappings =
            [
                new InterfaceActionMapping
                {
                    InterfaceActionName = "Search",
                    ConcreteActionName = "SearchBeta",
                },
            ],
        };

        var graphA = Graph(domains: [Domain("d", tA)], objectTypes: [tA], interfaces: [iface]);
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
    public async Task Version_ChangingInterfaceActionDescription_DoesNotChangeHash()
    {
        // InterfaceActionDescriptor.Description is free-form documentation,
        // matching the broader exclusion of Description fields from the hash.
        var ifaceA = new InterfaceDescriptor("ISearchable", typeof(IDisposable))
        {
            Actions =
            [
                new InterfaceActionDescriptor { Name = "Search", Description = "Original prose" },
            ],
        };
        var ifaceB = new InterfaceDescriptor("ISearchable", typeof(IDisposable))
        {
            Actions =
            [
                new InterfaceActionDescriptor { Name = "Search", Description = "Different prose" },
            ],
        };

        var graphA = Graph(domains: [Domain("d")], interfaces: [ifaceA]);
        var graphB = Graph(domains: [Domain("d")], interfaces: [ifaceB]);

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

    // ------------------------------------------------------------------
    // A5: reference fixture pins a known hash so any future drift in
    // OntologyGraphHasher serialization shape (field order, framing,
    // included fields) becomes a visible failure in CI.
    // ------------------------------------------------------------------

    // Pinned hash for the reference fixture. If this changes, the
    // OntologyGraphHasher serialization shape has drifted — review the diff
    // before updating.
    //
    // Regeneration: when intentionally adding a field to the hash, run this test
    // once with the old constant, capture the new hash from the assertion failure
    // message, and replace the constant. Confirm the diff matches the intended
    // hasher change before committing.
    private const string ReferenceFixtureVersion =
        "2095a57833d35ce0ee1dba1def232c79ab4b960631f2508955936c2b485e26d6";

    [Test]
    public async Task Version_ReferenceFixture_MatchesPinnedConstant()
    {
        var graph = BuildReferenceFixture();

        await Assert.That(graph.Version).IsEqualTo(ReferenceFixtureVersion);
    }

    private static OntologyGraph BuildReferenceFixture()
    {
        // Two domains, one ObjectType with a property + an action + a link,
        // one cross-domain link, one workflow chain. Small enough that the
        // pinned constant is easy to recompute by hand if needed; large
        // enough to exercise every section of the hasher.
        var orderType = new ObjectTypeDescriptor("Order", typeof(int), "trading")
        {
            Properties =
            [
                new PropertyDescriptor("Id", typeof(string), IsRequired: true),
            ],
            Actions =
            [
                new ActionDescriptor("Submit", "submit the order"),
            ],
            Links =
            [
                new LinkDescriptor("ForInstrument", "Instrument", LinkCardinality.OneToOne),
            ],
        };
        var instrumentType = new ObjectTypeDescriptor("Instrument", typeof(string), "market-data");

        var domains = new[]
        {
            new DomainDescriptor("trading") { ObjectTypes = [orderType] },
            new DomainDescriptor("market-data") { ObjectTypes = [instrumentType] },
        };

        var xdl = new ResolvedCrossDomainLink(
            "OrderToInstrument",
            "trading",
            orderType,
            "market-data",
            instrumentType,
            LinkCardinality.OneToOne,
            []);

        var chain = new WorkflowChain("OrderFlow", orderType, instrumentType);

        return new OntologyGraph(
            domains,
            [orderType, instrumentType],
            interfaces: [],
            crossDomainLinks: [xdl],
            workflowChains: [chain]);
    }
}
