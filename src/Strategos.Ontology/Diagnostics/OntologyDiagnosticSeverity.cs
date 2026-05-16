namespace Strategos.Ontology.Diagnostics;

/// <summary>
/// Severity classification for ontology diagnostics. Mirrored locally
/// (rather than re-exporting Roslyn's <c>DiagnosticSeverity</c>) so the
/// runtime <c>Strategos.Ontology</c> assembly does not bind to
/// <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
/// <remarks>
/// Task 6 expands the surrounding diagnostics machinery; this enum is
/// introduced in Task 5 so <see cref="OntologyCompositionException"/>
/// can carry typed diagnostic severity.
/// </remarks>
public enum OntologyDiagnosticSeverity
{
    /// <summary>Informational; non-fatal, surfaces via <c>OntologyGraph.NonFatalDiagnostics</c>.</summary>
    Info = 0,

    /// <summary>Warning; non-fatal, surfaces via <c>OntologyGraph.NonFatalDiagnostics</c>.</summary>
    Warning = 1,

    /// <summary>Error; fatal at graph-freeze, surfaces via <see cref="OntologyCompositionException.Diagnostics"/>.</summary>
    Error = 2,
}
