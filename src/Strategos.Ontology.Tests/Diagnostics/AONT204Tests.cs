using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 26): AONT204 is an info-severity graph-freeze diagnostic
/// emitted when an ingested descriptor (one whose graph-level Source
/// is <see cref="DescriptorSource.Ingested"/>) is not referenced by
/// any hand-authored descriptor via Links, ParentTypeName, or
/// KeyProperty references. Surfaces ingested noise that may indicate
/// orphan ingestion.
/// </summary>
public class AONT204Tests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public sealed class HandPortfolio
    {
        public string Id { get; set; } = string.Empty;
    }

    // CLR shadow for the ingested-only "Order" target. Never registered
    // as a hand-side Object<>; lives only so the HasMany<Order>(...) link
    // writes a target name of "Order" into the hand descriptor.
    public sealed class Order
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class HandPortfolioOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<HandPortfolio>("Portfolio", obj =>
            {
                obj.Key(p => p.Id);
            });
        }
    }

    private sealed class HandPortfolioLinkingOrderOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<HandPortfolio>("Portfolio", obj =>
            {
                obj.Key(p => p.Id);
                // Hand-side link references a target named "Order" — the
                // descriptor of that name is contributed purely by the
                // ingested source.
                obj.HasMany<Order>("Orders");
            });
        }
    }

    [Test]
    public async Task Build_IngestedTypeNoHandReference_AONT204Info()
    {
        // Ingestion contributes a brand-new descriptor "Order" that is
        // not referenced by any hand-authored type — no Links pointing
        // to it, no ParentTypeName, no KeyProperty references. AONT204
        // info diagnostic should fire.
        var orphanOrder = new ObjectTypeDescriptor
        {
            Name = "Order",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./ord.ts#Order",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(orphanOrder)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<HandPortfolioOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var aont204 = graph.NonFatalDiagnostics.FirstOrDefault(d => d.Id == "AONT204");
        await Assert.That(aont204).IsNotNull();
        await Assert.That(aont204!.Severity)
            .IsEqualTo(Strategos.Ontology.Diagnostics.OntologyDiagnosticSeverity.Info);
        await Assert.That(aont204.TypeName).IsEqualTo("Order");
        await Assert.That(aont204.DomainName).IsEqualTo("Trading");
    }

    [Test]
    public async Task Build_IngestedTypeReferencedByHand_NoAONT204()
    {
        // Hand-authored Portfolio carries a HasMany "Orders" link
        // pointing at the ingested "Order" descriptor — AONT204 should
        // not fire because the ingested type is referenced.
        var referencedOrder = new ObjectTypeDescriptor
        {
            Name = "Order",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./ord.ts#Order",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(referencedOrder)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<HandPortfolioLinkingOrderOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        await Assert.That(graph.NonFatalDiagnostics.Any(d => d.Id == "AONT204")).IsFalse();
    }
}
