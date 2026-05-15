namespace Strategos.Ontology.Diagnostics;

/// <summary>
/// Structured ontology diagnostic carried by
/// <see cref="OntologyCompositionException"/> and surfaced on
/// <c>OntologyGraph.NonFatalDiagnostics</c>.
/// </summary>
/// <param name="Id">Stable identifier (e.g. <c>"AONT201"</c>, <c>"AONT205"</c>).</param>
/// <param name="Message">Human-readable description; names specific identifiers per DR-10/DIM-8.</param>
/// <param name="Severity">Severity classification.</param>
/// <param name="DomainName">Owning domain, when applicable.</param>
/// <param name="TypeName">Owning object type, when applicable.</param>
/// <param name="PropertyName">Property or field involved, when applicable.</param>
/// <remarks>
/// Created in Task 5 to back <see cref="OntologyCompositionException"/>'s
/// diagnostics aggregation. Task 6 fleshes out the surrounding
/// <c>Strategos.Ontology.Diagnostics</c> machinery and the local
/// severity mirror.
/// </remarks>
public sealed record OntologyDiagnostic(
    string Id,
    string Message,
    OntologyDiagnosticSeverity Severity,
    string? DomainName = null,
    string? TypeName = null,
    string? PropertyName = null);
