using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 28): AONT206 is an opt-in info-severity hygiene hint
/// flagged when a hand-declared property is ALSO contributed by the
/// ingested side. Gated behind the MSBuild property
/// <c>OntologyEnableHygieneHints</c> — wired through
/// <see cref="OntologyOptions.EnableHygieneHints"/>. Off by default so
/// existing graphs are not flooded with cosmetic hints.
/// </summary>
public class AONT206Tests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public sealed class HandPos
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
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
            });
        }
    }

    private static TestOntologySource BuildSourceWithOverlappingSymbol() =>
        new()
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(
                    new ObjectTypeDescriptor
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
                            // Same property name as the hand declaration:
                            // hygiene-hint territory.
                            new("Symbol", typeof(string)) { Source = DescriptorSource.Ingested },
                        },
                    })
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

    [Test]
    public async Task Build_HandPropertyAlsoIngested_AONT206InfoWhenEnabled()
    {
        var options = new OntologyOptions { EnableHygieneHints = true };

        var graph = new OntologyGraphBuilder()
            .WithOptions(options)
            .AddDomain<HandPositionOntology>()
            .AddSources(new IOntologySource[] { BuildSourceWithOverlappingSymbol() })
            .Build();

        var aont206 = graph.NonFatalDiagnostics.FirstOrDefault(d => d.Id == "AONT206");
        await Assert.That(aont206).IsNotNull();
        await Assert.That(aont206!.Severity)
            .IsEqualTo(Strategos.Ontology.Diagnostics.OntologyDiagnosticSeverity.Info);
        await Assert.That(aont206.PropertyName).IsEqualTo("Symbol");
    }

    [Test]
    public async Task Build_HandPropertyAlsoIngested_NoAONT206ByDefault()
    {
        // No WithOptions call — default EnableHygieneHints is false; the
        // hint must not fire.
        var graph = new OntologyGraphBuilder()
            .AddDomain<HandPositionOntology>()
            .AddSources(new IOntologySource[] { BuildSourceWithOverlappingSymbol() })
            .Build();

        await Assert.That(graph.NonFatalDiagnostics.Any(d => d.Id == "AONT206")).IsFalse();
    }
}
