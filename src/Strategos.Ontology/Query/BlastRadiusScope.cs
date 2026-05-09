namespace Strategos.Ontology.Query;

/// <summary>
/// Classifies the impact of a proposed ontology change after blast-radius
/// expansion completes.
/// </summary>
public enum BlastRadiusScope
{
    /// <summary>Impact is limited to a single object type within one domain.</summary>
    Local,

    /// <summary>Impact spans multiple object types within one domain.</summary>
    Domain,

    /// <summary>Impact crosses one or more domain boundaries.</summary>
    CrossDomain,

    /// <summary>Impact crosses four or more distinct domains.</summary>
    Global,
}
