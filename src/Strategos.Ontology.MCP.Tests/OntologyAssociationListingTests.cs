using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Strategos.Ontology;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// DR-15 / T17 (#125): a descriptor/link/association LISTING surface — the DR-10
/// metadata an agent reads before it can drive an instance traversal. SymbolKey-only
/// (ingested, polyglot) targets must surface by descriptor name / SymbolKey and must
/// NOT leak a CLR type name (INV-8).
/// </summary>
public sealed class OntologyAssociationListingTests
{
    private const string ClrNodeDescriptor = "ListParty";
    private const string IngestedDescriptor = "IngestedNode";
    private const string IngestedSymbolKey = "scip-typescript ./ingested.ts#IngestedNode";
    private static readonly DateTimeOffset Ts = new(2026, 6, 16, 0, 0, 0, TimeSpan.Zero);

    public sealed record ListParty(string Id);

    public sealed record ListEdgeClr(string Id);

    // An in-memory source contributing a SymbolKey-ONLY (ClrType == null) ingested
    // node, a CLR node that links to it, and a reified association whose endpoints
    // are named by descriptor. Mirrors production source wiring (IOntologySource).
    private sealed class ListingSource : IOntologySource
    {
        public required string SourceId { get; init; }

        public required ImmutableArray<OntologyDelta> Deltas { get; init; }

        public async IAsyncEnumerable<OntologyDelta> LoadAsync([EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            foreach (var delta in Deltas)
            {
                ct.ThrowIfCancellationRequested();
                yield return delta;
            }
        }

        public async IAsyncEnumerable<OntologyDelta> SubscribeAsync([EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield break;
        }
    }

    private static OntologyGraph BuildMixedGraph()
    {
        var ingested = new ObjectTypeDescriptor
        {
            Name = IngestedDescriptor,
            DomainName = "listing",
            ClrType = null, // SymbolKey-only: no loaded CLR identity.
            SymbolKey = IngestedSymbolKey,
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "listing-source",
        };

        var clrNode = new ObjectTypeDescriptor(ClrNodeDescriptor, typeof(ListParty), "listing")
        {
            Source = DescriptorSource.Ingested,
            SourceId = "listing-source",
            Links =
            [
                // A link to the SymbolKey-only ingested node. Its target is named by
                // descriptor (TargetTypeName) + TargetSymbolKey, never a CLR type.
                new LinkDescriptor("ingestedLink", IngestedDescriptor, LinkCardinality.OneToMany)
                {
                    TargetSymbolKey = IngestedSymbolKey,
                },
            ],
        };

        var association = new ObjectTypeDescriptor
        {
            Name = "ListEdge",
            DomainName = "listing",
            ClrType = typeof(ListEdgeClr),
            Source = DescriptorSource.Ingested,
            SourceId = "listing-source",
            Kind = ObjectKind.Association,
            AssociationEndpoints =
            [
                new AssociationEndpoint("From", ClrNodeDescriptor),
                new AssociationEndpoint("To", IngestedDescriptor),
            ],
        };

        var source = new ListingSource
        {
            SourceId = "listing-source",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(ingested) { SourceId = "listing-source", Timestamp = Ts },
                new OntologyDelta.AddObjectType(clrNode) { SourceId = "listing-source", Timestamp = Ts },
                new OntologyDelta.AddObjectType(association) { SourceId = "listing-source", Timestamp = Ts }),
        };

        return new OntologyGraphBuilder().AddSources([source]).Build();
    }

    [Test]
    public async Task ListTool_ReturnsLinkAndAssociationTypes_SymbolKeyTargetsWithoutClrLeak()
    {
        // Arrange
        var graph = BuildMixedGraph();
        var tool = new OntologyExploreTool(graph);

        // Act — the associations scope lists every reified association with its
        // endpoints; the links scope lists links with their polyglot target keys.
        var associations = tool.Explore(scope: "associations", domain: "listing");
        var links = tool.Explore(scope: "links", domain: "listing", objectType: ClrNodeDescriptor);

        // Assert — the association is listed, named by descriptor, with endpoints
        // named by descriptor (one of which is the SymbolKey-only ingested node).
        await Assert.That(associations.Scope).IsEqualTo("associations");
        var listEdge = associations.Items.Single(i => i["name"]?.ToString() == "ListEdge");
        var endpoints = (IReadOnlyList<Dictionary<string, object?>>)listEdge["endpoints"]!;
        var endpointDescriptors = endpoints
            .Select(e => e["descriptorName"]?.ToString() ?? "")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        await Assert.That(endpointDescriptors).IsEquivalentTo(new[] { ClrNodeDescriptor, IngestedDescriptor });

        // The link to the ingested node surfaces its descriptor name AND its
        // polyglot SymbolKey.
        var ingestedLink = links.Items.Single(i => i["name"]?.ToString() == "ingestedLink");
        await Assert.That(ingestedLink["targetTypeName"]?.ToString()).IsEqualTo(IngestedDescriptor);
        await Assert.That(ingestedLink["targetSymbolKey"]?.ToString()).IsEqualTo(IngestedSymbolKey);

        // CLR-LEAK GUARD: the ingested node has ClrType == null, so any reflective
        // CLR-name path would be wrong. Its only correct identity is descriptor name
        // / SymbolKey. typeof(ListParty).FullName is the one CLR full name in play; it
        // must never be used to denote the ingested endpoint in the listing output.
        var allText = string.Join("\n", associations.Items.Concat(links.Items)
            .SelectMany(d => d.Values)
            .Select(Flatten));
        await Assert.That(allText).DoesNotContain(typeof(ListParty).FullName!);
    }

    private static string Flatten(object? value) => value switch
    {
        null => "",
        IReadOnlyList<Dictionary<string, object?>> rows =>
            string.Join("\n", rows.SelectMany(r => r.Values).Select(Flatten)),
        _ => value.ToString() ?? "",
    };
}
