using System.Collections.Immutable;

using Strategos.Ontology.Diagnostics;

namespace Strategos.Ontology;

/// <summary>
/// Raised when <c>OntologyGraphBuilder.Build()</c> or a runtime delta
/// application detects a fatal composition condition.
/// </summary>
/// <remarks>
/// DR-10 (failure modes): Error-severity diagnostics are aggregated on
/// <see cref="Diagnostics"/>; warning/info diagnostics that did not
/// fail the build are surfaced via <see cref="NonFatalDiagnostics"/>
/// for telemetry consumers. Legacy single-message construction is
/// preserved so existing throw sites compile unchanged.
/// </remarks>
public sealed class OntologyCompositionException : Exception
{
    /// <summary>
    /// Error-severity diagnostics that caused the build to fail. Empty
    /// when the exception was constructed via the legacy single-message
    /// ctor.
    /// </summary>
    public ImmutableArray<OntologyDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Non-fatal diagnostics (warning, info) observed during the build.
    /// Provided for telemetry / log inspection; never empty when
    /// constructed via the diagnostics-aware ctor and warnings were
    /// observed.
    /// </summary>
    public ImmutableArray<OntologyDiagnostic> NonFatalDiagnostics { get; }

    public OntologyCompositionException(string message)
        : base(message)
    {
        Diagnostics = ImmutableArray<OntologyDiagnostic>.Empty;
        NonFatalDiagnostics = ImmutableArray<OntologyDiagnostic>.Empty;
    }

    public OntologyCompositionException(string message, Exception innerException)
        : base(message, innerException)
    {
        Diagnostics = ImmutableArray<OntologyDiagnostic>.Empty;
        NonFatalDiagnostics = ImmutableArray<OntologyDiagnostic>.Empty;
    }

    /// <summary>
    /// Constructs an exception aggregating one or more error-severity
    /// diagnostics. The default message lists the first diagnostic's
    /// identifier to make logs scannable.
    /// </summary>
    public OntologyCompositionException(ImmutableArray<OntologyDiagnostic> diagnostics)
        : base(BuildMessage(diagnostics, ImmutableArray<OntologyDiagnostic>.Empty))
    {
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<OntologyDiagnostic>.Empty : diagnostics;
        NonFatalDiagnostics = ImmutableArray<OntologyDiagnostic>.Empty;
    }

    /// <summary>
    /// Constructs an exception aggregating both fatal and non-fatal
    /// diagnostics. Non-fatal items are passed through so callers can
    /// log warnings/info that surfaced before the build failed.
    /// </summary>
    public OntologyCompositionException(
        ImmutableArray<OntologyDiagnostic> diagnostics,
        ImmutableArray<OntologyDiagnostic> nonFatalDiagnostics)
        : base(BuildMessage(diagnostics, nonFatalDiagnostics))
    {
        Diagnostics = diagnostics.IsDefault ? ImmutableArray<OntologyDiagnostic>.Empty : diagnostics;
        NonFatalDiagnostics = nonFatalDiagnostics.IsDefault
            ? ImmutableArray<OntologyDiagnostic>.Empty
            : nonFatalDiagnostics;
    }

    private static string BuildMessage(
        ImmutableArray<OntologyDiagnostic> diagnostics,
        ImmutableArray<OntologyDiagnostic> nonFatalDiagnostics)
    {
        if (diagnostics.IsDefault || diagnostics.IsEmpty)
        {
            return "Ontology composition failed (no diagnostics attached).";
        }

        var first = diagnostics[0];
        var nonFatalSuffix = nonFatalDiagnostics.IsDefault || nonFatalDiagnostics.IsEmpty
            ? string.Empty
            : $" ({nonFatalDiagnostics.Length} non-fatal diagnostic(s) recorded).";

        if (diagnostics.Length == 1)
        {
            return $"{first.Id}: {first.Message}{nonFatalSuffix}";
        }

        return $"{first.Id}: {first.Message} (and {diagnostics.Length - 1} additional error-severity diagnostic(s)){nonFatalSuffix}";
    }
}
