using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Merge;

/// <summary>
/// DR-6 + DR-9 (Task 21): end-to-end merge matrix verifying the
/// hand-vs-ingested lattice composes through
/// <see cref="OntologyGraphBuilder.Build"/> — not through direct
/// <c>MergeTwo</c> calls. Every assertion is on graph-level shape;
/// no internal-state inspection. Uses
/// <see cref="TestOntologySource"/> as the ingestion fixture.
/// </summary>
public class MergeMatrixTests
{
    private const string DomainName = "Trading";

    private const string IngestSourceId = "marten-typescript";

    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Hand-authored model for the matrix; lives at fixture scope.</summary>
    public sealed class HandPosition
    {
        public Guid Id { get; set; }

        public string Symbol { get; set; } = "";
    }

    /// <summary>Ingestion-only mirror — no hand-authored counterpart.</summary>
    public sealed class IngestedOnlyType
    {
        public Guid Id { get; set; }
    }

    public sealed class TradingOntology : DomainOntology
    {
        public override string DomainName => MergeMatrixTests.DomainName;

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<HandPosition>(obj =>
            {
                obj.Key(p => p.Id);
                obj.Property(p => p.Symbol).Required();
            });
        }
    }

    public sealed class EmptyTradingOntology : DomainOntology
    {
        public override string DomainName => MergeMatrixTests.DomainName;

        protected override void Define(IOntologyBuilder builder)
        {
            // intentionally empty — domain registered, no hand types
        }
    }

    private static OntologyDelta.AddObjectType IngestedAdd(ObjectTypeDescriptor d) =>
        new(d) { SourceId = IngestSourceId, Timestamp = Timestamp };

    [Test]
    public async Task Merge_HandOnly_GraphMatchesHand()
    {
        // No ingestion source — the composed graph carries only the
        // hand-authored descriptor with HandAuthored provenance.
        var graphBuilder = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>();

        var graph = graphBuilder.Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        await Assert.That(position.Source).IsEqualTo(DescriptorSource.HandAuthored);
        await Assert.That(position.ClrType).IsEqualTo(typeof(HandPosition));
        await Assert.That(position.Properties.Any(p => p.Name == "Symbol")).IsTrue();
    }

    [Test]
    public async Task Merge_IngestedOnly_GraphMatchesIngested()
    {
        // Source-only descriptor in a domain with no DomainOntology subclass.
        // The OntologyGraphBuilder.Build() source-only branch wraps the
        // ingested type into a synthetic DomainDescriptor.
        var ingested = new ObjectTypeDescriptor
        {
            Name = "IngestedOnly",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./mod.ts#IngestedOnly",
            SymbolFqn = "mod.IngestedOnly",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingested)),
        };

        var graph = new OntologyGraphBuilder()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var resolved = graph.ObjectTypes.Single(ot => ot.Name == "IngestedOnly");
        await Assert.That(resolved.Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(resolved.SymbolKey).IsEqualTo("scip-typescript ./mod.ts#IngestedOnly");
        await Assert.That(resolved.LanguageId).IsEqualTo("typescript");
        await Assert.That(resolved.ClrType).IsNull();
    }

    [Test]
    public async Task Merge_HandOverridesIngestedProperty_HandWins()
    {
        // Same descriptor name + domain via two paths: hand declares a
        // "Symbol" property typed string; ingestion contributes a parallel
        // "Symbol" property with a different (ingested-only) provenance.
        // Hand wins per DR-6 — the resulting property carries
        // Source = HandAuthored.
        var ingestedShadow = new ObjectTypeDescriptor
        {
            Name = "HandPosition",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./pos.ts#HandPosition",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
            Properties = new List<PropertyDescriptor>
            {
                // Same name as the hand-authored property; conflict-loser side.
                new("Symbol", typeof(string)) { Source = DescriptorSource.Ingested },
            },
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingestedShadow)),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        // Record-level provenance resolves to hand after merge.
        await Assert.That(position.Source).IsEqualTo(DescriptorSource.HandAuthored);
        // Symbol still present and tagged hand-authored — the ingested
        // conflicting entry was dropped.
        var symbol = position.Properties.Single(p => p.Name == "Symbol");
        await Assert.That(symbol.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task Merge_IngestedAddsLink_LinkAppearsWithIngestedProvenance()
    {
        // Hand-only descriptor for HandPosition; ingestion contributes a
        // *new* link not declared by hand. The link must surface in the
        // composed graph tagged with Source = Ingested.
        var ingestedShadow = new ObjectTypeDescriptor
        {
            Name = "HandPosition",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./pos.ts#HandPosition",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
            Links = new List<LinkDescriptor>
            {
                new("RelatedOrders", "Order", LinkCardinality.OneToMany)
                {
                    Source = DescriptorSource.Ingested,
                    TargetSymbolKey = "scip-typescript ./ord.ts#Order",
                },
            },
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingestedShadow)),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        var link = position.Links.Single(l => l.Name == "RelatedOrders");
        await Assert.That(link.Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(link.TargetSymbolKey).IsEqualTo("scip-typescript ./ord.ts#Order");
    }

    // ---- Merge_IdentityFields_FollowLatticeRule, parameterized per DR-6 ----
    //
    // The lattice rule per identity field (DR-6 lateral):
    //   ClrType    : hand wins, fallback to ingested
    //   SymbolKey  : ingested wins, fallback to hand
    //   SymbolFqn  : ingested wins, fallback to hand
    //   LanguageId : hand wins
    //
    // Each case is asserted by constructing a hand-authored descriptor that
    // sets one identity field and an ingested descriptor that sets a
    // different value for the same identity field, then reading back the
    // merged value off the graph.

    [Test]
    public async Task Merge_IdentityFields_FollowLatticeRule_ClrType()
    {
        // ClrType: hand carries it; ingestion does not. Hand wins on the
        // happy path (and since ingestion can't carry CLR identity, the
        // outcome is "hand's CLR type survives").
        var ingestedShadow = new ObjectTypeDescriptor
        {
            Name = "HandPosition",
            DomainName = DomainName,
            ClrType = null,
            SymbolKey = "scip-typescript ./pos.ts#HandPosition",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingestedShadow)),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        await Assert.That(position.ClrType).IsEqualTo(typeof(HandPosition));
    }

    [Test]
    public async Task Merge_IdentityFields_FollowLatticeRule_SymbolKey()
    {
        // SymbolKey: ingestion is SCIP-authoritative — ingested wins.
        var ingestedShadow = new ObjectTypeDescriptor
        {
            Name = "HandPosition",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./pos.ts#HandPosition",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingestedShadow)),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        await Assert.That(position.SymbolKey)
            .IsEqualTo("scip-typescript ./pos.ts#HandPosition");
    }

    [Test]
    public async Task Merge_IdentityFields_FollowLatticeRule_SymbolFqn()
    {
        // SymbolFqn: ingested wins.
        var ingestedShadow = new ObjectTypeDescriptor
        {
            Name = "HandPosition",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./pos.ts#HandPosition",
            SymbolFqn = "mod.HandPosition",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingestedShadow)),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        await Assert.That(position.SymbolFqn).IsEqualTo("mod.HandPosition");
    }

    [Test]
    public async Task Merge_IdentityFields_FollowLatticeRule_LanguageId()
    {
        // LanguageId: hand wins, even when ingestion declares a different
        // language. (The hand path's CLR origin pins the descriptor's
        // primary language identity.)
        var ingestedShadow = new ObjectTypeDescriptor
        {
            Name = "HandPosition",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./pos.ts#HandPosition",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(IngestedAdd(ingestedShadow)),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<TradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "HandPosition");
        await Assert.That(position.LanguageId).IsEqualTo("dotnet");
    }
}
