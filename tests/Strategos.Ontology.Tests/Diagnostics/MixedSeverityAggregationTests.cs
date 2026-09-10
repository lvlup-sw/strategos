using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-10 AC3 — pins the design's mixed-severity aggregation contract:
/// when graph-freeze produces one Error (AONT201) + one Warning
/// (AONT202) + one Info (AONT204), <see cref="OntologyGraphBuilder.Build"/>
/// throws <see cref="OntologyCompositionException"/> with the Error in
/// <see cref="OntologyCompositionException.Diagnostics"/> and the
/// Warning + Info in <see cref="OntologyCompositionException.NonFatalDiagnostics"/>.
/// PR-A's test suite could not exercise this because the AONT200-series
/// triggers did not yet exist; PR-B closes that gap.
/// </summary>
public class MixedSeverityAggregationTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public sealed class HandPos
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    private sealed class HandPositionOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<HandPos>("Position", obj =>
            {
                obj.Key(p => p.Id);
                obj.Property(p => p.Symbol);
                obj.Property(p => p.Quantity);
            });
        }
    }

    [Test]
    public async Task Build_AONT201PlusAONT202PlusAONT204_ThrowsWithErrorOnlyInDiagnostics_NonFatalCarriesWarningsAndInfo()
    {
        // Position (ingested mirror): carries "Symbol" with a mismatched
        // type/kind ⇒ AONT202 (warning), and is missing "Quantity"
        // entirely ⇒ AONT201 (error). A separate ingested orphan type
        // "OrphanOrder" with no hand references ⇒ AONT204 (info).
        var positionMirror = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./pos.ts#Position",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
            Properties = new List<PropertyDescriptor>
            {
                new("Symbol", typeof(Guid))
                {
                    Kind = PropertyKind.Reference,
                    Source = DescriptorSource.Ingested,
                },
            },
        };

        var orphanOrder = new ObjectTypeDescriptor
        {
            Name = "OrphanOrder",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./ord.ts#OrphanOrder",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(positionMirror)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                },
                new OntologyDelta.AddObjectType(orphanOrder)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

        var graphBuilder = new OntologyGraphBuilder()
            .AddDomain<HandPositionOntology>()
            .AddSources(new IOntologySource[] { source });

        OntologyCompositionException? caught = null;
        try
        {
            graphBuilder.Build();
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();

        // Fatal: AONT201 only.
        await Assert.That(caught!.Diagnostics.Any(d => d.Id == "AONT201")).IsTrue();
        await Assert.That(caught.Diagnostics.Any(d => d.Id == "AONT202")).IsFalse();
        await Assert.That(caught.Diagnostics.Any(d => d.Id == "AONT204")).IsFalse();

        // Non-fatal: AONT202 + AONT204.
        await Assert.That(caught.NonFatalDiagnostics.Any(d => d.Id == "AONT202")).IsTrue();
        await Assert.That(caught.NonFatalDiagnostics.Any(d => d.Id == "AONT204")).IsTrue();
        await Assert.That(caught.NonFatalDiagnostics.Any(d => d.Id == "AONT201")).IsFalse();
    }
}
