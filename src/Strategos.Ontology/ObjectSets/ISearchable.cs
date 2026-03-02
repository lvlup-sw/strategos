namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Marker interface for domain objects that carry a pre-computed embedding vector.
/// </summary>
public interface ISearchable
{
    /// <summary>
    /// The pre-computed embedding vector for this object.
    /// </summary>
    float[] Embedding { get; }
}
