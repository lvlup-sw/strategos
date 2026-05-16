using Microsoft.Extensions.Logging;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Minimal capturing logger used by PR-C hybrid wiring tests to assert on
/// warn-once and exception-stack-logged behaviors. Intentionally process-local
/// (per-instance) so each test sees a fresh log buffer.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _gate = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var entry = new LogEntry(logLevel, formatter(state, exception), exception);
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
