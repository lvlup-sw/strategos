namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Marker interface for vector-searchable domain objects.
/// </summary>
public interface ISearchable
{
    /// <summary>
    /// Gets the embedding vector for this object.
    /// </summary>
    float[] Embedding { get; }
}
