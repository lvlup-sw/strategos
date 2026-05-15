using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 23): AONT201 fires at graph-freeze when a hand-authored
/// descriptor declares a property whose name is absent from the
/// corresponding ingested descriptor (after MergeTwo). The diagnostic
/// is error-severity, surfaces in <see cref="OntologyCompositionException.Diagnostics"/>,
/// and the message identifies the offending property and includes a
/// hint about the Pass-6b rename matcher per DR-7 AC3.
/// </summary>
public class AONT201Tests
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
    public async Task Build_HandDeclaresPropertyMissingFromIngested_AONT201Error()
    {
        // Ingested descriptor with the SAME name + domain but missing the
        // hand-declared "Quantity" property; after MergeTwo the merged
        // descriptor carries "Symbol" (ingested-present, will land in hand
        // anyway) and "Quantity" (hand-only) — Quantity is hand-only and
        // hence "missing on the ingested side": AONT201.
        var ingested = new ObjectTypeDescriptor
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
                new("Symbol", typeof(string)) { Source = DescriptorSource.Ingested },
            },
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(ingested)
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
        var aont201 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT201");
        await Assert.That(aont201).IsNotNull();
        await Assert.That(aont201!.Message).Contains("Quantity");
        // Hint about Pass-6b rename matcher (DR-7 AC3).
        await Assert.That(aont201.Message).Contains("Pass-6b rename matcher");
    }

    [Test]
    public async Task Build_HandDeclaresPropertyPresentInIngested_NoAONT201()
    {
        // Ingested side carries BOTH hand-declared properties — no
        // AONT201 should fire.
        var ingested = new ObjectTypeDescriptor
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
                new("Symbol", typeof(string)) { Source = DescriptorSource.Ingested },
                new("Quantity", typeof(decimal)) { Source = DescriptorSource.Ingested },
            },
        };

        var source = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(ingested)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<HandPositionOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        // No AONT201 should appear in NonFatalDiagnostics either — the
        // hand property is fully accounted for on the ingested side.
        await Assert.That(graph.NonFatalDiagnostics.Any(d => d.Id == "AONT201"))
            .IsFalse();
    }
}
