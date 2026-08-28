namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Field-level provenance for ontology descriptors. Distinguishes the
/// three authoring surfaces that can contribute a descriptor or field.
/// </summary>
/// <remarks>
/// <para>
/// Numeric values are part of the public contract. New members are
/// appended so existing values never move:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="HandAuthored"/> (<c>0</c>) — fluent
/// <c>DomainOntology.Define()</c> C# DSL.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="Ingested"/> (<c>1</c>) — mechanical
/// <c>IOntologySource</c> ingestion (Roslyn/SCIP, schema dump, live
/// system-of-record).
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="HandAuthoredContract"/> (<c>2</c>) — TypeSpec / JSON
/// contract authoring in <c>Strategos.Contracts</c> (<c>op</c>,
/// <c>interface</c>, <c>extern dec</c>).
/// </description>
/// </item>
/// </list>
/// <para>
/// Default is <see cref="HandAuthored"/>; this preserves existing descriptor
/// construction sites which predate the polyglot ingestion path.
/// AONT205 rejects intent-only fields only when <see cref="Ingested"/>.
/// </para>
/// </remarks>
public enum DescriptorSource
{
    /// <summary>
    /// Descriptor or field contributed by the fluent C# authoring surface
    /// <c>DomainOntology.Define()</c>.
    /// </summary>
    HandAuthored = 0,

    /// <summary>
    /// Descriptor or field contributed by a mechanical
    /// <c>IOntologySource</c> ingestion path. Must not carry intent-only
    /// fields (<c>Actions</c>, <c>Events</c>, <c>Lifecycle</c>,
    /// <c>InterfaceActionMappings</c>, <c>ExternalLinkExtensionPoints</c>).
    /// </summary>
    Ingested = 1,

    /// <summary>
    /// Descriptor or field contributed by the TypeSpec / JSON contract
    /// authoring surface (<c>Strategos.Contracts</c> ontology documents —
    /// <c>op</c>, <c>interface</c>, <c>extern dec</c>). Contract-authored
    /// intent is first-class and survives graph merge; AONT205 does not
    /// apply.
    /// </summary>
    HandAuthoredContract = 2,
}
