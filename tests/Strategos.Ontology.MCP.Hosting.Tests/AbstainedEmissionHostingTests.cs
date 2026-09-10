using Microsoft.Extensions.Logging;

using Strategos.Ontology.MCP;
using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// DR-17 (emission half, #152) across the hosting seam: the composer wired by
/// <see cref="OntologyServerToolFactory.CreateAnswerComposer"/> — the MCP-hosted entry
/// point — emits an <c>ontology.abstained</c> audit record through the concrete logging
/// sink on every <see cref="NoAnswerRecorded"/> it produces, and never on a cited
/// <see cref="Answer"/>. The audit line carries the nearest-records COUNT and the event
/// type only, never the abstention's record identities (no exfiltration through audit).
/// </summary>
public sealed class AbstainedEmissionHostingTests
{
    private static ResponseMeta Meta => new("sha256:testgraph");

    private static readonly RecordRef RecordA = new("Instrument", "inst-1");
    private static readonly RecordRef RecordB = new("Instrument", "inst-2");

    [Test]
    public async Task HostingWiredComposer_Abstain_LogsAbstainedRecordWithCount()
    {
        var factory = new CapturingLoggerFactory();
        var composer = OntologyServerToolFactory.CreateAnswerComposer(factory);

        var result = composer.Compose("ignored when abstaining", matchedRecords: [], new[] { RecordA, RecordB }, Meta);

        await Assert.That(result).IsTypeOf<NoAnswerRecorded>();

        var abstentionLogs = factory.Logger.Entries.Where(e => e.Message.Contains("ontology.abstained")).ToList();
        await Assert.That(abstentionLogs.Count).IsEqualTo(1);
        await Assert.That(abstentionLogs[0].Level).IsEqualTo(LogLevel.Information);
        await Assert.That(abstentionLogs[0].Message).Contains("nearest records searched: 2");

        // No record identity leaks into the audit line.
        await Assert.That(abstentionLogs[0].Message).DoesNotContain("inst-1");
        await Assert.That(abstentionLogs[0].Message).DoesNotContain("inst-2");
    }

    [Test]
    public async Task HostingWiredComposer_CitedAnswer_LogsNoAbstention()
    {
        var factory = new CapturingLoggerFactory();
        var composer = OntologyServerToolFactory.CreateAnswerComposer(factory);

        var result = composer.Compose("The instrument matured.", new[] { RecordA }, nearestRecords: [], Meta);

        await Assert.That(result).IsTypeOf<Answer>();
        await Assert.That(factory.Logger.Entries.Any(e => e.Message.Contains("ontology.abstained"))).IsFalse();
    }

    [Test]
    public async Task HostingWiredComposer_EmptyNearest_StillLogsAbstention_CountZero()
    {
        var factory = new CapturingLoggerFactory();
        var composer = OntologyServerToolFactory.CreateAnswerComposer(factory);

        composer.Compose("ignored", matchedRecords: [], nearestRecords: [], Meta);

        var abstentionLogs = factory.Logger.Entries.Where(e => e.Message.Contains("ontology.abstained")).ToList();
        await Assert.That(abstentionLogs.Count).IsEqualTo(1);
        await Assert.That(abstentionLogs[0].Message).Contains("nearest records searched: 0");
    }

    [Test]
    public async Task CreateAnswerComposer_RejectsNullLoggerFactory()
    {
        await Assert.That(() => OntologyServerToolFactory.CreateAnswerComposer(null!))
            .Throws<ArgumentNullException>();
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public CapturingLogger Logger { get; } = new();

        public ILogger CreateLogger(string categoryName) => Logger;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
