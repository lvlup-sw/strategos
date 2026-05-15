using System.Collections.Immutable;

using Microsoft.Extensions.Logging;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 25): AONT203 is a warning-severity graph-freeze
/// diagnostic emitted when an ingested-only property is missing from
/// the hand-authored <c>Define()</c> declaration of a type opted in
/// to strict mode via <c>[DomainEntity(Strict = true)]</c>. Strict is
/// opt-in: types without the attribute (or with Strict = false) do
/// not fire AONT203.
/// </summary>
public class AONT203Tests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    [DomainEntity(Strict = true)]
    public sealed class StrictPos
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
    }

    public sealed class LooseQuote
    {
        public string Id { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
    }

    private sealed class StrictTradingOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<StrictPos>("Position", obj =>
            {
                obj.Key(p => p.Id);
                obj.Property(p => p.Symbol);
                // Quantity (ingested-only) is intentionally not declared
                // here so AONT203 fires under Strict.
            });
        }
    }

    private sealed class LooseTradingOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.Object<LooseQuote>("Quote", obj =>
            {
                obj.Key(p => p.Id);
                obj.Property(p => p.Symbol);
            });
        }
    }

    [Test]
    public async Task Build_StrictTypeMissingIngestedProperty_AONT203Warning()
    {
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
            .AddDomain<StrictTradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var aont203 = graph.NonFatalDiagnostics.FirstOrDefault(d => d.Id == "AONT203");
        await Assert.That(aont203).IsNotNull();
        await Assert.That(aont203!.PropertyName).IsEqualTo("Quantity");
        await Assert.That(aont203.DomainName).IsEqualTo("Trading");
        await Assert.That(aont203.TypeName).IsEqualTo("Position");
    }

    [Test]
    public async Task Build_NonStrictTypeMissingIngestedProperty_NoAONT203()
    {
        // LooseQuote is not annotated with [DomainEntity(Strict=true)] —
        // missing ingested-only properties should NOT fire AONT203.
        var ingested = new ObjectTypeDescriptor
        {
            Name = "Quote",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./quote.ts#Quote",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
            Properties = new List<PropertyDescriptor>
            {
                new("Symbol", typeof(string)) { Source = DescriptorSource.Ingested },
                new("Bid", typeof(decimal)) { Source = DescriptorSource.Ingested },
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
            .AddDomain<LooseTradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        await Assert.That(graph.NonFatalDiagnostics.Any(d => d.Id == "AONT203")).IsFalse();
    }

    [Test]
    public async Task Build_AONT203Fires_LoggerReceivesStructuredWarning()
    {
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

        var logger = new FakeLogger<OntologyGraphBuilder>();

        _ = new OntologyGraphBuilder()
            .WithLogger(logger)
            .AddDomain<StrictTradingOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var warning = logger.Entries
            .FirstOrDefault(e => e.Level == LogLevel.Warning
                              && (string?)e.State["DiagnosticId"] == "AONT203");

        await Assert.That(warning).IsNotNull();
        await Assert.That((string?)warning!.State["TypeName"]).IsEqualTo("Position");
        await Assert.That((string?)warning.State["PropertyName"]).IsEqualTo("Quantity");
    }
}
