namespace Strategos.Ontology.Descriptors;

/// <summary>Identifies the ontology resource family named by an action frame.</summary>
public enum ActionResourceKind
{
    /// <summary>An object property.</summary>
    Property,

    /// <summary>An ontology link.</summary>
    Link,

    /// <summary>An emitted event type.</summary>
    Event,

    /// <summary>A named resource outside the ontology graph.</summary>
    External,
}
