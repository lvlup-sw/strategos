using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 24): AONT202 is a warning-severity graph-freeze
/// diagnostic emitted when a hand-declared property's type/kind
/// disagrees with the ingested side's contribution for the same
/// property name. The warning is non-fatal — it surfaces on
/// <see cref="OntologyGraph.NonFatalDiagnostics"/> and is also routed
/// through the wired <see cref="ILogger{T}"/> with structured properties.
/// </summary>
public class AONT202Tests
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
                // Hand declares Symbol as a Scalar string property.
                obj.Property(p => p.Symbol);
            });
        }
    }

    [Test]
    public async Task Build_HandScalarVsIngestedReference_AONT202Warning()
    {
        // Ingested side declares "Symbol" but as Reference-kinded, with a
        // mismatched property type. The hand-merged descriptor still uses
        // hand-side metadata, but AONT202 should fire as a warning so
        // ingest authors notice the drift.
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
                new("Symbol", typeof(Guid))
                {
                    Kind = PropertyKind.Reference,
                    Source = DescriptorSource.Ingested,
                },
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

        var aont202 = graph.NonFatalDiagnostics.FirstOrDefault(d => d.Id == "AONT202");
        await Assert.That(aont202).IsNotNull();
        await Assert.That(aont202!.Severity).IsEqualTo(Strategos.Ontology.Diagnostics.OntologyDiagnosticSeverity.Warning);
        await Assert.That(aont202.PropertyName).IsEqualTo("Symbol");
        await Assert.That(aont202.DomainName).IsEqualTo("Trading");
        await Assert.That(aont202.TypeName).IsEqualTo("Position");
    }

    [Test]
    public async Task Build_HandAndIngestedAgree_NoAONT202()
    {
        // Both sides declare Symbol as a scalar string property — no
        // mismatch, no AONT202.
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
                new("Symbol", typeof(string))
                {
                    Kind = PropertyKind.Scalar,
                    Source = DescriptorSource.Ingested,
                },
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

        await Assert.That(graph.NonFatalDiagnostics.Any(d => d.Id == "AONT202")).IsFalse();
    }

    [Test]
    public async Task Build_AONT202Fires_LoggerReceivesStructuredWarning()
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
                new("Symbol", typeof(Guid))
                {
                    Kind = PropertyKind.Reference,
                    Source = DescriptorSource.Ingested,
                },
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
            .AddDomain<HandPositionOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var warning = logger.Entries
            .FirstOrDefault(e => e.Level == LogLevel.Warning
                              && (string?)e.State["DiagnosticId"] == "AONT202");

        await Assert.That(warning).IsNotNull();
        await Assert.That((string?)warning!.State["DomainName"]).IsEqualTo("Trading");
        await Assert.That((string?)warning.State["TypeName"]).IsEqualTo("Position");
        await Assert.That((string?)warning.State["PropertyName"]).IsEqualTo("Symbol");
    }
}

/// <summary>
/// Minimal in-test logger that captures structured state pairs so
/// AONT2xx-tests can assert structured-property propagation without
/// pulling in Microsoft.Extensions.Logging.Testing.
/// </summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<FakeLogEntry> _entries = new();

    public IReadOnlyCollection<FakeLogEntry> Entries => _entries.ToArray();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var entry = new FakeLogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception),
        };

        if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
        {
            foreach (var kvp in pairs)
            {
                entry.State[kvp.Key] = kvp.Value;
            }
        }

        _entries.Enqueue(entry);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

internal sealed class FakeLogEntry
{
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, object?> State { get; } = new();
}
