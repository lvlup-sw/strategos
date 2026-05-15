namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Field-level provenance for ontology descriptors. Distinguishes hand-authored
/// <c>DomainOntology.Define()</c> contributions from contributions arriving via
/// <c>IOntologySource</c> ingestion paths.
/// </summary>
/// <remarks>
/// Default is <see cref="HandAuthored"/>; this preserves existing descriptor
/// construction sites which predate the polyglot ingestion path.
/// </remarks>
public enum DescriptorSource
{
    /// <summary>
    /// Descriptor or field contributed by hand-authored <c>DomainOntology.Define()</c>.
    /// </summary>
    HandAuthored = 0,

    /// <summary>
    /// Descriptor or field contributed by an <c>IOntologySource</c> ingestion path.
    /// </summary>
    Ingested = 1,
}
