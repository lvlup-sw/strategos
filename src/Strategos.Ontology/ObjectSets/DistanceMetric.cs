namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Distance metric used for vector similarity search.
/// </summary>
public enum DistanceMetric
{
    /// <summary>Cosine distance (1 - cosine similarity).</summary>
    Cosine = 0,

    /// <summary>Euclidean (L2) distance.</summary>
    L2 = 1,

    /// <summary>Inner (dot) product distance.</summary>
    InnerProduct = 2,
}
