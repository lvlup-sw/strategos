using System.Text.RegularExpressions;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Extensions;

namespace Strategos.Ontology.Tests;

/// <summary>
/// Tests for <see cref="OntologyGraph.Version"/> — the content-stable sha256
/// hash that downstream MCP consumers use to invalidate schema caches.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.1
/// </summary>
public class OntologyGraphVersionTests
{
    [Test]
    public async Task Version_OnEmptyGraph_ReturnsLowercaseSha256Hex()
    {
        var graph = new OntologyGraphBuilder().Build();

        await Assert.That(graph.Version).IsNotNull();
        await Assert.That(graph.Version.Length).IsEqualTo(64);
        await Assert.That(Regex.IsMatch(graph.Version, "^[0-9a-f]{64}$")).IsTrue();
    }

    [Test]
    public async Task Version_BuiltTwice_ReturnsSameHash()
    {
        var graphA = BuildEmptyGraph();
        var graphB = BuildEmptyGraph();

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    // ---- Task A2: ObjectType structural sensitivity ----

    [Test]
    public async Task Version_AddingObjectType_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureA>();
        var graphB = BuildWith<VersionFixtureWithOneType>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingProperty_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithOneType>();
        var graphB = BuildWith<VersionFixtureWithExtraProperty>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_RenamingAction_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithActionA>();
        var graphB = BuildWith<VersionFixtureWithActionB>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingLink_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithLinkSibling>();
        var graphB = BuildWith<VersionFixtureWithLink>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingEvent_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithoutEvent>();
        var graphB = BuildWith<VersionFixtureWithEvent>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_LifecycleStateAddition_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithLifecyclePending>();
        var graphB = BuildWith<VersionFixtureWithLifecyclePendingPlusActive>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ImplementedInterface_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithoutImplementedInterface>();
        var graphB = BuildWith<VersionFixtureWithImplementedInterface>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    // ---- Task A3: Interfaces, CrossDomainLinks, WorkflowChains ----

    [Test]
    public async Task Version_AddingInterface_ChangesHash()
    {
        var graphA = BuildWith<VersionFixtureWithoutInterface>();
        var graphB = BuildWith<VersionFixtureWithInterface>();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingCrossDomainLink_ChangesHash()
    {
        var graphA = new OntologyGraphBuilder()
            .AddDomain<VersionFixtureXSource>()
            .AddDomain<VersionFixtureXTarget>()
            .Build();
        var graphB = new OntologyGraphBuilder()
            .AddDomain<VersionFixtureXSourceWithLink>()
            .AddDomain<VersionFixtureXTarget>()
            .Build();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_AddingWorkflowChain_ChangesHash()
    {
        // Baseline: just the trading widget domain, no workflows registered.
        var builderA = new OntologyGraphBuilder();
        builderA.AddDomain<VersionFixtureWithOneType>();
        var graphA = builderA.Build();

        var builderB = new OntologyGraphBuilder();
        builderB.AddDomain<VersionFixtureWithOneType>();
        builderB.AddWorkflowMetadata(new[]
        {
            new WorkflowMetadataBuilder("widget-workflow")
                .Consumes<VersionWidget>()
                .Produces<VersionWidget>(),
        });
        var graphB = builderB.Build();

        await Assert.That(graphA.Version).IsNotEqualTo(graphB.Version);
    }

    // ---- Task A4: Description / Warnings INSENSITIVITY ----

    [Test]
    public async Task Version_ChangingActionDescription_DoesNotChangeHash()
    {
        var graphA = BuildWith<VersionFixtureWithActionDescriptionA>();
        var graphB = BuildWith<VersionFixtureWithActionDescriptionB>();

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_ChangingLinkDescription_DoesNotChangeHash()
    {
        var graphA = BuildWith<VersionFixtureWithLinkDescriptionA>();
        var graphB = BuildWith<VersionFixtureWithLinkDescriptionB>();

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    [Test]
    public async Task Version_DifferingWarnings_DoesNotChangeHash()
    {
        // Construct two graphs with structurally identical content but differing
        // warnings via the internal OntologyGraph constructor. The hash must match.
        var emptyDomains = new List<Strategos.Ontology.Descriptors.DomainDescriptor>();
        var emptyTypes = new List<Strategos.Ontology.Descriptors.ObjectTypeDescriptor>();
        var emptyInterfaces = new List<Strategos.Ontology.Descriptors.InterfaceDescriptor>();
        var emptyCrossLinks = new List<ResolvedCrossDomainLink>();
        var emptyChains = new List<WorkflowChain>();

        var graphA = new OntologyGraph(
            emptyDomains, emptyTypes, emptyInterfaces, emptyCrossLinks, emptyChains,
            objectTypeNamesByType: null,
            warnings: new List<string>());
        var graphB = new OntologyGraph(
            emptyDomains, emptyTypes, emptyInterfaces, emptyCrossLinks, emptyChains,
            objectTypeNamesByType: null,
            warnings: new List<string> { "synthetic warning A", "synthetic warning B" });

        await Assert.That(graphA.Version).IsEqualTo(graphB.Version);
    }

    // ---- Task A5: Reference fixture pins a known hash (regression guard) ----

    // Pinned hash for the reference fixture. If this changes, the OntologyGraphHasher
    // serialization shape has drifted — review the diff before updating.
    private const string ReferenceFixturePinnedVersion = "70d1d572862b77dfc23b65b22c543b1db0c426e09cc05736558eb37a550af486";

    [Test]
    public async Task Version_ReferenceFixture_MatchesPinnedConstant()
    {
        var graph = BuildReferenceFixture();

        await Assert.That(graph.Version).IsEqualTo(ReferenceFixturePinnedVersion);
    }

    private static OntologyGraph BuildReferenceFixture()
    {
        return new OntologyGraphBuilder()
            .AddDomain<VersionReferenceFixtureSource>()
            .AddDomain<VersionReferenceFixtureTarget>()
            .Build();
    }

    private static OntologyGraph BuildEmptyGraph() => new OntologyGraphBuilder().Build();

    private static OntologyGraph BuildWith<TDomain>()
        where TDomain : DomainOntology, new()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<TDomain>();
        return builder.Build();
    }
}
